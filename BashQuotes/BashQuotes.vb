Imports System.Net.Sockets
Imports System.Threading
Imports System.Timers
Imports VBot

Public Structure Quote
    Public Text As String
    Public Rating As Integer
End Structure

Public Class BashPlugin
    Inherits Plugin

    Private WithEvents QuoteTimer As System.Timers.Timer
    Private GetQuotesThread As Thread

    Private Quotes1 As SortedDictionary(Of Integer, Quote)
    Private Quotes2 As SortedDictionary(Of Integer, Quote)
    Private Index As Integer

    Public Overrides ReadOnly Property Name As String
        Get
            Return "Bash Quotes"
        End Get
    End Property

    Public Overrides Function Help(ByVal Topic As String, ByVal IsMajorChannel As Boolean) As String
        If Topic Is Nothing Then Return "bash.org quotes are being provided in this channel."
        Return Nothing
    End Function

    Public Overrides Sub OnChannelJoinSelf(Connection As IRCConnection, Sender As String, Channel As String)
        MyBase.OnChannelJoinSelf(Connection, Sender, Channel)

        If QuoteTimer Is Nothing Then
            ' Initial join; do some initialisation work here.

            QuoteTimer = New Timers.Timer(60000)
            QuoteTimer.Start()

            GetQuotesThread = New Thread(AddressOf GetQuotes)
            GetQuotesThread.Start()
        End If
    End Sub

    Public Sub QuoteTimer_Tick(sender As Object, e As ElapsedEventArgs) Handles QuoteTimer.Elapsed
        If Quotes2 IsNot Nothing And Quotes1 Is Nothing Then
            Quotes1 = Quotes2
            Quotes2 = Nothing
        End If

        If Quotes1 IsNot Nothing AndAlso Index < Quotes1.Count Then
            ' Show a quote.
            SayToAllChannels("$b-------- bash #" & Quotes1.Keys(Index) & " -$b Rating:$b " & Quotes1.Values(Index).Rating & " --------")
            For Each Line In Quotes1.Values(Index).Text.Split({"<br />", ChrW(10), ChrW(13)}, Integer.MaxValue, StringSplitOptions.RemoveEmptyEntries)
                If Line.Length <= 4 Then Continue For
                Thread.Sleep(1000)
                If Line.Length > 350 Then
                    Dim Index As Integer
                    For Index = 350 To 300 Step -1
                        If Line(Index) = " " Or Line(Index) = ChrW(160) Or Line(Index) = ChrW(9) Then Exit For
                    Next
                    SayToAllChannels(Line.Substring(0, Index))
                    Thread.Sleep(1000)
                    SayToAllChannels("    " & Line.Substring(Index + 1))
                Else
                    SayToAllChannels(Line)
                End If
            Next
            Index += 1
        End If

        If Quotes2 Is Nothing Then
            If Not GetQuotesThread.IsAlive And Index >= Quotes1.Count - 5 Then
                ' Download new quotes.
                GetQuotesThread = New Thread(AddressOf GetQuotes)
                GetQuotesThread.Start()
            End If
        Else
            If Quotes1 Is Nothing OrElse Index >= Quotes1.Count Then
                Quotes1 = Quotes2
                Quotes2 = Nothing
                Index = 0
            End If
        End If
    End Sub

    Public Sub GetQuotes()
        Console.WriteLine("Connecting to bash.org...")

        Dim Client As New System.Net.Sockets.TcpClient
        Const UserAgent = "VBot (annihilator127@gmail.com)"
        Dim WaitStart As Date = Now, ResponseCode As String, lData As New Text.StringBuilder, bData As Text.StringBuilder = Nothing, Data(1023) As Byte, n As Integer
        Dim ParsingHeaders As Boolean
        Dim Expiry As Date = Nothing, ContentType As String = Nothing

        ' Connect and send the request.
        Client.Connect("bash.org", 80)

        Console.WriteLine("Sending request...")

        Dim s As New IO.StreamWriter(Client.GetStream)
        s.WriteLine("GET /?random HTTP/1.1")
        s.WriteLine("Host: bash.org")
        s.WriteLine("User-Agent: " & UserAgent)
        s.WriteLine("Accept: text/html")
        s.WriteLine("Connection: Close")
        s.WriteLine()
        s.Flush()

        Console.WriteLine("Downloading data...")

        ' Wait for the response.
        Do
            n = Client.GetStream.Read(Data, 0, 1024)
            If n = 0 Then
                Exit Do
            Else
                For i = 0 To n - 1
