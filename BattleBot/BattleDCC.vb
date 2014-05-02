Imports System.Net.Sockets
Imports System.Text.RegularExpressions
Imports VBot

Partial Public Class BattleBot
    Public dccSocket As TcpClient
    Public dccData(511) As Byte
    Public dccMessage As New System.Text.StringBuilder
    Public dccListener As TcpListener
    Public DCCNickname As String
    Public DCCBattleChatEnabled As Boolean

    ' Listen for a DCC CHAT request.
    Public Overrides Sub OnPrivateCTCP(ByVal Connection As VBot.IRCConnection, ByVal Sender As VBot.IRCConnection.IRCUser, ByVal Message As String)
        If Sender.Nickname <> ArenaNickname Then Return ' Otherwise this could get abused. It can still get abused but not nearly as easily.

        Dim m = Regex.Match(Message, "DCC CHAT chat (\d+) (\d+)")
        If m.Success Then
            ' Parse the IP address.
            Dim IP As New System.Net.IPAddress({(CLng(m.Groups(1).Value) >> 24) And 255, (CLng(m.Groups(1).Value) >> 16) And 255, (CLng(m.Groups(1).Value) >> 8) And 255, (CLng(m.Groups(1).Value) >> 0) And 255})
            Dim Port As Integer = m.Groups(2).Value

            WriteMessage(1, 4, String.Format("Received a DCC CHAT request from {0}  IP: {1}  Port: {2}", ArenaNickname, IP, Port))
            DCCNickname = Connection.Nickname

            ' Attempt a DCC connection.
            WriteMessage(1, 4, "Connecting to the DCC session...")
            Try
                dccSocket = New TcpClient(IP.ToString, Port)
            Catch ex As Exception
                WriteMessage(1, 4, "Failed to connect: " & ChrW(3) & "15" & ex.Message)
                SayToAllChannels("My DCC connection failed. $k14[$k15" & ex.Message & "$k14]")
                Return
            End Try
            WriteMessage(1, 4, "Connected successfully.")

            Dim DCCThread = New Threading.Thread(AddressOf DCCRead)
            DCCThread.Start()

            If Not DCCBattleChatEnabled Then DCCSend("!toggle battle chat", Nothing)
        End If

    End Sub

    Friend Sub DCCRead()
        Do
            Dim n As Integer
            Try
                n = dccSocket.GetStream.Read(dccData, 0, 512)
            Catch ex As IO.IOException 'When TypeOf ex.InnerException Is SocketException
                OnDCCDisconnect(ex.Message)
                Return
            End Try

            If n < 1 Then
                OnDCCDisconnect("The server closed the connection.")
                Return
            End If

            For i = 0 To n - 1
                If dccData(i) = 10 Or dccData(i) = 13 Then
                    If dccMessage.Length > 0 Then
                        Dim params() As Object = {dccMessage.ToString}
                        Try
                            OnDCCRaw(dccMessage.ToString)
                        Catch ex As Exception
                            LogError("OnDCCRaw", ex)
                        End Try
                    End If
                    dccMessage = New System.Text.StringBuilder()
                Else
                    dccMessage.Append(ChrW(dccData(i)))
                End If
            Next
        Loop
    End Sub

    Friend Sub OnDCCDisconnect(ByVal Reason As String)
        LoggedIn = Nothing
        dccSocket = Nothing
        dccMessage = Nothing

        WriteMessage(1, 4, "DCC connection closed: $k15" & Reason)
    End Sub

    Private Sub OnDCCRaw(ByVal Message As String)
        OutputLine("\cBLUE" & MyKey & " DCC \cDKGREEN>>\cDKGRAY " & Message)

        ' Run the plugin's regex procedures on DCC messages.
        RunRegex(Nothing, ArenaNickname & "!*@*", "!" & MyKey & "/DCC/", Message, False)

        Dim m = Regex.Match(Message, "^\x034\[([^\]]*)\] <([^>]*)> \x0312(.*)$")
        If m.Success Then
            ' It's a chat message.
            VBot.EventCheck(Nothing, "!" & MyKey & "/DCC/#" & m.Groups(1).Value, "OnChannelMessage", {Nothing, m.Groups(2).Value & "!*@*", "!" & MyKey & "/DCC/#" & m.Groups(1).Value, m.Groups(3).Value})
            VBot.CheckMessage(Nothing, m.Groups(2).Value & "!*@*", "!" & MyKey & "/DCC/#" & m.Groups(1).Value, m.Groups(3).Value)
        End If

        m = Regex.Match(Message, "^\x0313\*\x034\[([^\]]*)\] [^ ]* \x0312 (.*)\x0313\*$")
        If m.Success Then
            ' It's a chat action.
            VBot.EventCheck(Nothing, "!" & MyKey & "/DCC/#" & m.Groups(1).Value, "OnChannelMessage", {Nothing, m.Groups(2).Value & "!*@*", "!" & MyKey & "/DCC/#" & m.Groups(1).Value, ChrW(1) & "ACTION " & m.Groups(3).Value & ChrW(1)})
            VBot.CheckMessage(Nothing, m.Groups(2).Value & "!*@*", "!" & MyKey & "/DCC/#" & m.Groups(1).Value, ChrW(1) & "ACTION " & m.Groups(3).Value & ChrW(1))
        End If
    End Sub

    <Regex("^\x033\x02Battle Chat\x02 has been (enabled|disabled)\.")>
    Public Sub OnDCCModeToggle(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        DCCBattleChatEnabled = (Match.Groups(1).Value = "enabled")
        If Not DCCBattleChatEnabled Then DCCSend("!toggle battle chat", Nothing)
    End Sub

    <Output("DCC")>
    Public Sub DCCSend(ByVal Message As String, ByVal Arguments As String)
        If dccSocket Is Nothing OrElse Not dccSocket.Connected Then Return ' Don't send if there's no connection.
        Dim w As New IO.StreamWriter(dccSocket.GetStream)
        w.Write(Message.Replace(ChrW(15), ChrW(3) & "12,99").Replace(ChrW(3) & "99", ChrW(3) & "12") & vbCrLf)
        OutputLine("\cBLUE" & MyKey & " DCC \cDKRED<<\cDKGRAY " & Message)
        w.Flush()
    End Sub
End Class