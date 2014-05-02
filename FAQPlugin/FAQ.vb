Imports VBot
Imports System.Text.RegularExpressions

Public Class Plugin
    Inherits VBot.Plugin

    Dim Contexts As New SortedDictionary(Of String, String())(StringComparer.OrdinalIgnoreCase)
    Dim FAQs As New SortedDictionary(Of String, FAQEntry)(StringComparer.OrdinalIgnoreCase)
    Dim Aliases As New SortedDictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)

    Dim NoShortcutChannels As String() = {}

    Dim LastEntry As String

    Public Overrides ReadOnly Property Name As String
        Get
            Return "FAQ"
        End Get
    End Property

    Class FAQEntry
        Public Data As String
        Public Regexes() As String = {}
        Public LastAccessTimes As New Dictionary(Of String, List(Of Date))(StringComparer.OrdinalIgnoreCase)
        Public RateLimitCount As Integer = 1
        Public RateLimitInterval As Integer = 120
        Public HideLabel As Boolean = False
        Public Hidden As Boolean = False
        Public NoticeOnJoin As Boolean = True
    End Class

    Sub New(ByVal Key As String)
        If My.Computer.FileSystem.FileExists("Config\" & Key & ".ini") Then
            Dim Reader = My.Computer.FileSystem.OpenTextFileReader("Config\" & Key & ".ini"), s As String, Section As String = ""
            Do Until Reader.EndOfStream
                s = Reader.ReadLine

                Dim Match As System.Text.RegularExpressions.Match
                Match = System.Text.RegularExpressions.Regex.Match(s, "^\s*\[(?<Section>.*?)\]?\s*$")
                If Match.Success Then
                    Section = Match.Groups("Section").Value
                    Continue Do
                End If

                Match = System.Text.RegularExpressions.Regex.Match(s, "^\s*(?<Property>(?>[^=]*))=(?<Value>.*)$")
                If Match.Success Then
                    Dim Identifier = Match.Groups("Property").Value
                    Dim Value = Match.Groups("Value").Value

                    If Identifier.ToLower = "noshortcutchannels" Then
                        NoShortcutChannels = Value.Split({","c, " "c})
                    End If
                    Continue Do
                End If
            Loop
            Reader.Close()
        End If

        LoadAllData()
    End Sub

    Private Sub CheckFAQ(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Action As String, ByVal Extra As String)
        Dim IsSpecificChannel As Boolean  ' This will determine whether we show the context name or not.

        For Each FAQ In FAQs
            If FAQ.Value.Regexes Is Nothing Then Continue For

            Dim Context = GetContext(FAQ.Key)
            Dim Key = GetKey(FAQ.Key)

            ' Check that the context includes this channel.
            Dim Channels = If(Context = "*" Or Context = "", {"*"}, Contexts(Context))
            If Connection Is Nothing Then
                If Channels.Contains("!" & MyKey & "/" & Channel) Or
    Channels.Contains("!*/" & Channel.Split("/"c)(1)) Then
                    IsSpecificChannel = True
                ElseIf Channels.Contains(Channel.Split("/"c)(0) & "/*") Or
                Channels.Contains("!*/*") Or Channels.Contains("*") Or Channels.Contains("!*") Then
                    IsSpecificChannel = False
                Else
                    Continue For
                End If
            Else
                If Channels.Contains(Connection.Address & "/" & Channel) Or
                    Channels.Contains("*/" & Channel) Then
                    IsSpecificChannel = True
                ElseIf Channels.Contains(Connection.Address & "/*") Or
                Channels.Contains("*/*") Or Channels.Contains("*") Then
                    IsSpecificChannel = False
                Else
                    Continue For
                End If
            End If

            Dim Match As System.Text.RegularExpressions.Match = Nothing
            For Each Regex In FAQ.Value.Regexes
                ' Make sure that the first field 'MSG:' etc. is correct.
                If Regex.Contains(":") AndAlso {"MSG", "ACTION", "JOIN", "PART", "QUIT", "KICK", "EXIT", "NICK"}.Contains(Regex.Split(":"c)(0).ToUpper) AndAlso _
                    Regex.Split(":"c)(0).ToUpper <> Action Then Continue For
                If Action <> "MSG" And (Not Regex.Contains(":") OrElse Not {"MSG", "ACTION", "JOIN", "PART", "QUIT", "KICK", "EXIT", "NICK"}.Contains(Regex.Split(":"c)(0).ToUpper)) Then Continue For
                Dim fields = Regex.Split({":"c}, 5)
                Dim mask As String = "*", perm As String = "*", chan As String = "*", message As String = ""

                For i = 1 To fields.Count - 2
                    If fields(i).Contains("!") Then mask = fields(i) Else If fields(i).Contains("#") Then chan = fields(i) Else 
                    If fields(i).StartsWith("^") Then
                        message &= If(message = "", "", ":") & fields(i)
                    ElseIf fields(i).Split("."c)(0).ToLower = "me" Or Plugins.Keys.Contains(fields(i).Split("."c)(0), StringComparer.OrdinalIgnoreCase) Then
                        perm = fields(i)
                    Else
                        message &= If(message = "", "", ":") & fields(i)
                    End If
                Next
                message &= If(message = "", "", ":") & fields(fields.Count - 1)
                If message = "" Then message = ".*"

                If Extra IsNot Nothing Then Match = System.Text.RegularExpressions.Regex.Match(Extra, message, RegexOptions.IgnoreCase)
                If Sender Like mask And If(Not chan.Contains("/") Or Connection Is Nothing, Channel, Connection.Address & "/" & Channel) Like chan.Replace("#", "[#]") And (perm = "*" OrElse UserHasPermission(Connection, Channel, Sender, perm)) And (Extra Is Nothing OrElse Match.Success) Then

                    Dim laKey As String
                    If Connection Is Nothing Then
                        laKey = Channel.Split(">"c)(0) & ">" & Sender.Split("!"c)(0)
                    Else
                        laKey = Connection.Address & "/" & Sender.Split("!"c)(0)
                    End If

                    If Action <> "JOIN" Or Not FAQ.Value.NoticeOnJoin Then
                        If (FAQ.Value.RateLimitCount > 0 And FAQ.Value.RateLimitInterval > 0) AndAlso
                                       FAQ.Value.LastAccessTimes.ContainsKey(laKey) AndAlso
                                       FAQ.Value.LastAccessTimes(laKey).Count >= FAQ.Value.RateLimitCount Then
                            For i = FAQ.Value.LastAccessTimes(laKey).Count - 1 To FAQ.Value.LastAccessTimes(laKey).Count - FAQ.Value.RateLimitCount Step -1
                                If Now - FAQ.Value.LastAccessTimes(laKey)(i) >= TimeSpan.FromSeconds(FAQ.Value.RateLimitInterval) Then GoTo Display
                            Next
                            Continue For
                        End If
                    End If
Display:

                    If Action = "JOIN" And FAQ.Value.NoticeOnJoin Then
                        For Each Line In FAQ.Value.Data.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.None)
                            ' Resolve certain %identifiers%.
                            Dim DisplayLine As String = Line.Replace("%nickname%", Sender.Split("!"c)(0)).Replace("%channel%", If(Channel.StartsWith("!"), Channel.Split("/"c)(1), Channel)).Replace("%me%", Nickname(Connection))
                            Dim DisplayKey As String = If(IsSpecificChannel, "", GetContext(FAQ.Key) & "/") & "$k12" & GetKey(FAQ.Key)

                            Reply(Connection, Channel, Sender, String.Format(If(FAQ.Value.HideLabel, "{0}", "$k2[FAQ: {1}$k2]$o {0}"), DisplayLine, DisplayKey))
                            Threading.Thread.Sleep(600)
                        Next
                    Else
                        For Each Line In FAQ.Value.Data.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.None)
                            Dim DisplayLine As String = Line.Replace("%nickname%", Sender.Split("!"c)(0)).Replace("%channel%", If(Channel.StartsWith("!"), Channel.Split("/"c)(1), Channel)).Replace("%me%", Nickname(Connection))
                            Dim DisplayKey As String = If(IsSpecificChannel, "", GetContext(FAQ.Key) & "/") & "$k12" & GetKey(FAQ.Key)

                            Say(Connection, Channel, String.Format(If(FAQ.Value.HideLabel, "{0}", "$k2[FAQ: {1}$k2]$o {0}"), DisplayLine, DisplayKey))
                            Threading.Thread.Sleep(600)
                        Next
                    End If

                    If Connection Is Nothing Then
                        laKey = "!" & Channel.Split(">"c)(0) & ">" & Sender.Split("!"c)(0)
                        If Not FAQ.Value.LastAccessTimes.ContainsKey(laKey) Then _
                            FAQ.Value.LastAccessTimes.Add(laKey, New List(Of Date))
                        If FAQ.Value.LastAccessTimes(laKey).Count = FAQ.Value.RateLimitCount Then FAQ.Value.LastAccessTimes(laKey).RemoveAt(0)
                        FAQ.Value.LastAccessTimes(laKey).Add(Now)
                    Else
                        For Each User In Connection.Channels(Channel).Users
                            laKey = Connection.Address & "/" & User.Value.Nickname
                            If Not FAQ.Value.LastAccessTimes.ContainsKey(laKey) Then _
                                FAQ.Value.LastAccessTimes.Add(laKey, New List(Of Date))
                            If FAQ.Value.LastAccessTimes(laKey).Count = FAQ.Value.RateLimitCount Then FAQ.Value.LastAccessTimes(laKey).RemoveAt(0)
                            FAQ.Value.LastAccessTimes(laKey).Add(Now)
                        Next
                    End If

                    Continue For
                End If
            Next
        Next

    End Sub

    Public Overrides Sub OnChannelMessage(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Message As String)
        MyBase.OnChannelMessage(Connection, Sender, Channel, Message)
        CheckFAQ(Connection, Sender, Channel, "MSG", Message)
    End Sub

    Public Overrides Sub OnChannelAction(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Message As String)
        MyBase.OnChannelMessage(Connection, Sender, Channel, Message)
        CheckFAQ(Connection, Sender, Channel, "ACTION", Message)
    End Sub

    Public Overrides Sub OnChannelJoin(ByVal Connection As VBot.IRCConnection, ByVal Sender As String, ByVal Channel As String)
        MyBase.OnChannelJoin(Connection, Sender, Channel)
        CheckFAQ(Connection, Sender, Channel, "JOIN", Nothing)
    End Sub

    Public Overrides Sub OnChannelPart(ByVal Connection As VBot.IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Reason As String)
        MyBase.OnChannelPart(Connection, Sender, Channel, Reason)
        CheckFAQ(Connection, Sender, Channel, "PART", Reason)
    End Sub

    Public Overrides Sub OnChannelKick(ByVal Connection As VBot.IRCConnection, ByVal Sender As VBot.IRCConnection.IRCUser, ByVal Channel As String, ByVal Target As String, ByVal Reason As String)
        CheckFAQ(Connection, Sender, Channel, "KICK", Target & ":" & Reason)
    End Sub

    Public Overrides Sub OnQuit(ByVal Connection As VBot.IRCConnection, ByVal Sender As String, ByVal Reason As String)
        MyBase.OnQuit(Connection, Sender, Reason)
        CheckFAQ(Connection, Sender, "*", "QUIT", Reason)
    End Sub

    Public Overrides Sub OnChannelExit(ByVal Connection As VBot.IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Reason As String)
        MyBase.OnChannelExit(Connection, Sender, Channel, Reason)
        CheckFAQ(Connection, Sender, Channel, "EXIT", Reason)
    End Sub

    Public Overrides Sub OnNicknameChange(ByVal Connection As VBot.IRCConnection, ByVal Sender As VBot.IRCConnection.IRCUser, ByVal NewNick As String)
        MyBase.OnNicknameChange(Connection, Sender, NewNick)
        CheckFAQ(Connection, Sender, "*", "NICK", NewNick)
    End Sub

    Private Function FindFAQ(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal Sender As String, ByVal Request As String, Optional ByVal PermissionNeeded As String = "") As String
        ' Handle the identifier '.' to be the last FAQ entry edited.
        If Request = "." Then Request = LastEntry Else LastEntry = Request

        If FAQs.ContainsKey(Request) Then
            Return Request
        ElseIf Aliases.ContainsKey(Request) Then
            Return Aliases(Request)
        Else
            For Each Context In Contexts
                ' If the context specifically includes this channel, see if it includes a FAQ entry that matches the request.
                ' e.g. Minecraft/Brewing can be referred to as just 'Brewing' in a Minecraft channel.
                If Connection Is Nothing Then
                    If (Context.Value.Contains("!" & MyKey & "/" & Channel) Or
                        Context.Value.Contains("!*/" & Channel.Split("/"c)(1))) Then
                        GoTo CheckPermission
                    End If
                Else
                    If (Context.Value.Contains(Connection.Address & "/" & Channel) Or
                        Context.Value.Contains("*/" & Channel)) Then
                        GoTo CheckPermission
                    End If
                End If
                Continue For

