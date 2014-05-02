Imports VBot
Imports System.Text
Imports System.Timers
Imports System.Text.RegularExpressions

Public Class RequestData
    Public RequestTime As Date
    Public RequestTimer As Timer
    Public Connection As IRCConnection
    Public Channel As String
    Public Sender As String
    Public Time As Date
    Public IsLocalTimeFirst As Boolean
    Public TimeZone As TimeSpan
    Public TimeZoneName As String
End Class

Public Class TimeZone
    Public Abbreviation As String
    Public Name As String
    Public Offset As TimeSpan
End Class

Enum TimeZonesColumns As Integer
    ColumnAbbreviation = 0
    ColumnName = 1
    ColumnOffset = 2
End Enum

Public Class TimePlugin
    Inherits Plugin

    Public TimeZones As New List(Of TimeZone)
    Public Requests As New List(Of RequestData)
    Public Times As New Dictionary(Of String, TimeSpan)

    Public Problems As New List(Of String)

    Public Sub New(ByVal Key As String)
        LoadTimeZones()
    End Sub


    Public Overrides Sub OnPrivateNotice(ByVal Connection As VBot.IRCConnection, ByVal Sender As String, ByVal Message As String)
        MyBase.OnPrivateNotice(Connection, Sender, Message)

        If Message.StartsWith(ChrW(1)) Then
            If Message.StartsWith(ChrW(1) & "TIME ") Then
                SyncLock Requests
                    Dim Request As RequestData, i As Integer
                    For i = 0 To Requests.Count - 1
                        Request = Requests(i)
                        If Request.Connection Is Connection And Request.Sender.Split("!"c)(0) = Sender.Split("!"c)(0) Then Exit For
                    Next
                    If i = Requests.Count Then Return

                    Dim Time As String = Message.TrimEnd(ChrW(1)).Split({" "c}, 2)(1)
                    Dim pTime As Date = ParseCTCPTime(Time)
                    If pTime = Nothing Then
                        Say(Connection, Sender.Split("!"c)(0), "Could not parse your CTCP TIME reply.")
                    Else
                        Dim MinutesDifference = CInt(Math.Round((pTime - Now).TotalMinutes / 30) * 30)
                        Dim UTCOffset = DateTimeOffset.Now.Offset + TimeSpan.FromMinutes(MinutesDifference)

                        If Request.IsLocalTimeFirst Then
                            CommandTime2(Connection, Sender, Request.Channel, Request.Time, "your time", UTCOffset, Request.TimeZoneName, Request.TimeZone)
                        Else
                            CommandTime2(Connection, Sender, Request.Channel, Request.Time, Request.TimeZoneName, Request.TimeZone, "your time", UTCOffset)
                        End If
                    End If
                    Request.RequestTimer.Dispose()
                    Requests.RemoveAt(i)
                End SyncLock
            End If
        End If
    End Sub

    Public Function ParseCTCPTime(ByVal Message As String) As Date
        Dim s As String, b As New StringBuilder, d As Date, i As Integer = -1, c As Char, j As Integer
        Dim Year As Short = -1, Month As Short = -1, Day As Short = -1, DayOfWeek As Short = -1, Hour As Short = -1, Minute As Short = -1, Second As Short = -1
        Do
            i += 1
            If i < Message.Length Then
                c = Message(i)
                If "()[]{}|,".Contains(c) Then Continue Do
                If c <> " "c Then b.Append(Char.ToUpper(c))
            Else
                c = ChrW(0)
            End If
            If c = " "c Or c = ChrW(0) Then
                s = b.ToString()
                b.Clear()
                If s.Length = 0 Then Continue Do
                If s.StartsWith("+") Or s.StartsWith("-") Then
                ElseIf Integer.TryParse(s, j) Then
                    If j >= 1000 Then
                        ' Assume it's a year.
                        Year = j
                    ElseIf j >= 1 And j <= 31 Then
                        ' Assume it's the day of the month.
                        Day = j
                    End If
                ElseIf s = "SUN" Or s = "SUNDAY" Then
                    DayOfWeek = 0
                ElseIf s = "MON" Or s = "MONDAY" Then
                    DayOfWeek = 1
                ElseIf s = "TUE" Or s = "TUESDAY" Then
                    DayOfWeek = 2
                ElseIf s = "WED" Or s = "WEDNESDAY" Then
                    DayOfWeek = 3
                ElseIf s = "THU" Or s = "THURSDAY" Then
                    DayOfWeek = 4
                ElseIf s = "FRI" Or s = "FRIDAY" Then
                    DayOfWeek = 5
                ElseIf s = "SAT" Or s = "SATURDAY" Then
                    DayOfWeek = 6
                ElseIf s = "JAN" Or s = "JANUARY" Then : Month = 1
                ElseIf s = "FEB" Or s = "FEBRUARY" Then : Month = 2
                ElseIf s = "MAR" Or s = "MARCH" Then : Month = 3
                ElseIf s = "APR" Or s = "APRIL" Then : Month = 4
                ElseIf s = "MAY" Or s = "MAY" Then : Month = 5
                ElseIf s = "JUN" Or s = "JUNE" Then : Month = 6
                ElseIf s = "JUL" Or s = "JULY" Then : Month = 7
                ElseIf s = "AUG" Or s = "AUGUST" Then : Month = 8
                ElseIf s = "SEP" Or s = "SEPTEMBER" Then : Month = 9
                ElseIf s = "OCT" Or s = "OCTOBER" Then : Month = 10
                ElseIf s = "NOV" Or s = "NOVEMBER" Then : Month = 11
                ElseIf s = "DEC" Or s = "DECEMBER" Then : Month = 12
                Else
                    Dim m = System.Text.RegularExpressions.Regex.Match(s, "^(\d\d?):(\d\d)(?::(\d\d))?(AM|PM)?$", RegularExpressions.RegexOptions.IgnoreCase)
                    If m.Success Then
                        Hour = m.Groups(1).Value
                        Minute = m.Groups(2).Value
                        If m.Groups(3).Success Then Second = m.Groups(3).Value Else Second = 0
                        If m.Groups(4).Value = "PM" Then
                            If Hour >= 1 And Hour < 12 Then Hour += 12
                            If Hour = 0 Then Return Nothing
                        ElseIf m.Groups(4).Value = "AM" Then
                            If Hour > 12 Then Return Nothing
                            If Hour = 12 Then Hour = 0
                        End If
                    End If
                End If
            End If
        Loop Until c = ChrW(0)

        If Month = -1 Or Day = -1 Or Hour = -1 Or Minute = -1 Then Return Nothing
        If Year = -1 Then
            If Now.Month = 1 And Now.Day <= 2 And Month = 12 And Day >= 30 Then : Year = Now.Year + 1
            ElseIf Now.Month = 12 And Now.Day >= 30 And Month = 1 And Day <= 2 Then : Year = Now.Year - 1
            Else : Year = Now.Year
            End If
        End If
        Return New Date(Year, Month, Day, Hour, Minute, Second, DateTimeKind.Unspecified)
    End Function

    Public Function ParseUserTime(ByVal Message As String) As Date
        Dim s As String, b As New StringBuilder, i As Integer = -1, c As Char, j As Integer
        Dim Year As Short = -1, Month As Short = -1, Day As Short = -1, DayOfWeek As Short = -1, Hour As Short = -1, Minute As Short = -1, Second As Short = -1
        Dim State As Short = 0
        Do
            i += 1
            If i < Message.Length Then
                c = Message(i)
                If "()[]{}|,".Contains(c) Then Continue Do
                If c <> " "c Then b.Append(Char.ToUpper(c))
            Else
                c = ChrW(0)
            End If
            If c = " "c Or c = ChrW(0) Then
                s = b.ToString()
                b.Clear()
                If s.Length = 0 Then Continue Do
                If s.StartsWith("+") Or s.StartsWith("-") Then Return Nothing
                If Integer.TryParse(s, j) Then
                    If j < 0 Then
                        Return Nothing
                    ElseIf j >= 1000 Then
                        ' Assume it's a year.
                        If Year <> -1 Then Return Nothing
                        Year = j
                        State = 1
                    ElseIf j >= 100 Then
                        ' Won't parse three-digit years.
                        Return Nothing
                    ElseIf j >= 1 And j <= 31 And Day = -1 Then
                        ' Assume it's the day of the month.
                        If State <> 1 AndAlso (Day <> -1 Or Month <> -1 Or Year <> -1) Then Return Nothing
                        Day = j
                        State = 1
                    Else
                        ' Assume it's a two-digit year.
                        If State <> 1 AndAlso (Day <> -1 Or Month <> -1 Or Year <> -1) Then Return Nothing
                        If j >= 30 Then Year = 1900 + j Else Year = 2000 + j
                        State = 1
                    End If
                ElseIf s = "SUN" Or s = "SUNDAY" Then : DayOfWeek = 0
                ElseIf s = "MON" Or s = "MONDAY" Then : DayOfWeek = 1
                ElseIf s = "TUE" Or s = "TUESDAY" Then : DayOfWeek = 2
                ElseIf s = "WED" Or s = "WEDNESDAY" Then : DayOfWeek = 3
                ElseIf s = "THU" Or s = "THURSDAY" Then : DayOfWeek = 4
                ElseIf s = "FRI" Or s = "FRIDAY" Then : DayOfWeek = 5
                ElseIf s = "SAT" Or s = "SATURDAY" Then : DayOfWeek = 6
                ElseIf s = "JAN" Or s = "JANUARY" Then
                    If State <> 1 AndAlso (Day <> -1 Or Month <> -1 Or Year <> -1) Then Return Nothing
                    Month = 1 : State = 1
                ElseIf s = "FEB" Or s = "FEBRUARY" Then
                    If State <> 1 AndAlso (Day <> -1 Or Month <> -1 Or Year <> -1) Then Return Nothing
                    Month = 2 : State = 1
                ElseIf s = "MAR" Or s = "MARCH" Then
                    If State <> 1 AndAlso (Day <> -1 Or Month <> -1 Or Year <> -1) Then Return Nothing
                    Month = 3 : State = 1
                ElseIf s = "APR" Or s = "APRIL" Then
                    If State <> 1 AndAlso (Day <> -1 Or Month <> -1 Or Year <> -1) Then Return Nothing
                    Month = 4 : State = 1
                ElseIf s = "MAY" Or s = "MAY" Then
                    If State <> 1 AndAlso (Day <> -1 Or Month <> -1 Or Year <> -1) Then Return Nothing
                    Month = 5 : State = 1
                ElseIf s = "JUN" Or s = "JUNE" Then
                    If State <> 1 AndAlso (Day <> -1 Or Month <> -1 Or Year <> -1) Then Return Nothing
                    Month = 6 : State = 1
                ElseIf s = "JUL" Or s = "JULY" Then
                    If State <> 1 AndAlso (Day <> -1 Or Month <> -1 Or Year <> -1) Then Return Nothing
                    Month = 7 : State = 1
                ElseIf s = "AUG" Or s = "AUGUST" Then
                    If State <> 1 AndAlso (Day <> -1 Or Month <> -1 Or Year <> -1) Then Return Nothing
                    Month = 8 : State = 1
                ElseIf s = "SEP" Or s = "SEPTEMBER" Then
                    If State <> 1 AndAlso (Day <> -1 Or Month <> -1 Or Year <> -1) Then Return Nothing
                    Month = 9 : State = 1
                ElseIf s = "OCT" Or s = "OCTOBER" Then
                    If State <> 1 AndAlso (Day <> -1 Or Month <> -1 Or Year <> -1) Then Return Nothing
                    Month = 10 : State = 1
                ElseIf s = "NOV" Or s = "NOVEMBER" Then
                    If State <> 1 AndAlso (Day <> -1 Or Month <> -1 Or Year <> -1) Then Return Nothing
                    Month = 11 : State = 1
                ElseIf s = "DEC" Or s = "DECEMBER" Then
                    If State <> 1 AndAlso (Day <> -1 Or Month <> -1 Or Year <> -1) Then Return Nothing
                    Month = 12 : State = 1
                ElseIf s = "AM" Then
                    If State <> 2 Then Return Nothing
                    If Hour > 12 Then Return Nothing
                    If Hour = 12 Then Hour = 0
                    State = 3
                ElseIf s = "PM" Then
                    If State <> 2 Then Return Nothing
                    If Hour >= 1 And Hour < 12 Then Hour += 12
                    If Hour = 0 Then Return Nothing
                    State = 3
                Else
                    Dim m = System.Text.RegularExpressions.Regex.Match(s, "^(\d\d?):(\d\d)(?::(\d\d))?(AM|PM)?$", RegularExpressions.RegexOptions.IgnoreCase)
                    If m.Success Then
                        If Hour <> -1 Or Minute <> -1 Or Second <> -1 Then Return Nothing
                        Hour = m.Groups(1).Value
                        Minute = m.Groups(2).Value
                        If m.Groups(3).Success Then Second = m.Groups(3).Value Else Second = 0
                        If m.Groups(4).Value = "PM" Then
                            If Hour >= 1 And Hour < 12 Then Hour += 12
                            If Hour = 0 Then Return Nothing
                        ElseIf m.Groups(4).Value = "AM" Then
                            If Hour > 12 Then Return Nothing
                            If Hour = 12 Then Hour = 0
                        End If
                        State = 2
                    Else
                        If Day <> -1 Or Month <> -1 Then Return Nothing
                        m = System.Text.RegularExpressions.Regex.Match(s, "^(?:(?:(\d{1,4})([/.-])(?:(\d{1,2})|(JAN|FEB|MAR|APR|MAY|JUN|JUL|AUG|SEP|OCT|NOV|DEC))\2(\d{1,2}))|(?:(\d{1,2})([/.-])(?:(\d{1,2})|(JAN|FEB|MAR|APR|MAY|JUN|JUL|AUG|SEP|OCT|NOV|DEC))\7(\d{1,4})))$", RegularExpressions.RegexOptions.IgnoreCase)
                        If m.Success Then
                            If m.Groups(1).Success Then
                                Year = Integer.Parse(m.Groups(1).Value)
                                Day = Integer.Parse(m.Groups(5).Value)
                                If m.Groups(3).Success Then
                                    Month = Integer.Parse(m.Groups(3).Value)
                                Else
                                    Month = Array.IndexOf({"JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC"}, m.Groups(4).Value) + 1
                                End If
                            Else
                                Day = Integer.Parse(m.Groups(6).Value)
                                Year = Integer.Parse(m.Groups(10).Value)
                                If m.Groups(3).Success Then
                                    Month = Integer.Parse(m.Groups(8).Value)
                                Else
                                    Month = Array.IndexOf({"JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC"}, m.Groups(9).Value) + 1
                                End If
                            End If
                        End If
                        State = 0
                    End If
                End If
            End If
        Loop Until c = ChrW(0)

        If Hour = -1 Then Return Nothing
        If Day = -1 Then
            If DayOfWeek = -1 Then Return New Date(999, 1, 3, Hour, Minute, Second, DateTimeKind.Unspecified)
            Return New Date(999, 1, 6 + DayOfWeek, Hour, Minute, Second, DateTimeKind.Unspecified)
        ElseIf Year = -1 Then
            If Now.Month = 1 And Now.Day <= 2 And Month = 12 And Day >= 30 Then : Year = Now.Year + 1
            ElseIf Now.Month = 12 And Now.Day >= 30 And Month = 1 And Day <= 2 Then : Year = Now.Year - 1
            Else : Year = Now.Year
            End If
        End If
        Dim result = New Date(Year, Month, Day, Hour, Minute, Second, DateTimeKind.Unspecified)
        If DayOfWeek <> -1 And result.DayOfWeek <> DayOfWeek Then Return Nothing
        Return result
    End Function

    Public Function FindTimeZone(ByRef Name As String, ByRef Offset As TimeSpan) As Boolean
        Name = Name.Trim()
        ' https://www.debuggex.com/r/PdXgjokCw9M2l3xe/1
        Dim m = Regex.Match(Name, "^\s*(?:((?:GMT|UTC)\s*$)|(?:(?:GMT|UTC)\s*)?([+-])\s*(?:(\d\d?)(?::(\d\d?)(?::(\d\d?))?)?|(\d\d)(?:(\d\d)(\d\d)?)?)\s*)$", RegexOptions.IgnoreCase)
        If m.Success Then
            Dim SecondsOffset As Integer
            If m.Groups(1).Success Then
                Offset = TimeSpan.Zero
                Name = "UTC"
                Return True
            End If
            If m.Groups(3).Success Then
                SecondsOffset = Integer.Parse(m.Groups(3).Value) * 3600
                If m.Groups(4).Success Then SecondsOffset += Integer.Parse(m.Groups(4).Value) * 60
                If m.Groups(5).Success Then SecondsOffset += Integer.Parse(m.Groups(5).Value)
                Name = String.Concat({"UTC ", m.Groups(2).Value, Integer.Parse(m.Groups(3).Value).ToString("0"),
                                      If(m.Groups(4).Success, ":" & Integer.Parse(m.Groups(4).Value).ToString("00"), ""),
                                      If(m.Groups(5).Success, ":" & Integer.Parse(m.Groups(5).Value).ToString("00"), "")})
            Else
                SecondsOffset = Integer.Parse(m.Groups(6).Value) * 3600
                If m.Groups(7).Success Then SecondsOffset += Integer.Parse(m.Groups(7).Value) * 60
                If m.Groups(8).Success Then SecondsOffset += Integer.Parse(m.Groups(8).Value)
                Name = String.Concat({"UTC ", m.Groups(2).Value, Integer.Parse(m.Groups(6).Value).ToString("0"),
                                      If(m.Groups(7).Success, ":" & Integer.Parse(m.Groups(7).Value).ToString("00"), ""),
                                      If(m.Groups(8).Success, ":" & Integer.Parse(m.Groups(8).Value).ToString("00"), "")})
            End If
            If m.Groups(2).Value = "-" Then SecondsOffset *= -1
            Offset = TimeSpan.FromSeconds(SecondsOffset)
            Return True
        End If

        Name = Name.ToUpper()
        For Each TimeZone In TimeZones
            If Name.Equals(TimeZone.Name, StringComparison.OrdinalIgnoreCase) Or Name.Equals(TimeZone.Abbreviation, StringComparison.OrdinalIgnoreCase) Then
                Name = TimeZone.Abbreviation
                Offset = TimeZone.Offset
                Return True
            End If
        Next
        Return Nothing
    End Function

    <Command("time", 1, 1, "time [zone]  or  time <time> [in <zone>] [to <zone>]",
    "Shows the time, or converts a time between zones.")>
    Public Sub CommandTime(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim arg = args(0).ToUpper()

        Dim d As Date, Source As String = Nothing, Target As String = Nothing
        Dim SourceOffset As TimeSpan, TargetOffset As TimeSpan
        Dim s As String, b As New StringBuilder, i As Integer = -1, c As Char
        Dim tb As New StringBuilder
        Dim State As Integer = 0

        SyncLock Requests
            For j = 0 To Requests.Count - 1
                If Requests(j).Connection Is Connection And Requests(j).Sender.Split("!"c)(0) = Sender.Split("!"c)(0) Then
                    Reply(Requests(j).Connection, Requests(j).Channel, Requests(j).Sender, "You've already made a pending request.")
                    Return
                End If
            Next

            Do
                i += 1
                If i < arg.Length Then
                    c = arg(i)
                    If c <> " "c Then b.Append(Char.ToUpper(c))
                Else
                    c = ChrW(0)
                End If
                If c = " "c Or c = ChrW(0) Then
                    s = b.ToString()
                    b.Clear()
                    If s.Length = 0 Then Continue Do

                    If State = 0 Then
                        If s = "IN" Then
                            d = ParseUserTime(tb.ToString())
                            If d = Nothing Then
                                Reply(Connection, Channel, Sender, "Could not parse that time.")
                                Return
                            End If
                            tb.Clear()
                            State = 1
                        ElseIf s = "TO" Then
                            d = ParseUserTime(tb.ToString())
                            If d = Nothing Then
                                Reply(Connection, Channel, Sender, "Could not parse that time.")
                                Return
                            End If
                            tb.Clear()
                            State = 2
                        ElseIf c = ChrW(0) Then
                            tb.Append(" ")
                            tb.Append(s)
                            d = ParseUserTime(tb.ToString())
                            If d = Nothing Then
                                Reply(Connection, Channel, Sender, "Could not parse that time.")
                                Return
                            End If
                            tb.Clear()
                        ElseIf tb.Length = 0 Then
                            tb.Append(s)
                        Else
                            tb.Append(" ")
                            tb.Append(s)
                        End If
                    ElseIf State = 1 Then
                        If c = ChrW(0) Then
                            tb.Append(" ")
                            tb.Append(s)
                        End If
                        If s = "TO" Or c = ChrW(0) Then
                            If tb.Length > 0 Then
                                s = tb.ToString()
                                tb.Clear()
                                If s.Equals("LOCAL", StringComparison.OrdinalIgnoreCase) Or s.Equals("MINE", StringComparison.OrdinalIgnoreCase) Then
                                    Source = Nothing
                                Else
                                    Source = s
                                    If Not FindTimeZone(Source, SourceOffset) Then
                                        Reply(Connection, Channel, Sender, String.Format("Time zone '{0}' is not recognised.", Source))
                                        Return
                                    End If
                                End If
                                State = 2
                            End If
                        ElseIf tb.Length = 0 Then
                            tb.Append(s)
                        Else
                            tb.Append(" ")
                            tb.Append(s)
                        End If
                    ElseIf State = 2 Then
                        If tb.Length = 0 Then
                            tb.Append(s)
                        Else
                            tb.Append(" ")
                            tb.Append(s)
                        End If
                        If c = ChrW(0) Then
                            If tb.Length > 0 Then
                                s = tb.ToString()
                                If s.Equals("LOCAL", StringComparison.OrdinalIgnoreCase) Or s.Equals("MINE", StringComparison.OrdinalIgnoreCase) Then
                                    Target = Nothing
                                Else
                                    Target = s
                                    If Not FindTimeZone(Target, TargetOffset) Then
                                        Reply(Connection, Channel, Sender, String.Format("Time zone '{0}' is not recognised.", Target))
                                        Return
                                    End If
                                End If
                                State = 2
                            End If
                        End If
                    End If
                End If
            Loop Until c = ChrW(0)
            If Source Is Nothing And Target Is Nothing Then
                Reply(Connection, Channel, Sender, String.Format("You must specify a source or target time zone.", Target))
                Reply(Connection, Channel, Sender, String.Format("Command usage: !time <time> [in <zone>] [to <zone>]", Target))
                Return
            End If
            If Source Is Nothing Or Target Is Nothing Then
                Dim newRequest As New RequestData
                newRequest.Connection = Connection
                newRequest.Channel = Channel
                newRequest.Sender = Sender
                newRequest.Time = d
                newRequest.RequestTime = Now
                newRequest.RequestTimer = New Timer(15000)
                AddHandler newRequest.RequestTimer.Elapsed, AddressOf RequestTimer_Elapsed
                newRequest.RequestTimer.AutoReset = False
                newRequest.RequestTimer.Start()
                If Source Is Nothing Then
                    newRequest.IsLocalTimeFirst = True
                    newRequest.TimeZone = TargetOffset
                    newRequest.TimeZoneName = Target
                Else
                    newRequest.IsLocalTimeFirst = False
                    newRequest.TimeZone = SourceOffset
                    newRequest.TimeZoneName = Source
                End If
                Requests.Add(newRequest)
                Say(Connection, Sender.Split("!"c)(0), ChrW(1) & "TIME" & ChrW(1), SayOptions.NoticeNever)
            Else
                CommandTime2(Connection, Sender, Channel, d, Source, SourceOffset, Target, TargetOffset)
            End If
        End SyncLock
    End Sub

    Public Sub CommandTime2(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Time As Date, ByVal Source As String, ByVal SourceOffset As TimeSpan, ByVal Target As String, ByVal TargetOffset As TimeSpan)
        Dim newTime = New DateTimeOffset(Time, SourceOffset).ToOffset(TargetOffset)

        Say(Connection, Channel, String.Format("{0} {1} equals$k9 {2} {3}$o.", TimeString(Time), Source, TimeString(newTime.DateTime), Target))
    End Sub

    Public Function TimeString(ByVal Time As Date) As String
        If Time.Year = 999 And Time.Month = 1 Then
            If Time.Day = 2 Then
                Return Time.TimeOfDay.ToString() & " the preceding day"
            ElseIf Time.Day = 3 Then
                Return Time.TimeOfDay.ToString()
            ElseIf Time.Day = 4 Then
                Return Time.TimeOfDay.ToString() & " the following day"
            ElseIf Time.Day >= 6 And Time.Day <= 14 Then
                Return Time.TimeOfDay.ToString() & {" on Sunday", " on Monday", " on Tuesday", " on Wednesday", " on Thursday", " on Friday", " on Saturday"}(Time.DayOfWeek)
            End If
        End If
        Return Time.ToString()
    End Function

    Public Sub RequestTimer_Elapsed(ByVal sender As Object, ByVal e As ElapsedEventArgs)
        SyncLock Requests
            For i = 0 To Requests.Count - 1
                If Requests(i).RequestTimer Is sender Then
                    Reply(Requests(i).Connection, Requests(i).Channel, Requests(i).Sender, "Didn't receive a CTCP TIME reply from you.")
                    Requests(i).RequestTimer.Dispose()
                    Requests.RemoveAt(i)
                    Return
                End If
            Next
        End SyncLock
    End Sub

    Public Sub LoadTimeZones()
        Dim ColumnIndex() As Integer = {-1, -1, -1, -1, -1}, HeaderLine As String = Nothing
        Dim LineNumber As Integer = 0

        TimeZones.Clear()

        If Not System.IO.File.Exists("timezones.csv") Then
            ReportProblem("timezones.csv", 0, "The file is missing.")
            Return
        End If

        Dim sr = My.Computer.FileSystem.OpenTextFileReader("timezones.csv")

        ' Find a header.
        Do Until sr.EndOfStream
            LineNumber += 1
            Dim s = sr.ReadLine
            If s.StartsWith("#") Then
                If s.StartsWith("# ") Then Continue Do
                HeaderLine = s.Substring(1)
                Continue Do
            End If
            Exit Do
        Loop

        If HeaderLine Is Nothing Then
            ReportProblem("timezones.csv", LineNumber, "I don't see a header row.")
            sr.Close()
            Throw New FormatException("Parsing failed: the data file doesn't include a header row.")
        Else
            'Parse the header.
            Dim Fields = HeaderLine.Split({","c})
            For i = 0 To UBound(Fields)
                Dim FieldIndex As Integer
                Select Case Fields(i).ToUpper
                    Case "ABBREVIATION"
                        FieldIndex = TimeZonesColumns.ColumnAbbreviation
                    Case "NAME", "FULL NAME"
                        FieldIndex = TimeZonesColumns.ColumnName
                    Case "OFFSET"
                        FieldIndex = TimeZonesColumns.ColumnOffset
                    Case Else
                        Continue For
                End Select
                ColumnIndex(FieldIndex) = i
            Next

            If ColumnIndex(TimeZonesColumns.ColumnAbbreviation) = -1 Or ColumnIndex(TimeZonesColumns.ColumnOffset) = -1 Then
                ReportProblem("timezones.csv", LineNumber, "The header row doesn't include an 'Abbreviation' and 'Offset' column.")
                sr.Close()
                Throw New FormatException("Parsing failed: the header row is missing an 'Abbreviation' and/or 'Offset' field.")
            End If
        End If

        Do Until sr.EndOfStream
            Dim fields() As String

            LineNumber += 1
            Dim s = sr.ReadLine
            If s.Trim = "" Or s.StartsWith("#") Then Continue Do 'It's a comment, so ignore it.

            fields = s.Split(","c)
            If fields.Length < 2 Then
                Debug.Print("I found an anomaly in timezones.csv, line " & LineNumber & ":" & vbCrLf & "    The line doesn't have enough fields. Andrio, if you're there, please take a look at it.")
                OutputLine("\cWHITEI found an anomaly in timezones.csv, line " & LineNumber & ":" & vbCrLf & "    The line doesn't have enough fields. Andrio, if you're there, please take a look at it.\r")
                Continue Do
            End If

            Dim newItem As New TimeZone

            If fields.Length <= ColumnIndex(TimeZonesColumns.ColumnAbbreviation) Then
                ReportProblem("timezones.csv", LineNumber, "The entry has no ID.")
                Continue Do
            End If
            If fields.Length <= ColumnIndex(TimeZonesColumns.ColumnOffset) Then
                ReportProblem("timezones.csv", LineNumber, "The entry has no offset.")
                Continue Do
            End If

            newItem.Abbreviation = fields(ColumnIndex(TimeZonesColumns.ColumnAbbreviation))
            If ColumnIndex(TimeZonesColumns.ColumnName) <> -1 AndAlso fields.Length > ColumnIndex(TimeZonesColumns.ColumnName) Then _
                newItem.Name = fields(ColumnIndex(TimeZonesColumns.ColumnName))

            ' Parse the offset.
            Dim f As Single
            If Single.TryParse(fields(ColumnIndex(TimeZonesColumns.ColumnOffset)), f) Then
                newItem.Offset = TimeSpan.FromHours(f)
            Else
                If Not FindTimeZone(fields(ColumnIndex(TimeZonesColumns.ColumnOffset)), newItem.Offset) Then
                    ReportProblem("timezones.csv", LineNumber, "The offset is not in a recognised format. e.g. valid is UTC+09:30")
                    Continue Do
                End If
            End If

            TimeZones.Add(newItem)
        Loop

        sr.Close()
    End Sub

    Private Sub ReportProblem(ByVal File As String, ByVal LineNumber As Integer, ByVal Message As String)
        OutputLine("\cWHITEI found a problem with " & File & If(LineNumber <> 0, ", line " & LineNumber, "") & ":" & vbCrLf & "    " & Message & "\r")
        Problems.Add(If(LineNumber <> 0, "Line " & LineNumber & ": ", "") & Message)
    End Sub

    <Command({"reloadzones", "reloadzone", "reloadzonedb", "zonereload", "zonesreload", "zonedbreload", "zonedbparse"}, 0, 0,
 "reloadzones",
 "Reloads the time zone database.",
  ".reload")>
    Public Sub CommandReloadZones(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        SyncLock Problems
            Problems.Clear()
            Try
                LoadTimeZones()
            Catch ex As FormatException
                Say(Connection, Channel, ex.Message)
                Return
            End Try
            Say(Connection, Channel, "Loaded " & TimeZones.Count & If(Items.Count = 1, " zone", " zones") & " from the data file and found " & Problems.Count & If(Problems.Count = 1, " error.", " errors."))
            For i = 0 To If(Problems.Count > 5, 4, Problems.Count - 1)
                Reply(Connection, Channel, Sender, Problems(i))
            Next
            If Problems.Count > 5 Then Reply(Connection, Channel, Sender, "plus " & Problems.Count - 5 & If(Problems.Count = 6, " more error", " more errors") & "; see the logs.")
        End SyncLock
    End Sub


End Class