#If CONFIG = "Debug" Then
                    If bData Is Nothing Then Console.Write(ChrW(Data(i)))
#End If
                    If bData IsNot Nothing Then
                        ' Receiving the response body.
                        bData.Append(ChrW(Data(i)))
                    ElseIf i > 0 AndAlso (Data(i) = 10 And Data(i - 1) = 13) Then
                        ' We've hit a CR+LF.
                        If Not ParsingHeaders Then
                            ' Register the response code.
                            ResponseCode = lData.ToString
                            If ResponseCode.Split({" "c}, 3).ElementAtOrDefault(1) = "200" Then
                                ' HTTP 200 OK
                                ParsingHeaders = True
                            Else
                                ' Something else.
                                Throw New Net.WebException("Received a HTTP " & ResponseCode.Split({" "c}, 3).ElementAtOrDefault(1) & ".")
                            End If
                        Else
                            ' Read a header.
                            If lData.Length = 0 Then
                                ' Blank line indicates the end of the header list.
                                If ContentType.ToLower <> "text/html" Then
                                    ' Not XML data.
                                    Throw New Net.WebException("The document isn't a HTML page.")
                                End If
                                bData = New Text.StringBuilder
                            Else
                                Dim Key = lData.ToString.Split({":"c}, 2)(0)
                                Dim Value = lData.ToString.Split({":"c}, 2).ElementAtOrDefault(1).TrimStart
                                Select Case Key.ToLower
                                    Case "content-type"
                                        ContentType = Value
                                End Select
                            End If
                        End If
                        lData.Clear()
                    ElseIf Data(i) <> 13 Then
                        ' Data
                        lData.Append(ChrW(Data(i)))
                    End If
                Next
            End If
        Loop Until Now - WaitStart >= TimeSpan.FromSeconds(30)

        If bData Is Nothing Then Throw New Net.WebException("The request timed out.")

        Client.Close()

        My.Computer.FileSystem.WriteAllText("bash.html", bData.ToString(), False)

        Console.WriteLine("Parsing data...")

        ' Run the regular expression
        Dim Regex = New System.Text.RegularExpressions.Regex("<p class=""quote""><a (?>[^>]*)><b>#(\d+)</b>.*?\((-?\d+)\).*?<p class=""qt"">((?>[^<]*)(?:<br />(?>[^<]*))*)</p>", System.Text.RegularExpressions.RegexOptions.IgnoreCase Or Text.RegularExpressions.RegexOptions.Singleline)
        Dim Matches = Regex.Matches(bData.ToString())
        Dim Match As System.Text.RegularExpressions.Match
        Quotes2 = New SortedDictionary(Of Integer, Quote)
        For Each Match In Matches
            If Match.Groups(3).Value = "" Then Continue For

            ' Reject quotes that are more than 20 lines long.
            Dim Lines As Integer = 0, Pos As Integer = 0
            For Lines = 0 To 20
                If Pos = -1 Then Exit For
                Pos = Match.Groups(3).Value.IndexOf("<br />", Pos + 1)
            Next
            If Lines > 19 Then Continue For

            Quotes2.Add(Match.Groups(1).Value, New Quote With {.Rating = Match.Groups(2).Value, .Text = System.Web.HttpUtility.HtmlDecode(Match.Groups(3).Value)})
        Next

        Console.WriteLine("Got {0} new quotes.", Quotes2.Count)

    End Sub

    Public Overrides Sub OnUnload()
        MyBase.OnUnload()
        QuoteTimer.Dispose()
        GetQuotesThread = Nothing
        Quotes1 = Nothing
        Quotes2 = Nothing
    End Sub

End Class