CheckPermission:
                If FAQs.ContainsKey(Context.Key & "/" & Request) And
              (PermissionNeeded = "" OrElse UserHasPermission(Connection, Channel, Sender, MyKey & "." & PermissionNeeded & "." & Context.Key.Replace("/"c, "."c))) Then
                    FindFAQ &= " " & Context.Key & "/" & Request
                ElseIf Aliases.ContainsKey(Context.Key & "/" & Request) Then
                    Dim Target = Aliases(Context.Key & "/" & Request)
                    If (PermissionNeeded = "" OrElse UserHasPermission(Connection, Channel, Sender, MyKey & "." & PermissionNeeded & "." & Context.Key.Replace("/"c, "."c))) Then
                        FindFAQ &= " " & Target
                        If Request <> "." Then LastEntry = Target
                    End If
                End If

            Next
        End If
        If FindFAQ = Nothing Then Throw New KeyNotFoundException Else Return FindFAQ.Substring(1)
    End Function

    Private Function FindContext(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal Sender As String, ByVal PermissionNeeded As String) As String()
        Dim FoundContext As List(Of String)
        For Each lContext In Contexts
            If Connection Is Nothing Then
                If (lContext.Value.Contains("!" & MyKey & "/" & Channel) Or
                    lContext.Value.Contains("!*/" & Channel.Split("/"c)(1))) Then
                    If FoundContext Is Nothing Then FoundContext = New List(Of String)
                    If UserHasPermission(Connection, Channel, Sender, MyKey & "." & PermissionNeeded & "." & lContext.Key.Replace("/"c, "."c)) Then _
                        FoundContext.Add(lContext.Key)
                End If
            Else
                If (lContext.Value.Contains(Connection.Address & "/" & Channel) Or
                    lContext.Value.Contains("*/" & Channel)) Then
                    If FoundContext Is Nothing Then FoundContext = New List(Of String)
                    If UserHasPermission(Connection, Channel, Sender, MyKey & "." & PermissionNeeded & "." & lContext.Key.Replace("/"c, "."c)) Then _
                        FoundContext.Add(lContext.Key)
                End If
            End If
        Next
        Return If(FoundContext Is Nothing, Nothing, FoundContext.ToArray)
    End Function

    Private Function GetContext(ByVal Key As String) As String
        If Not Key.Contains("/") Then Return "*"
        Dim fields = Key.Split("/"c)
        Do
            ReDim Preserve fields(UBound(fields) - 1) ' This will truncate the last element from the array.
        Loop Until Contexts.ContainsKey(String.Join("/", fields)) Or String.Join("/", fields) = "*" Or fields.Count = 0
        Return String.Join("/", fields)
    End Function
    Private Function GetKey(ByVal Key As String) As String
        If Not Key.Contains("/") Then Return Key
        Dim fields = Key.Split("/"c)
        Do
            ReDim Preserve fields(UBound(fields) - 1) ' This will truncate the last element from the array.
        Loop Until Contexts.ContainsKey(String.Join("/", fields)) Or String.Join("/", fields) = "*" Or fields.Count = 0
        Return String.Join("/"c, Key.Split("/"c).Skip(fields.Count))
    End Function

    Public Overrides Sub OnSave()
        Try
            Dim Writer = My.Computer.FileSystem.OpenTextFileWriter("Config\" & MyKey & ".ini", False)

            Writer.WriteLine("[Settings]")
            Writer.WriteLine("NoShortcutChannels=" & String.Join(",", NoShortcutChannels))

            Writer.Close()
        Catch ex As Exception
            OutputLine("\cGRAY[\cREDERROR\cGRAY] \cWHITEI was unable to save settings for plugin '" & MyKey & "': \cRED$k04" & ex.Message & "\r")
        End Try
        SaveAllData()
    End Sub

#Region "Commands"

    <Regex({"^\? (?<Key>[^ ]+)( (?<Target>.+))?"},
    Nothing, 3, False)>
    Public Sub RegexFAQ(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As System.Text.RegularExpressions.Match)
        If Not ShortcutCheck(Connection, Channel) Then Return
        If Match.Groups("Target").Success Then
            CommandFAQ(Connection, Sender, Channel, {Match.Groups("Key").Value, Match.Groups("Target").Value})
        Else
            CommandFAQ(Connection, Sender, Channel, {Match.Groups("Key").Value})
        End If
    End Sub
    <Command("faq", 1, 2,
  "faq <key> [nickname][@|@@]",
  "Displays a FAQ, or, if you don't specify arguments, displays a list of FAQs." & vbCrLf & "Specify the $k10nickname$k11@$o argument to highlight a given IRC user, or  $k10nickname$k11@@$o to send the message privately to a given IRC user (default yourself).")>
    Public Sub CommandFAQ(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        ' Dim aContext As String = ""
        Dim aKey As String = ""

        Try
            aKey = FindFAQ(Connection, Channel, Sender, args(0))
            If aKey.Contains(" ") Then
                Reply(Connection, Channel, Sender.Split("!"c)(0), "That specification matches " & Choose(Choose("more than one ", "multiple ") & "FAQ entries."))
                Reply(Connection, Channel, Sender.Split("!"c)(0), "Please specify one of $k07" & String.Join("$o, $k07", aKey.Split(" "c)) & "$o.")
                Return
            End If
        Catch ex As KeyNotFoundException
            Reply(Connection, Channel, Sender, "That FAQ entry " & VBot.Choose("doesn't exist.", "hasn't been entered.", "isn't defined."))
            Return
        End Try

        Dim FAQ As FAQEntry = FAQs(aKey)

        Dim laKey As String
        If Connection Is Nothing Then
            laKey = "!" & Channel.Split(">"c)(0) & ">" & Sender.Split("!"c)(0)
        Else
            laKey = Connection.Address & "/" & Sender.Split("!"c)(0)
        End If

        If args.Count = 2 AndAlso args(1).EndsWith("@@") Then
            For Each Line In FAQ.Data.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.None)
                ' Resolve certain %identifiers%.
                Dim DisplayLine As String = Line.Replace("%nickname%", Sender.Split("!"c)(0)).Replace("%channel%", If(Channel.StartsWith("!"), Channel.Split("/"c)(1), Channel)).Replace("%me%", Nickname(Connection))
                ' Show the context name if, and only if, it was specified in the request.
                Dim DisplayKey As String = If(args(0).ToLower = aKey.ToLower, GetContext(aKey) & "/", "") & "$k12" & GetKey(aKey)

                Reply(Connection, Channel, args(1).TrimEnd("@"c), String.Format("$k2[FAQ: $k12{1}$k2]$o {2}{0}", DisplayLine, DisplayKey, If(args.Count = 2 AndAlso args(1).TrimEnd("@"c), "$b" & args(1).TrimStart(">"c) & "$b: ", "")))
                Threading.Thread.Sleep(600)
            Next
        Else
            For Each Line In FAQ.Data.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.None)
                ' Resolve certain %identifiers%.
                Dim DisplayLine As String = Line.Replace("%nickname%", Sender.Split("!"c)(0)).Replace("%channel%", If(Channel.StartsWith("!"), Channel.Split("/"c)(1), Channel)).Replace("%me%", Nickname(Connection))
                ' Show the context name if, and only if, it was specified in the request.
                Dim DisplayKey As String = If(args(0).ToLower = aKey.ToLower, GetContext(aKey) & "/", "") & "$k12" & GetKey(aKey)

                Say(Connection, Channel, String.Format("$k2[FAQ: {1}$k2]$o {2}{0}", DisplayLine, DisplayKey, If(args.Count = 2 AndAlso args(1).StartsWith(">"), "$b" & args(1).TrimEnd("@"c) & "$b: ", "")))
                Threading.Thread.Sleep(600)
            Next
        End If

        If Connection Is Nothing Then
            laKey = Channel.Split(">"c)(0) & ">" & Sender.Split("!"c)(0)
            If Not FAQ.LastAccessTimes.ContainsKey(laKey) Then _
                        FAQ.LastAccessTimes.Add(laKey, New List(Of Date))
            If FAQ.LastAccessTimes(laKey).Count = FAQ.RateLimitCount Then FAQ.LastAccessTimes(laKey).RemoveAt(0)
            FAQ.LastAccessTimes(laKey).Add(Now)
        Else
            For Each User In Connection.Channels(Channel).Users
                laKey = Connection.Address & "/" & User.Value.Nickname
                If Not FAQ.LastAccessTimes.ContainsKey(laKey) Then _
                            FAQ.LastAccessTimes.Add(laKey, New List(Of Date))
                If FAQ.LastAccessTimes(laKey).Count = FAQ.RateLimitCount Then FAQ.LastAccessTimes(laKey).RemoveAt(0)
                FAQ.LastAccessTimes(laKey).Add(Now)
            Next
        End If
    End Sub

    <Regex({"^\?:( (?<Context>.*))?"},
".list", 3, False)>
    Public Sub RegexFAQList(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As System.Text.RegularExpressions.Match)
        If Not ShortcutCheck(Connection, Channel) Then Return
        If Match.Groups("Context").Success Then
            CommandFAQList(Connection, Sender, Channel, {Match.Groups("Context").Value})
        Else
            CommandFAQList(Connection, Sender, Channel, {})
        End If
    End Sub
    <Command({"faqlist", "listfaq", "faqs"}, 0, 1,
  "faqlist [context]",
  "Displays a list of FAQ entries." & vbCrLf & "Use " & ChrW(3) & "11$cfaqlist " & ChrW(3) & "10<context>" & ChrW(3) & " to list the FAQ entries associated with a given context.",
  ".list")>
    Public Sub CommandFAQList(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        If args.Count = 0 Then
            Dim FoundContext = FindContext(Connection, Channel, Sender, "list")

            If FoundContext Is Nothing Then
                Reply(Connection, Channel, Sender.Split("!"c)(0), Choose(Choose("There is no ", "There's no ", "There isn't any ") & "FAQ context assigned to this channel."))
            ElseIf FoundContext.Count = 0 Then
                Reply(Connection, Channel, Sender.Split("!"c)(0), "You don't have permission to list this FAQ context.")
            Else
                For Each Context In FoundContext
                    CommandFAQList(Connection, Sender, Channel, {Context})
                Next
            End If
        Else
            If args(0) <> "*" And args(0) <> "" And Not Contexts.ContainsKey(args(0)) Then
                Reply(Connection, Channel, Sender, String.Format(VBot.Choose(VBot.Choose("There is no ", "There isn't any ") & "context set with the key {0}. ", "The context of {0} " & VBot.Choose("hasn't been ", "has not been ", "isn't ", "is not ") & "defined. "), IRCColours.Orange & args(0) & "$o"))
                Return
            End If

            If Not UserHasPermission(Connection, Channel, Sender, MyKey & ".list." & If(args(0) = "*" Or args(0) = "", "nocontext", args(0).Replace("/"c, "."c))) Then
                Reply(Connection, Channel, Sender, "You don't have permission to list the context of $k7" & args(1) & "$o.")
                Return
            End If

            Dim sFAQs As New List(Of String)
            For Each FAQ In FAQs
                If GetContext(FAQ.Key) = If(args(0) = "*", "", args(0)) Then
                    If Not FAQ.Value.Hidden Then
                        sFAQs.Add(IRCColours.Yellow & GetKey(FAQ.Key) & "$o")
                    ElseIf UserHasPermission(Connection, Channel, Sender, MyKey & ".listhidden." & args(0).Replace("/"c, "."c)) Then
                        sFAQs.Add(IRCColours.Gray & GetKey(FAQ.Key) & "$o")
                    End If
                End If
            Next

            If sFAQs.Count = 0 Then
                Reply(Connection, Channel, Sender, VBot.Choose("There are no FAQs " & VBot.Choose("defined ", "set "), "No FAQs have been " & VBot.Choose("defined ", "set ")) & "for " & VBot.Choose("$k07", "the topic of $k07", "the context of $k07") & If(args(0) = "", "*", args(0)) & "$o.")
            Else
                Reply(Connection, Channel, Sender, "The following " & VBot.Choose("FAQs ", "topics ") & "have been " & VBot.Choose("defined ", "set ") & "for " & VBot.Choose("$k07", "the topic of $k07", "the context of $k07") & If(args(0) = "", "*", args(0)) & "$o:")
                Reply(Connection, Channel, Sender, String.Join(", ", sFAQs))
            End If
        End If
    End Sub

    <Regex({"^\?\+ (?<Key>[^ ]+) (?<Text>.*)"},
  ".add", 3, False)>
    Public Sub RegexFAQAdd(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As System.Text.RegularExpressions.Match)
        If Not ShortcutCheck(Connection, Channel) Then Return
        CommandFAQAdd(Connection, Sender, Channel, {Match.Groups("Key").Value, Match.Groups("Text").Value})
    End Sub
    <Command("faqadd", 2, 2,
         "faqadd [<context>/]<key> <data>",
         "Adds a FAQ.",
          ".add")>
    Public Sub CommandFAQAdd(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim Context As String, Key As String, Data As String

        If args(0) = "." Then Reply(Connection, Channel, Sender.Split("!"c)(0), "The identifier $k4.$o is reserved and may not be used.")

        If args(0).Contains("/") And GetContext(args(0)) <> "" Then
            Context = GetContext(args(0))
            Key = GetKey(args(0))
        Else
            Dim FoundContext = FindContext(Connection, Channel, Sender, "add")
            If FoundContext Is Nothing Then
                Reply(Connection, Channel, Sender.Split("!"c)(0), Choose(Choose("There is no ", "There's no ", "There isn't any ") & "FAQ context assigned to this channel."))
                Return
            ElseIf FoundContext.Count = 0 Then
                Reply(Connection, Channel, Sender.Split("!"c)(0), "You don't have permission to add to this FAQ context.")
                Return
            ElseIf FoundContext.Count > 1 Then
                Reply(Connection, Channel, Sender.Split("!"c)(0), Choose(Choose("There is more than one ", "There are multiple ") & "FAQ contexts assigned to this channel."))
                Reply(Connection, Channel, Sender.Split("!"c)(0), "Please specify one of $k07" & String.Join("/$k08" & args(0) & "$o, $k07", FoundContext) & "/$k08" & args(0) & "$o.")
                Return
            Else
                Context = FoundContext(0)
                Key = args(0)
            End If
        End If

        Data = args(1).Replace("\r", vbCr).Replace("\n", vbLf)

        If Not UserHasPermission(Connection, Channel, Sender, MyKey & ".add." & Context) Then
            Reply(Connection, Channel, Sender.Split("!"c)(0), "You don't have permission to add a FAQ entry there.")
            Return
        End If

        If Context <> "*" And Not Contexts.ContainsKey(Context) Then _
            Say(Connection, Sender.Split("!"c)(0), String.Format(VBot.Choose(VBot.Choose("There is no ", "There isn't any ") & "context set with the key {0}. ", "The context of {0} " & VBot.Choose("hasn't been ", "has not been ", "isn't ", "is not ") & "defined. ") & "Use the command $k11$ccontextadd $k10<key> [channels]$o to define a FAQ context.", IRCColours.Orange & Context & "$o"))
        If FAQs.ContainsKey(If(Context = "*", Key, Context & "/" & Key)) Then
            Data = FAQs(If(Context = "*", Key, Context & "/" & Key)).Data & vbCrLf & Data
            FAQs(If(Context = "*", Key, Context & "/" & Key)).Data = Data
            Reply(Connection, Channel, Sender, "Appended text to the FAQ entry for " & If(Context <> "*", IRCColours.Orange & Context & "/", "") & IRCColours.Yellow & Key & "$o.")
            LastEntry = If(Context = "*", Key, Context & "/" & Key)
        Else
            FAQs.Add(If(Context = "*", Key, Context & "/" & Key), New FAQEntry With {.Data = Data})
            Reply(Connection, Channel, Sender, "Added a FAQ entry for " & If(Context <> "*", IRCColours.Orange & Context & "/", "") & IRCColours.Yellow & Key & "$o.")
            LastEntry = If(Context = "*", Key, Context & "/" & Key)
        End If
    End Sub

    <Regex({"^\?@\+? (?<Key>[^ ]+) (?<Target>[^ ]+)"},
".add", 3, False)>
    Public Sub RegexFAQAliasAdd(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As System.Text.RegularExpressions.Match)
        If Not ShortcutCheck(Connection, Channel) Then Return
        CommandFAQAliasAdd(Connection, Sender, Channel, {Match.Groups("Key").Value, Match.Groups("Target").Value})
    End Sub
    <Command({"faqaliasadd", "faqaddalias", "faqalias"}, 2, 2,
       "faqaliasadd [<context>/]<key> <target>",
       "Adds a FAQ alias. Where the alias is used in a command, it will be treated as the target.",
        ".add")>
    Public Sub CommandFAQAliasAdd(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim Context As String, Key As String, Target As String

        If args(0).Contains("/") Then
            Context = GetContext(args(0))
            Key = GetKey(args(0))
        Else
            Dim FoundContext = FindContext(Connection, Channel, Sender, "add")
            If FoundContext Is Nothing Then
                Reply(Connection, Channel, Sender.Split("!"c)(0), Choose(Choose("There is no ", "There's no ", "There isn't any ") & "FAQ context assigned to this channel."))
                Return
            ElseIf FoundContext.Count = 0 Then
                Reply(Connection, Channel, Sender.Split("!"c)(0), "You don't have permission to add to this FAQ context.")
                Return
            ElseIf FoundContext.Count > 1 Then
                Reply(Connection, Channel, Sender.Split("!"c)(0), Choose(Choose("There is more than one ", "There are multiple ") & "FAQ contexts assigned to this channel."))
                Reply(Connection, Channel, Sender.Split("!"c)(0), "Please specify one of $k07" & String.Join("/$k08" & args(0) & "$o, $k07", FoundContext) & "/$k08" & args(0) & "$o.")
                Return
            Else
                Context = FoundContext(0)
                Key = args(0)
            End If
        End If

        ' Resolve the target FAQ entry.
        Try
            Target = FindFAQ(Connection, Channel, Sender, args(1))
            If Target.Contains(" ") Then
                Reply(Connection, Channel, Sender.Split("!"c)(0), "That specification matches " & Choose(Choose("more than one ", "multiple ") & "FAQ entries."))
                Reply(Connection, Channel, Sender.Split("!"c)(0), "Please specify one of $k07" & String.Join("$o, $k07", Target.Split(" "c)) & "$o.")
                Return
            End If
        Catch ex As KeyNotFoundException
            Reply(Connection, Channel, Sender, "That FAQ entry " & VBot.Choose("doesn't exist.", "hasn't been entered.", "isn't defined."))
            Return
        End Try

        If Not UserHasPermission(Connection, Channel, Sender, MyKey & ".add." & Context) Then
            Reply(Connection, Channel, Sender.Split("!"c)(0), "You don't have permission to add a FAQ entry there.")
            Return
        End If

        If Context <> "*" And Not Contexts.ContainsKey(Context) Then _
            Say(Connection, Sender.Split("!"c)(0), String.Format(VBot.Choose(VBot.Choose("There is no ", "There isn't any ") & "context set with the key {0}. ", "The context of {0} " & VBot.Choose("hasn't been ", "has not been ", "isn't ", "is not ") & "defined. ") & "Use the command $k11$ccontextadd $k10<key> [channels]$o to define a FAQ context.", IRCColours.Orange & Context & "$o"))
        If FAQs.ContainsKey(If(Context = "*", Key, Context & "/" & Key)) Then
            Reply(Connection, Channel, Sender, "$k4A FAQ entry " & Choose("already exists ", "has already been entered ") & "with that key.")
        ElseIf Aliases.ContainsKey(If(Context = "*", Key, Context & "/" & Key)) Then
            Reply(Connection, Channel, Sender, "$k4An alias " & Choose("already exists ", "has already been entered ") & "with that key.")
        Else
            Aliases.Add(If(Context = "*", Key, Context & "/" & Key), Target)
            Reply(Connection, Channel, Sender, "Added an alias " & If(Context <> "*", IRCColours.Orange & Context & "/", "") & IRCColours.Yellow & Key & "$o.")
        End If
    End Sub

    <Regex({"^\?@: (?<Key>[^ ]*)"},
".list", 3, False)>
    Public Sub RegexFAQAliasList(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As System.Text.RegularExpressions.Match)
        If Not ShortcutCheck(Connection, Channel) Then Return
        CommandFAQAliasList(Connection, Sender, Channel, {Match.Groups("Key").Value})
    End Sub
    <Command({"faqaliaslist", "faqlistalias", "faqaliaseslist", "faqlistaliases"}, 2, 2,
         "faqaliaslist <key>  or  faqaliaslist <context>",
         "Lists all aliases of the specified entry or in the specified context.",
          ".list")>
    Public Sub CommandFAQAliasList(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        If Contexts.ContainsKey(args(0)) Then

            If Not UserHasPermission(Connection, Channel, Sender, MyKey & ".list." & args(0)) Then
                Reply(Connection, Channel, Sender.Split("!"c)(0), "You don't have permission to list this FAQ context.")
                Return
            End If

            Dim rAliases As New List(Of String)
            For Each lAlias In Aliases
                If GetContext(lAlias.Key) = args(0) Then
                    rAliases.Add(GetKey(lAlias.Key))
                    Reply(Connection, Channel, Sender, "The context of $k07 " & args(0) & "$o contains the following aliases:")
                    Reply(Connection, Channel, Sender, "$k08" & String.Join("$o, $k08", rAliases) & "$o.")
                End If
            Next
        Else
            Dim Target As String
            ' Resolve the target FAQ entry.
            Try
                Target = FindFAQ(Connection, Channel, Sender, args(1))
                If Target.Contains(" ") Then
                    Reply(Connection, Channel, Sender.Split("!"c)(0), "That specification matches " & Choose(Choose("more than one ", "multiple ") & "FAQ entries."))
                    Reply(Connection, Channel, Sender.Split("!"c)(0), "Please specify one of $k07" & String.Join("$o, $k07", Target.Split(" "c)) & "$o.")
                    Return
                End If
            Catch ex As KeyNotFoundException
                Reply(Connection, Channel, Sender, "That FAQ entry " & VBot.Choose("doesn't exist.", "hasn't been entered.", "isn't defined."))
                Return
            End Try

            If Not UserHasPermission(Connection, Channel, Sender, MyKey & ".list." & GetContext(Target)) Then
                Reply(Connection, Channel, Sender.Split("!"c)(0), "You don't have permission to list this FAQ context.")
                Return
            End If

            Dim rAliases As New List(Of String)
            For Each lAlias In Aliases
                If lAlias.Value = Target Then
                    rAliases.Add("$k7" & If(args(0).ToLower = Target.ToLower, GetContext(Target) & "/", "") & "$k8" & GetKey(Target) & "$o")
                End If
            Next
            Dim DisplayKey As String = "$k7" & If(args(0).ToLower = Target.ToLower, GetContext(Target) & "/", "") & "$k8" & GetKey(Target) & "$o"
            Reply(Connection, Channel, Sender, DisplayKey & "$o has the following aliases:")
            Reply(Connection, Channel, Sender, String.Join(", ", rAliases) & ".")
        End If
    End Sub

    <Regex({"^\?- (?<Key>[^ ]+)"},
    ".delete", 3, False)>
    Public Sub RegexFAQDelete(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As System.Text.RegularExpressions.Match)
        If Not ShortcutCheck(Connection, Channel) Then Return
        CommandFAQDelete(Connection, Sender, Channel, {Match.Groups("Key").Value})
    End Sub
    <Command({"faqdelete", "faqdel"}, 1, 1,
      "faqdelete [<context>/]<key>",
      "Deletes a FAQ entry.",
  ".delete")>
    Public Sub CommandFAQDelete(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        ' Resolve the target FAQ entry.
        Dim Target As String
        Try
            Target = FindFAQ(Connection, Channel, Sender, args(0))
            If Target.Contains(" ") Then
                Reply(Connection, Channel, Sender.Split("!"c)(0), "That specification matches " & Choose(Choose("more than one ", "multiple ") & "FAQ entries."))
                Reply(Connection, Channel, Sender.Split("!"c)(0), "Please specify one of $k07" & String.Join("$o, $k07", Target.Split(" "c)) & "$o.")
                Return
            End If
        Catch ex As KeyNotFoundException
            Reply(Connection, Channel, Sender, "That FAQ entry " & VBot.Choose("doesn't exist.", "hasn't been entered.", "isn't defined."))
            Return
        End Try

        If Not UserHasPermission(Connection, Channel, Sender, MyKey & ".delete." & GetContext(Target)) Then
            Reply(Connection, Channel, Sender.Split("!"c)(0), "You don't have permission to delete a FAQ entry there.")
            Return
        End If

        FAQs.Remove(Target)

        Dim DisplayKey As String = "$k7" & If(args(0).ToLower = Target.ToLower, GetContext(Target) & "/", "") & "$k8" & GetKey(Target) & "$o"
        Reply(Connection, Channel, Sender, "Deleted the FAQ entry for " & DisplayKey & ".")
    End Sub

    <Regex({"^\?@- (?<Key>[^ ]+)"},
  ".delete", 3, False)>
    Public Sub RegexFAQAliasDelete(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As System.Text.RegularExpressions.Match)
        If Not ShortcutCheck(Connection, Channel) Then Return
        CommandFAQAliasDelete(Connection, Sender, Channel, {Match.Groups("Key").Value})
    End Sub
    <Command({"faqaliasdelete", "faqdeletealias", "faqaliasdel", "faqdelalias"}, 1, 1,
    "faqaliasdelete [<context>/]<key>",
    "Deletes a FAQ alias.",
".delete")>
    Public Sub CommandFAQAliasDelete(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim Context As String, Key As String

        If args(0).Contains("/") Then
            Context = GetContext(args(0))
            Key = GetKey(args(0))
        Else
            Dim FoundContext = FindContext(Connection, Channel, Sender, "delete")
            If FoundContext Is Nothing Then
                Reply(Connection, Channel, Sender.Split("!"c)(0), Choose(Choose("There is no ", "There's no ", "There isn't any ") & "FAQ context assigned to this channel."))
                Return
            ElseIf FoundContext.Count = 0 Then
                Reply(Connection, Channel, Sender.Split("!"c)(0), "You don't have permission to add to this FAQ context.")
                Return
            ElseIf FoundContext.Count > 1 Then
                Reply(Connection, Channel, Sender.Split("!"c)(0), Choose(Choose("There is more than one ", "There are multiple ") & "FAQ contexts assigned to this channel."))
                Reply(Connection, Channel, Sender.Split("!"c)(0), "Please specify one of $k07" & String.Join("/$k08" & args(0) & "$o, $k07", FoundContext) & "/$k08" & args(0) & "$o.")
                Return
            Else
                Context = FoundContext(0)
                Key = args(0)
            End If
        End If

        If Not UserHasPermission(Connection, Channel, Sender, MyKey & ".delete." & Context) Then
            Reply(Connection, Channel, Sender.Split("!"c)(0), "You don't have permission to delete a FAQ entry there.")
            Return
        End If

        If Not Aliases.ContainsKey(If(Context = "*", Key, Context & "/" & Key)) Then
            If Not FAQs.ContainsKey(If(Context = "*", Key, Context & "/" & Key)) Then
                Say(Connection, Channel, "$k4That is a FAQ entry, not an alias.$o")
            Else
                Say(Connection, Channel, "That isn't an alias.")
            End If
        Else
            Aliases.Remove(If(Context = "*", Key, Context & "/" & Key))
            Reply(Connection, Channel, Sender, "Deleted the alias " & If(Context <> "*", IRCColours.Orange & Context & "/", "") & IRCColours.Yellow & Key & "$o.")
        End If
    End Sub

    <Command({"faqset", "faqsetting"}, 0, 3,
  "faqset [[<context>/]<key>] [setting] [value]",
  "Changes settings for a FAQ entry.",
   ".set")>
    Public Sub CommandFAQSet(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        ' Resolve the target FAQ entry.
        Dim Target As String
        Try
            Target = FindFAQ(Connection, Channel, Sender, args(0))
            If Target.Contains(" ") Then
                Reply(Connection, Channel, Sender.Split("!"c)(0), "That specification matches " & Choose(Choose("more than one ", "multiple ") & "FAQ entries."))
                Reply(Connection, Channel, Sender.Split("!"c)(0), "Please specify one of $k07" & String.Join("$o, $k07", Target.Split(" "c)) & "$o.")
                Return
            End If
        Catch ex As KeyNotFoundException
            Reply(Connection, Channel, Sender, "That FAQ entry " & VBot.Choose("doesn't exist.", "hasn't been entered.", "isn't defined."))
            Return
        End Try

        If Not UserHasPermission(Connection, Channel, Sender, MyKey & ".set." & GetContext(Target)) Then
            Reply(Connection, Channel, Sender.Split("!"c)(0), "You don't have permission to modify a FAQ entry here.")
            Return
        End If

        Dim FAQ As FAQEntry

        If args.Count > 0 Then
            FAQ = FAQs(Target)
        Else
            Reply(Connection, Channel, Sender, "You can change the following settings for a FAQ entry:")
            Reply(Connection, Channel, Sender, "$k12RateLimitCount$o - the maximum number of times regexes will trigger.")
            Reply(Connection, Channel, Sender, "$k12RateLimitInterval$o - the amount of time required after RateLimitCount triggers before it will trigger again.")
            Reply(Connection, Channel, Sender, "$k12HideLabel$o - whether the FAQ label will be displayed when its regular expression is triggered.")
            Reply(Connection, Channel, Sender, "$k12NoticeOnJoin$o - whether to NOTICE the user when a FAQ is triggered by a JOIN instead of speaking to the channel.")
            Reply(Connection, Channel, Sender, "$k12Hidden$o - whether the FAQ will be listed. Unlisted (hidden) FAQs can still be triggered, and will still be recorded in the configuration files.")
            Return
        End If

        Dim DisplayKey As String = "$k7" & If(args(0).ToLower = Target.ToLower, GetContext(Target) & "/", "") & "$k8" & GetKey(Target) & "$o"
        Select Case args.Count
            Case 1
                Reply(Connection, Channel, Sender, VBot.Choose("The current settings ", "Settings ", "Current settings ") & "for " & DisplayKey & VBot.Choose(" are:", " are as follows:", ":"))
                Reply(Connection, Channel, Sender, "RateLimitCount: $k12" & FAQ.RateLimitCount & "$o trigger" & If(FAQ.RateLimitCount = 1, "", "s"))
                Reply(Connection, Channel, Sender, "RateLimitInterval: $k12" & FAQ.RateLimitInterval & "$o second" & If(FAQ.RateLimitInterval = 1, "", "s"))
                Reply(Connection, Channel, Sender, "HideLabel: " & If(FAQ.HideLabel, "$k11on", "$k10off"))
                Reply(Connection, Channel, Sender, "NoticeOnJoin: " & If(FAQ.HideLabel, "$k9on", "$k4off"))
                Reply(Connection, Channel, Sender, "Hidden: " & If(FAQ.HideLabel, "$k11yes", "$k10no"))
            Case 2
                Select Case args(1).ToLower
                    Case "ratelimitcount"
                        Reply(Connection, Channel, Sender, String.Format(VBot.Choose("The {1} for {0} is " & VBot.Choose("currently set to ", "currently at ", "set to ", "") & "{2} trigger{3}.", "{0}'s {1} is " & VBot.Choose("currently set to ", "currently at ", "set to ", "") & "{2} trigger{3}.", "{0} will trigger " & VBot.Choose("up to ", "a maximum of ") & "{2} time{3} within the set interval."), DisplayKey, "$k12rate limit count$o", IRCColours.Blue & FAQ.RateLimitCount & "$o", If(FAQ.RateLimitCount = 1, "", "s")))
                    Case "ratelimitinterval"
                        Reply(Connection, Channel, Sender, String.Format(VBot.Choose("The {1} for {0} is " & VBot.Choose("currently set to ", "currently at ", "set to ", "") & "{2} second{3}.", "{0}'s {1} is " & VBot.Choose("currently set to ", "currently at ", "set to ", "") & "{2} second{3}.", "{0}'s trigger limit lasts " & VBot.Choose("for ", "") & "{2} second{3}."), DisplayKey, "$k12rate limit count$o", IRCColours.Blue & FAQ.RateLimitInterval & "$o", If(FAQ.RateLimitInterval = 1, "", "s")))
                    Case "hidelabel"
                        If FAQ.HideLabel Then
                            Reply(Connection, Channel, Sender, String.Format("{0} " & VBot.Choose("is currently set to ", "is set to ", "will ") & "$k9hide$o the label" & VBot.Choose(" when it is triggered.", "."), DisplayKey))
                        Else
                            Reply(Connection, Channel, Sender, String.Format("{0} " & VBot.Choose("is currently set to ", "is set to ", "will ") & "$k4show$o the label" & VBot.Choose(" when it is triggered.", "."), DisplayKey))
                        End If
                    Case "noticeonjoin"
                        If FAQ.NoticeOnJoin Then
                            Reply(Connection, Channel, Sender, String.Format("{0} " & VBot.Choose("is currently set to ", "is set to ", "will ") & "$k9hide$o the label" & VBot.Choose(" when it is triggered.", "."), DisplayKey))
                        Else
                            Reply(Connection, Channel, Sender, String.Format("{0} " & VBot.Choose("is currently set to ", "is set to ", "will ") & "$k4show$o the label" & VBot.Choose(" when it is triggered.", "."), DisplayKey))
                        End If
                    Case "hidden"
                        If FAQ.Hidden Then
                            Reply(Connection, Channel, Sender, String.Format("{0} is currently $k9hidden$o.", DisplayKey))
                        Else
                            Reply(Connection, Channel, Sender, String.Format("{0} is currently $k4listed$o.", DisplayKey))
                        End If
                    Case Else
                        Reply(Connection, Channel, Sender, VBot.Choose("", "Sorry " & Sender.Split("!"c)(0) & ", ") & "I don't recognise " & VBot.Choose(IRCColours.Red & args(2) & "$o", "that setting") & ".")
                End Select
            Case 3
                Select Case args(1).ToLower
                    Case "ratelimitcount"
                        Dim Value As Integer
                        If Integer.TryParse(args(2), Value) Then
                            If Value < 0 Then
                                Reply(Connection, Channel, Sender, "That isn't a valid setting. Use a non-negative integer.")
                            Else
                                FAQ.RateLimitCount = Value
                                If FAQ.RateLimitCount = 0 Then
                                    Reply(Connection, Channel, Sender, String.Format(VBot.Choose("The trigger limit for {0} has been removed.", "{0} is no longer limited.", "{0} will now trigger unlimited times."), DisplayKey, "$k12rate limit count$o", IRCColours.Blue & FAQ.RateLimitCount & "$o"))
                                Else
                                    Reply(Connection, Channel, Sender, String.Format(VBot.Choose("The {1} for {0} " & VBot.Choose("is now set to ", "is now at ", "is now ", "was changed to ", "has been changed to ") & "{2} trigger{3}.", "{0}'s {1} " & VBot.Choose("is now set to ", "is now at ", "is now ", "was changed to ", "has been changed to ") & "{2} trigger{3}.", "{0} will now trigger " & VBot.Choose("up to ", "a maximum of ") & "{2} time{3} within the set interval."), DisplayKey, "$k12rate limit count$o", IRCColours.Blue & FAQ.RateLimitCount & "$o", If(FAQ.RateLimitCount = 1, "", "s")))
                                    If FAQ.RateLimitInterval = 0 Then _
                                        Reply(Connection, Channel, Sender, String.Format(VBot.Choose("The trigger limit interval for {0} is still set to zero though, so {0} is still unlimited.", "However, {0} won't be limited until the rate limit interval is set above zero."), DisplayKey, "$k12rate limit count$o", IRCColours.Blue & FAQ.RateLimitCount & "$o"))
                                End If
                            End If
                        Else
                        End If
                    Case "ratelimitinterval"
                        Dim Value As Integer
                        If Integer.TryParse(args(2), Value) Then
                            If Value < 0 Then
                                Reply(Connection, Channel, Sender, "That isn't a valid setting. Use a non-negative integer.")
                            Else
                                FAQ.RateLimitInterval = Value
                                If FAQ.RateLimitInterval = 0 Then
                                    Reply(Connection, Channel, Sender, String.Format(VBot.Choose("The trigger limit for {0} has been removed.", "{0} is no longer limited.", "{0} will now trigger unlimited times."), DisplayKey, "$k12rate limit count$o", IRCColours.Blue & FAQ.RateLimitCount & "$o"))
                                Else
                                    Reply(Connection, Channel, Sender, String.Format(VBot.Choose("The {1} for {0} " & VBot.Choose("is now set to ", "is now at ", "is now ", "was changed to ", "has been changed to ") & "{2} second{3}.", "{0}'s {1} " & VBot.Choose("is now set to ", "is now at ", "is now ", "was changed to ", "has been changed to ") & "{2} second{3}.", "{0}'s trigger limit will now last " & VBot.Choose("for ", "") & "{2} second{3}."), DisplayKey, "$k12rate limit interval$o", IRCColours.Blue & FAQ.RateLimitInterval & "$o", If(FAQ.RateLimitInterval = 1, "", "s")))
                                    If FAQ.RateLimitCount = 0 Then _
                                        Reply(Connection, Channel, Sender, String.Format(VBot.Choose("The trigger limit count for {0} is still set to zero though, so {0} is still unlimited.", "However, {0} won't be limited until the rate limit count is set above zero."), DisplayKey, "$k12rate limit count$o", IRCColours.Blue & FAQ.RateLimitCount & "$o"))
                                End If
                            End If
                        Else
                        End If
                    Case "hidelabel"
                        If {"hide", "1", "-1", "on", "yes", "true", "+", "enable", "enabled", "activate", "activated", "active"}.Contains(args(2)) Then
                            FAQ.HideLabel = True
                            Reply(Connection, Channel, Sender, String.Format(VBot.Choose("{0} " & VBot.Choose("is now set to ", "will now ") & "$9hide$o the label", "A label will no longer be shown for {0}") & VBot.Choose(" when it is triggered.", "."), DisplayKey))
                        ElseIf {"show", "0", "off", "no", "false", "-", "disable", "disabled", "deactivate", "deactivated", "inactive"}.Contains(args(2)) Then
                            FAQ.HideLabel = False
                            Reply(Connection, Channel, Sender, String.Format(VBot.Choose("{0} " & VBot.Choose("is now set to ", "will now ") & "$4show$o the label", "A label will now be shown for {0}") & VBot.Choose(" when it is triggered.", "."), DisplayKey))
                        Else
                            Reply(Connection, Channel, Sender, VBot.Choose("", "Sorry " & Sender.Split("!"c)(0) & ", ") & "I don't recognise " & VBot.Choose(IRCColours.Red & args(2) & "$o", "that") & " as a Boolean value. Please enter $k11yes$o or $k11no$o.")
                        End If
                    Case "noticeonjoin"
                        If {"hide", "1", "-1", "on", "yes", "true", "+", "enable", "enabled", "activate", "activated", "active", "notice", "pm", "pmsg", "pnotice"}.Contains(args(2)) Then
                            FAQ.NoticeOnJoin = True
                            Reply(Connection, Channel, Sender, String.Format(VBot.Choose("{0} " & VBot.Choose("is now set to ", "will now ") & "$9message the channel$o when triggered by someone joining."), DisplayKey))
                        ElseIf {"show", "0", "off", "no", "false", "-", "disable", "disabled", "deactivate", "deactivated", "inactive", "channel", "channelmessage"}.Contains(args(2)) Then
                            FAQ.NoticeOnJoin = False
                            Reply(Connection, Channel, Sender, String.Format(VBot.Choose("{0} " & VBot.Choose("is now set to ", "will now ") & "$4noticea user$o who joins the channel."), DisplayKey))
                        Else
                            Reply(Connection, Channel, Sender, VBot.Choose("", "Sorry " & Sender.Split("!"c)(0) & ", ") & "I don't recognise " & VBot.Choose(IRCColours.Red & args(2) & "$o", "that") & " as a Boolean value. Please enter $k11yes$o or $k11no$o.")
                        End If
                    Case "hidden"
                        If {"hide", "1", "-1", "on", "yes", "true", "+", "enable", "enabled", "activate", "activated", "active", "hidden", "hide"}.Contains(args(2)) Then
                            FAQ.Hidden = True
                            Reply(Connection, Channel, Sender, String.Format("{0} is now $k9hidden$o.", DisplayKey))
                        ElseIf {"show", "0", "off", "no", "false", "-", "disable", "disabled", "deactivate", "deactivated", "inactive", "shown", "show"}.Contains(args(2)) Then
                            FAQ.Hidden = False
                            Reply(Connection, Channel, Sender, String.Format("{0} is now $k4listed$o.", DisplayKey))
                        Else
                            Reply(Connection, Channel, Sender, VBot.Choose("", "Sorry " & Sender.Split("!"c)(0) & ", ") & "I don't recognise " & VBot.Choose(IRCColours.Red & args(2) & "$o", "that") & " as a Boolean value. Please enter $k11yes$o or $k11no$o.")
                        End If
                    Case Else
                        Reply(Connection, Channel, Sender, VBot.Choose("", "Sorry " & Sender.Split("!"c)(0) & ", ") & "I don't recognise " & VBot.Choose(IRCColours.Red & args(2) & "$o", "that setting") & ".")
                End Select
        End Select
    End Sub

    <Command({"contextadd", "contadd", "conadd", "faqcadd", "faqcontadd", "faqconadd", "faqcontextadd"}, 1, 2,
"contextadd <key> [channels]",
"Defines a FAQ context." & vbCrLf & "A FAQ context lets you organise the FAQ data better, and also lets you set which channels I should listen in for regular expressions.",
 ".contextadd")>
    Public Sub CommandContextAdd(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim Channels As New List(Of String)

        If args(0) = "*" Then
            Reply(Connection, Channel, Sender, "The context identifier * is reserved and may not be used.")
            Return
        ElseIf Contexts.ContainsKey(args(0)) Then
            Reply(Connection, Channel, Sender, "The context $k07" & args(0) & "$o has already been set. Changing its associated channels")
            Contexts.Remove(args(0))
        End If

        If args.Count = 1 Then
            Channels.Add("*")
        Else
            For Each arg In args(1).Split({","c, " "c})
                Channels.Add(arg)
            Next
        End If

        Contexts.Add(args(0), Channels.ToArray)
        Reply(Connection, Channel, Sender, "Added a context entry for $k07" & args(0) & "$o.")
    End Sub

    <Command({"contextlist", "contlist", "conlist", "faqclist", "faqcontlist", "faqconlist", "faqcontextlist"}, 0, 0,
"contextlist",
"Lists all defined contexts.",
".contextlist")>
    Public Sub CommandContextList(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Reply(Connection, Channel, Sender, "The following contexts have been " & VBot.Choose("defined ", "set ") & ":")
        For Each c In Contexts
            Reply(Connection, Channel, Sender, IRCColours.Orange & c.Key & "$k5 - " & String.Join(", ", c.Value))
        Next
    End Sub

    <Command({"contextdelete", "contdelete", "condelete", "faqcdelete", "faqcontdelete", "faqcondelete", "faqcontextdelete"}, 1, 2,
"contextdelete <key>",
"Deletes a FAQ context.",
".contextdelete")>
    Public Sub CommandContextDelete(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        If args(0) = "*" Then
            Reply(Connection, Channel, Sender, "The context identifier * is reserved and may not be used.")
            Return
        ElseIf Contexts.ContainsKey(args(0)) Then
            Contexts.Remove(args(0))
            Reply(Connection, Channel, Sender, VBot.Choose("Deleted ", "Removed ", "Erased ") & "the context %c07" & args(0) & "$o.")
        Else
            Say(Connection, Sender.Split("!"c)(0), String.Format(VBot.Choose(VBot.Choose("There is no ", "There isn't any ") & "context set with the key {0}. ", "The context of {0} " & VBot.Choose("hasn't been ", "has not been ", "isn't ", "is not ") & "defined. ") & "Use the command " & IRCColours.Blue & "$ccontextadd <key> [channels]$o to define a FAQ context.", IRCColours.Orange & args(0) & "$o"))
        End If
    End Sub

    <Regex({"^\?= (?<Key>[^ ]+)( (?<Line>(\+?|\>?)\d+)( (?<Text>.*))?)?"},
".edit", 3, False)>
    Public Sub RegexFAQEdit(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As System.Text.RegularExpressions.Match)
        If Not ShortcutCheck(Connection, Channel) Then Return
        If Match.Groups("Line").Success Then
            If Match.Groups("Text").Success Then
                CommandFAQEdit(Connection, Sender, Channel, {Match.Groups("Key").Value, Match.Groups("Line").Value, Match.Groups("Text").Value})
            Else
                CommandFAQEdit(Connection, Sender, Channel, {Match.Groups("Key").Value, Match.Groups("Line").Value})
            End If
        Else
            CommandFAQEdit(Connection, Sender, Channel, {Match.Groups("Key").Value})
        End If
    End Sub
    <Command("faqedit", 1, 3,
   "faqedit <context>/<key> [[+]<line #>] [replacement line]",
   "Allows you to edit a FAQ entry. Use the command with only a FAQ entry key to see your options." & vbCrLf &
   "Alternatively, you can omit the replacement line to delete a line, or prefix it with + to insert a line.",
   ".edit")>
    Public Sub CommandFAQEdit(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        ' Resolve the target FAQ entry.
        Dim Target As String
        Try
            Target = FindFAQ(Connection, Channel, Sender, args(0))
            If Target.Contains(" ") Then
                Reply(Connection, Channel, Sender.Split("!"c)(0), "That specification matches " & Choose(Choose("more than one ", "multiple ") & "FAQ entries."))
                Reply(Connection, Channel, Sender.Split("!"c)(0), "Please specify one of $k07" & String.Join("$o, $k07", Target.Split(" "c)) & "$o.")
                Return
            End If
        Catch ex As KeyNotFoundException
            Reply(Connection, Channel, Sender, "That FAQ entry " & VBot.Choose("doesn't exist.", "hasn't been entered.", "isn't defined."))
            Return
        End Try

        Dim FAQ = FAQs(Target)

        If Not UserHasPermission(Connection, Channel, Sender, MyKey & ".edit." & GetContext(Target)) Then
            Reply(Connection, Channel, Sender.Split("!"c)(0), "You don't have permission to edit a FAQ entry there.")
            Return
        End If

        Dim Lines = FAQ.Data.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.RemoveEmptyEntries), Replacement As String

        Dim DisplayKey As String = "$k7" & If(args(0).ToLower = Target.ToLower, GetContext(Target) & "/", "") & "$k8" & GetKey(Target) & "$o"
        Select Case args.Count
            Case 1
                Say(Connection, Channel, DisplayKey & ":")
                For i = 0 To UBound(Lines)
                    Reply(Connection, Channel, Sender, IRCColours.Yellow & i & "$k7: $k12" & Lines(i))
                    Threading.Thread.Sleep(600)
                Next
                Reply(Connection, Channel, Sender, "To replace the entire entry, use $k11$cfaqedit " & IRCConnection.RemoveCodes(DisplayKey) & " " & "$k10<replacement text>$o.")
                Reply(Connection, Channel, Sender, "To change a line, use $k11$cfaqedit " & IRCConnection.RemoveCodes(DisplayKey) & " " & "$k10<line number> <replacement text>$o.")
                Reply(Connection, Channel, Sender, "To insert a line, use $k11$cfaqedit " & IRCConnection.RemoveCodes(DisplayKey) & " " & "+$k10<line number>$o.")
                Reply(Connection, Channel, Sender, "To remove a line, use $k11$cfaqedit " & IRCConnection.RemoveCodes(DisplayKey) & " " & "$k10<line number>$o without replacement text.")
            Case 2
                Dim LineNumber As UShort
                If Not UShort.TryParse(args(1), LineNumber) Then
                    Replacement = args(1)
                    GoTo NoLineNumber
                ElseIf LineNumber > UBound(Lines) Then
                    Reply(Connection, Channel, Sender, DisplayKey & " doesn't have a line number $k04" & LineNumber & "$o.")
                Else
                    FAQ.Data = ""
                    For i = 0 To UBound(Lines)
                        If i = LineNumber Then Continue For
                        If FAQ.Data <> "" Then FAQ.Data &= vbCrLf
                        FAQ.Data &= Lines(i)
                    Next
                    Reply(Connection, Channel, Sender, String.Format(VBot.Choose(VBot.Choose("Removed ", "Deleted ") & "line " & VBot.Choose("number ", "") & "{0} ", "Line " & VBot.Choose("number ", "") & "{0} has been " & VBot.Choose("removed ", "deleted ")) & "from " & VBot.Choose("", "FAQ ", "FAQ entry ", "the entry for ") & "{1}.", IRCColours.Blue & LineNumber & "$o", DisplayKey))
                    Lines = FAQ.Data.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.RemoveEmptyEntries)
                End If
                For i = 0 To UBound(Lines)
                    Reply(Connection, Channel, Sender, IRCColours.Yellow & i & "$k7: $o" & Lines(i))
                    Threading.Thread.Sleep(600)
                Next
            Case 3
                Dim LineNumber As UShort, Insert As Boolean
                If args(1).StartsWith("+") Or args(1).StartsWith(">") Then Insert = True

                If Not UShort.TryParse(If(Insert, args(1).Substring(1), args(1)), LineNumber) Then
                    Replacement = args(1) & " " & args(2)
                    GoTo NoLineNumber
                ElseIf LineNumber > UBound(Lines) + 1 Then
                    Reply(Connection, Channel, Sender, DisplayKey & " doesn't have a line number $k04" & LineNumber & "$o.")
                ElseIf LineNumber = UBound(Lines) + 1 Then
                    FAQ.Data &= vbCrLf & args(2)
                    Reply(Connection, Channel, Sender, "Appended text to the FAQ entry for " & DisplayKey & ".")
                    Lines = FAQ.Data.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.RemoveEmptyEntries)
                Else
                    FAQ.Data = ""
                    For i = 0 To UBound(Lines)
                        If i = LineNumber Then
                            If args(2) = "" Then Continue For
                            If FAQ.Data <> "" Then FAQ.Data &= vbCrLf
                            FAQ.Data &= args(2)
                            If Not Insert Then Continue For
                        End If
                        If FAQ.Data <> "" Then FAQ.Data &= vbCrLf
                        FAQ.Data &= Lines(i)
                    Next

                    If Insert Then
                        Reply(Connection, Channel, Sender, String.Format("Inserted text into the FAQ entry for {0}.", DisplayKey))
                    Else
                        Reply(Connection, Channel, Sender, String.Format(VBot.Choose(VBot.Choose("Changed ", "Modified ", "Replaced ", "Rewrote ", "Edited ") & "line " & VBot.Choose("number ", "") & "{0} ", "Line " & VBot.Choose("number ", "") & "{0} has been " & VBot.Choose("changed ", "modified ", "replaced ", "rewritten ", "edited ")) & "in " & VBot.Choose("", "FAQ ", "FAQ entry ", "the entry for ") & "{1}.", IRCColours.Blue & LineNumber & "$o", DisplayKey))
                    End If
                    Lines = FAQ.Data.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.RemoveEmptyEntries)
                End If
                For i = 0 To UBound(Lines)
                    Reply(Connection, Channel, Sender, IRCColours.Yellow & i & "$k7: $o" & Lines(i))
                    Threading.Thread.Sleep(600)
                Next
        End Select
        Return
NoLineNumber:
        FAQ.Data = Replacement
        Reply(Connection, Channel, Sender, String.Format(VBot.Choose("Changed ", "Modified ", "Replaced ", "Rewrote ", "Edited ") & VBot.Choose("", "FAQ ", "FAQ entry ", "the entry for ") & "{0}.", DisplayKey))
        Lines = FAQ.Data.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.RemoveEmptyEntries)
        For i = 0 To UBound(Lines)
            Reply(Connection, Channel, Sender, IRCColours.Yellow & i & "$k7: $o" & Lines(i))
            Threading.Thread.Sleep(600)
        Next
    End Sub

    <Regex({"^\?\*\+ (?<Key>[^ ]+) (?<Text>.*)"},
".regex", 3, False)>
    Public Sub RegexFAQRegexAdd(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As System.Text.RegularExpressions.Match)
        If Not ShortcutCheck(Connection, Channel) Then Return
        CommandFAQRegexAdd(Connection, Sender, Channel, {Match.Groups("Key").Value, Match.Groups("Text").Value})
    End Sub
    <Command("faqregexadd", 2, 2,
 "faqregexadd [<context>/]<key> [MSG:|ACTION:|JOIN:|PART:|QUIT:|KICK:|EXIT:|NICK:]<regex>",
 "Assigns a regular expression to a FAQ entry. When someone sends a message to a channel within the FAQ's context that matches the regex, the FAQ data will be displayed in the channel.",
 ".regex")>
    Public Sub CommandFAQRegexAdd(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        ' Resolve the target FAQ entry.
        Dim Target As String
        Try
            Target = FindFAQ(Connection, Channel, Sender, args(0))
            If Target.Contains(" ") Then
                Reply(Connection, Channel, Sender.Split("!"c)(0), "That specification matches " & Choose(Choose("more than one ", "multiple ") & "FAQ entries."))
                Reply(Connection, Channel, Sender.Split("!"c)(0), "Please specify one of $k07" & String.Join("$o, $k07", Target.Split(" "c)) & "$o.")
                Return
            End If
        Catch ex As KeyNotFoundException
            Reply(Connection, Channel, Sender, "That FAQ entry " & VBot.Choose("doesn't exist.", "hasn't been entered.", "isn't defined."))
            Return
        End Try

        Dim FAQ = FAQs(Target)

        If Not UserHasPermission(Connection, Channel, Sender, MyKey & ".regex." & GetContext(Target)) Then
            Reply(Connection, Channel, Sender, "You don't have permission to modify a FAQ entry there.")
            Return
        End If

        If FAQ.Regexes Is Nothing Then
            FAQ.Regexes = {args(1)}
        Else
            ReDim Preserve FAQ.Regexes(UBound(FAQ.Regexes) + 1)
            FAQ.Regexes(UBound(FAQ.Regexes)) = args(1)
        End If

        Dim DisplayKey As String = "$k7" & If(args(0).ToLower = Target.ToLower, GetContext(Target) & "/", "") & "$k8" & GetKey(Target) & "$o"
        Reply(Connection, Channel, Sender, "Associated a regular expression with " & DisplayKey & ".")
    End Sub

    <Regex({"^\?\*:? (?<Key>[^ ]+)"},
".regexlist", 3, False)>
    Public Sub RegexFAQRegexList(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As System.Text.RegularExpressions.Match)
        If Not ShortcutCheck(Connection, Channel) Then Return
        CommandFAQRegexList(Connection, Sender, Channel, {Match.Groups("Key").Value})
    End Sub
    <Command({"faqregexlist", "faqregexes", "faqregex"}, 1, 1,
  "faqregexlist <context>/<key>",
  "Lists all the regular expressions assigned to a FAQ entry." & vbCrLf & "When someone sends a message to a channel within the FAQ's context that matches the regex, the FAQ data will be displayed in the channel.",
  ".regexlist")>
    Public Sub CommandFAQRegexList(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        ' Resolve the target FAQ entry.
        Dim Target As String
        Try
            Target = FindFAQ(Connection, Channel, Sender, args(0))
            If Target.Contains(" ") Then
                Reply(Connection, Channel, Sender.Split("!"c)(0), "That specification matches " & Choose(Choose("more than one ", "multiple ") & "FAQ entries."))
                Reply(Connection, Channel, Sender.Split("!"c)(0), "Please specify one of $k07" & String.Join("$o, $k07", Target.Split(" "c)) & "$o.")
                Return
            End If
        Catch ex As KeyNotFoundException
            Reply(Connection, Channel, Sender, "That FAQ entry " & VBot.Choose("doesn't exist.", "hasn't been entered.", "isn't defined."))
            Return
        End Try

        Dim FAQ = FAQs(Target)

        If Not UserHasPermission(Connection, Channel, Sender, MyKey & ".regexlist." & GetContext(Target)) Then
            Reply(Connection, Channel, Sender, "You don't have permission to modify a FAQ entry there.")
            Return
        End If

        Dim DisplayKey As String = "$k7" & If(args(0).ToLower = Target.ToLower, GetContext(Target) & "/", "") & "$k8" & GetKey(Target) & "$o"
        If FAQ.Regexes Is Nothing Then
            Reply(Connection, Channel, Sender, String.Format(VBot.Choose("There are no regular expressions " & VBot.Choose("for ", "associated with ", "assigned to ") & "{0}.", "{0} " & VBot.Choose("has no ", "does not have any ", "doesn't have any ", "isn't associated with any ", "is associated with no ", "is not associated with any ") & "regular expressions.", "There are no regular expressions " & VBot.Choose("assigned to ", "associated with ") & "{0}."), DisplayKey))
        Else
            Reply(Connection, Channel, Sender, String.Format(VBot.Choose("Regular expressions " & VBot.Choose("for ", "associated with ", "assigned to ") & "{0}:", "{0} " & VBot.Choose("has ", "is associated with ") & "the following regular expressions:", "The following regular expressions are " & VBot.Choose("assigned to ", "associated with ") & "{0}:"), DisplayKey))
            For i = 0 To UBound(FAQ.Regexes)
                Reply(Connection, Channel, Sender, IRCColours.Yellow & i & "$k7:$o " & FAQ.Regexes(i))
            Next
        End If
    End Sub

    <Regex({"^\?\*= (?<Key>[^ ]+) (?<Number>\d+) (?<Replacement>.*)"},
".regex", 3, False)>
    Public Sub RegexFAQRegexEdit(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As System.Text.RegularExpressions.Match)
        If Not ShortcutCheck(Connection, Channel) Then Return
        CommandFAQRegexEdit(Connection, Sender, Channel, {Match.Groups("Key").Value, Match.Groups("Number").Value, Match.Groups("Replacement").Value})
    End Sub
    <Command({"faqregexedit"}, 3, 3,
  "faqregexlist <context>/<key> <number> <replacement>",
  "Lists all the regular expressions assigned to a FAQ entry." & vbCrLf & "When someone sends a message to a channel within the FAQ's context that matches the regex, the FAQ data will be displayed in the channel.",
  ".regex")>
    Public Sub CommandFAQRegexEdit(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        ' Resolve the target FAQ entry.
        Dim Target As String
        Try
            Target = FindFAQ(Connection, Channel, Sender, args(0))
            If Target.Contains(" ") Then
                Reply(Connection, Channel, Sender.Split("!"c)(0), "That specification matches " & Choose(Choose("more than one ", "multiple ") & "FAQ entries."))
                Reply(Connection, Channel, Sender.Split("!"c)(0), "Please specify one of $k07" & String.Join("$o, $k07", Target.Split(" "c)) & "$o.")
                Return
            End If
        Catch ex As KeyNotFoundException
            Reply(Connection, Channel, Sender, "That FAQ entry " & VBot.Choose("doesn't exist.", "hasn't been entered.", "isn't defined."))
            Return
        End Try

        Dim FAQ = FAQs(Target)

        If Not UserHasPermission(Connection, Channel, Sender, MyKey & ".regex." & GetContext(Target)) Then
            Reply(Connection, Channel, Sender, "You don't have permission to modify a FAQ entry there.")
            Return
        End If

        Dim LineNumber As UShort

        Dim DisplayKey As String = "$k7" & If(args(0).ToLower = Target.ToLower, GetContext(Target) & "/", "") & "$k8" & GetKey(Target) & "$o"
        If FAQ.Regexes Is Nothing Then
            Say(Connection, Channel, String.Format(VBot.Choose("There are no regular expressions " & VBot.Choose("for ", "associated with ", "assigned to ") & "{0}.", "{0} " & VBot.Choose("has no ", "does not have any ", "doesn't have any ", "isn't associated with any ", "is associated with no ", "is not associated with any ") & "regular expressions.", "There are no regular expressions " & VBot.Choose("assigned to ", "associated with ") & "{0}."), DisplayKey))
        ElseIf Not UShort.TryParse(args(1), LineNumber) Then
            Reply(Connection, Channel, Sender, "$k04" & args(1) & "$o isn't a valid number.")
        ElseIf LineNumber > UBound(FAQ.Regexes) + 1 Then
            Reply(Connection, Channel, Sender, DisplayKey & " doesn't have a line number $k04" & LineNumber & "$o.")
        ElseIf LineNumber = UBound(FAQ.Regexes) + 1 Then
            CommandFAQAdd(Connection, Sender, Channel, {args(0), args(2)})
        Else
            FAQ.Regexes(LineNumber) = args(2)
            Reply(Connection, Channel, Sender, String.Format(VBot.Choose(VBot.Choose("Changed ", "Modified ", "Replaced ", "Rewrote ", "Edited ") & "regex " & VBot.Choose("number ", "") & "{0} ", "Regex " & VBot.Choose("number ", "") & "{0} has been " & VBot.Choose("changed ", "modified ", "replaced ", "rewritten ", "edited ")) & "in " & VBot.Choose("", "FAQ ", "FAQ entry ", "the entry for ") & "{1}.", IRCColours.Blue & LineNumber & "$o", DisplayKey))
        End If
    End Sub

    <Regex({"^\?\*- (?<Key>[^ ]+) (?<Number>.*)"},
".regex", 3, False)>
    Public Sub RegexFAQRegexDelete(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As System.Text.RegularExpressions.Match)
        If Not ShortcutCheck(Connection, Channel) Then Return
        CommandFAQRegexDelete(Connection, Sender, Channel, {Match.Groups("Key").Value, Match.Groups("Number").Value})
    End Sub
    <Command({"faqregexdelete", "faqregexdel"}, 1, 2,
  "faqregexdelete [<context>/]<key> <which one to delete>",
  "Disassociates a regular expression from a FAQ entry. Use $cfaqregexlist to get a list of assigned regular expressions.",
  ".regex")>
    Public Sub CommandFAQRegexDelete(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim LineNumber As UShort

        ' Resolve the target FAQ entry.
        Dim Target As String
        Try
            Target = FindFAQ(Connection, Channel, Sender, args(0))
            If Target.Contains(" ") Then
                Reply(Connection, Channel, Sender.Split("!"c)(0), "That specification matches " & Choose(Choose("more than one ", "multiple ") & "FAQ entries."))
                Reply(Connection, Channel, Sender.Split("!"c)(0), "Please specify one of $k07" & String.Join("$o, $k07", Target.Split(" "c)) & "$o.")
                Return
            End If
        Catch ex As KeyNotFoundException
            Reply(Connection, Channel, Sender, "That FAQ entry " & VBot.Choose("doesn't exist.", "hasn't been entered.", "isn't defined."))
            Return
        End Try

        Dim FAQ = FAQs(Target)

        If Not UserHasPermission(Connection, Channel, Sender, MyKey & ".regex." & GetContext(Target)) Then
            Reply(Connection, Channel, Sender, "You don't have permission to modify a FAQ entry there.")
            Return
        End If

        Dim DisplayKey As String = "$k7" & If(args(0).ToLower = Target.ToLower, GetContext(Target) & "/", "") & "$k8" & GetKey(Target) & "$o"
        If Not UShort.TryParse(args(1), LineNumber) Then
            Reply(Connection, Channel, Sender, IRCColours.Red & args(1) & "$o isn't a valid index.")
        ElseIf LineNumber > UBound(If(FAQ.Regexes, {})) Then
            Reply(Connection, Channel, Sender, DisplayKey & " doesn't have that many regular expressions.")
        Else
            'FAQ.Data = ""
            For i = LineNumber To UBound(FAQ.Regexes) - 1
                FAQ.Regexes(i) = FAQ.Regexes(i + 1)
            Next
            ReDim Preserve FAQ.Regexes(UBound(FAQ.Regexes) - 1)
            Reply(Connection, Channel, Sender, String.Format(VBot.Choose(VBot.Choose("Removed ", "Deleted ") & "regex " & VBot.Choose("number ", "") & "{1} ", "Line " & VBot.Choose("number ", "") & "{0} has been " & VBot.Choose("removed ", "deleted ")) & "from " & VBot.Choose("", "FAQ ", "FAQ entry ", "the entry for ") & "{1}.", IRCColours.Blue & LineNumber & "$o", DisplayKey))
        End If

        If FAQ.Regexes IsNot Nothing Then
            Reply(Connection, Channel, Sender, String.Format(VBot.Choose("Regular expressions " & VBot.Choose("for ", "associated with ", "assigned to ") & "{0}:", "{0} " & VBot.Choose("has ", "is associated with ") & "the following regular expressions:", "The following regular expressions are " & VBot.Choose("assigned to ", "associated with ") & "{0}:"), DisplayKey))
            For i = 0 To UBound(FAQ.Regexes)
                Reply(Connection, Channel, Sender, IRCColours.Yellow & i & "$k7: $k12" & FAQ.Regexes(i))
                Threading.Thread.Sleep(600)
            Next
        End If
    End Sub

    <Command({"faqpset", "faqpconfig", "faqpproperty"}, 1, 2,
    "set <property> <value>",
    "Changes settings for this plugin." & vbCrLf &
    "You can set the following properties: $k11noshortcutchans$o." & vbCrLf &
    "Alternatively, you can omit the $k11value$o parameter to just check a property's value.",
     ".set")>
    Public Sub CommandSet(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim lProperty = args(0)
        Dim lValue = args.ElementAtOrDefault(1)

        Select Case lProperty.ToLower.Replace("_", "")
            Case "noshortcutchans", "noshortcutchannels", "noregexchans", "noregexchannels", "noregexpchans", "noregexpchannels"
                If lValue = Nothing Then
                    Say(Connection, Channel, "$k11?$o will not be treated as a command in these channels: $k12" & String.Join("$o, $k12", NoShortcutChannels))
                Else
                    NoShortcutChannels = lValue.Split(","c)
                    Say(Connection, Channel, "$k11?$o will not be treated as a command in these channels: $k12" & String.Join("$o, $k12", NoShortcutChannels))
                End If
            Case Else
                Say(Connection, Channel, "I don't manage a property named $k04" & lProperty & "$o for CraftBukkit servers.")
        End Select
    End Sub

    <Command("faqload", 0, 0,
"faqload",
"Loads FAQs from the data file.",
".reload")>
    Public Sub CommandFAQLoad(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Try
            FAQs.Clear()
            Contexts.Clear()
            LoadAllData()
            Reply(Connection, Channel, Sender, "$k9FAQ data has been reloaded successfully.")
        Catch ex As Exception
            Reply(Connection, Channel, Sender, "I couldn't reload FAQ data: $k04" & ex.Message)
        End Try

    End Sub

    <Command("faqsave", 0, 0,
"faqsave",
"Saves FAQs to the data file.",
".save")>
    Public Sub CommandFAQSave(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Try
            SaveAllData()
            Reply(Connection, Channel, Sender, "$k9FAQ data has been saved successfully.")
        Catch ex As Exception
            Reply(Connection, Channel, Sender, "I couldn't save FAQ data: $k04" & ex.Message)
        End Try
    End Sub

#End Region

#Region "Filing"

    Private Sub LoadAllDataXML(Optional ByVal FileName As String = "FAQs.xml")
        If Not My.Computer.FileSystem.FileExists(FileName) Then
        Else
            Try
                Dim Reader = Xml.XmlReader.Create(FileName)

                Do Until Reader.EOF : Reader.Read()
                    If Reader.NodeType = Xml.XmlNodeType.Element Then
                        If Reader.Name.ToLower = "faqdb" Then
                            NoShortcutChannels = Reader.GetAttribute("noshortcutchannels").Split({","c, " "c}, System.StringSplitOptions.RemoveEmptyEntries)
                            ''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' faqdb
                            Do Until Reader.EOF : Reader.Read()
                                If Reader.NodeType = Xml.XmlNodeType.Element Then
                                    If Reader.Name.ToLower = "contexts" Then
                                        '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' faqdb.contexts

                                        If Reader.IsEmptyElement Then Continue Do
                                        Do Until Reader.EOF : Reader.Read()
                                            If Reader.NodeType = Xml.XmlNodeType.Element Then
                                                If Reader.Name.ToLower = "context" Then
                                                    Dim newKey = Reader.GetAttribute("key"), newValue As New List(Of String)

                                                    '''''''''''''''''''''''''''''''''''''''''''''''' faqdb.contexts.context
                                                    If Reader.IsEmptyElement Then Continue Do
                                                    Do Until Reader.EOF : Reader.Read()
                                                        If Reader.NodeType = Xml.XmlNodeType.Element Then
                                                            If Reader.Name.ToLower = "channels" Then
                                                                ''''''''''''''''''''''''''' faqdb.contexts.context.channels
                                                                If Reader.IsEmptyElement Then Continue Do
                                                                Do Until Reader.EOF : Reader.Read()
                                                                    If Reader.NodeType = Xml.XmlNodeType.Text Then
                                                                        newValue.AddRange(Reader.Value.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.RemoveEmptyEntries))
                                                                    ElseIf Reader.NodeType = Xml.XmlNodeType.EndElement AndAlso Reader.Name = "channels" Then
                                                                        Exit Do
                                                                    ElseIf Reader.NodeType = Xml.XmlNodeType.Element Then
                                                                        If Not Reader.IsEmptyElement Then : Dim MiscElementName = Reader.Name : Do Until Reader.EOF : Reader.Read() : If Reader.NodeType = Xml.XmlNodeType.EndElement AndAlso Reader.Name = MiscElementName Then : Exit Do : End If : Loop : End If
                                                                    End If
                                                                Loop

                                                            ElseIf Not Reader.IsEmptyElement Then : Dim MiscElementName = Reader.Name : Do Until Reader.EOF : Reader.Read() : If Reader.NodeType = Xml.XmlNodeType.EndElement AndAlso Reader.Name = MiscElementName Then : Exit Do : End If : Loop
                                                            End If
                                                        ElseIf Reader.NodeType = Xml.XmlNodeType.EndElement AndAlso Reader.Name = "context" Then
                                                            Contexts.Add(newKey, newValue.ToArray)
                                                            Exit Do
                                                        End If
                                                    Loop

                                                ElseIf Not Reader.IsEmptyElement Then : Dim MiscElementName = Reader.Name : Do Until Reader.EOF : Reader.Read() : If Reader.NodeType = Xml.XmlNodeType.EndElement AndAlso Reader.Name = MiscElementName Then : Exit Do : End If : Loop
                                                End If
                                            ElseIf Reader.NodeType = Xml.XmlNodeType.EndElement AndAlso Reader.Name = "contexts" Then
                                                Exit Do
                                            End If
                                        Loop

                                    ElseIf Reader.Name.ToLower = "faqs" Then
                                        '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' faqdb.faqs

                                        If Reader.IsEmptyElement Then Continue Do
                                        Do Until Reader.EOF : Reader.Read()
                                            If Reader.NodeType = Xml.XmlNodeType.Element Then
                                                If Reader.Name.ToLower = "faq" Then
                                                    Dim newKey = Reader.GetAttribute("key"), newValue As String = "", newRegexes As New List(Of String)
                                                    Dim newCount = Reader.GetAttribute("limitcount"), newInterval = Reader.GetAttribute("limitinterval"), newHide = Reader.GetAttribute("hidelabel")

                                                    '''''''''''''''''''''''''''''''''''''''''''''''''''' faqdb.faqs.faq
                                                    If Reader.IsEmptyElement Then Continue Do
                                                    Do Until Reader.EOF : Reader.Read()
                                                        If Reader.NodeType = Xml.XmlNodeType.Element Then
                                                            If Reader.Name.ToLower = "regex" Then
                                                                '''''''''''''''''''''''''''''''''' faqdb.faqs.faq.regex
                                                                Do Until Reader.EOF : Reader.Read()
                                                                    If Reader.NodeType = Xml.XmlNodeType.Text Then
                                                                        newRegexes.Add(Reader.Value.Replace("\x01", Chr(1)).Replace("\x02", Chr(2)).Replace("\x03", Chr(3)).Replace("\x0F", Chr(15)).Replace("\b", "\"))
                                                                    ElseIf Reader.NodeType = Xml.XmlNodeType.EndElement AndAlso Reader.Name = "regex" Then
                                                                        Exit Do
                                                                    ElseIf Reader.NodeType = Xml.XmlNodeType.Element Then
                                                                        If Not Reader.IsEmptyElement Then : Dim MiscElementName = Reader.Name : Do Until Reader.EOF : Reader.Read() : If Reader.NodeType = Xml.XmlNodeType.EndElement AndAlso Reader.Name = MiscElementName Then : Exit Do : End If : Loop : End If
                                                                    End If
                                                                Loop

                                                            ElseIf Not Reader.IsEmptyElement Then : Dim MiscElementName = Reader.Name : Do Until Reader.EOF : Reader.Read() : If Reader.NodeType = Xml.XmlNodeType.EndElement AndAlso Reader.Name = MiscElementName Then : Exit Do : End If : Loop
                                                            End If
                                                        ElseIf Reader.NodeType = Xml.XmlNodeType.Text Then
                                                            newValue = If(newValue = "", "", newValue & vbCrLf) & Reader.Value.Replace("\x01", Chr(1)).Replace("\x02", Chr(2)).Replace("\x03", Chr(3)).Replace("\x0F", Chr(15)).Replace("\b", "\")
                                                        ElseIf Reader.NodeType = Xml.XmlNodeType.EndElement AndAlso Reader.Name = "faq" Then
                                                            FAQs.Add(newKey, New FAQEntry With {.Data = newValue, .Regexes = If(newRegexes IsNot Nothing, newRegexes.ToArray, Nothing), .RateLimitCount = If(newCount, 1), .RateLimitInterval = If(newInterval, 120), .HideLabel = {"hide", "yes", "on", "true", "+", "1", "-1"}.Contains(newHide)})
                                                            Exit Do
                                                        End If
                                                    Loop

                                                ElseIf Not Reader.IsEmptyElement Then : Dim MiscElementName = Reader.Name : Do Until Reader.EOF : Reader.Read() : If Reader.NodeType = Xml.XmlNodeType.EndElement AndAlso Reader.Name = MiscElementName Then : Exit Do : End If : Loop
                                                End If
                                            ElseIf Reader.NodeType = Xml.XmlNodeType.EndElement AndAlso Reader.Name = "faqs" Then
                                                Exit Do
                                            End If
                                        Loop

                                    ElseIf Not Reader.IsEmptyElement Then : Dim MiscElementName = Reader.Name : Do Until Reader.EOF : Reader.Read() : If Reader.NodeType = Xml.XmlNodeType.EndElement AndAlso Reader.Name = MiscElementName Then : Exit Do : End If : Loop
                                    End If
                                ElseIf Reader.NodeType = Xml.XmlNodeType.EndElement AndAlso Reader.Name = "faqdb" Then
                                    Exit Do
                                End If
                            Loop


                        ElseIf Not Reader.IsEmptyElement Then : Dim MiscElementName = Reader.Name : Do Until Reader.EOF : Reader.Read() : If Reader.NodeType = Xml.XmlNodeType.EndElement AndAlso Reader.Name = MiscElementName Then : Exit Do : End If : Loop
                        End If
                    End If
                Loop
            Catch ex As Xml.XmlException
                OutputLine("\cGRAY[\cREDERROR\cGRAY] \cWHITEThe data file contains an XML error: $k04" & ex.Message & "\r")
            Catch ex As Exception
                OutputLine("\cGRAY[\cREDERROR\cGRAY] \cWHITEI was unable to retrieve FAQ data from the file: $k04" & ex.Message & "\r")
            End Try
        End If
    End Sub

    Private Sub SaveAllDataXML(Optional ByVal FileName As String = "FAQs.xml")
        Try
            Dim Writer = Xml.XmlWriter.Create(FileName, New Xml.XmlWriterSettings With {.Indent = True, .IndentChars = vbTab, .NewLineOnAttributes = False})
            Writer.WriteStartElement("faqdb")
            Writer.WriteAttributeString("noshortcutchannels", String.Join(",", NoShortcutChannels))

            Writer.WriteStartElement("contexts")

            For Each Context In Contexts
                Writer.WriteStartElement("context")
                Writer.WriteAttributeString("key", Context.Key)
                Writer.WriteElementString("channels", String.Join(vbCrLf, Context.Value))
                Writer.WriteEndElement()
            Next
            Writer.WriteEndElement()

            Writer.WriteStartElement("faqs")

            For Each FAQ In FAQs
                Writer.WriteStartElement("faq")
                Writer.WriteAttributeString("key", FAQ.Key)
                Writer.WriteAttributeString("limitcount", FAQ.Value.RateLimitCount)
                Writer.WriteAttributeString("limitinterval", FAQ.Value.RateLimitInterval)
                Writer.WriteAttributeString("hidelabel", If(FAQ.Value.HideLabel, "true", "false"))
                Writer.WriteString(FAQ.Value.Data.Replace("\", "\b").Replace(Chr(1), "\x01").Replace(Chr(2), "\x02").Replace(Chr(3), "\x03").Replace(Chr(15), "\x0F"))
                If FAQ.Value.Regexes IsNot Nothing Then
                    For Each Regex In FAQ.Value.Regexes
                        Writer.WriteElementString("regex", Regex.Replace("\", "\b").Replace(Chr(1), "\x01").Replace(Chr(2), "\x02").Replace(Chr(3), "\x03").Replace(Chr(15), "\x0F"))
                    Next
                End If
                Writer.WriteEndElement()
            Next

            Writer.WriteEndElement()

            Writer.WriteEndElement()

            Writer.Close()
        Catch ex As Exception
            OutputLine("\cGRAY[\cREDERROR\cGRAY] \cWHITEI was unable to record the FAQ data: \cRED$k04" & ex.Message & "\r")
        End Try
    End Sub

    Private Sub LoadAllData(Optional ByVal FileName As String = "FAQs.ini")
        If Not My.Computer.FileSystem.FileExists(FileName) Then
        Else
            Try
                Dim Reader = My.Computer.FileSystem.OpenTextFileReader(FileName)

                Dim Section As String = "", Field As String = "", Value As String = ""

                Do Until Reader.EndOfStream
                    Dim s = Reader.ReadLine

                    Dim Match As System.Text.RegularExpressions.Match
                    Match = System.Text.RegularExpressions.Regex.Match(s, "^\s*\[(?<Section>.*?)\]?\s*$")
                    If Match.Success Then
                        Section = Match.Groups("Section").Value
                        If Section.ToLower <> "contexts" And Section.ToLower <> "aliases" And Not FAQs.ContainsKey(Section) Then FAQs.Add(Section, New FAQEntry)
                        Continue Do
                    End If

                    Match = System.Text.RegularExpressions.Regex.Match(s, "^\s*(?<Field>(?>[^=]*))=(?<Value>.*)$")
                    If Match.Success Then
                        Field = Match.Groups("Field").Value
                        Value = Match.Groups("Value").Value

                        If Section.ToLower = "contexts" Then
                            Contexts.Add(Field, Value.Split({","c, " "c}, StringSplitOptions.RemoveEmptyEntries))
                        ElseIf Section.ToLower = "aliases" Then
                            Try
                                Aliases.Add(Field, Value)
                            Catch ex As Exception
                            End Try
                        Else
                                Select Case Field.ToLower
                                    Case "data", "text", "message", "msg"
                                        FAQs(Section).Data &= If(FAQs(Section).Data = "", "", vbCrLf) & Value
                                    Case "regex", "regexp", "trigger", "regularexpression", "regextrigger", "regexptrigger"
                                        ReDim Preserve FAQs(Section).Regexes(UBound(FAQs(Section).Regexes) + 1)
                                        FAQs(Section).Regexes(UBound(FAQs(Section).Regexes)) = Value
                                    Case "hidden", "hide", "unlisted"
                                        If {"show", "0", "off", "no", "false", "-", "negative", "disable", "disabled", "deactivate", "deactivated", "inactive", "shown", "show"}.Contains(Value.ToLower) Then
                                            FAQs(Section).Hidden = False
                                        ElseIf {"hide", "1", "-1", "on", "yes", "true", "+", "enable", "enabled", "activate", "activated", "active", "hidden", "hide"}.Contains(Value.ToLower) Then
                                            FAQs(Section).Hidden = True
                                        Else
                                            OutputLine("\cGRAY[\cYELLOWERROR\cGRAY] occured while loading FAQ data: (" & Section & ".Hidden) \cWHITE" & Value & "\cGRAY is not a valid Boolean value.\r")
                                        End If
                                    Case "hidelabel", "labelhide", "labelhidden", "nolabel", "hidelabelonregex", "nolabelonregex"
                                        If {"show", "0", "off", "no", "false", "-", "negative", "disable", "disabled", "deactivate", "deactivated", "inactive", "shown", "show"}.Contains(Value.ToLower) Then
                                            FAQs(Section).HideLabel = False
                                        ElseIf {"hide", "1", "-1", "on", "yes", "true", "+", "enable", "enabled", "activate", "activated", "active", "hidden", "hide"}.Contains(Value.ToLower) Then
                                            FAQs(Section).HideLabel = True
                                        Else
                                            OutputLine("\cGRAY[\cYELLOWERROR\cGRAY] occured while loading FAQ data: (" & Section & ".HideLabel) \cWHITE" & Value & "\cGRAY is not a valid Boolean value.\r")
                                        End If
                                    Case "noticeonjoin"
                                        If {"0", "off", "no", "false", "-", "negative", "disable", "disabled", "deactivate", "deactivated", "inactive", "cmsg", "chanmsg", "channelmsg", "normal"}.Contains(Value.ToLower) Then
                                            FAQs(Section).NoticeOnJoin = False
                                        ElseIf {"1", "-1", "on", "yes", "true", "+", "enable", "enabled", "activate", "activated", "active", "notice", "privmsg", "pm"}.Contains(Value.ToLower) Then
                                            FAQs(Section).NoticeOnJoin = True
                                        Else
                                            OutputLine("\cGRAY[\cYELLOWERROR\cGRAY] occured while loading FAQ data: (" & Section & ".NoticeOnJoin) \cWHITE" & Value & "\cGRAY is not a valid Boolean value.\r")
                                        End If
                                    Case "ratelimitcount", "ratelimittriggers", "ratelimittriggers", "ratelimitnumber", "triggerlimit"
                                        Dim i As Integer
                                        If Integer.TryParse(Value, i) Then
                                            If i >= 0 Then
                                                FAQs(Section).RateLimitCount = i
                                            Else
                                                OutputLine("\cGRAY[\cYELLOWERROR\cGRAY] occured while loading FAQ data: (" & Section & ".RateLimitCount) \cWHITE" & Value & "\cGRAY is not a valid setting.\r")
                                            End If
                                        Else
                                            OutputLine("\cGRAY[\cYELLOWERROR\cGRAY] occured while loading FAQ data: (" & Section & ".RateLimitCount) \cWHITE" & Value & "\cGRAY is not a valid integer value.\r")
                                        End If
                                    Case "ratelimitinterval", "ratelimittime"
                                        Dim i As Integer
                                        If Integer.TryParse(Value, i) Then
                                            If i >= 0 Then
                                                FAQs(Section).RateLimitInterval = i
                                            Else
                                                OutputLine("\cGRAY[\cYELLOWERROR\cGRAY] occured while loading FAQ data: (" & Section & ".RateLimitCount) \cWHITE" & Value & "\cGRAY is not a valid setting.\r")
                                            End If
                                        Else
                                            OutputLine("\cGRAY[\cYELLOWERROR\cGRAY] occured while loading FAQ data: (" & Section & ".RateLimitCount) \cWHITE" & Value & "\cGRAY is not a valid integer value.\r")
                                        End If
                                    Case Else
                                        If Not String.IsNullOrWhiteSpace(s) Then FAQs(Section).Data &= s & vbCrLf
                                End Select
                        End If
                        Continue Do
                    End If
                Loop
                Reader.Close()

            Catch ex As Exception
                OutputLine("\cGRAY[\cREDERROR\cGRAY] \cWHITEI was unable to retrieve FAQ data from the file: $k04" & ex.Message & "\r")
            End Try
        End If
    End Sub

    Private Sub SaveAllData(Optional ByVal FileName As String = "FAQs.ini")
        Try
            Dim Writer = My.Computer.FileSystem.OpenTextFileWriter(FileName, False)

            Writer.WriteLine("; This file contains all of the FAQ data.")
            Writer.WriteLine("; Feel free to edit it using Notepad, or any other text editor, if you wish.")
            Writer.WriteLine("; If you do this while the bot is running, run the command !faqload to reload the data.")
            Writer.WriteLine()

            Writer.WriteLine("[Contexts]")

            For Each Context In Contexts
                Writer.WriteLine(Context.Key & "=" & String.Join(",", Context.Value))
            Next
            Writer.WriteLine()

            For Each FAQ In FAQs
                Writer.WriteLine("[" & FAQ.Key & "]")
                For Each Line In If(FAQ.Value.Data, "").Split({vbCrLf, vbCr, vbLf}, StringSplitOptions.RemoveEmptyEntries)
                    Writer.WriteLine("Data=" & Line)
                Next
                For Each Regex In If(FAQ.Value.Regexes, {})
                    Writer.WriteLine("Regex=" & Regex)
                Next
                If FAQ.Value.Hidden Then Writer.WriteLine("Hidden=Yes")
                If FAQ.Value.HideLabel Then Writer.WriteLine("HideLabel=Yes")
                If Not FAQ.Value.NoticeOnJoin Then Writer.WriteLine("NoticeOnJoin=No")
                If FAQ.Value.RateLimitCount <> 1 Then Writer.WriteLine("RateLimitCount=" & FAQ.Value.RateLimitCount)
                If FAQ.Value.RateLimitInterval <> 120 Then Writer.WriteLine("RateLimitInterval=" & FAQ.Value.RateLimitInterval)
                Writer.WriteLine()
            Next
            If Aliases.Count <> 0 Then
                Writer.WriteLine("[Aliases]")
                For Each lAlias In Aliases
                    Writer.WriteLine(lAlias.Key & "=" & lAlias.Value)
                Next
            End If
            Writer.Close()
        Catch ex As Exception
            OutputLine("\cGRAY[\cREDERROR\cGRAY] \cWHITEI was unable to record the FAQ data: \cRED$k04" & ex.Message & "\r")
        End Try
    End Sub

#End Region

    Private Function ShortcutCheck(ByVal Connection As IRCConnection, ByVal Channel As String) As Boolean
        If Connection Is Nothing Then
            For Each lChannel In NoShortcutChannels
                If Channel.Contains(">") Then
                    If lChannel = "*" OrElse
                    System.Text.RegularExpressions.Regex.IsMatch(Channel, String.Format("(\*|!(\*|{0}))/(\*|{1})(/(\*|>\*|>{2}))?", System.Text.RegularExpressions.Regex.Escape(MyKey), System.Text.RegularExpressions.Regex.Escape(lChannel.Split({">"c}, 2)(0).Split({"/"c}, 3).ElementAtOrDefault(1)), System.Text.RegularExpressions.Regex.Escape(lChannel.Split({">"c}, 2).ElementAtOrDefault(1)))) Then Return False
                Else
                    If lChannel = "*" OrElse
                    System.Text.RegularExpressions.Regex.IsMatch(Channel, String.Format("(\*|!(\*|{0}))/(\*|{1})(/(\*|>\*))?", System.Text.RegularExpressions.Regex.Escape(MyKey), System.Text.RegularExpressions.Regex.Escape(lChannel.Split({">"c}, 2)(0).Split({"/"c}, 3).ElementAtOrDefault(1)))) Then Return False
                End If
            Next
            Return False
        Else
            Return Not (NoShortcutChannels.Contains(Connection.Address & "/" & Channel) Or
                NoShortcutChannels.Contains("*/" & Channel) Or
                NoShortcutChannels.Contains(Connection.Address & "/*") Or
               NoShortcutChannels.Contains("*/*") Or NoShortcutChannels.Contains("*"))
        End If
        Return True
    End Function
End Class
