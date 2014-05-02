' General to-do list:
'   TODO: Split this up into multiple plugins.

Imports System.Net
Imports System.Net.Sockets
Imports VBot

Public Class CorePlugin
    Inherits Plugin

    Public Overrides ReadOnly Property Name As String
        Get
            Return "Main Commands"
        End Get
    End Property

    Public Overrides Function Help(ByVal Topic As String, ByVal IsMajorChannel As Boolean) As String
        If Topic Is Nothing Then Return "Please enter $k11$ccmdlist$k for a list of my commands."
        Return Nothing
    End Function

    <Command({"help"}, 0, 2,
    "help [[channel] <topic>]",
    "Gives information about what I'm doing in a channel.")>
    Public Sub CommandHelp(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim Responses As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase), Topic = args.ElementAtOrDefault(0)
        Dim TargetChannel = If(Connection Is Nothing, "", Connection.Address) & "/" & Channel
        Dim NoSpecial As Boolean = True

        For Each m In VBot.Plugins
            Dim Response As String
            If m.Value.Obj.IsMajorChannel(Connection, Channel) Then
                Response = m.Value.Obj.Help(Topic, True)
                If Response <> Nothing Then Responses.Add(m.Key, Response.Replace("%nickname%", Sender.Split("!"c)(0)).Replace("%channel%", If(Channel.StartsWith("!"), Channel.Split("/"c)(1), Channel)))
            ElseIf m.Value.Obj.IsMinorChannel(Connection, Channel) Then
                Response = If(m.Value.Obj.Help(Topic, False), "").Replace("%nickname%", Sender.Split("!"c)(0))
                If Response <> Nothing Then Responses.Add(m.Key, Response.Replace("%nickname%", Sender.Split("!"c)(0)).Replace("%channel%", If(Channel.StartsWith("!"), Channel.Split("/"c)(1), Channel)))
            End If

            If NoSpecial And Response <> Nothing Then
                If TargetChannel.StartsWith("!") Then
                    If m.Value.Obj.Channels.Contains(TargetChannel.Split("/"c)(0) & "/*", System.StringComparer.OrdinalIgnoreCase) Or m.Value.Obj.Channels.Contains("!*/*") Or m.Value.Obj.Channels.Contains("!*") Or m.Value.Obj.Channels.Contains("*/*") Or m.Value.Obj.Channels.Contains("*") Then
                    ElseIf m.Value.Obj.Channels.Contains(Channel.Split({"/"c}, 3)(0) & "/" & Channel.Split({"/"c}, 3)(1), System.StringComparer.OrdinalIgnoreCase) Or m.Value.Obj.Channels.Contains("!*" & "/" & Channel.Split({"/"c}, 2)(1), System.StringComparer.OrdinalIgnoreCase) Then
                        NoSpecial = False
                    Else
                        Continue For
                    End If
                Else
                    If m.Value.Obj.Channels.Contains(TargetChannel.Split("/"c)(0) & "/*", System.StringComparer.OrdinalIgnoreCase) Or m.Value.Obj.Channels.Contains("*/*") Or m.Value.Obj.Channels.Contains("*") Then
                    ElseIf m.Value.Obj.Channels.Contains(TargetChannel, System.StringComparer.OrdinalIgnoreCase) Or m.Value.Obj.Channels.Contains("*" & "/" & TargetChannel.Split("/"c)(1), System.StringComparer.OrdinalIgnoreCase) Then
                        NoSpecial = False
                    Else
                        Continue For
                    End If
                End If
            End If

        Next

        If NoSpecial Then
            Threading.Thread.Sleep(700)
            Reply(Connection, Channel, Sender, "I don't do anything special in this channel.")
        End If
        For Each Response In Responses
            For Each Line In Response.Value.Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries)
                Threading.Thread.Sleep(700)
                Reply(Connection, Channel, Sender, Line)
            Next
        Next
    End Sub

    <Regex({"What (do|can) you do\??",
            "What are your commands\??",
            "What commands do you have\??"},
            "", 3, True)>
    Public Sub RegexCommandList(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As System.Text.RegularExpressions.Match)
        CommandCommandList(Connection, Sender, Channel, {})
    End Sub
    <Command({"cmdlist", "listcmd", "listcmds", "listcommands", "commandlist", "commands", "cmdinfo", "commandinfo", "commands"}, 0, 1,
    "cmdlist [channel] or !cmdinfo [command]",
    "Retrieves a list of commands, or information about a command.")>
    Public Sub CommandCommandList(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        If args.Count = 1 Then
            If args(0).Contains("/") AndAlso args(0).Split("/"c)(1).StartsWith("#") Then
                HelpChannel(Connection, Sender, Channel, args, args(0))
            ElseIf args(0).StartsWith("#") Then
                ' Assume it's a channel.
                If Channel.StartsWith("!") Then
                    HelpChannel(Connection, Sender, Channel, args, Nothing)
                Else
                    HelpChannel(Connection, Sender, Channel, args, Connection.Address & "/" & args(0))
                End If
            Else
                ' Assume it's a command.
                If CommandPrefixes(Connection, Channel).Contains(args(0)(0)) Then
                    HelpCommand(Connection, Sender, Channel, args, Me, If(args(0).Split(" ")(0).Length = 1, "", args(0).Split(" ")(0).Substring(1)))
                Else
                    HelpCommand(Connection, Sender, Channel, args, Me, args(0).Split(" ")(0))
                End If
            End If
        Else
            HelpChannel(Connection, Sender, Channel, args, If(Connection Is Nothing, Channel, Connection.Address & "/" & Channel))
        End If
    End Sub

    <Command({"cmdinfo", "commandinfo"}, 1, 1,
"cmdinfo <command>",
"Retrieves information about a command.")>
    Public Sub CommandCommandInfo(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        If CommandPrefixes(Connection, Channel).Contains(args(0)(0)) Then
            HelpCommand(Connection, Sender, Channel, args, Me, If(args(0).Split(" ")(0).Length = 1, "", args(0).Split(" ")(0).Substring(1)))
        Else
            HelpCommand(Connection, Sender, Channel, args, Me, args(0).Split(" ")(0))
        End If
    End Sub

    Private Sub HelpCommand(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String, ByVal iModule As Plugin, ByVal Command As String)
        Dim Names() As String, MaxArgumentCount As Short, MinArgumentCount As Short, Description As String, Syntax As String, Permission As String, NoPermissionMessage As String, Scope As CommandAttribute.CommandScope
        Dim Method As Reflection.MethodInfo, attr As Object

        For Each m In Plugins.Values
            For Each Method In m.Obj.GetType.GetMethods
                For Each attr In Method.GetCustomAttributes(False)
                    If TypeOf attr Is CommandAttribute Then
                        Names = CType(attr, CommandAttribute).Names
                        If Names.Contains(Command) Then GoTo invoke
                    End If
                Next
            Next
            Continue For

Invoke:
            MaxArgumentCount = CType(attr, CommandAttribute).MaxArgumentCount
            MinArgumentCount = CType(attr, CommandAttribute).MinArgumentCount
            Syntax = CType(attr, CommandAttribute).Syntax
            Description = CType(attr, CommandAttribute).Description
            Permission = CType(attr, CommandAttribute).Permission
            If If(Permission, "").StartsWith(".") Then Permission = m.Obj.MyKey & Permission
            NoPermissionMessage = CType(attr, CommandAttribute).NoPermissionsMessage
            Scope = CType(attr, CommandAttribute).Scope

            If ((Scope And Plugin.CommandAttribute.CommandScope.PM) = 0 And Not Channel.Contains("#")) Then
                Reply(Connection, Channel, Sender, Choose("Sorry " & Sender.Split("!"c)(0) & ", ", "") & Choose("you can't use that command " & Choose("in this context.", "like this.", "here.") & Choose("Please use", "You have to use", "You need to use", "You must use", "Use") & " it in a channel.", "you can't use that command in a private message." & Choose(Choose("Please use", "You have to use", "You need to use", "You must use", "Use") & " it in a channel."), "you can only use that command in a channel."), True)
                Return
            End If
            If ((Scope And Plugin.CommandAttribute.CommandScope.Channel) = 0 And Channel.Contains("#")) Then
                Reply(Connection, Channel, Sender, Choose("Sorry " & Sender.Split("!"c)(0) & ", ", "") & Choose("you can't use that command " & Choose("in this context.", "like this.", "here.") & Choose("Please use", "You have to use", "You need to use", "You must use", "Use") & " it in a private message (/msg) to me.", "you can't use that command in a channel." & Choose(Choose("Please use", "You have to use", "You need to use", "You must use", "Use") & " it in a private message (/msg) to me."), "you can only use that command in a private message to me."), True)
                Return
            End If

            'If (Access And bModule.CommandAttribute.CommandAccess.Identified) > 0 AndAlso Not VBot.Identifications.ContainsKey(Sender.Split("!"c)(0)) Then
            '    Reply(Connection, Channel, Sender, Choose("Sorry " & Sender.Split("!"c)(0) & ", ", "") & Choose("that command is only available to identified users.", Choose("you are ", "you're ") & "not identified.", "only identified users may use that command."), True)
            '    Return
            'ElseIf (Access And bModule.CommandAttribute.CommandAccess.ChannelOp) > 0 AndAlso Not IsOp(Connection, Channel, Sender.Split("!"c)(0)) Then
            '    Reply(Connection, Channel, Sender, Choose("Sorry " & Sender.Split("!"c)(0) & ", ", "") & Choose("that command is only available to operators.", Choose("you are ", "you're ") & "not an operator.", "only operators may use that command."), True)
            '    Return
            'ElseIf (Access And bModule.CommandAttribute.CommandAccess.Owner) > 0 AndAlso Not VBot.IsOwner(Connection, Channel, Sender.Split("!"c)(0)) Then
            '    Reply(Connection, Channel, Sender, Choose("Sorry " & Sender.Split("!"c)(0) & ", ", "") & Choose("that command is only available to bot owners.", Choose("you are ", "you're ") & "not a bot owner.", "only bot owners may use that command."), True)
            '    Return
            'End If
            If Permission IsNot Nothing AndAlso Not UserHasPermission(Connection, Channel, Sender, Permission) Then
                If NoPermissionMessage <> Nothing Then Say(Connection, Channel, NoPermissionMessage)
                Return
            End If

            Say(Connection, Channel, "$k12Command syntax: $o$c" & Syntax)
            Say(Connection, Channel, Description)
            Return
        Next
        Reply(Connection, Channel, Sender, Choose("Sorry " & Sender.Split("!"c)(0) & ", ", "") & "I don't recognise that command.")
    End Sub

    '<Command({"loadconfig", "reloadconfig", "loadcfg", "reloadcfg", "configload", "configreload", "cfgload", "cfgreload"}, 0, 1,
    '"!loadconfig [connections|plugins|<plugin key>]",
    '"Reloads general configuration.",
    'CommandAttribute.CommandAccess.Owner)>
    'Public Sub CommandReload(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
    '    Select Case args.ElementAtOrDefault(0)
    '        Case Nothing
    '            LoadConfig()
    '            LoadPlugins()
    '        Case "connections", "config"
    '            LoadConfig()
    '        Case "plugins"
    '            LoadPlugins()
    '        Case Else

    '    End Select
    'End Sub

    Private Sub HelpChannel(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String, ByVal TargetChannel As String)
        Dim GeneralCommands As New Dictionary(Of String, SortedSet(Of String))
        Dim ChannelCommands As New Dictionary(Of String, SortedSet(Of String))
        Dim NoCommands As Boolean = True

        Dim IsGeneral As Boolean

        For Each m In VBot.Plugins
            If TargetChannel.StartsWith("!") Then
                If m.Value.Obj.Channels.Contains(TargetChannel.Split("/"c)(0) & "/*", System.StringComparer.OrdinalIgnoreCase) Or m.Value.Obj.Channels.Contains("!*/*") Or m.Value.Obj.Channels.Contains("!*") Or m.Value.Obj.Channels.Contains("*/*") Or m.Value.Obj.Channels.Contains("*") Then
                    IsGeneral = True
                ElseIf m.Value.Obj.Channels.Contains(Channel.Split({"/"c}, 3)(0) & "/" & Channel.Split({"/"c}, 3)(1), System.StringComparer.OrdinalIgnoreCase) Or m.Value.Obj.Channels.Contains("!*" & "/" & Channel.Split({"/"c}, 2)(1), System.StringComparer.OrdinalIgnoreCase) Then
                    IsGeneral = False
                Else
                    Continue For
                End If
            Else
                If m.Value.Obj.Channels.Contains(TargetChannel.Split("/"c)(0) & "/*", System.StringComparer.OrdinalIgnoreCase) Or m.Value.Obj.Channels.Contains("*/*") Or m.Value.Obj.Channels.Contains("*") Then
                    IsGeneral = True
                ElseIf m.Value.Obj.Channels.Contains(TargetChannel) Or m.Value.Obj.Channels.Contains("*" & "/" & TargetChannel.Split("/"c)(1), System.StringComparer.OrdinalIgnoreCase) Then
                    IsGeneral = False
                Else
                    Continue For
                End If
            End If

            Dim Name As String, Permission As String, Scope As CommandAttribute.CommandScope
            Dim Method As Reflection.MethodInfo, attr As Object

            'Dim UserAccess As CommandAttribute.CommandAccess = CommandAttribute.CommandAccess.General
            'If Connection Is Nothing Then
            '    If VBot.Identifications.ContainsKey("!" & MyKey & "/" & Sender.Split("!"c)(0)) Then
            '        UserAccess += CommandAttribute.CommandAccess.Identified
            '        If VBot.Accounts(VBot.Identifications("!" & MyKey & "/" & Sender.Split("!"c)(0)).AccountName).Owner Then UserAccess += CommandAttribute.CommandAccess.Owner
            '    End If
            'Else
            '    If VBot.Identifications.ContainsKey(Connection.Address & "/" & Sender.Split("!"c)(0)) Then UserAccess += CommandAttribute.CommandAccess.Identified
            '    If IsOp(Connection, Channel, Sender.Split("!"c)(0)) Then UserAccess += CommandAttribute.CommandAccess.ChannelOp
            '    If VBot.IsOwner(Connection, Channel, Sender.Split("!"c)(0)) Then UserAccess += CommandAttribute.CommandAccess.Owner
            'End If

            GeneralCommands.Add(m.Key, New SortedSet(Of String))
            ChannelCommands.Add(m.Key, New SortedSet(Of String))

            For Each Method In m.Value.Obj.GetType.GetMethods

                For Each attr In Method.GetCustomAttributes(False)
                    If TypeOf attr Is CommandAttribute Then
                        Name = CType(attr, CommandAttribute).Names(0)
                        Permission = CType(attr, CommandAttribute).Permission
                        If If(Permission, "").StartsWith(".") Then Permission = m.Key & Permission

                        If (Not UserHasPermission(Connection, Channel, Sender, Permission)) Then Continue For

                        If Not GeneralCommands(m.Key).Contains(Name) And Not ChannelCommands(m.Key).Contains(Name) Then
                            If IsGeneral Then GeneralCommands(m.Key).Add(Name) Else ChannelCommands(m.Key).Add(Name)
                            NoCommands = False
                        End If
                    End If
                Next
            Next

        Next

        ' Show the command list to the user.
        If NoCommands Then
            Reply(Connection, Channel, Sender, "I don't have any commands for " & Choose("this channel.", IRCColours.Blue & Channel & "$o."))
        Else
            Dim gReply As String = "", cReply As String = ""
            For Each Plugin In GeneralCommands
                If Plugin.Value.Count = 0 Then Continue For
                gReply &= "$o  " & "$k15,14[$k0,14 " & Plugin.Key & " $k15,14|$k9,1 " & String.Join("  ", Plugin.Value) & " $k15,1]"
            Next
            For Each Plugin In ChannelCommands
                If Plugin.Value.Count = 0 Then Continue For
                cReply &= "$o  " & "$k15,14[$k0,14 " & Plugin.Key & " $k15,14|$k9,1 " & String.Join("  ", Plugin.Value) & " $k15,1]"
            Next

            If gReply <> "" Then Reply(Connection, Channel, Sender, "$bGeneral commands:" & gReply)
            If cReply <> "" Then Reply(Connection, Channel, Sender, "$bCommands for " & Channel & ":" & cReply)
            Reply(Connection, Channel, Sender, "Enter $k11$ccmdinfo $k10<command name>$o for more information about a command.")
        End If

    End Sub

    <Regex({"(What is|What's) the IP (address )?of (?<Host>.*?)\??$"})>
    Public Sub RegexResolve(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As System.Text.RegularExpressions.Match)
        CommandResolve(Connection, Sender, Channel, {Match.Groups("Host").Value})
    End Sub
    <Command({"resolve", "host", "dns"}, 1, 1,
        "resolve <hostname|IP>",
        "Resolves a hostname or an IP address.")>
    Public Sub CommandResolve(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim Address As String = args.ElementAtOrDefault(0)
        Dim ip As Net.IPAddress, hostname As String
        Try
            If Net.IPAddress.TryParse(Address, ip) Then
                ' Check if it's a null, private or loopback address. The user needs permission to look at private addresses.
                Select Case ip.AddressFamily
                    Case AddressFamily.InterNetwork  ' IPv4 address. The loopback address range is 127.0.0.0/8; private address ranges include 10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16
                        Dim ipb = ip.GetAddressBytes
                        If ipb(0) = 127 Then
                            Say(Connection, Channel, IRCColours.Blue & ip.ToString & "$o resolves to $k09localhost$o.")
                            Return
                        ElseIf ipb(0) = 10 Or (ipb(0) = 172 And (ipb(1) And 240) = 16) Or (ipb(0) = 192 And ipb(1) = 168) Then
                            If Not UserHasPermission(Connection, Channel, Sender, "me.resolve.private") Then
                                Reply(Connection, Channel, Sender, "You shall not poke at private addresses.")
                                Return
                            End If
                        ElseIf ipb(0) = 0 And ipb(1) = 0 And ipb(2) = 0 And ipb(3) = 0 Then
                            GoTo TrollingAttempt
                        End If
                    Case AddressFamily.InterNetworkV6  ' IPv6 address. The loopback address is ::1; private address ranges include fe80::/16
                        Dim ipb = ip.GetAddressBytes
                        If ipb(0) = 0 And ipb(1) = 0 And ipb(2) = 0 And ipb(3) = 0 And ipb(4) = 0 And ipb(5) = 0 And ipb(6) = 0 And ipb(7) = 0 And
                            ipb(8) = 0 And ipb(9) = 0 And ipb(10) = 0 And ipb(11) = 0 And ipb(12) = 0 And ipb(13) = 0 And ipb(14) = 0 And ipb(15) = 1 Then
                            Say(Connection, Channel, IRCColours.Blue & ip.ToString & "$o resolves to $k09localhost$o.")
                            Return
                        ElseIf ipb(0) = &HFE And ipb(1) = &H80 Then
                            If Not UserHasPermission(Connection, Channel, Sender, "me.resolve.private") Then
                                Reply(Connection, Channel, Sender, "You shall not poke at private addresses.")
                                Return
                            End If
                        ElseIf ipb(0) = 0 And ipb(1) = 0 And ipb(2) = 0 And ipb(3) = 0 And ipb(4) = 0 And ipb(5) = 0 And ipb(6) = 0 And ipb(7) = 0 And
                                ipb(8) = 0 And ipb(9) = 0 And ipb(10) = 0 And ipb(11) = 0 And ipb(12) = 0 And ipb(13) = 0 And ipb(14) = 0 And ipb(15) = 0 Then
                            GoTo TrollingAttempt
                        End If
                End Select

                Dim entry = System.Net.Dns.GetHostEntry(ip)
                hostname = entry.HostName
                Say(Connection, Channel, IRCColours.Blue & ip.ToString & "$o resolves to $k09" & hostname & "$o.")
            Else
                hostname = Address
                Dim entry = System.Net.Dns.GetHostEntry(hostname)
                ip = entry.AddressList(0)
                Dim Message As String = "$o.", Addresses As New List(Of IPAddress)

                ' Check if it's a private address. The user needs permission to look at those.
                Select Case ip.AddressFamily
                    Case AddressFamily.InterNetwork  ' IPv4 address; private address ranges include 10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16
                        Dim ipb = ip.GetAddressBytes
                        If ipb(0) = 10 Or (ipb(0) = 172 And (ipb(1) And 240) = 16) Or (ipb(0) = 192 And ipb(1) = 168) Then
                            If Not UserHasPermission(Connection, Channel, Sender, "me.resolve.private") Then
                                Reply(Connection, Channel, Sender, "You shall not poke at private hosts.")
                                Return
                            End If
                        End If
                    Case AddressFamily.InterNetworkV6  ' IPv6 address; private address ranges include fe80::/16
                        Dim ipb = ip.GetAddressBytes
                        If ipb(0) = &HFE And ipb(1) = &H80 Then
                            If Not UserHasPermission(Connection, Channel, Sender, "me.resolve.private") Then
                                Reply(Connection, Channel, Sender, "You shall not poke at private hosts.")
                                Return
                            End If
                        End If
                End Select

                For Each addr In entry.AddressList
                    If Addresses.Count >= 10 Then
                        Message = "$o plus " & entry.AddressList.Count - 10 & " more."
                        Exit For
                    ElseIf Not Addresses.Contains(addr) Then
                        Addresses.Add(addr)
                    End If
                Next

                Say(Connection, Channel, IRCColours.Blue & hostname & "$o resolves to $k09" & String.Join("$o, $k09", Addresses) & Message)
            End If
        Catch ex As SocketException When ex.SocketErrorCode = SocketError.HostNotFound
            Say(Connection, Channel, "Sorry " & Sender.Split("!"c)(0) & ", I " & Choose("couldn't ", "was unable to ", "could not ", "was not able to ") & "identify $k04" & Address & "$o.")
        Catch ex As ArgumentException When ex.Message.StartsWith("IPv4 address 0.0.0.0 and IPv6 address ::0 are unspecified addresses that cannot be used as a target address.")
            GoTo TrollingAttempt
        Catch ex As Exception When ex.Message = "The requested name is valid, but no data of the requested type was found"
            Say(Connection, Channel, String.Format("Sorry {0}, " & Choose("I " & Choose("couldn't ", "was unable to ", "could not ", "was not able to ") & "find $k04{1}$o.", "$k04{1}$o doesn't seem to exist from here."), Sender.Split("!"c)(0), Address))
        Catch ex As Exception
            Say(Connection, Channel, "I " & Choose("couldn't ", "was unable to ", "could not ", "was not able to ") & "resolve $k04" & Address & "$o: $k4" & ex.Message)
        End Try
        Return
TrollingAttempt:
        If Rnd() < 0.5 Then Say(Connection, Channel, Chr(1) & "ACTION smirks at " & Sender.Split("!"c)(0) & "." & Chr(1))
        Say(Connection, Channel, Choose("Are you trying to crash me?", "Nice try.", "Ha ha; very funny."))
    End Sub

    <Command({"unixtime"}, 1, 1,
    "$cunixtime <unixtime> [+difference]  or  $cunixtime <time>",
    "Converts a time to and from the UNIX time format.")>
    Public Sub CommandUNIXTime(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim UnixTime As Long, Time As DateTimeOffset

        If args(0).Split({" "c}).Count <= 2 AndAlso Long.TryParse(args(0).Split({" "c}, 2)(0), UnixTime) Then
            ' Convert a UNIX time value to a date.
            Time = New DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero) + TimeSpan.FromSeconds(UnixTime)

            Dim m = System.Text.RegularExpressions.Regex.Match(If(args(0).Split({" "c}, 2).ElementAtOrDefault(1), ""), "UTC|local|(\+|-)(\d\d)(?::?([0-5]\d(?::([0-5]\d))?)?)?", Text.RegularExpressions.RegexOptions.IgnoreCase)
            Dim Offset As TimeSpan = TimeSpan.Zero
            If m.Success Then
                If m.Value.ToUpper = "UTC" Then
                    'Offset = TimeSpan.Zero
                Else
                    If m.Groups(1).Value = "+" Then Offset += TimeSpan.FromHours(m.Groups(2).Value)
                    If m.Groups(3).Success Then If m.Groups(1).Value = "+" Then Offset += TimeSpan.FromMinutes(m.Groups(3).Value)
                    If m.Groups(5).Success Then If m.Groups(1).Value = "+" Then Offset += TimeSpan.FromSeconds(m.Groups(5).Value)
                    Time = Time.ToOffset(Offset)
                End If
            End If

            Say(Connection, Channel, Choose("Your time value ", "That time value ") & Choose("corresponds to ", "represents ") & "$k09" & Time.ToString & "$o.")
        ElseIf DateTimeOffset.TryParse(args(0), System.Globalization.DateTimeFormatInfo.InvariantInfo, Globalization.DateTimeStyles.AssumeUniversal, Time) Then
            ' Convert a date to a UNIX time value.
            UnixTime = (Time.ToUniversalTime - New Date(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds

            Say(Connection, Channel, Choose("Your date ", "That date ") & Choose("corresponds to ", "is represented by ") & "$k09" & UnixTime & "$o.")
        Else
            Reply(Connection, Channel, Sender, "That isn't a valid parameter.")
        End If
    End Sub

    '<Command("opme", 0, 0,
    '"!opme",
    '"Gives channel operator status to a bot owner.",
    'CommandAttribute.CommandAccess.Owner, CommandAttribute.CommandScope.Channel)>
    'Public Sub CommandOpMe(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
    '    Connection.Send("MODE " & Channel & " +o " & Sender.Split("!"c)(0))
    '    Connection.Send("NAMES " & Channel)
    'End Sub

    <Regex({"List ((all )?((of )?the )?)?(running|active) (processes|programs|tasks)( on (the|your) (server|machine|host|computer))?.?",
            "(Give|Show) me (the|a) list of ((all )?((of )?the )?)?(running|active) (processes|programs|tasks)( on (the|your) (server|machine|host|computer))?.?"},
            "server.tasklist", 3, True)>
    Public Sub RegexTaskList(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As System.Text.RegularExpressions.Match)
        CommandTaskList(Connection, Sender, Channel, {})
    End Sub
    <Command({"tasklist", "tasks", "listtasks"}, 0, 0,
        "tasklist",
        "Lists all processes running on my server. Only ops may use this command.",
        "server.tasklist")>
    Public Sub CommandTaskList(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim PerformanceCounter As New SortedDictionary(Of String, PerformanceCounter)
        For Each p In Process.GetProcesses
            Threading.Thread.Sleep(10)

            Dim key As String = p.ProcessName, i As Integer = 1
            Do While PerformanceCounter.ContainsKey(key)
                key = p.ProcessName & "#" & i
                i += 1
            Loop

            Dim NewCounter As New PerformanceCounter
            NewCounter.CategoryName = "Process"
            NewCounter.CounterName = "% Processor Time"
            NewCounter.InstanceName = key
            Try : NewCounter.NextValue() : Catch e As Exception : End Try

            PerformanceCounter.Add(key & "|" & p.Id, NewCounter)
        Next
        Threading.Thread.Sleep(2000)
        For Each c In PerformanceCounter
            Threading.Thread.Sleep(200)
            Try
                Dim CPUTime = (c.Value.NextValue() / 2)
                Say(Connection, Sender.Split("!"c)(0), String.Format("$k2Name: $k12{0}  $k2PID: $k12{1}  $k2CPU: $k{3}{2}$k{4}%", c.Key.Split("#"c)(0).Split("|"c)(0).PadRight(32), c.Key.Split("#"c)(0).Split("|"c)(1).PadRight(32), CPUTime.ToString("0.00"), Interaction.Switch(CPUTime < 5, "12", CPUTime < 15, "09", CPUTime < 30, "08", CPUTime < 50, "07", True, "04"), Interaction.Switch(CPUTime < 5, "2", CPUTime < 15, "3", CPUTime < 30, "07", CPUTime < 50, "05", True, "05")), SayOptions.NoticeNever)
            Catch e As Exception
                Say(Connection, Sender.Split("!"c)(0), String.Format("$k2Name: $k12{0}  $k2PID: $k12{1}  $k04{2}", c.Key.Split("#"c)(0).Split("|"c)(0).PadRight(32), c.Key.Split("#"c)(0).Split("|"c)(1).PadRight(32), e.Message), SayOptions.NoticeNever)
            End Try
        Next
    End Sub

    <Regex({"(Kill|Stop|Shut down) (process|(the (process|application|program|task) with )?(process |P)ID) (?<Target>\d*?)\.?$",
"(Kill|Stop|Shut down) (the )?(process(es)?|applications?|programs?|tasks?) (named|called) (?<Target>.*?)\.?$"},
        "server.taskkill", 3, True)>
    Public Sub RegexTaskKill(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As System.Text.RegularExpressions.Match)
        CommandTaskKill(Connection, Sender, Channel, {Match.Groups("Target").Value})
    End Sub
    <Command({"taskkill", "endtask", "killtask", "prockill", "killproc", "endproc"}, 1, 1,
        "taskkill <PID|name>",
        "Kills running processes on my server. Only ops may use this command.",
        "server.taskkill")>
    Public Sub CommandTaskKill(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim Process = args.ElementAtOrDefault(0)
        Dim TargetProcesses() As Process, TargetProcessID As Integer
        If Integer.TryParse(Process, TargetProcessID) Then 'This will return True if Process can represent an Integer, or False otherwise.
            'If the argument is a number, take it as a process ID.
            Try
                TargetProcesses = {System.Diagnostics.Process.GetProcessById(TargetProcessID)}
            Catch ex As ArgumentException 'The process specified by the processId parameter is not running. The identifier might be expired.
                Say(Connection, Channel, Choose("There is no running process ", "There's no running process ", "There isn't a running process ", "There isn't any running process ", "No process is running ", "I couldn't find " & Choose("a ", "any ") & "running process ", "I couldn't find " & Choose("a ", "any ") & "running process ") & Choose("with that ID", "with ID $k04" & Process & "$o") & Choose("", ", " & Sender.Split({"!"c}, 2)(0)) & ".")
                Return
            End Try
        Else
            'If the argument is not a number, take it as a name.
            Try
                TargetProcesses = System.Diagnostics.Process.GetProcessesByName(Process)
            Catch ex As InvalidOperationException  'There are problems accessing the performance counter API's used to get process information. This exception is specific to Windows NT, Windows 2000, and Windows XP.
                Say(Connection, Channel, "I was unable to enumerate processes under that name: $k04" & ex.Message)
                Return
            End Try
            If TargetProcesses.Count = 0 Then
                Say(Connection, Channel, Choose("There is no running process ", "There's no running process ", "There isn't a running process ", "There isn't any running process ", "No process is running ", "I couldn't find " & Choose("a ", "any ") & "running processes ", "There are no running processes ", "There aren't any running processes ") & Choose("under the name $k04" & Process & "$o", "with the name $k04" & Process & "$o", "named $k04" & Process & "$o", "under that name", "with that name") & Choose("", ", " & Sender.Split({"!"c}, 2)(0)) & ".")
            End If
        End If

        Dim s As String = ""
        Dim MessageFormat As String = ""
        For Each p In TargetProcesses
            Try
                p.Kill()
                s &= ", $k09" & p.Id & "$o"
            Catch ex As ComponentModel.Win32Exception 'The associated process could not be terminated. -or- The process is terminating. -or- The associated process is a Win16 executable.
                If MessageFormat = "" Then
                    MessageFormat = Choose("I was not able to ", "I wasn't able to ", "I couldn't ", "I was unable to ", "I could not ") & Choose("terminate ", "kill ", "shut down ", "stop ") & "the process with ID $k04{0}$o: $k04{1}"
                End If
                Say(Connection, Channel, String.Format(MessageFormat, p.Id, ex.Message.Replace(vbCrLf, " / ")))
            End Try
        Next
        If s <> "" Then
            Say(Connection, Channel, "I successfully " & Choose("terminated ", "killed ", "shut down ", "stopped ") & "the processes with ID " & s.Substring(2) & ".")
        End If
    End Sub

    '<Command({"minecraft", "mine", "mc"}, 1, 1,
    '    "!minecraft <command>",
    '    "Runs a command in the context of Minecraft.")>
    'Public Sub CommandMinecraft(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
    '    VBot.Modules("Minecraft").Obj.RunCommand(Connection, Sender, Channel, "!" & args(0))
    'End Sub

    '<Command({"isop", "checkop"}, 0, 2,
    '    "!isop [nickname] [channel]",
    '    "Tells you whether the specified user (default yourself) is an operator in a channel (default this channel).")>
    'Public Sub CommandCheckOp(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
    '    Dim TargetNickname As String = If(args.ElementAtOrDefault(0), Sender.Split("!"c)(0))
    '    Dim TargetChannel As String = If(args.ElementAtOrDefault(1), Channel)
    '    If IsOp(Connection, TargetChannel, TargetNickname) Then
    '        If TargetNickname = Sender.Split("!"c)(0) Then
    '            Say(Connection, Channel, Choose(IRCColours.Blue & TargetNickname & "$o, y", "$oY") & "ou $k9have$o operator privileges " & If(TargetChannel = Channel, Choose("here.", "in this channel.", "in $k12" & TargetChannel & "$o."), "in $k12" & TargetChannel & "$o."))
    '        Else
    '            Say(Connection, Channel, IRCColours.Blue & TargetNickname & " $k9has$o operator privileges " & If(TargetChannel = Channel, Choose("here.", "in this channel.", "in $k12" & Channel & "$o."), "in $k12" & TargetChannel & "$o."))
    '        End If
    '    Else
    '        If TargetNickname = Sender.Split("!"c)(0) Then
    '            Say(Connection, Channel, Choose(IRCColours.Blue & TargetNickname & "$o, y", "$oY") & "ou $k4do not have$o operator privileges " & If(TargetChannel = Channel, Choose("here.", "in this channel.", "in $k12" & TargetChannel & "$o."), "in $k12" & TargetChannel & "$o."))
    '        Else
    '            Say(Connection, Channel, IRCColours.Blue & TargetNickname & " $k4does not have$o operator privileges " & If(TargetChannel = Channel, Choose("here.", "in this channel.", "in $k12" & Channel & "$o."), "in $k12" & TargetChannel & "$o."))
    '        End If
    '    End If
    'End Sub

    '<Command({"isowner", "checkowner"}, 0, 2,
    '    "!isowner [nickname] [method]",
    '    "Tells you whether the specified user (default yourself) is registered as a bot owner.")>
    'Public Sub CommandCheckOwner(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
    '    Dim TargetNickname As String = If(args.ElementAtOrDefault(0), Sender.Split("!"c)(0))
    '    Dim TargetChannel As String = If(args.ElementAtOrDefault(1), Channel)
    '    If VBot.IsOwner(Connection, TargetChannel, TargetNickname) Then
    '        If TargetNickname = Sender.Split("!"c)(0) Then
    '            Say(Connection, Channel, Choose(IRCColours.Blue & TargetNickname & "$o, y", "Y") & "ou $k9have$o bot owner status.")
    '        Else
    '            Say(Connection, Channel, IRCColours.Blue & TargetNickname & " $k9has$o bot owner status.")
    '        End If
    '    Else
    '        If TargetNickname = Sender.Split("!"c)(0) Then
    '            Say(Connection, Channel, Choose(IRCColours.Blue & TargetNickname & "$o, y", "Y") & "ou $k4do not have$o bot owner status.")
    '        Else
    '            Say(Connection, Channel, IRCColours.Blue & TargetNickname & " $k4does not have$o bot owner status.")
    '        End If
    '    End If
    'End Sub

    <Command({"id", "identify", "ident", "login"}, 1, 2,
        "id [user name] <password>",
        "Identifies you to me.")>
    Public Sub CommandIdentify(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        'If Channel.StartsWith("!") Then
        '    Say(Connection, Channel, Choose("Sorry " & Sender.Split("!"c)(0) & ", ", "") & Choose("you can only identify from IRC.", "you can't identify from that channel."), True)
        '    Return
        'End If

        If Channel.Contains("#") Then ' This indicates a channel message. Identification should (obviously) be done privately.
            Say(Connection, Channel, Choose(Choose("Hey ", "") & Sender.Split("!"c)(0) & ", ", "") & Choose("I think " & Choose("that ", "")) & Choose("you should probably ", "you'll want to ") & Choose("run ", "use ", "invoke ") & "that command in a PM to me, " & Choose("not in a channel.", "rather than in a channel."), SayOptions.Capitalise)
            ' TODO: Prompt the user to change their password.
        End If

        Dim ChannelList As New List(Of String)
        If Connection Is Nothing Then
            ChannelList.Add(Channel.Split({"/"c}, 3)(1) & "/" & Channel.Split({">"c})(0).Split({"/"c}, 3)(2))
        Else
            For Each c In Connection.Channels
                If c.Value.Users.ContainsKey(Sender.Split("!"c)(0)) Then
                    ChannelList.Add(c.Key)
                End If
            Next
        End If

        If ChannelList.Count = 0 Then
            Reply(Connection, Channel, Sender.Split("!"c)(0), Choose("You need to ", "You must ") & "be in " & Choose("at least one ", "a ") & "channel with me to identify yourself" & Choose(", " & Sender.Split("!"c)(0), "") & ".")
            Return
        End If

        Dim Message As String, Identification As Identification, Result As Boolean
        If args.Count = 1 Then
            Result = Identify(If(Connection Is Nothing, Channel.Split({"/"c}, 3)(0), Connection.Address) & "/" & Sender.Split("!"c)(0), Sender.Split("!"c)(0), args(0), Identification, Message)
        Else
            Result = Identify(If(Connection Is Nothing, Channel.Split({"/"c}, 3)(0), Connection.Address) & "/" & Sender.Split("!"c)(0), args(0), args(1), Identification, Message)
        End If

        ' Give channel status as appropriate.
        If Result Then
            Identification.Channels = ChannelList
            If Connection IsNot Nothing Then
                For Each ch In Connection.Channels
                    If ch.Value.Users.ContainsKey(Sender.Split("!"c)(0)) Then
                        If UserHasPermission(Connection, Channel, Sender, "irc.autohalfvoice." & Connection.Address.Replace("."c, "-"c) & "." & ch.Key.Replace("."c, "-"c)) Then _
                            Connection.Send("MODE " & ch.Key & " +V " & Sender.Split("!")(0))
                        If UserHasPermission(Connection, Channel, Sender, "irc.autovoice." & Connection.Address.Replace("."c, "-"c) & "." & ch.Key.Replace("."c, "-"c)) Then _
                            Connection.Send("MODE " & ch.Key & " +v " & Sender.Split("!")(0))
                        If UserHasPermission(Connection, Channel, Sender, "irc.autohalfop." & Connection.Address.Replace("."c, "-"c) & "." & ch.Key.Replace("."c, "-"c)) Then _
                            Connection.Send("MODE " & ch.Key & " +h " & Sender.Split("!")(0))
                        If UserHasPermission(Connection, Channel, Sender, "irc.autoop." & Connection.Address.Replace("."c, "-"c) & "." & ch.Key.Replace("."c, "-"c)) Then _
                            Connection.Send("MODE " & ch.Key & " +o " & Sender.Split("!")(0))
                        If UserHasPermission(Connection, Channel, Sender, "irc.autoadmin." & Connection.Address.Replace("."c, "-"c) & "." & ch.Key.Replace("."c, "-"c)) Then _
                            Connection.Send("MODE " & ch.Key & " +ao " & Sender.Split("!")(0) & " " & Sender.Split("!")(0))
                        If UserHasPermission(Connection, Channel, Sender, "irc.autoquiet." & Connection.Address.Replace("."c, "-"c) & "." & ch.Key.Replace("."c, "-"c)) Then _
                            Connection.Send("MODE " & ch.Key & " +q " & "*!*" & Sender.Split("!"c)(0))
                        If UserHasPermission(Connection, Channel, Sender, "irc.autoban" & Connection.Address.Replace("."c, "-"c) & "." & ch.Key.Replace("."c, "-"c)) Then _
                            Connection.Send("MODE " & ch.Key & " +b " & "*!*" & Sender.Split("!"c)(0))
                    End If
                Next
            End If
        End If
        ' Send them a report.
        If Connection IsNot Nothing Then
            Say(Connection, Sender.Split("!"c)(0), Message)
        Else
            Say(Nothing, Channel.Split({"/"c}, 3)(0) & "/" & Channel.Split({"/"c}, 3)(1) & "/" & Channel.Split({">"c})(0).Split({"/"c}, 3)(2) & ">" & Sender.Split("!"c)(0), Message)
        End If
    End Sub

    <Regex({"join ((the )?(IRC )?channel )?(?<Channel>#[^ ,]*)( on (the (IRC )?(server |network )(at ))?(?<Network>[^ ]*))?"},
        "me.ircsend", 3, True)>
    Public Sub RegexJoin(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As System.Text.RegularExpressions.Match)
        If Match.Groups("Network").Success Then
            CommandJoin(Connection, Sender, Channel, {Match.Groups("Network").Value, Match.Groups("Channel").Value})
        Else
            CommandJoin(Connection, Sender, Channel, {Match.Groups("Channel").Value})
        End If
    End Sub
    <Command("ircjoin", 1, 2,
        "join [connection] <channel>",
        "Instructs me to join a channel on an IRC network.",
        "me.ircsend")>
    Public Sub CommandJoin(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim TargetAddress As String, TargetChannel As String
        If args.Count = 1 Then
            TargetAddress = Connection.Address
            TargetChannel = args(0)
        Else
            TargetAddress = args(0)
            TargetChannel = args(1)
        End If

        For Each c In Connections
            If c.Address = TargetAddress Then
                If c.IsConnected Then
                    If c.Channels.ContainsKey(TargetChannel) Then
                        Say(Connection, Channel, "I'm already on that channel. ^_^")
                    Else
                        Say(Connection, Channel, "Attempting to join $k12" & TargetChannel & "$o on $k13" & TargetAddress & "$o...")
                        c.Send("JOIN " & TargetChannel)
                    End If
                Else
                    Say(Connection, Channel, "My connection to $k13" & TargetAddress & "$o is currently down.")
                End If
                Return
            End If
        Next
        Say(Connection, Channel, "I'm not connected to $k04" & TargetAddress & "$o at the moment. Please use $k11$cconnect$o.")
    End Sub

    <Command("names", 1, 2,
    "names [connection] <channel>",
    "Lists users on an IRC channel.",
    "me.debug")>
    Public Sub CommandNames(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim TargetAddress As String, TargetChannel As String
        If args.Count = 1 Then
            TargetAddress = Connection.Address
            TargetChannel = args(0)
        Else
            TargetAddress = args(0)
            TargetChannel = args(1)
        End If

        For Each c In Connections
            If c.Address = TargetAddress Then
                If c.IsConnected Then
                    If c.Channels.ContainsKey(TargetChannel) Then
                        Say(Connection, Channel, "Users on $b" & TargetChannel & "$o: $k15" & String.Join(" ", c.Channels(TargetChannel).Users.Keys))
                    Else
                        Say(Connection, Channel, "I'm not on that channel.")
                    End If
                Else
                    Say(Connection, Channel, "My connection to $k13" & TargetAddress & "$o is currently down.")
                End If
                Return
            End If
        Next
        Say(Connection, Channel, "I'm not connected to $k04" & TargetAddress & "$o at the moment. Please use $k11$cconnect$o.")

    End Sub

    <Command("autojoin", 0, 2,
        "autojoin [connection] <+|-><channel>  or  autojoin [connection] [channels]",
        "Instructs me to join a channel on an IRC network.",
        "me.ircsend")>
    Public Sub CommandAutoJoin(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim TargetAddress As String, TargetChannel As String
        If args.Count = 0 Then
            If AutoJoinChannels.Count = 0 Then
                Reply(Connection, Channel, Sender, "No autojoin lists have been set up$o.")
                Return
            Else
                Reply(Connection, Channel, Sender, "I will automatically join channels on these networks: $k12" & String.Join("$o, $k12", AutoJoinChannels.Keys) & "$o.")
            End If

            TargetAddress = Connection.Address.ToLower
            TargetChannel = Nothing
        ElseIf args.Count = 1 And (args(0).Contains("#") Or args(0).ToLower = "none" Or args(0).ToLower = "nothing") Then
            TargetAddress = Connection.Address.ToLower
            TargetChannel = args(0)
        ElseIf args.Count = 1 Then
            TargetAddress = args(0).ToLower
            TargetChannel = Nothing
        Else
            TargetAddress = args(0).ToLower
            TargetChannel = args(1)
        End If

        ' If no channel is specified, list the autojoin channels that are already set.
        If IsNothing(TargetChannel) Then
            If AutoJoinChannels.ContainsKey(TargetAddress) Then
                Reply(Connection, Channel, Sender, "I will automatically join these channels on $k09" & TargetAddress & "$o: $k12" & String.Join("$o, $k12", AutoJoinChannels(TargetAddress)) & "$o.")
            Else
                Reply(Connection, Channel, Sender, "I have not been set to automatically join any channels on $k10" & TargetAddress & "$o.")
            End If
        ElseIf TargetChannel.StartsWith("+") Then
            ' Add a channel (or channels) to the list.
            Dim ChannelsToAdd As String() = TargetChannel.Substring(1).Split({","c, " "c}), ChannelsAdded As String() = {}
            Dim newList As String()
            If AutoJoinChannels.ContainsKey(TargetAddress) Then
                newList = AutoJoinChannels(TargetAddress)
            Else
                newList = {}
            End If

            For Each Channel In ChannelsToAdd
                If newList.Contains(Channel, System.StringComparer.OrdinalIgnoreCase) Then
                    Reply(Connection, Channel, Sender, "$k06" & Channel & "$o is already on the autojoin list for $b" & TargetAddress & "$b.")
                Else
                    AppendArray(newList, Channel)
                    AppendArray(ChannelsAdded, Channel)
                End If
            Next

            If ChannelsAdded.Count > 0 Then
                AutoJoinChannels.Remove(TargetAddress)
                AutoJoinChannels.Add(TargetAddress, newList)

                Reply(Connection, Channel, Sender, "Added $k12" & String.Join("$o, $k12", ChannelsAdded) & "$o to the autojoin list for $b" & TargetAddress & "$b.")
                Reply(Connection, Channel, Sender, "I will automatically join these channels on $k09" & TargetAddress & "$o: $k12" & String.Join("$o, $k12", AutoJoinChannels(TargetAddress)) & "$o.")
            End If
        ElseIf TargetChannel.StartsWith("-") Then
            Dim ChannelsToRemove As String() = TargetChannel.Substring(1).Split({","c, " "c}), ChannelsRemoved As String() = {}
            Dim newList As String()

            If AutoJoinChannels.ContainsKey(TargetAddress) Then
                newList = AutoJoinChannels(TargetAddress)
            Else
                Reply(Connection, Channel, Sender, "I have not been set to automatically join any channels on $k10" & TargetAddress & "$o.")
                Return
            End If

            For Each lChannel In ChannelsToRemove
                Dim Found As Boolean = False
                For i = 0 To newList.Count - 1
                    If Not Found AndAlso newList(i).ToLower = lChannel.ToLower Then
                        Found = True
                    End If
                    If Found AndAlso i < newList.Count - 1 Then
                        newList(i) = newList(i + 1)
                    End If
                Next

                If Found Then
                    AppendArray(ChannelsRemoved, lChannel)
                    ReDim Preserve newList(newList.Count - 2)
                Else
                    Reply(Connection, Channel, Sender, "$k06" & lChannel & "$o is not on the autojoin list for $b" & TargetAddress & "$b.")
                End If
            Next

            If ChannelsRemoved.Count > 0 Then
                AutoJoinChannels.Remove(TargetAddress)
                AutoJoinChannels.Add(TargetAddress, newList)

                Reply(Connection, Channel, Sender, "Removed $k12" & String.Join("$o, $k12", ChannelsRemoved) & "$o to the autojoin list for $b" & TargetAddress & "$b.")
                Reply(Connection, Channel, Sender, "I will automatically join these channels on $k09" & TargetAddress & "$o: $k12" & String.Join("$o, $k12", AutoJoinChannels(TargetAddress)) & "$o.")
            End If
        ElseIf args(0).ToLower = "none" Or args(0).ToLower = Nothing Then
            If AutoJoinChannels.ContainsKey(TargetAddress) Then
                AutoJoinChannels.Remove(TargetAddress)
                Reply(Connection, Channel, Sender, "Removed the autojoin list for $k09" & TargetAddress & "$o.")
            Else
                Reply(Connection, Channel, Sender, "I have not been set to automatically join any channels on $k10" & TargetAddress & "$o.")
            End If
        Else
            Dim ChannelsToAdd As String() = TargetChannel.Substring(0).Split({","c, " "c}), ChannelsAdded As String() = {}

            For Each lChannel In ChannelsToAdd
                If ChannelsAdded.Contains(lChannel, System.StringComparer.OrdinalIgnoreCase) Then
                    Reply(Connection, Channel, Sender, "$k06" & lChannel & "$o has been repeated in your entry.")
                Else
                    AppendArray(ChannelsAdded, lChannel)
                End If
            Next

            AutoJoinChannels.Remove(TargetAddress)
            AutoJoinChannels.Add(TargetAddress, ChannelsAdded)
            Reply(Connection, Channel, Sender, "I will automatically join these channels on $k09" & TargetAddress & "$o: $k12" & String.Join("$o, $k12", AutoJoinChannels(TargetAddress)) & "$o.")
        End If
    End Sub

    <Regex({"(part|leave) ((the )?(IRC )?channel )?(?<Channel>#[^ ,]*)( on (the (IRC )?(server |network )(at )?)?(?<Network>[^ ]*))?"},
        "me.ircsend", 3, True)>
    Public Sub RegexPart(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As System.Text.RegularExpressions.Match)
        If Match.Groups("Network").Success Then
            CommandPart(Connection, Sender, Channel, {Match.Groups("Network").Value, Match.Groups("Channel").Value})
        Else
            CommandPart(Connection, Sender, Channel, {Match.Groups("Channel").Value})
        End If
    End Sub
    <Command("ircpart", 1, 3,
        "part [connection] <channel> [message]",
        "Instructs me to part a channel on an IRC network.",
        "me.ircsend")>
    Public Sub CommandPart(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim TargetAddress As String, TargetChannel As String, Message As String
        If args.Count = 1 Then
            TargetAddress = Connection.Address
            TargetChannel = args(0)
            Message = ""
        ElseIf args.Count = 2 And args(0).StartsWith("#") Then
            TargetAddress = Connection.Address
            TargetChannel = args(0)
            Message = args(1)
        ElseIf args.Count = 2 Then
            TargetAddress = args(0)
            TargetChannel = args(1)
            Message = ""
        ElseIf args(0).StartsWith("#") Then
            TargetAddress = Connection.Address
            TargetChannel = args(0)
            Message = args(1) & " " & args(2)
        Else
            TargetAddress = args(0)
            TargetChannel = args(1)
            Message = args(2)
        End If

        For Each c In Connections
            If c.Address = TargetAddress Then
                If c.IsConnected Then
                    Say(Connection, Channel, "Leaving $k12" & TargetChannel & "$o on $k13" & TargetAddress & "$o...")
                    c.Send("PART " & TargetChannel & If(Message = "", "", " :" & Message))
                Else
                    Say(Connection, Channel, "My connection to $k13" & TargetAddress & "$o is currently down.")
                End If
                Return
            End If
        Next
        Say(Connection, Channel, "I'm not connected to $k04" & TargetAddress & "$o at the moment. Please use $k11$cconnect$o.")
    End Sub

    <Command("ircsend", 2, 2,
        "ircsend <connection> <message>",
        "Instructs me to send a raw message to an IRC network (as with /quote on most clients).",
        "me.ircsend")>
    Public Sub CommandIRCSend(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim TargetAddress As String, TargetChannel As String
        TargetAddress = args(0)
        TargetChannel = args(1)

        For Each c In Connections
            If c.Address = TargetAddress Then
                If c.IsConnected Then
                    Reply(Connection, Channel, Sender, "Acknowledged.")
                    c.Send(args(1))
                Else
                    Say(Connection, Channel, "My connection to $k13" & TargetAddress & "$o is currently down.")
                End If
                Return
            End If
        Next
        Say(Connection, Channel, "I'm not connected to $k04" & TargetAddress & "$o at the moment. Please use $k11$cconnect$o.")
    End Sub

    <Command("loadplugin", 1, 2,
    "loadplugin <key> [filename]",
    "Instructs me to load a plugin. If you don't specify a filename, I'll try to load from the VBotPlugins folder.",
    "me.manageplugins")>
    Public Sub CommandPluginLoad(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim Key As String, Filename As String
        Key = args(0)
        Filename = If(args.ElementAtOrDefault(1), "VBotPlugins/" & Key & ".dll")

        If Plugins.ContainsKey(Key) Then
            Say(Connection, Channel, "I have already loaded a plugin with that key. Please use a different key.")
        ElseIf Not My.Computer.FileSystem.FileExists(Filename) Then
            Say(Connection, Channel, "The file $k04" & Filename & "$o does not exist.")
        Else
            Try
                LoadPlugin(Key, Filename)
                If Plugins.ContainsKey(Key) Then _
                Say(Connection, Channel, "Loaded $k09" & Plugins(Key).Obj.Name & "$o.")
            Catch ex As Exception
                Say(Connection, Channel, "I couldn't load a plugin from $k04" & Filename & "$o: $k04" & ex.Message)
            End Try
        End If
    End Sub

    <Command({"pluginchans", "pluginchannels"}, 1, 3,
"pluginchans <plugin> [major|minor] [channels]",
"Sets which channels a plugin is active in.",
"me.manageplugins")>
    Public Sub CommandPluginSetChannels(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim Key As String, Channels() As String
        Key = args(0)
        If args.Count = 1 Then
            Channels = Plugins(args(0)).Obj.Channels
            Say(Connection, Channel, IRCColours.Blue & Key & "$o is active in these major channels: $k09" & String.Join("$o, $k09", Channels))
            Channels = Plugins(args(0)).Obj.MinorChannels
            Say(Connection, Channel, IRCColours.Blue & Key & "$o is active in these minor channels: $k11" & String.Join("$o, $k11", Channels))
        ElseIf args.Count = 2 And args(1).ToLower = "major" Then
            Channels = Plugins(args(0)).Obj.Channels
            Say(Connection, Channel, IRCColours.Blue & Key & "$o is active in these major channels: $k09" & String.Join("$o, $k09", Channels))
        ElseIf args.Count = 2 And args(1).ToLower = "minor" Then
            Channels = Plugins(args(0)).Obj.MinorChannels
            Say(Connection, Channel, IRCColours.Blue & Key & "$o is active in these minor channels: $k11" & String.Join("$o, $k11", Channels))
        ElseIf args.Count = 2 Then
            Channels = args(1).Split({","c, " "c})
            For i = 0 To UBound(Channels)
                If Not Channels(i).Contains("/") Then
                    Channels(i) = If(Connection Is Nothing, Channel.Split("/"c)(0), Connection.Address) & "/" & Channels(i)
                End If
            Next

            If Plugins.ContainsKey(Key) Then
                Plugins(Key).Obj.Channels = Channels
                Plugins(Key).Obj.MinorChannels = {}
                Reply(Connection, Channel, Sender, IRCColours.Blue & Key & "$o is now only active in these channels: $k09" & String.Join("$o, $k09", Channels))
            Else
                Say(Connection, Channel, "I have not loaded a plugin with that key.")
            End If
        Else
            Channels = args(2).Split({","c, " "c})
            For i = 0 To UBound(Channels)
                If Not Channels(i).Contains("/") Then
                    Channels(i) = If(Connection Is Nothing, Channel.Split("/"c)(0), Connection.Address) & "/" & Channels(i)
                End If
            Next

            If Plugins.ContainsKey(Key) Then
                If args(1).ToLower = "minor" Then
                    Plugins(Key).Obj.MinorChannels = Channels
                    Reply(Connection, Channel, Sender, IRCColours.Blue & Key & "$o is now only active in these minor channels: $k11" & String.Join("$o, $k11", Channels))
                Else
                    Plugins(Key).Obj.Channels = Channels
                    Reply(Connection, Channel, Sender, IRCColours.Blue & Key & "$o is now only active in these major channels: $k09" & String.Join("$o, $k09", Channels))
                End If
            Else
                Say(Connection, Channel, "I have not loaded a plugin with that key.")
            End If
        End If

    End Sub

    <Command({"pluginlabel", "pluginminorlabel"}, 1, 2,
"pluginlabel <plugin> [label|*]",
"Sets the label that is used on messages to minor channels.",
"me.manageplugins")>
    Public Sub CommandPluginSetLabel(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim Key As String, Label As String
        Key = args(0)
        If args.Count = 1 Then
            If Plugins.ContainsKey(Key) Then
                Label = Plugins(Key).Obj.MinorLabel
                Reply(Connection, Channel, Sender, IRCColours.Blue & Key & "$o has this label: " & Label)
            Else
                Say(Connection, Channel, "I have not loaded a plugin with that key.")
            End If
        Else
            If Plugins.ContainsKey(Key) Then
                Label = If(args(1) = "*", "$k15[$o" & MyKey & "$k15] $o", args(1))
                Plugins(Key).Obj.MinorLabel = Label
                Reply(Connection, Channel, Sender, IRCColours.Blue & Key & "$o now has this label: " & Label)
            Else
                Say(Connection, Channel, "I have not loaded a plugin with that key.")
            End If
        End If

    End Sub

    <Command("unloadplugin", 1, 1,
"unloadplugin <key>",
"Instructs me to unload a plugin.",
"me.manageplugins")>
    Public Sub CommandPluginUnload(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim Key As String
        Key = args(0)

        If Not Plugins.ContainsKey(Key) Then
            Say(Connection, Channel, "I have not loaded a plugin with that key.")
        Else
            Plugins.Remove(Key)
            Say(Connection, Channel, "Unloaded $k06" & Key & "$o.")
        End If
    End Sub

    <Command("reloadplugin", 1, 1,
"reloadplugin <key> [filename]",
"Instructs me to reload a plugin.",
"me.manageplugins")>
    Public Sub CommandPluginReload(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim Key As String
        Key = args(0)

        If Not Plugins.ContainsKey(Key) Then
            Say(Connection, Channel, "I have not loaded a plugin with that key.")
        Else
            Dim FileName = If(args.ElementAtOrDefault(1), Plugins(Key).Filename)
            Dim Channels = Plugins(Key).Obj.Channels
            Dim MinorChannels = Plugins(Key).Obj.MinorChannels
            Dim Label = Plugins(Key).Obj.MinorLabel
            Plugins.Remove(Key)
            Try
                LoadPlugin(Key, FileName, Channels)
                Plugins(Key).Obj.MinorChannels = MinorChannels
                Plugins(Key).Obj.MinorLabel = MinorLabel
                Say(Connection, Channel, "Reloaded $k09" & Plugins(Key).Obj.Name & "$o.")
            Catch ex As Exception
                Say(Connection, Channel, "I couldn't load a plugin from $k04" & FileName & "$o: $k04" & ex.Message)
            End Try
        End If
    End Sub

    <Command("vbotconfigload", 0, 0,
"vbotconfigload",
"Instructs me to reload my configuration file. This will $k4close all of my IRC connections$o first.",
"me.manageplugins")>
    Public Sub CommandConfigReload(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        For Each Connection In Connections
            Connection.Send("QUIT :Reloading client.")
        Next
        Connections.Clear()
        Identifications.Clear()
        Try
            LoadConfig()
        Catch ex As Exception
            OutputLine("\cREDI couldn't load the configuration file: " & ex.Message)
        End Try
        For Each Connection In Connections
            Try
                Connection.Connect()
            Catch ex As Exception
                OutputLine("\cREDI could not initialise an IRC connection to " & Connection.Address & ": " & ex.Message & "\r")
            End Try
        Next
    End Sub

    <Command("vbotpluginsload", 0, 0,
"vbotpluginsload",
"Instructs me to reload my loaded plugin list. This will $k4unload all plugins$o first.",
"me.reload")>
    Public Sub CommandPluginsReload(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Plugins.Clear()
        Try
            LoadPlugins()
            Say(Connection, Channel, "$k9Reloaded plugins successfully.")
        Catch ex As Exception
            Say(Connection, Channel, "I couldn't reload the plugins: $k04" & ex.Message)
        End Try
    End Sub

    <Command("usersload", 0, 0,
"usersload",
"Instructs me to reload my user list. This will $k4invalidate all identifications$o.",
"me.reload")>
    Public Sub CommandUsersReload(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Accounts.Clear()
        Identifications.Clear()
        Try
            LoadUsers()
            Say(Connection, Channel, "$k9Reloaded users successfully.")
        Catch ex As Exception
            Say(Connection, Channel, "I couldn't reload the users: $k04" & ex.Message)
        End Try
    End Sub

    <Command("vbotconfigsave", 0, 0,
"vbotconfigsave",
"Instructs me to save my configuration file.",
"me.save")>
    Public Sub CommandConfigSave(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Try
            SaveConfig()
            Say(Connection, Channel, "$k9I saved my configuration successfully.")
        Catch ex As Exception
            Say(Connection, Channel, "I couldn't save my configuration file: $k04" & ex.Message)
        End Try
    End Sub

    <Command("pluginssave", 0, 0,
"pluginssave",
"Instructs me to save my loaded plugin list.",
"me.save")>
    Public Sub CommandPluginsSave(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Try
            SavePlugins()
            Say(Connection, Channel, "$k9I saved my plugin configuration successfully.")
        Catch ex As Exception
            Say(Connection, Channel, "I couldn't save my plugin configuration file: $k04" & ex.Message)
        End Try
    End Sub

    <Command("pluginsave", 1, 1,
"pluginsave <plugin>",
"Saves data for a specific plugin..",
"me.save")>
    Public Sub CommandPluginSave(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim Key As String
        Key = args(0)
        If Plugins.ContainsKey(Key) Then
            Try
                Plugins(Key).Obj.OnSave()
                Say(Connection, Channel, "$k9" & Key & "$o has been saved.")
            Catch ex As Exception
                Say(Connection, Channel, "An error occured while saving plugin data: $k04" & ex.Message)
            End Try
        Else
            Say(Connection, Channel, "I have not loaded a plugin with that key.")
        End If
    End Sub

    <Command("userssave", 0, 0,
"userssave",
"Instructs me to save my user list.",
"me.save")>
    Public Sub CommandUsersSave(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Try
            SaveUsers()
            Say(Connection, Channel, "$k9I saved my user configuration successfully.")
        Catch ex As Exception
            Say(Connection, Channel, "I couldn't save my user configuration file: $k04" & ex.Message)
        End Try
    End Sub

    <Regex({"(quit|leave|disconnect from) (the (IRC )?(server |network )(at ))?(?<Network>[^ ]*)"},
        "me.ircsend", 3, True)>
    Public Sub RegexQuit(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As System.Text.RegularExpressions.Match)
        CommandQuit(Connection, Sender, Channel, {Match.Groups("Network").Value})
    End Sub
    <Command("ircquit", 1, 2,
        "ircquit <connection> [message]",
        "Instructs me to quit an IRC network.",
        "me.ircsend")>
    Public Sub CommandQuit(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim TargetAddress As String, Message As String
        TargetAddress = args(0)
        Message = If(args.ElementAtOrDefault(1), "")

        For Each c In Connections
            If c.Address = TargetAddress Then
                If c.IsConnected Then
                    Say(Connection, Channel, "Quitting $k13" & TargetAddress & "$o...")
                    c.Send("QUIT" & If(Message = "", "", " :" & Message))
                Else
                    Say(Connection, Channel, "My connection to $k13" & TargetAddress & "$o is currently down.")
                End If
                Return
            End If
        Next
        Say(Connection, Channel, "I'm not connected to $k04" & TargetAddress & "$o at the moment. Please use $11$cconnect$o.")
    End Sub
    <Command({"quitall", "ircquitall"}, 0, 1,
        "quitall [message]",
        "Instructs me to quit all IRC networks that I am connected to.",
        "me.ircsend")>
    Public Sub CommandQuitAll(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim Message As String
        Message = If(args.ElementAtOrDefault(0), "")

        For Each c In Connections
            Say(Connection, Channel, "Quitting $k13$ball$b IRC networks$o...")
            If c.IsConnected Then
                c.Send("QUIT" & If(Message = "", "", " :" & Message))
            End If
        Next
    End Sub
    <Command({"disconnect", "dc"}, 1, 1,
        "disconnect <connection>",
        "Instructs me to disconnect from an IRC network.",
        "me.ircsend")>
    Public Sub CommandDisconnect(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim TargetAddress As String
        TargetAddress = args(0)

        For Each c In Connections
            If c.Address = TargetAddress Then
                Say(Connection, Channel, "Disconnecting from $k13" & TargetAddress & "$o.")
                If c.IsConnected Then
                    c.Send("QUIT")
                End If
                c.Disconnect()
                Connections.Remove(c)
                Return
            End If
        Next
        Say(Connection, Channel, "I'm not connected to $k04" & TargetAddress & "$o at the moment. Please use $11$cconnect$o.")
    End Sub

    <Regex({"(join|connect to) ((the|a|an) (IRC )?(server |network )(at ))?(?<Network>[^ ]*)"},
        "me.ircsend", 3, True)>
    Public Sub RegexConnect(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As System.Text.RegularExpressions.Match)
        CommandConnect(Connection, Sender, Channel, {Match.Groups("Network").Value})
    End Sub
    <Command("connect", 0, 1,
        "connect <server>",
        "Instructs me to connect to an IRC network. Without arguments, displays a list of the networks I am connected to.",
        "me.ircsend")>
    Public Sub CommandConnect(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        If args.Count = 0 Then
            Say(Connection, Channel, "I am connected to the following IRC networks:")
            For Each c In Connections
                If c.IsConnected Then
                    If c.IsRegistered Then
                        If c.Channels.Count > 1 Then
                            Say(Connection, Channel, IRCColours.Blue & c.Address & "$o - $k9Online$o, on channels $k9" & String.Join("$o, $k9", c.Channels.Keys))
                        ElseIf c.Channels.Count = 1 Then
                            Say(Connection, Channel, IRCColours.Blue & c.Address & "$o - $k9Online$o, on channel $k9" & String.Join("$o, $k9", c.Channels.Keys))
                        Else
                            Say(Connection, Channel, IRCColours.Blue & c.Address & "$o - $k9Online")
                        End If
                    Else
                        Say(Connection, Channel, IRCColours.Blue & c.Address & "$o - $k8Connecting")
                    End If
                Else
                    Say(Connection, Channel, IRCColours.Blue & c.Address & "$o - $k4Offline")
                End If
            Next
            Return
        End If

        Dim TargetAddress As String, Message As String
        TargetAddress = args(0)
        Message = If(args.ElementAtOrDefault(1), "")

        If Connection IsNot Nothing Then
            For Each c In Connections
                If c.Address = TargetAddress Then
                    If c.IsConnected Then
                        Say(Connection, Channel, "I'm already connected to $k13" & TargetAddress & "$o.")
                    Else
                        Say(Connection, Channel, "Connecting to $k13" & TargetAddress & "$o...")
                        c.Connect()
                    End If
                    Return
                End If
            Next
        End If
        Say(Connection, Channel, "Connecting to $k13" & TargetAddress & "$o...")
        Dim lNewConnection = NewConnection(TargetAddress, 6667, If(Connection, Connections(0)).Nicknames, If(Connection, Connections(0)).Username, If(Connection, Connections(0)).FullName)
        lNewConnection.Connect()
    End Sub

    <Command("nickserv", 1, 3,
    "nickserv [connection] <property> [value]  or  nickserv [connection] remove  or  nickserv [connection] add <nicknames> <password> [anynickname] [useghostcommand] [hostmask] [requestmask][ > <identifycommand>]",
    "Changes NickServ related settings." & vbCrLf &
    "You can set the following properties: $bRegisteredNicknames$b, $bAnyNickname$b, $bUseGhostCommand$b, $bPassword$b, $bIdentifyCommand$b, $bHostmask$b, $bRequestMask$b",
    "me.nickserv")>
    Public Sub CommandNickServ(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim TargetAddress As String, Field As String, Value As String

        For Each lConnection In Connections
            If lConnection.Address = args(0) Then
                TargetAddress = args(0)
                Field = args.ElementAtOrDefault(1)
                Value = args.ElementAtOrDefault(args(2))
                GoTo ConnectionFound
            End If
        Next

        TargetAddress = Connection.Address
        Field = args(0)
        Value = args.ElementAtOrDefault(1)

ConnectionFound:
        Dim Data As NickServData
        If NickServ.ContainsKey(TargetAddress.ToLower) Then
            Data = NickServ(TargetAddress.ToLower)
        ElseIf Field <> "add" Then
            Reply(Connection, Channel, Sender, "NickServ settings are not defined for $k6" & TargetAddress & "$o.")
            Return
        End If

        Select Case Field
            Case "remove"
                If NickServ.ContainsKey(TargetAddress.ToLower) Then
                    NickServ.Remove(TargetAddress.ToLower)
                    Reply(Connection, Channel, Sender, "Removed NickServ settings for $k13" & TargetAddress & "$o.")
                Else
                    Reply(Connection, Channel, Sender, "NickServ settings are not defined for $k6" & TargetAddress & "$o.")
                End If
            Case "add"
                Data = New NickServData
                Dim sData = Value.Split({" "c}, 6)
                If sData.Count < 2 Then
                    Reply(Connection, Channel, Sender, "$k4You must set a registered nickname and the password.")
                Else
                    Data.RegisteredNicknames = sData(0).Split(","c)
                    Data.Password = sData(1)
                    If sData.Count >= 3 Then
                        Select Case sData(2).ToLower
                            Case "yes", "true", "on", "y", "t"
                                Data.AnyNickname = True
                            Case "no", "false", "off", "n", "f"
                                Data.AnyNickname = False
                            Case Else
                                Reply(Connection, Channel, Sender, VBot.Choose("", "Sorry " & Sender.Split("!"c)(0) & ", ") & "I don't recognise " & VBot.Choose(IRCColours.Red & sData(2) & "$o", "that") & " as a Boolean value. Please enter $k11yes$o or $k11no$o.")
                        End Select
                    End If
                    If sData.Count >= 4 Then
                        Select Case sData(2).ToLower
                            Case "yes", "true", "on", "y", "t"
                                Data.UseGhostCommand = True
                            Case "no", "false", "off", "n", "f"
                                Data.UseGhostCommand = False
                            Case Else
                                Reply(Connection, Channel, Sender, VBot.Choose("", "Sorry " & Sender.Split("!"c)(0) & ", ") & "I don't recognise " & VBot.Choose(IRCColours.Red & sData(2) & "$o", "that") & " as a Boolean value. Please enter $k11yes$o or $k11no$o.")
                        End Select
                    End If
                    If sData.Count >= 5 Then Data.Hostmask = sData(4)
                    If sData.Count >= 6 Then
                        If sData(5).Contains(" > ") Then
                            Data.RequestMask = sData(5).Split({" > "}, 2, StringSplitOptions.None)(0)
                            Data.IdentifyCommand = sData(5).Split({" > "}, 2, StringSplitOptions.None)(1)
                        Else
                            Data.RequestMask = sData(5)
                        End If
                    End If
                End If

                NickServ.Add(TargetAddress.ToLower, Data)
                Reply(Connection, Channel, Sender, "Added NickServ settings for $k13" & TargetAddress & "$o.")
            Case "nicknames", "nicks", "names"
                If Value.StartsWith("+") Then
                    ' Add a nickname (or nicknames) to the list.
                    Dim NicknamesToAdd As String() = Value.Substring(1).Split({","c, " "c}), NicknamesAdded As String() = {}

                    For Each lNickname In NicknamesToAdd
                        If Data.RegisteredNicknames.Contains(Channel, System.StringComparer.OrdinalIgnoreCase) Then
                            Reply(Connection, Channel, Sender, "$k06" & lNickname & "$o is already recognised on $b" & TargetAddress & "$b.")
                        Else
                            AppendArray(Data.RegisteredNicknames, lNickname)
                            AppendArray(NicknamesAdded, lNickname)
                        End If
                    Next

                    If NicknamesAdded.Count > 0 Then
                        Reply(Connection, Channel, Sender, "Added $k12" & String.Join("$o, $k12", NicknamesAdded) & "$o to the list for $b" & TargetAddress & "$b.")
                        Reply(Connection, Channel, Sender, "I will automatically identify to these nicknames on $k09" & TargetAddress & "$o: $k12" & String.Join("$o, $k12", Data.RegisteredNicknames) & "$o.")
                    End If
                ElseIf Value.StartsWith("-") Then
                    Dim NicknamesToRemove As String() = Value.Substring(1).Split({","c, " "c}), NicknamesRemoved As String() = {}

                    For Each lNickname In NicknamesToRemove
                        Dim Found As Boolean = False
                        For i = 0 To Data.RegisteredNicknames.Count - 1
                            If Not Found AndAlso Data.RegisteredNicknames(i).ToLower = lNickname.ToLower Then
                                Found = True
                            End If
                            If Found AndAlso i < Data.RegisteredNicknames.Count - 1 Then
                                Data.RegisteredNicknames(i) = Data.RegisteredNicknames(i + 1)
                            End If
                        Next

                        If Found Then
                            AppendArray(NicknamesRemoved, lNickname)
                            ReDim Preserve Data.RegisteredNicknames(Data.RegisteredNicknames.Count - 2)
                        Else
                            Reply(Connection, Channel, Sender, "$k06" & lNickname & "$o is not recognised on $b" & TargetAddress & "$b.")
                        End If
                    Next

                    If NicknamesRemoved.Count > 0 Then
                        Reply(Connection, Channel, Sender, "Removed $k12" & String.Join("$o, $k12", NicknamesRemoved) & "$o from the list for $b" & TargetAddress & "$b.")
                        Reply(Connection, Channel, Sender, "I will automatically identify to these nicknames on $k09" & TargetAddress & "$o: $k12" & String.Join("$o, $k12", Data.RegisteredNicknames) & "$o.")
                    End If
                Else
                    Dim NicknamesToAdd As String() = Value.Split({","c, " "c}), NicknamesAdded As String() = {}

                    For Each lNickname In NicknamesToAdd
                        If NicknamesAdded.Contains(Nickname, System.StringComparer.OrdinalIgnoreCase) Then
                            Reply(Connection, Channel, Sender, "$k06" & lNickname & "$o has been repeated in your entry.")
                        Else
                            AppendArray(NicknamesAdded, lNickname)
                        End If
                    Next

                    Reply(Connection, Channel, Sender, "I will automatically identify to these nicknames on $k09" & TargetAddress & "$o: $k12" & String.Join("$o, $k12", Data.RegisteredNicknames) & "$o.")
                End If
            Case "password", "pass"
                Data.Password = Value
            Case "anynickname", "anynick", "anyname"
                Select Case Value.ToLower
                    Case "yes", "true", "on", "y", "t"
                        Data.AnyNickname = True
                    Case "no", "false", "off", "n", "f"
                        Data.AnyNickname = False
                    Case Else
                        Reply(Connection, Channel, Sender, VBot.Choose("", "Sorry " & Sender.Split("!"c)(0) & ", ") & "I don't recognise " & VBot.Choose(IRCColours.Red & Value & "$o", "that") & " as a Boolean value. Please enter $k11yes$o or $k11no$o.")
                End Select
            Case "useghostcommand", "ghost", "useghost", "ghostcommand", "useghostcmd", "ghostcmd", "killghost"
                Select Case Value.ToLower
                    Case "yes", "true", "on", "y", "t"
                        Data.UseGhostCommand = True
                    Case "no", "false", "off", "n", "f"
                        Data.UseGhostCommand = False
                    Case Else
                        Reply(Connection, Channel, Sender, VBot.Choose("", "Sorry " & Sender.Split("!"c)(0) & ", ") & "I don't recognise " & VBot.Choose(IRCColours.Red & Value & "$o", "that") & " as a Boolean value. Please enter $k11yes$o or $k11no$o.")
                End Select
            Case "hostmask", "host"
                Data.Hostmask = Value
            Case "requestmask", "request"
                Data.RequestMask = Value
            Case "identifycommand", "identifycmd", "idcommand", "idcmd", "identify", "id"
                If Value = "default" Then
                    Data.IdentifyCommand = "PRIVMSG $target :IDENTIFY $password"
                Else
                    Data.IdentifyCommand = Value
                End If
            Case "ghostcommand", "ghostcmd", "ghost"
                If Value = "default" Then
                    Data.GhostCommand = "PRIVMSG $target :GHOST $nickname $password"
                Else
                    Data.GhostCommand = Value
                End If
        End Select
    End Sub

    <Command({"prefixadd", "commandprefixadd", "cprefixadd", "cpadd"}, 2, 2,
"prefixadd <server>/<channel> <prefix to add>",
"Adds a command prefix to a certain channel's list.",
"me.prefix")>
    Public Sub CommandPrefixExcept(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim Key As String
        If args(0).StartsWith("!") Then
            Key = args(0)
        ElseIf args(0).Contains("/") Then
            Key = args(0)
        Else
            Key = If(Connection Is Nothing, Channel.Split("/"c)(0), Connection.Address) & "/" & args(0)
        End If
        If ChannelCommandPrefixes.ContainsKey(Key.ToLower) Then
            If ChannelCommandPrefixes(Key.ToLower).Contains(args(1)) Then
                Reply(Connection, Channel, Sender, Choose("$k13" & args(1) & "$o", "That prefix") & " is already allowed in " & Choose("$k04" & Key & "$o.", "that channel."))
            Else
                AppendArray(ChannelCommandPrefixes(Key.ToLower), args(1))
                Reply(Connection, Channel, Sender, "$k13" & args(1) & "$o" & " will now be used as a command prefix in $k12" & Key & "$o.")
            End If
        Else
            ChannelCommandPrefixes.Add(Key.ToLower, {args(1)})
            Reply(Connection, Channel, Sender, "$k13" & args(1) & "$o" & " will now be used as a command prefix in $k12" & Key & "$o.")
        End If
    End Sub

    <Command({"prefixremove", "commandprefixremove", "cpremove"}, 2, 2,
"prefixremove [server]/<channel> <prefix to remove>",
"Removes a command prefix from a channel;s list.",
"me.prefix")>
    Public Sub CommandPrefixUnExcept(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim Key As String
        If args(0).StartsWith("!") Then
            Key = args(0)
        ElseIf args(0).Contains("/") Then
            Key = args(0)
        Else
            Key = If(Connection Is Nothing, Channel.Split("/"c)(0), Connection.Address) & "/" & args(0)
        End If
        If ChannelCommandPrefixes.ContainsKey(Key.ToLower) Then
            If ChannelCommandPrefixes(Key.ToLower).Contains(args(1)) Then
                If ChannelCommandPrefixes(Key.ToLower).Count = 1 Then
                    ChannelCommandPrefixes.Remove(Key.ToLower)
                Else
                    For i = Array.IndexOf(ChannelCommandPrefixes(Key.ToLower), args(1)) To UBound(ChannelCommandPrefixes(Key.ToLower)) - 1
                        ChannelCommandPrefixes(Key.ToLower)(i) = ChannelCommandPrefixes(Key.ToLower)(i + 1)
                    Next
                    ReDim Preserve ChannelCommandPrefixes(Key.ToLower)(UBound(ChannelCommandPrefixes(Key.ToLower)) - 1)
                End If
                Reply(Connection, Channel, Sender, "$k13" & args(1) & "$o" & " will no longer be used as a command prefix in $k12" & Key & "$o.")
            Else
                Reply(Connection, Channel, Sender, Choose("$k13" & args(1) & "$o", "That prefix") & " is not used in " & Choose("$k04" & Key & "$o.", "that channel."))
            End If
        Else
            Reply(Connection, Channel, Sender, "The default prefixes are being used in " & Choose("$k04" & Key & "$o.", "that channel."))
        End If
    End Sub

    <Command({"prefixlist", "commandprefixes", "commandprefixlist", "prefixes", "cplist"}, 1, 1,
"cplist [server]/<channel>",
"Shows a list of command prefixes that will be used in a given channel.",
"me.prefix")>
    Public Sub CommandPrefixExceptions(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim Key As String
        If args(0).StartsWith("!") Then
            Key = args(0)
        ElseIf args(0).Contains("/") Then
            Key = args(0)
        Else
            Key = If(Connection Is Nothing, Channel.Split("/"c)(0), Connection.Address) & "/" & args(0)
        End If
        If ChannelCommandPrefixes.ContainsKey(Key.ToLower) Then
            Say(Connection, Channel, "The following command prefixes are used in $k12" & Key & "$o: $k13" & String.Join("$o, $k13", ChannelCommandPrefixes(Key.ToLower)))
        Else
            Reply(Connection, Channel, Sender, "The default prefixes are used in " & Choose("$k04" & Key & "$o.", "that channel."))
        End If
    End Sub

    <Command({"echo"}, 1, 1,
"echo <text>",
"Instructs me to repeat your message back to you.", "me.echo")>
    Public Sub CommandEcho(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Say(Connection, Channel, args(0))
    End Sub

    <Output("Console", 8)>
    Public Sub SayToConsole(ByVal Message As String, ByVal Arguments As String)
        Message = Message.Replace("\", "\\")

        ' Remove the IRC colour codes.
        Dim regex = New System.Text.RegularExpressions.Regex("\x03(?<Fore>\d{0,2})(,(?<Back>\d{1,2}))?|\x0F|\x16")
        Dim pos As Integer = 0, PreviousForeColour As String = "", PreviousBackColour As String = ""
        Do
            Dim Match = regex.Match(Message, pos)
            If Not Match.Success Then Exit Do
            pos = Match.Index

            Dim Replacement As String = ""
            Select Case Match.Value(0)
                Case Chr(3)
                    If Match.Groups("Fore").Value <> "99" And Match.Groups("Fore").Value <> "16" Then
                        PreviousForeColour = {"WHITE", "BLACK", "DKBLUE", "DKGREEN", "RED", "DKRED", "DKMAGENTA", "DKYELLOW", "YELLOW", "GREEN", "DKCYAN", "CYAN", "BLUE", "MAGENTA", "DKGRAY", "GRAY"}(Val(Match.Groups("Fore").Value) Mod 16)
                        Replacement &= "\c" & PreviousForeColour
                    End If
                    If Match.Groups("Back").Success Then
                        If Match.Groups("Back").Value = "99" Or Match.Groups("Back").Value = "16" Then
                            PreviousBackColour = ""
                            Replacement &= "\bo"
                        Else
                            PreviousBackColour = {"WHITE", "BLACK", "DKBLUE", "DKGREEN", "RED", "DKRED", "DKMAGENTA", "DKYELLOW", "YELLOW", "GREEN", "DKCYAN", "CYAN", "BLUE", "MAGENTA", "DKGRAY", "GRAY"}(Val(Match.Groups("Back").Value) Mod 16)
                            Replacement &= "\b" & PreviousBackColour
                        End If
                    End If
                Case Chr(15)
                    PreviousForeColour = ""
                    PreviousBackColour = ""
                    Replacement &= "\cWHITE\bo"
                Case Chr(22)
                    Dim Temp As String
                    Temp = PreviousForeColour

                    If PreviousBackColour = "" Then
                        PreviousForeColour = "black"
                        Replacement &= "\cBLACK"
                    ElseIf PreviousBackColour = "white" Then
                        PreviousForeColour = ""
                        Replacement &= "\cWHITE"
                    Else
                        PreviousForeColour = PreviousBackColour
                        Replacement &= "\c" & PreviousForeColour
                    End If

                    If Temp = "" Then
                        PreviousBackColour = "white"
                        Replacement &= "\bWHITE"
                    ElseIf Temp = "black" Then
                        PreviousBackColour = ""
                        Replacement &= "\bo"
                    Else
                        PreviousBackColour = Temp
                        Replacement &= "\b" & PreviousBackColour
                    End If
            End Select

            Message = Message.Remove(pos, Match.Length).Insert(pos, Replacement)
            pos += Replacement.Length
        Loop

        OutputLine("\cWHITE" & Message & "\r")
    End Sub

    <Command({"echonext"}, 0, 1,
"echonext [timeout]",
"Instructs me to repeat your next message back to you.", "me.echo")>
    Public Sub CommandEchoNext(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim t As New Threading.Thread(New Threading.ParameterizedThreadStart(AddressOf EchoNext))
        t.Start({Connection, Channel, Sender.Split("!"c)(0), If(args.ElementAtOrDefault(0), 0)})
    End Sub

    Public Sub EchoNext(ByVal data As Object)
        Say(data(0), data(1), WaitForMessage(data(0), data(1), data(2), data(3)))
    End Sub

    Public Class WaitData
        Public Response As String
    End Class

    Dim Waiting As New Dictionary(Of String, WaitData)(StringComparer.OrdinalIgnoreCase)
    Public Function WaitForMessage(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal Nickname As String, Optional ByVal Timeout As UInteger = 0)
        Dim data = New WaitData With {.Response = Nothing}
        Waiting.Add(If(Connection Is Nothing, "", Connection.Address & "/") & Channel & "/" & Nickname, data)

        Dim stwWait As Stopwatch = Nothing
        If Timeout > 0 Then stwWait = Stopwatch.StartNew

        Do Until (Timeout > 0 AndAlso stwWait.ElapsedMilliseconds >= Timeout) Or data.Response IsNot Nothing
            Threading.Thread.Sleep(500)
        Loop

        Waiting.Remove(If(Connection Is Nothing, "", Connection.Address & "/") & Channel & "/" & Nickname)
        Return data.Response
    End Function

    <Command({"die"}, 0, 1,
    "die [message]",
    "Shuts me down.",
    "me.die")>
    Public Sub CommandDie(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim Message As String
        Message = If(args.ElementAtOrDefault(0), "Shutting down.")
        Say(Connection, Channel, "Goodbye, " & Sender.Split("!"c)(0))

        For Each c In Connections
            If c.IsConnected Then
                c.Send("QUIT" & If(Message = "", "", " :" & Message))
            End If
        Next

        Threading.Thread.Sleep(5000)
        VBot.Die()
    End Sub

    <Command({"crash"}, 0, 0,
    "crash",
    "Raises an exception. For testing.",
    "me.debug")>
    Public Sub CommandCrash(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Throw New Exception
    End Sub
End Class
