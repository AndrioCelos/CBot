' General to-do list:
'   TODO: Rename files. This is no longer specifically designed for that TShock server.
'   TODO: Add code to work with the forthcoming parsability server mod.

Imports System.Timers
Imports VBot

Public Class TerrariaServerPlugin
    Inherits Plugin
    'Inherits ServerHandler

    Dim WithEvents MaintenanceTimer As Timer

    Dim WorkingDirectory As String
    Dim Arguments As String
    Dim ExecutablePath As String = "TerrariaServer.exe"

    Dim ServerConfigFile As String = "serverconfig.txt"
    Dim BanListFile As String = "banlist.txt"
    Dim Autostart As Boolean = False

    Dim AutosaveInterval As Integer = 0
    Dim AutoRestartInterval As Integer = 24
    Dim AutoRestartInProgress As Boolean = False

    Dim lIsTShock As Boolean = False
    Public ReadOnly Property IsTShock As Boolean
        Get
            Return lIsTShock
        End Get
    End Property

    Private Connecting As New Queue(Of String)
    Dim OnlinePlayers As Dictionary(Of String, PlayerData)

    Private Structure PlayerData
        Dim IP As String
        Dim LoginTime As Date
    End Structure

    Public WithEvents Server As Process

    Public Overrides ReadOnly Property Name As String
        Get
            Return "Terraria Server Bridge"
        End Get
    End Property
    Public Overrides ReadOnly Property UseGlobalKeyCommand As Boolean
        Get
            Return True
        End Get
    End Property

    Sub New(ByVal Key As String)
        'MaintenanceTimer = New Timer(60000) With {.Enabled = True}
        LoadSettings("Config\" & Key & ".ini")

        If Autostart Then Start()
    End Sub

    Public Overrides Sub OnSave()
        SaveSettings()
    End Sub

#Region "Filing"

    Public Function GetSetting(ByVal Setting As String) As String
        Dim Filename As String
        If System.IO.Path.IsPathRooted(ServerConfigFile) Or ServerConfigFile.StartsWith("%") Then
            Filename = ServerConfigFile
        Else
            Filename = System.IO.Path.Combine(WorkingDirectory, ServerConfigFile)
        End If

        Dim Reader = My.Computer.FileSystem.OpenTextFileReader(Filename)
        Do Until Reader.EndOfStream
            Dim Line = Reader.ReadLine
            If Line.StartsWith("#") Then Continue Do
            If Not Line.Contains("=") Then Continue Do

            If Line.Split({"="c}, 2)(0).ToLower = Setting.ToLower Then
                Reader.Close()
                Return Line.Split({"="c}, 2)(1)
            End If
        Loop
        Reader.Close()
        Throw New KeyNotFoundException
    End Function

    Public Sub PutSetting(ByVal Setting As String, ByVal Value As String)
        Dim Filename As String
        If System.IO.Path.IsPathRooted(ServerConfigFile) Or ServerConfigFile.StartsWith("%") Then
            Filename = ServerConfigFile
        Else
            Filename = System.IO.Path.Combine(WorkingDirectory, ServerConfigFile)
        End If

        Dim newFileText As String = "", Put As Boolean

        If My.Computer.FileSystem.FileExists(Filename) Then
            Dim Reader = My.Computer.FileSystem.OpenTextFileReader(Filename)
            Do Until Reader.EndOfStream
                Dim Line = Reader.ReadLine
                If Not Line.StartsWith("#") AndAlso Line.Contains("=") AndAlso Line.Split({"="c}, 2)(0).ToLower = Setting.ToLower Then
                    If Value <> Nothing And Value <> "" Then newFileText &= If(newFileText = "", "", vbCrLf) & Setting & "=" & Value
                    Put = True
                    Continue Do
                End If
                newFileText &= If(newFileText = "", "", vbCrLf) & Line
            Loop
            Reader.Close()
        End If
        If Not Put Then newFileText &= If(newFileText = "", "", vbCrLf) & Setting & "=" & Value

        My.Computer.FileSystem.WriteAllText(Filename, newFileText, False)
    End Sub

    Public Sub LoadSettings()
        LoadSettings("Config\" & MyKey & ".ini")
    End Sub
    Public Sub LoadSettings(ByVal Filename As String)
        If My.Computer.FileSystem.FileExists(Filename) Then

            Dim Reader = My.Computer.FileSystem.OpenTextFileReader(Filename)

            Dim Section As String = "", Field As String = "", Value As String = ""

            Do Until Reader.EndOfStream
                Dim s = Reader.ReadLine
                ' Check for comments.
                If s.TrimStart.StartsWith(";") Then Continue Do

                Dim Match As System.Text.RegularExpressions.Match
                Match = System.Text.RegularExpressions.Regex.Match(s, "^\s*\[(?<Section>.*?)\]?\s*$")
                If Match.Success Then
                    Section = Match.Groups("Section").Value
                    Continue Do
                End If
                If Section = "" Then Continue Do

                Match = System.Text.RegularExpressions.Regex.Match(s, "^\s*(?<Field>(?>[^=]*))=(?<Value>.*)$")
                If Match.Success Then
                    Field = Match.Groups("Field").Value
                    Value = Match.Groups("Value").Value

                    If Section.ToLower = "server" Then
                        Select Case Field.ToLower
                            Case "workingdir", "workingdirectory"
                                WorkingDirectory = Value
                            Case "executablefile", "exefile", "executablelocation", "executableloc", "exelocation", "exeloc", "executable", "exe", "executablepath", "exepath"
                                ExecutablePath = Value
                            Case "configfile", "configurationfile", "serverconfigfile", "serverconfigurationfile"
                                ServerConfigFile = Value
                            Case "autostart"
                                Autostart = Value
                        End Select
                    End If
                End If
            Loop
            Reader.Close()
        End If
    End Sub

    Public Sub SaveSettings()
        SaveSettings("Config\" & MyKey & ".ini")
    End Sub
    Public Sub SaveSettings(ByVal Filename As String)
        Dim Writer = My.Computer.FileSystem.OpenTextFileWriter(Filename, False)

        Writer.WriteLine("[Server]")
        Writer.WriteLine("WorkingDirectory=" & WorkingDirectory)
        Writer.WriteLine("Executable=" & ExecutablePath)
        Writer.WriteLine("ConfigFile=" & ServerConfigFile)
        Writer.WriteLine("AutoStart=" & Autostart)

        Writer.Close()
    End Sub
#End Region

    Public Function IsRunning() As Boolean
        Try
            Return If(Server Is Nothing, False, Not Server.HasExited)
        Catch ex As InvalidOperationException
            Return False
        End Try
    End Function

    Public Function WorkingDirectoryCheck() As Boolean
        If WorkingDirectory = Nothing And Not (System.IO.Path.IsPathRooted(ServerConfigFile) Or ServerConfigFile.StartsWith("%")) Then
            SayToAllChannels("$k6[@]$o Couldn't start the server: the working directory is not set.")
            Return False
        End If

        Dim Filename As String
        If System.IO.Path.IsPathRooted(ExecutablePath) Or ExecutablePath.StartsWith("%") Then
            Filename = ServerConfigFile
        Else
            Filename = System.IO.Path.Combine(WorkingDirectory, ExecutablePath)
        End If
        If Not My.Computer.FileSystem.FileExists(Filename) Then
            SayToAllChannels("$k6[@]$o Couldn't start the server: the executable is missing.")
            Return False
        End If
        Return True
    End Function

    <Regex("(Start|Boot) (up )?the (T(erraria)?Shock |Terraria )?server\.?",
         ".startstop", RegexAttribute.CommandScope.Channel, True)>
    Public Sub RegexStart(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As System.Text.RegularExpressions.Match)
        CommandStart(Connection, Sender, Channel, {})
    End Sub
    <Command("start", 0, 0,
        "start",
        "Starts the server.",
        ".startstop", CommandAttribute.CommandScope.Channel)>
    Public Sub CommandStart(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        If Not WorkingDirectoryCheck() Then Return

        If IsRunning() Then
            Say(Connection, Channel, Choose("It", "The server") & Choose(Choose(" is", "'s") & Choose(" already up", " already started", " already running"), Choose(" has", "'s") & " already " & Choose("been ", "") & "started") & Choose(".", ", " & Sender.Split("!"c)(0) & "."))
        ElseIf Not My.Computer.FileSystem.FileExists(My.Computer.FileSystem.CombinePath(WorkingDirectory, ExecutablePath)) Then
            SayToAllChannels("$k6[@]$o The executable location is currently set to $k04" & ExecutablePath & "$o, but it doesn't exist there. Please set the correct location using $k11$cset exe $k10<file path>$o.")
        Else
            Start()
        End If
    End Sub

    <Regex("(Gracefully )?(Stop|Shut down|Close|End) the (T(erraria)?Shock |Terraria )?server\.?",
         ".startstop", RegexAttribute.CommandScope.Channel, True)>
    Public Sub RegexStop(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As System.Text.RegularExpressions.Match)
        CommandStop(Connection, Sender, Channel, {})
    End Sub
    <Command({"stop", "shutdown"}, 0, 0,
        "stop",
        "Gracefully stops the server.",
        ".startstop", CommandAttribute.CommandScope.Channel)>
    Public Sub CommandStop(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        If Not IsRunning() Then
            If AutoRestartInProgress Then
                AutoRestartInProgress = False
                Say(Connection, Channel, Choose("Cancelled ", "Stopped ", "Aborted ") & "the automatic restart.")
            Else
                Say(Connection, Channel, Choose("It", "The server") & Choose(Choose(" is", "'s") & Choose(" already down", " already stopped", " not running", " not up", " not started"), Choose(" has", "'s") & " already " & Choose(" stopped", " shut down", " been stopped", " been taken down", " been shut down"), Choose(" isn't running", " isn't up")) & Choose(".", ", " & Sender.Split("!"c)(0) & "."))
            End If
        Else
            ShutDown()
        End If
    End Sub

    Public Sub Start()
        If Not WorkingDirectoryCheck() Then Return
        ' Make sure the executable file actually exists.
        If Not My.Computer.FileSystem.FileExists(My.Computer.FileSystem.CombinePath(WorkingDirectory, ExecutablePath)) Then
            SayToAllChannels("$k6[@]$o The executable location is currently set to $k04" & ExecutablePath & "$o, but it doesn't exist. The server could not be started.")
            Return
        End If
        Dim ConfigFilename As String, Arguments As String
        If System.IO.Path.IsPathRooted(ServerConfigFile) Or ServerConfigFile.StartsWith("%") Then
            ConfigFilename = ServerConfigFile
        Else
            ConfigFilename = System.IO.Path.Combine(WorkingDirectory, ServerConfigFile)
        End If
        If My.Computer.FileSystem.FileExists(ConfigFilename) Then
            Arguments = "-config """ & ServerConfigFile & """"
        Else
            SayToAllChannels("$k6[@]$o The server configuration file is missing. Starting without a configuration file.")
            Arguments = ""
        End If

        OnlinePlayers = New Dictionary(Of String, PlayerData)(StringComparer.OrdinalIgnoreCase)
        Connecting = New Queue(Of String)
        lIsTShock = False

        Server = New Process With {.StartInfo = New ProcessStartInfo With {
       .FileName = My.Computer.FileSystem.CombinePath(WorkingDirectory, ExecutablePath),
       .Arguments = Arguments,
       .WorkingDirectory = WorkingDirectory,
       .UseShellExecute = False, .CreateNoWindow = True, .RedirectStandardInput = True, .RedirectStandardOutput = True, .RedirectStandardError = True}}
        Server.EnableRaisingEvents = True
        Server.Start()
        Server.BeginOutputReadLine()
        Server.BeginErrorReadLine()

        SayToAllChannels("$k6[@]$o " & VBot.Choose("I have", "I've") & " started the server.", 0, {"!" & MyKey & "/TerrariaServerMessage"})
        SayToMinorChannels(MinorLabel & "$k6[@]$o " & VBot.Choose("I have", "I've") & " started the server.")
    End Sub

    Public Sub ShutDown()
        SayToAllChannels("$k6[@]$o The server is being shut down.")
        SayToMinorChannels(MinorLabel & "$k6[@]$o " & "The server is being shut down.")
        Server.StandardInput.WriteLine("exit")
        Server.StandardInput.WriteLine("off")
    End Sub

    <Regex("(Gracefully )?(Restart|Reboot|Refresh) the (T(erraria)?Shock |Terraria )?server\.?",
        ".startstop", RegexAttribute.CommandScope.Channel, True)>
    Public Sub RegexRestart(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As System.Text.RegularExpressions.Match)
        CommandRestart(Connection, Sender, Channel, {})
    End Sub
    <Command({"restart", "reboot"}, 0, 0,
        "restart",
        "Stops and restarts the server.",
        ".startstop", CommandAttribute.CommandScope.Channel)>
    Public Sub CommandRestart(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        If IsRunning() Then
            ShutDown()
            Server.WaitForExit(60000)
            If Not Server.HasExited Then Server.Kill()
        End If
        If Not My.Computer.FileSystem.FileExists(My.Computer.FileSystem.CombinePath(WorkingDirectory, ExecutablePath)) Then
            SayToAllChannels("$k6[@]$o The executable location is currently set to $k04" & ExecutablePath & "$o, but it doesn't exist there. Please set the correct location using $k11$cset exe $k10<file path>$o.")
        Else
            Start()
        End If
    End Sub

    Protected Sub ServerExited() Handles Server.Exited
        SayToAllChannels("$k6[@]$o The server shut down.", 0, {"!" & MyKey & "/TerrariaServerMessage"})
        SayToMinorChannels(MinorLabel & "$k6[@]$o " & "The server shut down.")
        'CurrentCBVersion = ""
        'LatestCBVersion = Nothing
        'NeedsUpdate = False
    End Sub

    Protected Sub OutputDataReceived(ByVal sender As Object, ByVal e As DataReceivedEventArgs) Handles Server.OutputDataReceived
        If e.Data = Nothing Then Exit Sub
        If e.Data = ">" Or e.Data = "" Or e.Data.Contains("[WARNING] Can't keep up! Did the system time change, or is the server overloaded?") Or e.Data.Contains("[INFO] [Metrics] {0}") Then Exit Sub

        If OnOutput(e.Data) Then Return

        SayToAllChannels("$k3Server: $k09" & e.Data, SayOptions.OpsOnly, {"!" & MyKey & "/TerrariaServerMessage"})
    End Sub

    Protected Sub ErrorDataReceived(ByVal sender As Object, ByVal e As DataReceivedEventArgs) Handles Server.ErrorDataReceived
        If e.Data = Nothing Then Exit Sub
        If e.Data = ">" Or e.Data = "" Or e.Data.Contains("[WARNING] Can't keep up! Did the system time change, or is the server overloaded?") Or e.Data.Contains("[INFO] [Metrics] {0}") Then Exit Sub

        If OnOutput(e.Data) Then Return

        SayToAllChannels("$k5Server: $k04" & e.Data, SayOptions.OpsOnly, {"!" & MyKey & "/TerrariaServerMessage"})
    End Sub

    Private Function OnOutput(ByVal Message As String)
        If Message.StartsWith(": ") Then Message = Message.Substring(2)

        Dim m As System.Text.RegularExpressions.Match '= System.Text.RegularExpressions.Regex.Match(Message, "\d\d:\d\d:\d\d \[INFO\] This server is running CraftBukkit version .* \(MC: .*\) \(Implementing API version (?<Version>.*)\)")
        'If m.Success Then
        '   CurrentCBVersion = m.Groups("Version").Value
        '   NeedsUpdate = False
        'End If

        m = System.Text.RegularExpressions.Regex.Match(Message, "^TShock v[\d.]+ (.*) initiated\.|^\[Server API\] Info Plugin TShock v\d+\.\d+\.\d+\.\d+ \(by [^)]*\) initiated\.")
        If m.Success And Not lIsTShock Then
            lIsTShock = True
            Return True
        End If

        m = System.Text.RegularExpressions.Regex.Match(Message, "^Server executed: ")
        If m.Success AndAlso Not OnlinePlayers.ContainsKey("Server") Then
            Return True
        End If

        m = System.Text.RegularExpressions.Regex.Match(Message, "^(?<IP>\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}:\d{1,5}) is connecting...$")
        If m.Success Then
            Connecting.Enqueue(m.Groups("IP").Value)
            Return False
        End If

        m = System.Text.RegularExpressions.Regex.Match(Message, "^\(Whisper From\)\<(?<Player>.{1,20})\>(?<Message>.*)$")
        If m.Success And lIsTShock Then
            TerrariaPrivateMessage(m.Groups("Player").Value, m.Groups("Message").Value)
            Return True
        End If

        m = System.Text.RegularExpressions.Regex.Match(Message, "^\<From (?<Player>.{1,20})\> (?<Message>.*)$")
        If m.Success And lIsTShock Then
            TerrariaPrivateMessage(m.Groups("Player").Value, m.Groups("Message").Value)
            Return True
        End If

        m = System.Text.RegularExpressions.Regex.Match(Message, "^\<To .{1,20}\> ")
        If m.Success And lIsTShock Then
            Return True
        End If

        m = System.Text.RegularExpressions.Regex.Match(Message, "^(\(.*\) )?(?<Player>.{1,20}?): (?<Message>.*)$")
        If m.Success And lIsTShock Then
            If OnlinePlayers.ContainsKey(m.Groups("Player").Value) Then
                TerrariaChatMessage(m.Groups("Player").Value, m.Groups("Message").Value)
                Return True
            End If
        End If

        m = System.Text.RegularExpressions.Regex.Match(Message, "^\(Server Broadcast\) (?<Message>.*)$")
        If m.Success And lIsTShock Then
            TerrariaBroadcast(m.Groups("Message").Value)
            Return True
        End If

        m = System.Text.RegularExpressions.Regex.Match(Message, "^\<(?<Player>.{1,20})> (?<Message>.*)$")
        If m.Success Then
            If Not lIsTShock And m.Groups("Player").Value = "Server" Then
                TerrariaBroadcast(m.Groups("Message").Value)
                Return True
            ElseIf OnlinePlayers.ContainsKey(m.Groups("Player").Value) Then
                TerrariaChatMessage(m.Groups("Player").Value, m.Groups("Message").Value)
                Return True
            End If
        End If

        m = System.Text.RegularExpressions.Regex.Match(Message, "^(?<Name>.{1,20}) authenticated successfully as user (?<Account>.*)\.$")
        If m.Success Then
            For Each Account In VBot.Accounts
                If Account.Key.ToUpper() = "$TSHOCK." & MyKey.ToUpper() & ":" & m.Groups("Account").Value.ToUpper() Then
                    VBot.Identifications.Add("!" & MyKey & "/" & m.Groups("Name").Value, New Identification With {.AccountName = Account.Key, .Channels = New List(Of String) From {"TerrariaServerMessage/"}})
                    SayToServer("Welcome, " & Account.Key & ".", ">" & m.Groups("Name").Value)
                End If
            Next
        End If

        m = System.Text.RegularExpressions.Regex.Match(Message, "^(?<Name>.{1,20}) (has joined|beigetreten|ha aderito|a rejoint|se ha unido)\.?$")
        If m.Success Then
            If Not OnlinePlayers.ContainsKey(m.Groups("Name").Value) And Connecting.Count > 0 Then
                OnlinePlayers.Add(m.Groups("Name").Value, New PlayerData With {.IP = Connecting.Peek, .LoginTime = Now})
                SayToAllChannels(String.Format("$k12[+]$o {0}$k15 has joined the server.", IRCColours.NicknameColour(m.Groups("Name").Value) & m.Groups("Name").Value), 0, {"!" & MyKey & "/TerrariaServerMessage"})
                SayToMinorChannels(MinorLabel & "$k12[+]$o " & String.Format("{0}$k15 has joined the server.", IRCColours.NicknameColour(m.Groups("Name").Value) & m.Groups("Name").Value), 0, {"!" & MyKey & "/TerrariaServerMessage"})
                SayToAllChannels(String.Format("{0}$o connected from $k12{1}$o.", IRCColours.NicknameColour(m.Groups("Name").Value) & m.Groups("Name").Value, Connecting.Dequeue()), SayOptions.OpsOnly, {"!" & MyKey & "/TerrariaServerMessage"})
            End If
        End If

        m = System.Text.RegularExpressions.Regex.Match(Message, "^(?<Name>.*?) ((has )?left|beenden|ha smesso di|a quitté|ha dejado de)\.?$")
        If m.Success Then
            If OnlinePlayers.ContainsKey(m.Groups("Name").Value) Then
                OnlinePlayers.Remove(m.Groups("Name").Value)
                SayToAllChannels(String.Format("$k4[-]$o {0}$k15 has left the server.", IRCColours.NicknameColour(m.Groups("Name").Value) & m.Groups("Name").Value), 0, {"!" & MyKey & "/MinecraftServerMessage"})
                SayToMinorChannels(MinorLabel & "$k4[-]$o " & String.Format("{0}$k15 has left the server.", IRCColours.NicknameColour(m.Groups("Name").Value) & m.Groups("Name").Value), 0, {"!" & MyKey & "/MinecraftServerMessage"})

                If Identifications.ContainsKey("!" & MyKey & "/" & m.Groups("Name").Value) Then
                    If Identifications("!" & MyKey & "/" & m.Groups("Name").Value).Channels.Contains("TerrariaServerMessage/") Then
                        Identifications("!" & MyKey & "/" & m.Groups("Name").Value).Channels.Remove("TerrariaServerMessage/")
                        If Identifications("!" & MyKey & "/" & m.Groups("Name").Value).Channels.Count = 0 Then
                            Identifications.Remove("!" & MyKey & "/" & m.Groups("Name").Value)
                        End If
                    End If
                End If
                Return True
            ElseIf Connecting.Count > 0 Then
                SayToAllChannels(String.Format("$k6[@]$o {0}$k4 at $k12{1}$k4 was denied access to the server.", IRCColours.NicknameColour(m.Groups("Name").Value) & m.Groups("Name").Value, Connecting.Dequeue()), SayOptions.OpsOnly, {"!" & MyKey & "/TerrariaServerMessage"})
                Return True
            End If
        End If
        Return False
    End Function

    <Regex({"(can|may) I (p(riv(ate(ly)?)?)? ?(message|m|msg)|whisper) (to )?(yo)?u\??", "How (can |should |do |must )?I (p(riv(ate(ly)?)?)? ?(message|m|msg)|whisper) (to )?you\??", "(p(riv(ate(ly)?)?)? ?(message|m|msg)|whisper) (to )?me(\.|!)?$"},
     Nothing, RegexAttribute.CommandScope.Channel, True)>
    Public Sub RegexTWhisper(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As System.Text.RegularExpressions.Match)
        CommandTWhisper(Connection, Sender, Channel, {})
    End Sub
    <Regex("How (can |should |do |must )?I (p(riv(ate(ly)?)?)? ?(message|m|msg)|whisper) (to )?($me|the bot|this bot|the console|the server|console)\??",
    Nothing, RegexAttribute.CommandScope.Channel, False)>
    Public Sub RegexTWhisper2(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As System.Text.RegularExpressions.Match)
        CommandTWhisper(Connection, Sender, Channel, {})
    End Sub
    <Command("twhisper", 0, 1,
        "twhisper [name]",
        "Enables a player on the Terraria server to send messages privately to the console.",
         Nothing, CommandAttribute.CommandScope.Channel)>
    Public Sub CommandTWhisper(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        If Not IsRunning() Then
            Say(Connection, Channel, "The server " & Choose(Choose("is not", "isn't") & " running", Choose("is not", "isn't") & " up", Choose("has not", "hasn't") & " been started") & Choose("", " yet") & Choose(".", ", " & Sender.Split("!"c)(0) & "."))
        Else
            If args.Count = 0 Then
                If Not Channel.StartsWith("!" & MyKey & "/TerrariaServerMessage") Then
                    Reply(Connection, Channel, Sender, Choose("You can only use that command on the Terraria server.", "That command is meant to be used on the Terraria server."))
                Else
                    Reply(Connection, Channel, Sender, "Please use  /reply <message>  to send me a private message.")
                    Reply(Connection, Channel, Sender, "This will work until someone else sends you a private message.")
                End If
            Else
                If Not OnlinePlayers.ContainsKey(args(0)) Then
                    Say(Connection, Channel, Choose("They aren't on the server.", "There's no one by that name online."))
                Else
                    SayToServer("Please use  /reply <message>  to send me a private message.", ">" & args(0))
                    SayToServer("This will work until someone else sends you a private message.", ">" & args(0))
                End If
            End If
        End If
    End Sub

    <Regex({"Who.* on the (Terraria |TShock )?server\??",
        "List .* (player|adventurer|people|Terrarian|everyone).* on the (Terraria |TShock )?server\??"},
 Nothing, RegexAttribute.CommandScope.Channel, True)>
    Public Sub RegexPlayers(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As System.Text.RegularExpressions.Match)
        CommandPlayers(Connection, Sender, Channel, {})
    End Sub
    <Command({"players", "who", "playing", "online", "names"}, 0, 0,
    "players",
    "Lists the players who are online on the server.",
     Nothing, CommandAttribute.CommandScope.Channel)>
    Public Sub CommandPlayers(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        If Not IsRunning() Then
            Say(Connection, Channel, "The server " & Choose(Choose("is not", "isn't") & " running", Choose("is not", "isn't") & " up", Choose("has not", "hasn't") & " been started") & Choose("", " yet") & Choose(".", ", " & Sender.Split("!"c)(0) & "."))
        ElseIf OnlinePlayers.Count = 0 Then
            ' No one online.
            Say(Connection, Channel, Choose(Choose("There is no one ", "There are no " & Choose("players ", "people ", "adventurers ", "Terrarians ")) & Choose("online", "on the server") & Choose("at the moment", "right now", "at this time", "", "") & ".",
                "No " & Choose("players ", "people ", "adventurers ", "Terrarians ") & "are " & Choose("online ", "on the server ") & Choose("at the moment", "right now", "at this time", "", "") & "."))
        Else
            Say(Connection, Channel, "The following " & Choose("players ", "people ", "adventurers ", "Terrarians ") & "are " & Choose("online", "on the server") & Choose("at the moment", "right now", "at this time", "", "") & ":")
            Say(Connection, Channel, "$k12" & String.Join("$o, $k12", OnlinePlayers.Keys))
        End If
    End Sub

    Private Sub Maintenance(ByVal sender As Object, ByVal e As ElapsedEventArgs) Handles MaintenanceTimer.Elapsed
        'If e.SignalTime.Second <> 0 Then Return
        'If Not IsRunning() Then Return
        If e.SignalTime.Minute = 0 Then
            If IsRunning() Then
                If e.SignalTime.Hour = 0 Then
                    AutoRestartInProgress = True
                    ShutDown()
                    '        Else
                    '            SayToAllChannels("$k6[@]$o Autosaving the world; brace for a little lag.")
                    '            Server.StandardInput.WriteLine("say Autosaving the world; brace for a little lag.")
                    '            Server.StandardInput.WriteLine("save-all")
                End If
            End If
        ElseIf AutoRestartInProgress And e.SignalTime.Hour = 0 And e.SignalTime.Minute = 5 Then
            If IsRunning() Then
                'TODO: Message the channel.
                Server.Kill()
                Server.WaitForExit()
            End If

            'If My.Computer.FileSystem.FileExists(My.Computer.FileSystem.CombinePath(WorkingDirectory, "server.log")) Then
            '    Dim LogsMonthDirectory = My.Computer.FileSystem.CombinePath(WorkingDirectory, "logs\" & Today.ToString("yyyy-MM"))

            '    If Not My.Computer.FileSystem.DirectoryExists(LogsMonthDirectory) Then _
            '        My.Computer.FileSystem.CreateDirectory(LogsMonthDirectory)

            '    Dim LogFile = My.Computer.FileSystem.CombinePath(LogsMonthDirectory, "server-" & Today.ToString("yyyy-MM-dd") & ".log")

            '    My.Computer.FileSystem.WriteAllText(LogFile, My.Computer.FileSystem.ReadAllText(My.Computer.FileSystem.CombinePath(WorkingDirectory, "server.log")), True)
            '    My.Computer.FileSystem.DeleteFile(My.Computer.FileSystem.CombinePath(WorkingDirectory, "server.log"))
            'End If

            Start()
            AutoRestartInProgress = False
            'ElseIf e.SignalTime.Hour = 0 And e.SignalTime.Minute = 30 Then
            '    CheckForUpdates()
        End If
    End Sub

    <Command({"set", "config", "property"}, 1, 2,
        "set <property> <value>",
        "Changes server parameters. If the server is still running, this won't do anything until you restart it." & vbCrLf &
        "You can set the following properties: $k11workingdir$o, $k11exe$o." & vbCrLf &
        "You can also set the following properties: $k11port$o, $k11players$o, $k11pass$o, $k11worldpath$o, $k11world$o, $k11autocreate$o, $k11banlist$o, $k11worldname$o, $k11secure$o." & vbCrLf &
        "Alternatively, you can omit the $k11value$o parameter to just check a property's value.",
         ".set", CommandAttribute.CommandScope.Channel)>
    Public Sub CommandSet(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim lProperty = args(0)
        Dim lValue = args.ElementAtOrDefault(1)

        Select Case lProperty.ToLower.Replace("_", "")
            Case "workingdirectory", "workingdir"
                If lValue = Nothing Then
                    Say(Connection, Channel, "The working directory is currently set to $k12" & WorkingDirectory & "$o.")
                Else
                    WorkingDirectory = lValue
                    Say(Connection, Channel, "The working directory is now set to $k12" & WorkingDirectory & "$o.")
                End If
            Case "configfile", "configurationfile", "serverconfig", "serverconfigfile", "serverconfigurationfile"
                If lValue = Nothing Then
                    Say(Connection, Channel, "The server configuration will be loaded from $k12" & ServerConfigFile & "$o.")
                Else
                    WorkingDirectory = lValue
                    Say(Connection, Channel, "The server configuration will now be loaded from $k12" & ServerConfigFile & "$o.")
                End If
            Case "exefilename", "exe", "serverexe", "exelocation", "exeloc"
                If lValue = Nothing Then
                    Say(Connection, Channel, "I will start the server from $k12" & ExecutablePath & "$o.")
                Else
                    ExecutablePath = lValue
                    Say(Connection, Channel, "I will now start the server from $k12" & ExecutablePath & "$o.")
                End If
            Case "autostart"
                If lValue = Nothing Then
                    If Autostart Then
                        Say(Connection, Channel, "I will $k9automatically start the server$o when the plugin is loaded.")
                    Else
                        Say(Connection, Channel, "I will $k4wait for your command$o to start the server.")
                    End If
                Else
                    If {"1", "-1", "on", "yes", "true", "+", "enable", "enabled", "activate", "activated", "active"}.Contains(lValue.ToLower) Then
                        Autostart = True
                        Say(Connection, Channel, "I will now $k9automatically start the server$o when the plugin is loaded.")
                    ElseIf {"0", "off", "no", "false", "-", "disable", "disabled", "deactivate", "deactivated", "inactive"}.Contains(lValue.ToLower) Then
                        Autostart = False
                        Say(Connection, Channel, "I will now $k4wait for your command$o to start the server.")
                    Else
                        Reply(Connection, Channel, Sender, VBot.Choose("", "Sorry " & Sender.Split("!"c)(0) & ", ") & "I don't recognise " & VBot.Choose(IRCColours.Red & lValue & "$o", "that") & " as a Boolean value. Please enter $k11on$o or $k11off$o.")
                    End If
                End If
            Case Else
                ' It's a serverconfig.txt property.
                If WorkingDirectory = Nothing And Not (System.IO.Path.IsPathRooted(ServerConfigFile) Or ServerConfigFile.StartsWith("%")) Then
                    Reply(Connection, Channel, Sender, "The working directory is not set.")
                    Return
                End If

                Dim Key As String, GetFormat As String, SetFormat As String, DeleteFormat As String
                Select Case lProperty.ToLower.Replace("_", "")
                    Case "maxplayers", "players", "maximumplayers", "numplayers", "numslots", "slots", "maxslots"
                        Key = "maxplayers"
                        GetFormat = Choose("The server will support up to $k12{0}$o players.", "Up to $k12{0}$o players are allowed on the server.", "The maximum number of players is $k12{0}$o.")
                        SetFormat = Choose("The server will now support up to $k09{0}$o players.", "Up to $k09{0}$o players are now allowed on the server.", "The maximum number of players is now $k09{0}$o.")
                        DeleteFormat = Choose("The player limit has been removed.", "Removed the player limit.")

                        If lValue IsNot Nothing And lValue <> "" Then
                            Dim MaxPlayers As Integer
                            If Not Integer.TryParse(lValue, MaxPlayers) Then
                                Say(Connection, Channel, Choose("That isn't a valid integer.", "The setting must be an integer."))
                                lValue = Nothing
                            ElseIf MaxPlayers < 1 Or MaxPlayers > 255 Then
                                Say(Connection, Channel, Choose("The setting must be between $b1$b and $b255$b."))
                                lValue = Nothing
                            Else
                                lValue = MaxPlayers
                            End If
                        End If
                    Case "world", "worldfile"
                        Key = "world"
                        GetFormat = "The world will be loaded from $k12{0}$o."
                        SetFormat = "The world will now be loaded from $k09{0}$o."
                        DeleteFormat = "You will be asked which world to load when the server starts."
                    Case "port", "listeningport", "serverport"
                        Key = "port"
                        GetFormat = "The server " & Choose("will ", "is set to ") & Choose("listen ", "accept connections ") & "on port $k12{0}$o."
                        SetFormat = "The server " & Choose("will now ", "is now set to ") & Choose("listen ", "accept connections ") & "on port $k09{0}$o."
                        DeleteFormat = "You will be asked what port to use when the server starts."
                    Case "password", "pass", "serverpass", "serverpassword"
                        Key = "password"
                        GetFormat = "The server password is $k12{0}$o."
                        SetFormat = "The server password has been changed."
                        If IsRunning() And lValue IsNot Nothing Then Server.StandardInput.WriteLine("password " & lValue)
                    Case "motd", "greeting", "messageoftheday"
                        Key = "motd"
                        GetFormat = "The MotD is $k12{0}$o."
                        SetFormat = "The MotD has been changed."
                        DeleteFormat = Choose("The default MotD will be restored.", "The MotD has been reset.")
                        If IsRunning() And lValue IsNot Nothing Then Server.StandardInput.WriteLine("motd " & lValue)
                    Case "worldpath", "worldloc", "worldlocation", "worldfolder"
                        Key = "worldpath"
                        If lValue <> Nothing Then
                            Dim FolderName As String
                            If System.IO.Path.IsPathRooted(lValue) Or lValue.StartsWith("%") Then
                                FolderName = lValue
                            Else
                                FolderName = System.IO.Path.Combine(WorkingDirectory, lValue)
                            End If
                            If System.IO.Directory.Exists(FolderName) Then
                                lValue = FolderName
                            Else
                                Reply(Connection, Channel, Sender, "That folder doesn't exist" & Choose(".", ", " & Sender.Split("!"c)(0) & "."))
                                lValue = Nothing
                            End If
                        End If
                        GetFormat = "The server will look for world files in $k12{0}$o."
                        SetFormat = "The server will now look for world files in $k09{0}$o."
                        DeleteFormat = "The server will now look for world files in the default location."
                    Case "autocreate", "autogenerate", "size", "worldsize"
                        Key = "autocreate"
                        If lValue <> Nothing Then
                            Select Case lValue.ToLower
                                Case "1", "small", "s", "tiny", "little"
                                    lValue = "1"
                                Case "2", "medium", "moderate", "m", "middle"
                                    lValue = "2"
                                Case "3", "large", "l", "big", "huge"
                                    lValue = "3"
                                Case Else
                                    Reply(Connection, Channel, Sender, String.Format(Choose("$k04{0}$o ", "That ") & Choose("is not ", "isn't ") & "a valid setting. Use $k11small$o, $k11medium$o or $k11large$o.", lValue))
                                    lValue = Nothing
                            End Select
                        End If
                        GetFormat = "New worlds created by this server will be $k12{0}$o."
                        SetFormat = "New worlds created by this server will now be $k09{0}$o."
                        DeleteFormat = "The server will no longer create a new world."
                    Case "worldname", "autocreatename", "createname"
                        Key = "worldname"
                        GetFormat = "The new world will be named $k12{0}$o."
                        SetFormat = "The new world will now be named $k09{0}$o."
                        DeleteFormat = "The server will now have the default name."
                    Case "banlist"
                        Key = "banlist"
                        GetFormat = "The ban list will be loaded from $k12{0}$o."
                        SetFormat = "The ban list will now be loaded from $k09{0}$o."
                        SetFormat = "The ban list will now be loaded from $k3banlist.txt$o."
                    Case "secure", "cheatprotection", "cheatprotect", "anticheat", "nocheat"
                        Key = "secure"
                        If lValue <> Nothing Then
                            Select Case lValue.ToLower
                                Case "1", "on", "enabled", "enable", "activated", "active", "secured", "secure"
                                    lValue = "1"
                                Case "0", "off", "disabled", "disable", "deactivated", "inactive", "unsecured", "insecure"
                                    lValue = "0"
                                Case Else
                                    Reply(Connection, Channel, Sender, String.Format(Choose("$k04{0}$o ", "That ") & Choose("is not ", "isn't ") & "a valid setting. Use $k11on$o or $k11off$o.", lValue))
                                    lValue = Nothing
                            End Select
                        End If
                        GetFormat = "Cheat protection is $k12{0}$o."
                        SetFormat = "Cheat protection is now $k09{0}$o."
                        SetFormat = "Cheat protection is now $k3disabled$o."
                    Case "lang", "language", "sprache", "lingua", "langue", "idioma"
                        Key = "lang"
                        If lValue <> Nothing Then
                            Select Case lValue.ToLower
                                ' We'll let users tell the bot which language they want to use in any of the available languages.
                                Case "1", "en", "english", "englisch", "inglese", "anglais", "inglés", "ingles"
                                    lValue = "1"
                                Case "2", "de", "german", "deutsch", "tedesco", "allemand", "alemán", "aleman"
                                    lValue = "2"
                                Case "3", "it", "italian", "italienisch", "italiano", "italien"
                                    lValue = "3"
                                Case "4", "fr", "french", "französisch", "franzosisch", "francese", "français", "francais", "francés", "frances"
                                    lValue = "4"
                                Case "5", "es", "spanish", "spanisch", "spagnolo", "espagnol", "español", "espanol"
                                    lValue = "5"
                                Case Else
                                    Reply(Connection, Channel, Sender, String.Format(Choose("", "Sorry " & Sender.Split("!")(0) & ", ") & "Terraria doesn't recognise that language. Please use one of $k11English$o, $k11German$o, $k11Italian$o, $k11French$o, $k11Spanish$o.", lValue))
                                    lValue = Nothing
                            End Select
                        End If
                        GetFormat = Choose("The server is using $k12{0}$o.", "The server is in $k12{0}$o.")
                        SetFormat = Choose("The server will now use $k09{0}$o.", "The server will now be in $k09{0}$o.")
                        DeleteFormat = Choose("The server will now use the default language.", "The server will now be in the default language.")
                    Case "priority", "pri", "cpu", "cpupriority"
                        Key = "priority"
                        If lValue <> Nothing Then
                            Select Case lValue.ToLower.Replace(" ", "").Replace("-", "").Replace("_", "")
                                Case "0", "realtime"
                                    lValue = "0"
                                Case "1", "high"
                                    lValue = "1"
                                Case "2", "abovenormal"
                                    lValue = "2"
                                Case "3", "normal"
                                    lValue = "3"
                                Case "4", "belownormal"
                                    lValue = "4"
                                Case "5", "low", "idle"
                                    lValue = "5"
                                Case Else
                                    Reply(Connection, Channel, Sender, String.Format(Choose("$k04{0}$o ", "That ") & Choose("is not ", "isn't ") & "a valid setting. Use one of $k11realtime$o, $k11high$o, $k11above normal$o, $k11normal$o, $k11below normal$o, $k11idle$o.", lValue))
                                    lValue = Nothing
                            End Select
                        End If
                        GetFormat = "The server process should have $k12{0}$o priority."
                        SetFormat = "The server process should now have $k9{0}$o priority."
                        DeleteFormat = "The server process should now have $k3normal$o priority."
                    Case Else
                        Reply(Connection, Channel, Sender, "I don't manage a property named $k04" & lProperty & "$o for Terraria servers.")
                        Return
                End Select
                If lValue = Nothing Then
                    ' Read the value and tell the user.
                    Try
                        lValue = GetSetting(Key)
                        If Key = "autocreate" Then lValue = {"", "small", "medium", "large"}(lValue)
                        If Key = "secure" Then lValue = {"off", "on"}(lValue)
                        If Key = "lang" Then lValue = {"English", "English", "German", "Italian", "French", "Spanish"}(lValue)
                        If Key = "priority" Then lValue = {"$k4$brealtime$b", "high", "above normal", "normal", "below normal", "idle"}(lValue)

                        Say(Connection, Channel, String.Format(GetFormat, lValue))
                    Catch ex As IO.FileNotFoundException
                        Reply(Connection, Channel, Sender, String.Format(Choose(
                            "The configuration file, $k04{0}$o, is missing.",
                            "I couldn't find the configuration file at $k04{0}$o."),
                            ServerConfigFile))
                    Catch ex As KeyNotFoundException
                        Say(Connection, Channel, String.Format(Choose(
                            "$k06{0}$o is not " & Choose("defined ", "set ") & "in the configuration file.",
                            "There is no setting in the configuration file of $k06{0}$o."),
                            Key))
                    End Try
                Else
                    ' Set the value in the file.
                    Try
                        PutSetting(Key, lValue)

                        If Key = "autocreate" Then lValue = {"", "small", "medium", "large"}(lValue)
                        If Key = "secure" Then lValue = {"off", "on"}(lValue)
                        If Key = "lang" Then lValue = {"English", "English", "German", "Italian", "French", "Spanish"}(lValue)
                        If Key = "priority" Then lValue = {"$k4$brealtime$b", "high", "above normal", "normal", "below normal", "idle"}(lValue)

                        If lValue = "" Then
                            Say(Connection, Channel, String.Format(DeleteFormat, lValue))
                        Else
                            Say(Connection, Channel, String.Format(SetFormat, lValue))
                        End If
                    Catch ex As Exception
                        Reply(Connection, Channel, Sender, String.Format(Choose(
                              "There was a problem writing the setting to the file: $k04{0}",
                              "I " & Choose("was unable to ", "wasn't able to ", "couldn't ", "could not ") & "write the setting to the file: $k04{0}"),
                              ex.Message))
                    End Try
                End If
        End Select
    End Sub

    <Regex({"^\>(?<Command>.*)"},
         ".console", RegexAttribute.CommandScope.Channel)>
    Public Sub RegexInput(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As System.Text.RegularExpressions.Match)
        CommandSend(Connection, Sender, Channel, {Match.Groups("Command").Value})
    End Sub
    <Command({"send", "input", "s", "i"}, 1, 1,
    "send <command>",
    "Sends a command to the server's standard input stream. You can also do this by prefixing your command with a $k11>$o.",
     ".console", CommandAttribute.CommandScope.Channel)>
    Public Sub CommandSend(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        If IsRunning() Then
            Server.StandardInput.WriteLine(args(0))
        Else
            Say(Connection, Channel, "The server " & Choose(Choose("is not", "isn't") & " running", Choose("is not", "isn't") & " up", Choose("has not", "hasn't") & " been started") & Choose("", " yet") & Choose(".", ", " & Sender.Split("!"c)(0) & "."))
        End If
    End Sub

    Public Overrides Sub OnChannelJoin(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String)
        MyBase.OnChannelJoin(Connection, Sender, Channel)
        SayToServer("[IRC] " & Sender.Split("!"c)(0) & " has joined " & Channel & ".", "#,noprefix")
    End Sub

    Public Overrides Sub OnChannelPart(ByVal Connection As VBot.IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Reason As String)
        MyBase.OnChannelPart(Connection, Sender, Channel, Reason)
        SayToServer("[IRC] " & Sender.Split("!"c)(0) & " has left " & Channel & "." & If(Reason <> Nothing, " (" & Reason & ")", ""), "#,noprefix")
    End Sub

    Public Overrides Sub OnQuit(ByVal Connection As VBot.IRCConnection, ByVal Sender As String, ByVal Reason As String)
        MyBase.OnQuit(Connection, Sender, Reason)
        SayToServer("[IRC] " & Sender.Split("!"c)(0) & " has left IRC." & If(Reason <> Nothing, " (" & Reason & ")", ""), "#,noprefix")
    End Sub

    Public Overrides Sub OnChannelKick(ByVal Connection As VBot.IRCConnection, ByVal Sender As VBot.IRCConnection.IRCUser, ByVal Channel As String, ByVal Target As String, ByVal Reason As String)
        MyBase.OnChannelKick(Connection, Sender, Channel, Target, Reason)
        SayToServer("[IRC] " & Target & " was kicked out by " & Sender.Nickname & "." & If(Reason <> Nothing, " (" & Reason & ")", ""), "#,noprefix")
    End Sub

    Public Overrides Sub OnChannelMessage(ByVal Connection As VBot.IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Message As String)
        MyBase.OnChannelMessage(Connection, Sender, Channel, Message)
        If Connection Is Nothing And Channel.StartsWith("!" & MyKey & "/", StringComparison.OrdinalIgnoreCase) Then Return
        If Message.StartsWith("!") Or (Message.StartsWith(">") AndAlso Not Message.StartsWith("> ") AndAlso UserHasPermission(Connection, Channel, Sender, MyKey & ".console")) Then Return
        SayToServer("[IRC] " & Sender.Split("!"c)(0) & ": " & Message, "#,noprefix")
    End Sub

    Public Overrides Sub OnChannelAction(ByVal Connection As VBot.IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Message As String)
        MyBase.OnChannelAction(Connection, Sender, Channel, Message)
        If Connection Is Nothing And Channel.StartsWith("!" & MyKey & "/", StringComparison.OrdinalIgnoreCase) Then Return
        SayToServer("[IRC] * " & Sender.Split("!"c)(0) & " " & Message, "#,noprefix")
    End Sub

    <Command({"irc"}, 1, 32767, "irc rejoin [channel]  or  irc kick [channel] <target> [message]  or  irc ban [channel] <target> [message]  or  irc mode [channel] <modes>",
"Rejoins the IRC channel",
 ".ircop")>
    Public Sub CommandIRC(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim TargetConnection As IRCConnection, TargetChannel As String, arg As Short
        If args.Count > 2 AndAlso args(1).Contains("#") Then
            arg = 2
            Dim SpecifiedNetwork As String, SpecifiedChannel As String
            If args(1).Contains("/") Then
                SpecifiedNetwork = args(1).Split({"/"c}, 2)(0)
                SpecifiedChannel = args(1).Split({"/"c}, 2)(1)
            Else
                SpecifiedChannel = args(1)
            End If

            For Each Channel In Channels
                If Channel.StartsWith("!") Then Continue For
                If Channel.Split({"/"c}, 2)(1).ToUpper() = SpecifiedChannel.ToUpper() AndAlso Channel.Split({"/"c}, 2)(0).StartsWith(SpecifiedNetwork, StringComparison.OrdinalIgnoreCase) Then
                    If TargetChannel Is Nothing Then
                        TargetChannel = Channel
                    Else
                        TargetChannel = "?"
                        Exit For
                    End If
                End If
            Next
        Else
            arg = 1
            For Each Channel In Channels
                If Channel.StartsWith("!") Then Continue For
                If TargetChannel Is Nothing Then
                    TargetChannel = Channel
                Else
                    TargetChannel = "?"
                    Exit For
                End If
            Next
        End If

        If TargetChannel = "?" Then
            Reply(Connection, Channel, Sender, "More than one IRC channel matches your specification. Please be more specific.")
            Return
        End If
        If Not TargetChannel.StartsWith("!") Then
            For Each Connection In Connections
                If Connection.Address.ToUpper() = TargetChannel.Split({"/"c}, 2)(0).ToUpper() Then
                    TargetConnection = Connection
                    Exit For
                End If
            Next
            TargetChannel = TargetChannel.Split({"/"c}, 2)(1)
        End If

        Select Case args(0).ToUpper()
            Case "REJOIN", "JOIN", "J"
                TargetConnection.Send("JOIN " & TargetChannel)
            Case "KICK", "K"
                If args.Count < arg + 1 Then
                    Reply(Connection, Channel, Sender, "Usage: $circ kick [channel] <user> [reason]")
                    Return
                End If
                Dim Nickname As String
                For Each User In Connection.Channels(TargetChannel).Users
                    If User.Value.Nickname.StartsWith(args(arg), StringComparison.OrdinalIgnoreCase) Then
                        If Nickname Is Nothing Then
                            Nickname = User.Value.Nickname
                        Else
                            Nickname = "?"
                            Exit For
                        End If
                    End If
                Next
                If Nickname Is Nothing Then
                    Reply(Connection, Channel, Sender, "No such user is online.")
                    Return
                ElseIf Nickname = "?" Then
                    Reply(Connection, Channel, Sender, "Multiple matching users are online.")
                    Return
                End If
                Dim Message As String
                If args.Count > arg + 1 Then Message = String.Join(" ", args, arg + 1, args.Count - arg - 1)
                TargetConnection.Send("KICK " & TargetChannel & " " & Nickname & " :Kick requested by " & Sender.Split("!"c)(0) & If(args.Count = arg + 2, ": " & Message, "."))
            Case "BAN", "B"
                If args.Count < arg + 1 Then
                    Reply(Connection, Channel, Sender, "Usage: $circ ban [channel] <user|hostmask> [reason]")
                    Return
                End If
                Dim Nickname As String, Username As String, Host As String
                Dim Message As String
                If args.Count > arg + 1 Then Message = String.Join(" ", args, arg + 1, args.Count - arg - 1)
                If args(arg).Contains("@") Then
                    Username = args(arg).Split({"@"c}, 2)(0)
                    Host = args(arg).Split({"@"c}, 2)(1)
                    If Username.Contains("!") Then
                        Nickname = Username.Split({"!"c}, 2)(0)
                        Username = Username.Split({"!"c}, 2)(1)
                    Else
                        Nickname = "*"
                    End If
                Else
                    For Each User In Connection.Channels(TargetChannel).Users
                        If User.Value.Nickname.StartsWith(args(arg), StringComparison.OrdinalIgnoreCase) Then
                            If Nickname Is Nothing Then
                                Nickname = User.Value.Nickname
                                Host = User.Value.Host
                            Else
                                Nickname = "?"
                                Exit For
                            End If
                        End If
                    Next
                    If Nickname Is Nothing Then
                        Reply(Connection, Channel, Sender, "No such user is online.")
                        Return
                    ElseIf Nickname = "?" Then
                        Reply(Connection, Channel, Sender, "Multiple matching users are online.")
                        Return
                    Else
                        Nickname = "*"
                        Username = "*"
                    End If
                End If
                Dim Pattern = Nickname & "!" & Username & "@" & Host
                If Host IsNot Nothing Then TargetConnection.Send("MODE " & TargetChannel & " +b " & Pattern)
                Dim PatternBuilder As New System.Text.StringBuilder
                For Each c In Pattern
                    If c = "#"c Or c = "["c Then
                        PatternBuilder.Append("["c)
                        PatternBuilder.Append(c)
                        PatternBuilder.Append("]"c)
                    Else
                        PatternBuilder.Append(Char.ToUpper(c))
                    End If
                Next
                Pattern = PatternBuilder.ToString()
                For Each User In Connection.Channels(TargetChannel).Users
                    If User.Value.ToString().ToUpper() Like Pattern Then
                        TargetConnection.Send("KICK " & TargetChannel & " " & User.Value.Nickname & " :Ban requested by " & Sender.Split("!"c)(0) & If(args.Count = arg + 2, ": " & Message, "."))
                    End If
                Next
            Case "MODE", "M"
                If args.Count < arg + 1 Then
                    Reply(Connection, Channel, Sender, "Usage: $circ mode [channel] <modes> [parameters]")
                    Return
                End If
                TargetConnection.Send("MODE " & TargetChannel & " " & String.Join(" ", args, arg, args.Count - arg))
        End Select
    End Sub

    Public Function TerrariaChatMessage(ByVal Player As String, ByVal Message As String)
        'If Player = Nickname() Then Return True ' So the bot doesn't process its own messages.

        If Message = "!" Or Not Message.StartsWith("!") Then
            SayToAllChannels(IRCColours.NicknameColour(Player) & Player & "$o: " & Message, 0, {"!" & MyKey & "/TerrariaServerMessage"})
            SayToMinorChannels(MinorLabel & IRCColours.NicknameColour(Player) & Player & "$o: " & Message, 0, {"!" & MyKey & "/TerrariaServerMessage"})
        End If

        VBot.EventCheck(Nothing, "!" & MyKey & "/TerrariaServerMessage/#", "OnChannelMessage", {Nothing, Player & "!Terraria@Terraria", "!" & MyKey & "/TerrariaServerMessage/#", Message})
        VBot.CheckMessage(Nothing, Player & "!Terraria@Terraria", "!" & MyKey & "/TerrariaServerMessage/#", Message)
    End Function

    Public Function TerrariaBroadcast(ByVal Message As String)
        If Message.StartsWith("[IRC] ") Then Return True
        If Message.StartsWith(Nickname() & ": ") Then Return True
        If Message.StartsWith(Nickname() & " > ") Then Return True

        If Not Message.StartsWith("!") Then
            SayToAllChannels("$k13[Broadcast]$o " & Message, 0, {"!" & MyKey & "/TerrariaServerMessage"})
            SayToMinorChannels(MinorLabel & "$k13[Broadcast]$o " & Message, 0, {"!" & MyKey & "/TerrariaServerMessage"})
        End If

        Return True
    End Function

    <Output("TerrariaServerMessage")>
    Public Sub SayToServer(ByVal Message As String, ByVal Arguments As String)
        'If Message.StartsWith(IRCColours.DarkGreen & "Server: " & IRCColours.Green) Then Return
        'If Message.StartsWith(IRCColours.DarkRed & "Server: " & IRCColours.Red) Then Return

        ' Remove the IRC colour codes.
        Dim regex = New System.Text.RegularExpressions.Regex("\x03(\d{0,2}(,\d{1,2})?)?")
        Message = regex.Replace(Message.Trim, "")
        Message = Message.Replace(Chr(2), "")
        Message = Message.Replace(Chr(15), "")
        Message = Message.Replace(Chr(31), "")

        If Arguments = "#" Or Arguments = Nothing Then
            Debug.Print("Said '" & Message & "' to the Terraria server.")
        ElseIf Arguments <> Nothing AndAlso Arguments.Contains(">") Then
            Debug.Print("Said '" & Message & "' to " & Arguments.Split({">"c}, 2)(1) & " on the Terraria server.")
        End If

        If Not IsRunning() Then Exit Sub


        If Arguments Is Nothing OrElse Not Arguments.Contains(">") Then
            Server.StandardInput.WriteLine("say " & If(Arguments = "#,noprefix", "", Nickname() & ": ") & Message)
        ElseIf Arguments <> Nothing AndAlso Arguments.Contains(">") Then
            If lIsTShock Then
                Server.StandardInput.WriteLine("tell " & Arguments.Substring(Arguments.IndexOf(">"c) + 1) & " " & If(Arguments.Split(">"c)(0) = "noprefix", "", Nickname() & ": ") & Message)
            Else
                Server.StandardInput.WriteLine("say " & Nickname() & " -> " & Arguments.Substring(Arguments.IndexOf(">"c) + 1) & ": " & Message)
            End If
        End If
    End Sub

#If DEBUG Then
    <Command({"chat"}, 2, 2,
        "chat <player> <message>",
        "Simulates Minecraft server chat. For debugging.",
         ".debug", CommandAttribute.CommandScope.Channel)>
    Public Sub CommandChat(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        TerrariaChatMessage(args(0), args(1))
    End Sub

    <Command({"testw"}, 2, 2,
    "testw <player> <message>",
    "Simulates Terraria server whispers. For debugging.",
     ".debug", CommandAttribute.CommandScope.Channel)>
    Public Sub CommandWhisper(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        TerrariaPrivateMessage(args(0), args(1))
    End Sub

    <Command({"test"}, 1, 1,
        "test <message>",
        "Simulates Terraria server messages. For debugging.",
         ".debug", CommandAttribute.CommandScope.Channel)>
    Public Sub CommandTest(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        OnOutput(args(0))
    End Sub
#End If

    Private Function TerrariaPrivateMessage(ByVal Player As String, ByVal Message As String) As Boolean
        If Not Message.StartsWith("!") Then
            SayToAllChannels(IRCColours.NicknameColour(Player) & Player & "$k15 whispers$o: " & Message, SayOptions.OpsOnly, {"!" & MyKey & "/TerrariaServerMessage"})
        End If

        VBot.EventCheck(Nothing, "!" & MyKey & "/TerrariaServerMessage/>" & Player, "OnChannelMessage", {Nothing, Player & "!Terraria@Terraria", "!" & MyKey & "/TerrariaServerMessage/>" & Player, Message})
        VBot.CheckMessage(Nothing, Player & "!Terraria@Terraria", "!" & MyKey & "/TerrariaServerMessage/>" & Player, Message)
        Return True
    End Function
End Class