Imports System.Timers
Imports VBot
Imports System.Text.RegularExpressions

Class MinecraftServerPlugin
    Inherits Plugin
    'Inherits ServerHandler

    Dim WithEvents MaintenanceTimer As Timer
    Public WorkingDirectory As String
    Public Arguments As String
    Public ExecutablePath As String
    Public JarFilename As String = "minecraft_server.jar"
    Public JavaFilename As String = "C:\Program Files\Java\jre6\bin\java.exe"
    Public InitMemoryHeap As String = "1024M"
    Public MaxMemoryHeap As String = "2048M"
    Public AutosaveInterval As Integer = 60
    Public AutoRestartInterval As Integer = 24
    Dim AutoRestartInProgress As Boolean = False
    Public Xincgc As Boolean = False
    Public AutoStart As Boolean = False
    Public UsingBukkit As Boolean = False

    Public RelayServerChat As Boolean = True
    Public RelayIRCChat As Boolean = True
    Public RelayLogins As Boolean = True
    Public RelayAchievements As Boolean = True
    Public RelayDeaths As Boolean = True

    Private ReadOnly ServerConfigFile = "server.properties"

    Public ServerVersion As String
    Public CBVersion As String, NeedsUpdate As Boolean, LatestCBVersion As Build
    Public OnlinePlayers As Dictionary(Of String, PlayerData)
    Dim UUIDPending As Dictionary(Of String, Guid)

    Dim UseProxyServer As Boolean = True, ProxyServerAddress As String = "10.141.232.19", ProxyServerPort As UShort = 800

    Public Structure PlayerData
        Dim IP As String
        Dim EntityID As Integer
        Dim LoginTime As Date
        Dim UUID As Guid
    End Structure

    Public WithEvents Server As Process

    Public Overrides ReadOnly Property Name As String
        Get
            Return "Minecraft Server Handler"
        End Get
    End Property
    Public Overrides ReadOnly Property UseGlobalKeyCommand As Boolean
        Get
            Return True
        End Get
    End Property

    Sub New(ByVal Key As String)
        MaintenanceTimer = New Timer(60000) With {.Enabled = True}

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

                    Select Case Section.ToLower
                        Case "server"
                            Select Case Identifier.ToLower
                                Case "jar"
                                    JarFilename = Value
                                Case "java"
                                    ExecutablePath = Value
                                Case "workingdir"
                                    WorkingDirectory = Value
                                Case "initheap"
                                    InitMemoryHeap = Value
                                Case "maxheap"
                                    MaxMemoryHeap = Value
                                Case "incgc"
                                    Xincgc = Value
                                Case "autostart"
                                    AutoStart = Value
                            End Select
                        Case "bot"
                            Select Case Identifier.ToLower
                                Case "relayserverchat"
                                    RelayServerChat = Value
                                Case "relayircchat"
                                    RelayIRCChat = Value
                                Case "relaylogins"
                                    RelayLogins = Value
                                Case "relayachievements"
                                    RelayAchievements = Value
                                Case "relaydeaths"
                                    RelayDeaths = Value
                            End Select
                    End Select
                    Continue Do
                End If
            Loop
            Reader.Close()
        End If
    End Sub

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

    Public Function IsRunning() As Boolean
        Try
            Return If(Server Is Nothing, False, Not Server.HasExited)
        Catch ex As InvalidOperationException
            Return False
        End Try
    End Function

    <Regex("(Start|Boot) (up )?the (Minecraft |(Craft)?Bukkit )?server.?",
         ".startstop", RegexAttribute.CommandScope.Channel, True)>
    Public Sub RegexStart(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As System.Text.RegularExpressions.Match)
        CommandStart(Connection, Sender, Channel, {})
    End Sub
    <Command("start", 0, 0,
        "start",
        "Starts the server.",
         ".startstop", CommandAttribute.CommandScope.Channel)>
    Public Sub CommandStart(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        If IsRunning() Then
            Say(Connection, Channel, Choose("It", "The server") & Choose(Choose(" is", "'s") & Choose(" already up", " already started", " already running"), Choose(" has", "'s") & " already " & Choose("been ", "") & "started") & Choose(".", ", " & Sender.Split("!"c)(0) & "."))
        ElseIf Not My.Computer.FileSystem.FileExists(My.Computer.FileSystem.CombinePath(WorkingDirectory, ExecutablePath)) Then
            SayToAllChannels("$k6[@]$o The Java EXE location is currently set to $k04" & ExecutablePath & "$o, but it doesn't exist there. Please set the correct location using $k11$cset java $k10<file path>$o.")
        Else
            Start()
        End If
    End Sub

    <Regex("(Gracefully )?(Stop|Shut down|Close|End) the (Minecraft |(Craft)?Bukkit )?server.?",
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
        ' Make sure the executable file actually exists.
        If Not My.Computer.FileSystem.FileExists(My.Computer.FileSystem.CombinePath(WorkingDirectory, ExecutablePath)) Then
            SayToAllChannels("$k6[@]$o The Java location is currently set to $k04" & ExecutablePath & "$o, but it doesn't exist. The server could not be started.")
            Return
        End If
        OnlinePlayers = New Dictionary(Of String, PlayerData)(StringComparer.OrdinalIgnoreCase)
        UUIDPending = New Dictionary(Of String, Guid)(StringComparer.OrdinalIgnoreCase)
        Server = New Process With {.StartInfo = New ProcessStartInfo With {
            .FileName = ExecutablePath,
            .Arguments = String.Format("-Xms{1} -Xmx{2} {3}-jar {0} nogui -nojline", If(JarFilename.IndexOfAny({" "c, ">"c, "<"c, "|"c}) >= 0, """" & JarFilename & """", JarFilename), InitMemoryHeap, MaxMemoryHeap, If(Xincgc, "-Xincgc ", "")),
            .WorkingDirectory = WorkingDirectory,
            .UseShellExecute = False, .CreateNoWindow = True, .RedirectStandardInput = True, .RedirectStandardOutput = True, .RedirectStandardError = True}}
        Server.EnableRaisingEvents = True
        Server.Start()
        Server.BeginOutputReadLine()
        Server.BeginErrorReadLine()

        SayToAllChannels("$k6[@]$o " & VBot.Choose("I have", "I've") & " started the server.")
        SayToMinorChannels(MinorLabel & "$k6[@]$o " & VBot.Choose("I have", "I've") & " started the server.")
    End Sub

    Public Sub ShutDown()
        SayToAllChannels("$k6[@]$o Stopping the server...")
        SayToMinorChannels(MinorLabel & "$k6[@]$o The server is being shut down.")
        Server.StandardInput.WriteLine("stop")
    End Sub

    <Regex("(Gracefully )?(Restart|Reboot|Refresh) the (Minecraft |(Craft)?Bukkit )?server.?",
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
            SayToAllChannels("$k6[@]$o The Java EXE location is currently set to $k04" & ExecutablePath & "$o, but it doesn't exist there. Please set the correct location using $k11set java $k10<file path>$o.")
        Else
            Start()
        End If
    End Sub

    Protected Sub ServerExited() Handles Server.Exited
        SayToAllChannels("$k6[@]$o The server shut down.")
        SayToMinorChannels(MinorLabel & "$k6[@]$o The server shut down.")
        'CurrentCBVersion = ""
        'LatestCBVersion = Nothing
        'NeedsUpdate = False
    End Sub

    Protected Sub OutputDataReceived(ByVal sender As Object, ByVal e As DataReceivedEventArgs) Handles Server.OutputDataReceived
        If e.Data = Nothing Then Exit Sub
        If e.Data = ">" Or e.Data = "" Or e.Data.Contains("Can't keep up! Did the system time change, or is the server overloaded?") Or e.Data.Contains("[INFO] [Metrics] {0}") Then Exit Sub

        Dim m As Match

        ' 1.7 server output
        m = Regex.Match(e.Data, "^(?>\[\d\d:\d\d:\d\d\] \[(?<Source>[^/]+)/(?<Type>[^\]]+)\]: (?<Message>.*))")
        If m.Success Then
            If OnOutput(m.Groups("Message").Value, m.Groups("Type").Value, m.Groups("Source").Value, True) Then Return
            If m.Groups("Source").Value = "Server thread" Then : SayToAllChannels("$k9Server: $k03[" & m.Groups("Type").Value & "] " & ProcessMinecraftColours(m.Groups("Message").Value).Replace(ChrW(15), ChrW(15) & ChrW(3) & "03"), SayOptions.OpsOnly, {"!" & MyKey & "/MinecraftServerMessage"})
            Else : SayToAllChannels("$k9" & m.Groups("Source").Value & ": $k03[" & m.Groups("Type").Value & "] " & ProcessMinecraftColours(m.Groups("Message").Value).Replace(ChrW(15), ChrW(15) & ChrW(3) & "03"), SayOptions.OpsOnly, {"!" & MyKey & "/MinecraftServerMessage"})
            End If
        Else
            ' Legacy server output
            m = Regex.Match(e.Data, "\d\d:\d\d:\d\d \[(?<Type>INFO|WARNING|SEVERE)\] (?<Message>.*)")
            If m.Success Then
                If OnOutput(m.Groups("Message").Value, m.Groups("Type").Value, "Legacy server", True) Then Return
                SayToAllChannels("$k9Server: $k03" & ProcessMinecraftColours(e.Data).Replace(ChrW(15), ChrW(15) & ChrW(3) & "03"), SayOptions.OpsOnly, {"!" & MyKey & "/MinecraftServerMessage"})
            ElseIf Not e.Data.StartsWith("Reason: ") Then
                SayToAllChannels("$k9Process: $k03" & ProcessMinecraftColours(e.Data).Replace(ChrW(15), ChrW(15) & ChrW(3) & "03"), SayOptions.OpsOnly, {"!" & MyKey & "/MinecraftServerMessage"})
            End If
        End If
    End Sub

    Protected Sub ErrorDataReceived(ByVal sender As Object, ByVal e As DataReceivedEventArgs) Handles Server.ErrorDataReceived
        If e.Data = Nothing Then Exit Sub
        If e.Data = ">" Or e.Data = "" Or e.Data.Contains("Can't keep up! Did the system time change, or is the server overloaded?") Or e.Data.Contains("[INFO] [Metrics] {0}") Then Exit Sub

        Dim m As Match

        ' 1.7 server output
        m = Regex.Match(e.Data, "^(?>\[\d\d:\d\d:\d\d\] \[(?<Source>[^/]+)/(?<Type>[^\]]+)\]: (?<Message>.*))")
        If m.Success Then
            If OnOutput(m.Groups("Message").Value, m.Groups("Type").Value, m.Groups("Source").Value, True) Then Return
            If m.Groups("Source").Value = "Server thread" Then : SayToAllChannels("$k4Server: $k05[" & m.Groups("Type").Value & "] " & ProcessMinecraftColours(m.Groups("Message").Value).Replace(ChrW(15), ChrW(15) & ChrW(3) & "05"), SayOptions.OpsOnly, {"!" & MyKey & "/MinecraftServerMessage"})
            Else : SayToAllChannels("$k4" & m.Groups("Source").Value & ": $k05[" & m.Groups("Type").Value & "] " & ProcessMinecraftColours(m.Groups("Message").Value).Replace(ChrW(15), ChrW(15) & ChrW(3) & "05"), SayOptions.OpsOnly, {"!" & MyKey & "/MinecraftServerMessage"})
            End If
        Else
            ' Legacy server output
            m = Regex.Match(e.Data, "\d\d:\d\d:\d\d \[(?<Type>INFO|WARNING|SEVERE)\] (?<Message>.*)")
            If m.Success Then
                If OnOutput(m.Groups("Message").Value, m.Groups("Type").Value, "Legacy server", True) Then Return
                SayToAllChannels("$k4Server: $k05" & ProcessMinecraftColours(e.Data).Replace(ChrW(15), ChrW(15) & ChrW(3) & "05"), SayOptions.OpsOnly, {"!" & MyKey & "/MinecraftServerMessage"})
            Else
                SayToAllChannels("$k4Process: $k05" & ProcessMinecraftColours(e.Data).Replace(ChrW(15), ChrW(15) & ChrW(3) & "05"), SayOptions.OpsOnly, {"!" & MyKey & "/MinecraftServerMessage"})
            End If
        End If
    End Sub

    Private Function OnOutput(ByVal Message As String, ByVal MessageType As String, ByVal Source As String, ByVal StandardError As Boolean) As Boolean
        Try
            If MessageType = "INFO" And (Source = "Server thread" Or Source = "Legacy server") Then
                Dim m = System.Text.RegularExpressions.Regex.Match(Message, "^Starting minecraft server version (?<Version>.*)")
                If m.Success Then
                    ServerVersion = m.Groups("Version").Value
                    CBVersion = Nothing
                    NeedsUpdate = False
                End If

                m = System.Text.RegularExpressions.Regex.Match(Message, "^This server is running CraftBukkit version .* \(MC: (?<Version>.*)\) \(Implementing API version (?<CBVersion>.*)\)")
                If m.Success Then
                    ServerVersion = m.Groups("Version").Value
                    CBVersion = m.Groups("CBVersion").Value
                    NeedsUpdate = False
                End If

                m = System.Text.RegularExpressions.Regex.Match(Message, "^(\[(?<World>.*)\])? ?\<(?<Player>.*)\> (?<Message>.*)$")
                If m.Success Then
                    If MinecraftChatMessage(m.Groups("World").Value, m.Groups("Player").Value, m.Groups("Message").Value) Then Return True
                End If

                m = System.Text.RegularExpressions.Regex.Match(Message, "^\[(?<Player>.*) -\> me\] (?<Message>.*)")
                If m.Success Then
                    If MinecraftPrivateMessage(Nothing, m.Groups("Player").Value, m.Groups("Message").Value) Then Return True
                End If

                m = System.Text.RegularExpressions.Regex.Match(Message, "^(?<Name>.*?) ?\[/(?<Address>\d{1,3}.\d{1,3}.\d{1,3}.\d{1,3}:\d{1,5})\] logged in with entity id (?<EntityID>\d+) at \((\[(?<World>.*)\] )?(?<X>-?\d+(\.\d+)?), (?<Y>-?\d+(\.\d+)?), (?<Z>-?\d+(\.\d+)?)\)")
                If m.Success Then
                    Dim UUID As Guid = Nothing
                    If UUIDPending.ContainsKey(m.Groups("Name").Value) Then
                        UUID = UUIDPending(m.Groups("Name").Value)
                        UUIDPending.Remove(m.Groups("Name").Value)
                    End If
                    ' Check for an illegal player name.
                    If System.Text.RegularExpressions.Regex.IsMatch(m.Groups("Name").Value, "^\S+$") Then
                        OnlinePlayers.Add(m.Groups("Name").Value, New PlayerData With {.IP = m.Groups("Address").Value, .UUID = UUID, .LoginTime = Now, .EntityID = m.Groups("EntityID").Value})
                        If RelayLogins Then
                            SayToAllChannels(String.Format("$k12[+]$o {0}$o has joined the server.", IRCColours.NicknameColour(m.Groups("Name").Value) & m.Groups("Name").Value), 0, {"!" & MyKey & "/MinecraftServerMessage"})
                            SayToMinorChannels(MinorLabel & "$k12[+]$o " & String.Format("{0}$o has joined the server.", IRCColours.NicknameColour(m.Groups("Name").Value) & m.Groups("Name").Value), 0, {"!" & MyKey & "/MinecraftServerMessage"})
                            SayToAllChannels(String.Format("{0}$o connected from $k12{1}$o and spawned with entity ID $k12{2}$o in $k12{3}$o at ($k12{4}$o, $k12{5}$o, $k12{6}$o)", IRCColours.NicknameColour(m.Groups("Name").Value) & m.Groups("Name").Value, m.Groups("Address").Value, m.Groups("EntityID").Value, m.Groups("World").Value, m.Groups("X").Value, m.Groups("Y").Value, m.Groups("Z").Value), SayOptions.OpsOnly, {"!" & MyKey & "/MinecraftServerMessage"})
                        End If
                        OnPlayerJoin(m.Groups("Name").Value, m.Groups("Address").Value, m.Groups("EntityID").Value, m.Groups("World").Value, m.Groups("X").Value, m.Groups("Y").Value, m.Groups("Z").Value)
                        Return True
                    Else
                        If RelayLogins Then SayToAllChannels(String.Format("$k13[@]$o Rejecting {0}$o: illegal name.", IRCColours.NicknameColour(m.Groups("Name").Value) & m.Groups("Name").Value), 0, {"!" & MyKey & "/MinecraftServerMessage"})
                        Server.StandardInput.WriteLine("ban-ip " & m.Groups("Address").Value.Split(":"c)(0) & " Illegal name")
                        Server.StandardInput.WriteLine("pardon-ip " & m.Groups("Address").Value.Split(":"c)(0))
                        Return True
                    End If
                End If

                m = System.Text.RegularExpressions.Regex.Match(Message, "^(\[(?<Source>.*?): )?Kicked (?<Name>.*?) from the game: '(?<Reason>.*?)'")
                If m.Success Then
                    OnlinePlayers.Remove(m.Groups("Name").Value)
                    If m.Groups("Source").Success Then
                        SayToAllChannels(String.Format("$k4[-]$o {0}$o was $k4kicked out by {2}$o: {1}", IRCColours.NicknameColour(m.Groups("Name").Value) & m.Groups("Name").Value, ProcessMinecraftColours(m.Groups("Reason").Value.TrimEnd("]"c)), m.Groups("Source").Value), 0, {"!" & MyKey & "/MinecraftServerMessage"})
                        SayToMinorChannels(MinorLabel & "$k4[-]$o " & String.Format("{0}$o was $k4kicked out by {2}$o: {1}", IRCColours.NicknameColour(m.Groups("Name").Value) & m.Groups("Name").Value, ProcessMinecraftColours(m.Groups("Reason").Value.TrimEnd("]"c)), m.Groups("Source").Value), 0, {"!" & MyKey & "/MinecraftServerMessage"})
                    Else
                        SayToAllChannels(String.Format("$k4[-]$o {0}$o was $k4kicked out$o: {1}", IRCColours.NicknameColour(m.Groups("Name").Value) & m.Groups("Name").Value, ProcessMinecraftColours(m.Groups("Reason").Value.TrimEnd("]"c))), 0, {"!" & MyKey & "/MinecraftServerMessage"})
                        SayToMinorChannels(MinorLabel & "$k4[-]$o " & String.Format("{0}$o was $k4kicked out$o: {1}", IRCColours.NicknameColour(m.Groups("Name").Value) & m.Groups("Name").Value, ProcessMinecraftColours(m.Groups("Reason").Value.TrimEnd("]"c))), 0, {"!" & MyKey & "/MinecraftServerMessage"})
                    End If
                End If

                m = System.Text.RegularExpressions.Regex.Match(Message, "^(?<Name>.*) lost connection(: TextComponent\{text='(?<Reason>[^']*)'|: TranslatableComponent\{key='(?<Key>[^']*)', args=\[(?<Parameter>[^\]]*)|: (?<Message>.*))?")
                If m.Success Then
                    Dim DisconnectMessage As String = Nothing, Parameter As String = Nothing
                    If m.Groups("Reason").Success Then : DisconnectMessage = m.Groups("Reason").Value
                    ElseIf m.Groups("Key").Success Then
                        DisconnectMessage = m.Groups("Key").Value
                        If m.Groups("Parameter").Success Then Parameter = m.Groups("Parameter").Value
                    ElseIf m.Groups("Message").Success Then : DisconnectMessage = m.Groups("Message").Value
                    End If

                    If AuthenticationTimer IsNot Nothing And m.Groups("Name").Value = "Andrio_Celos" Then
                        AuthenticationTimer.Stop()
                        CurrentAuthenticate = Nothing
                    End If

                    If OnlinePlayers.ContainsKey(m.Groups("Name").Value) Then
                        OnlinePlayers.Remove(m.Groups("Name").Value)
                        If DisconnectMessage Is Nothing Then
                            SayToAllChannels(String.Format("$k4[-]$o {0}$o has disconnected.", IRCColours.NicknameColour(m.Groups("Name").Value) & m.Groups("Name").Value), 0, {"!" & MyKey & "/MinecraftServerMessage"})
                            SayToMinorChannels(MinorLabel & "$k4[-]$o " & String.Format("{0}$o has disconnected.", IRCColours.NicknameColour(m.Groups("Name").Value) & m.Groups("Name").Value), 0, {"!" & MyKey & "/MinecraftServerMessage"})
                        Else
                            If RelayLogins Then
                                Select Case DisconnectMessage
                                    Case "disconnect.quitting"
                                        SayToAllChannels(String.Format("$k4[-]$o {0}$o has $k4left the server$o.", IRCColours.NicknameColour(m.Groups("Name").Value) & m.Groups("Name").Value), 0, {"!" & MyKey & "/MinecraftServerMessage"})
                                        SayToMinorChannels(MinorLabel & "$k4[-]$o " & String.Format("{0}$o has $k4left the server$o.", IRCColours.NicknameColour(m.Groups("Name").Value) & m.Groups("Name").Value), 0, {"!" & MyKey & "/MinecraftServerMessage"})
                                    Case "disconnect.endOfStream"
                                        SayToAllChannels(String.Format("$k4[-]$o {0}$o $k4lost their connection$o.", IRCColours.NicknameColour(m.Groups("Name").Value) & m.Groups("Name").Value), 0, {"!" & MyKey & "/MinecraftServerMessage"})
                                        SayToMinorChannels(MinorLabel & "$k4[-]$o " & String.Format("{0}$o $k4lost their connection$o.", IRCColours.NicknameColour(m.Groups("Name").Value) & m.Groups("Name").Value), 0, {"!" & MyKey & "/MinecraftServerMessage"})
                                    Case "disconnect.overflow"
                                        SayToAllChannels(String.Format("$k4[-]$o {0}$o was disconnected due to a $k4buffer overflow$o.", IRCColours.NicknameColour(m.Groups("Name").Value) & m.Groups("Name").Value), 0, {"!" & MyKey & "/MinecraftServerMessage"})
                                        SayToMinorChannels(MinorLabel & "$k4[-]$o " & String.Format("{0}$o was disconnected due to a $k4buffer overflow$o.", IRCColours.NicknameColour(m.Groups("Name").Value) & m.Groups("Name").Value), 0, {"!" & MyKey & "/MinecraftServerMessage"})
                                    Case "disconnect.spam"
                                        SayToAllChannels(String.Format("$k4[-]$o {0}$o was $k4kicked out for spamming$o.", IRCColours.NicknameColour(m.Groups("Name").Value) & m.Groups("Name").Value), 0, {"!" & MyKey & "/MinecraftServerMessage"})
                                        SayToMinorChannels(MinorLabel & "$k4[-]$o " & String.Format("{0}$o was $k4kicked out for spamming$o.", IRCColours.NicknameColour(m.Groups("Name").Value) & m.Groups("Name").Value), 0, {"!" & MyKey & "/MinecraftServerMessage"})
                                    Case "disconnect.genericReason"
                                        If Parameter IsNot Nothing Then
                                            SayToAllChannels(String.Format("$k4[-]$o {0}$o has disconnected: $k4{1}$o.", IRCColours.NicknameColour(m.Groups("Name").Value) & m.Groups("Name").Value, Parameter), 0, {"!" & MyKey & "/MinecraftServerMessage"})
                                            SayToMinorChannels(MinorLabel & "$k4[-]$o " & String.Format("{0}$o has disconnected: $k4{1}$o.", IRCColours.NicknameColour(m.Groups("Name").Value) & m.Groups("Name").Value, Parameter), 0, {"!" & MyKey & "/MinecraftServerMessage"})
                                        Else
                                            SayToAllChannels(String.Format("$k4[-]$o {0}$o has $k4disconnected$o.", IRCColours.NicknameColour(m.Groups("Name").Value) & m.Groups("Name").Value), 0, {"!" & MyKey & "/MinecraftServerMessage"})
                                            SayToMinorChannels(MinorLabel & "$k4[-]$o " & String.Format("{0}$o has $k4disconnected$o.", IRCColours.NicknameColour(m.Groups("Name").Value) & m.Groups("Name").Value), 0, {"!" & MyKey & "/MinecraftServerMessage"})
                                        End If
                                    Case Else
                                        SayToAllChannels(String.Format("$k4[-]$o {0}$o has disconnected: $k4{1}$o.", IRCColours.NicknameColour(m.Groups("Name").Value) & m.Groups("Name").Value, DisconnectMessage), 0, {"!" & MyKey & "/MinecraftServerMessage"})
                                        SayToMinorChannels(MinorLabel & "$k4[-]$o " & String.Format("{0}$o has disconnected: $k4{1}$o.", IRCColours.NicknameColour(m.Groups("Name").Value) & m.Groups("Name").Value, DisconnectMessage), 0, {"!" & MyKey & "/MinecraftServerMessage"})
                                End Select
                            End If
                        End If
                    End If

                    If Identifications.ContainsKey("!" & MyKey & "/" & m.Groups("Name").Value) Then
                        If Identifications("!" & MyKey & "/" & m.Groups("Name").Value).Channels.Contains("MinecraftServerMessage/") Then
                            Identifications("!" & MyKey & "/" & m.Groups("Name").Value).Channels.Remove("MinecraftServerMessage/")
                            If Identifications("!" & MyKey & "/" & m.Groups("Name").Value).Channels.Count = 0 Then
                                Identifications.Remove("!" & MyKey & "/" & m.Groups("Name").Value)
                            End If
                        End If
                    End If
                    Return True
                End If

            ElseIf Source.StartsWith("User Authenticator ") And MessageType = "INFO" Then
                Dim m = System.Text.RegularExpressions.Regex.Match(Message, "^UUID of player (?<Player>.*?) is (?<UUID>[0-9a-f]{32})$")
                If m.Success Then UUIDPending.Add(m.Groups("Player").Value, New Guid(m.Groups("UUID").Value))
            End If
        Catch ex As Exception
            LogError("OnOutput", ex)
        End Try
        Return False
    End Function

    Dim SecurityChallenge As String, SecurityAnswer As String
    Dim CurrentAuthenticate As String
    Public WithEvents AuthenticationTimer As Timer
    Private FailedAuthenticates As New Dictionary(Of String, Short)
    Private Sub OnPlayerJoin(ByVal PlayerName As String, ByVal Address As String, ByVal EntityID As Integer, ByVal WorldName As String, ByVal x As Double, ByVal y As Double, ByVal z As Double)
        ' Check for an operator.
        'TODO: Make this general.
        Dim Authenticate = Address.Split(":"c)(0)
        If PlayerName = "Andrio_Celos" Then
            If OnlinePlayers(PlayerName).UUID.ToString("N") <> "ef56c41b3a8241ffb75133e66c9dfa6b" Then
                ' My master has logged in. Let's check if he's really my master.
                ' Generate a security challenge.
                ' The challenge works as follows: generate three numbers, and show them to the player.
                ' Then wait for the player to send a number back in the chat.
                ' It should be T1 + T2 * T3. If so, authentication succeeds.
                CurrentAuthenticate = Authenticate
                Server.StandardInput.WriteLine("deop Andrio_Celos")
                Dim t1 As Short, t2 As Short, t3 As Short
                Randomize()
                t1 = Int(Rnd() * 15 + 1)
                t2 = Int(Rnd() * 15 + 1)
                t3 = Int(Rnd() * 15 + 1)
                SecurityChallenge = String.Join(" ", t1, t2, t3)
                SecurityAnswer = (t1 + t2 * t3).ToString
                Server.StandardInput.WriteLine(String.Format("tell Andrio_Celos <Angelina> {0} {1} {2}", t1, t2, t3))
                AuthenticationTimer = New Timer(15000)
                AuthenticationTimer.Start()
            Else
                Server.StandardInput.WriteLine("tell Andrio_Celos <Angelina> You are recognised by your UUID.")
                AuthenticationSuccess()
            End If
        End If
    End Sub
    Private Sub AuthenticationTimeout(ByVal sender As Object, ByVal e As ElapsedEventArgs) Handles AuthenticationTimer.Elapsed
        AuthenticationFailure()
    End Sub
    Private Sub AuthenticationSuccess()
        AuthenticationTimer.Stop()

        Dim id = New Identification With {.AccountName = "Andrio", .Channels = New List(Of String)}
        id.Channels.Add("MinecraftServerMessage/")
        Identifications.Add("!" & MyKey & "/Andrio_Celos", id)
        If FailedAuthenticates.ContainsKey(CurrentAuthenticate) Then FailedAuthenticates.Remove(CurrentAuthenticate)

        AuthenticationTimer.Dispose()
        AuthenticationTimer = Nothing
        CurrentAuthenticate = Nothing
    End Sub
    Private Sub AuthenticationFailure()
        Dim Count As Short
        If FailedAuthenticates.ContainsKey(CurrentAuthenticate) Then
            Count = FailedAuthenticates(CurrentAuthenticate)
            Count += 1
            FailedAuthenticates(CurrentAuthenticate) = Count
        Else
            Count = 1
            FailedAuthenticates.Add(CurrentAuthenticate, Count)
        End If
        ' Kick them off the server.
        If Count >= 3 Then
            Server.StandardInput.WriteLine("ban-ip " & CurrentAuthenticate & " <Angelina> You failed to authenticate too many times. Begone, impostor.")
        Else
            Server.StandardInput.WriteLine("kick Andrio_Celos <Angelina> You failed to authenticate in time.")
        End If

        AuthenticationTimer.Dispose()
        AuthenticationTimer = Nothing
    End Sub

    <Regex({"Who.* on the (Minecraft |(Craft)?Bukkit )?server\??",
            "List .* (player|adventurer|people|Minecraftian|everyone).* on the (Minecraft |(Craft)?Bukkit )?server\??"},
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
            Say(Connection, Channel, Choose("There is no one ", "There are no " & Choose("players ", "people ", "adventurers ", "Minecraftians ")) & Choose("online", "on the server") & Choose("at the moment", "right now", "at this time", "", "") & ".",
                "No " & Choose("players ", "people ", "adventurers ", "Minecraftians ") & "are " & Choose("online", "on the server") & Choose("at the moment", "right now", "at this time", "", "") & ".")
        Else
            Say(Connection, Channel, "The following " & Choose("players ", "people ", "adventurers ", "Minecraftians ") & "are " & Choose("online", "on the server") & Choose("at the moment", "right now", "at this time", "", "") & ":")
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
                Else
                    SayToAllChannels("$k6[@]$o Autosaving the world; brace for a little lag.")
                    Server.StandardInput.WriteLine("say Autosaving the world; brace for a little lag.")
                    Server.StandardInput.WriteLine("save-all")
                End If
            End If
        ElseIf AutoRestartInProgress And e.SignalTime.Hour = 0 And e.SignalTime.Minute = 5 Then
            If IsRunning() Then
                'TODO: Message the channel.
                Server.Kill()
                Server.WaitForExit()
            End If

            If My.Computer.FileSystem.FileExists(My.Computer.FileSystem.CombinePath(WorkingDirectory, "server.log")) Then
                Dim LogsMonthDirectory = My.Computer.FileSystem.CombinePath(WorkingDirectory, "logs\" & Today.ToString("yyyy-MM"))

                If Not My.Computer.FileSystem.DirectoryExists(LogsMonthDirectory) Then _
                    My.Computer.FileSystem.CreateDirectory(LogsMonthDirectory)

                Dim LogFile = My.Computer.FileSystem.CombinePath(LogsMonthDirectory, "server-" & Today.ToString("yyyy-MM-dd") & ".log")

                My.Computer.FileSystem.WriteAllText(LogFile, My.Computer.FileSystem.ReadAllText(My.Computer.FileSystem.CombinePath(WorkingDirectory, "server.log")), True)
                My.Computer.FileSystem.DeleteFile(My.Computer.FileSystem.CombinePath(WorkingDirectory, "server.log"))
            End If

            Start()
            AutoRestartInProgress = False
        ElseIf e.SignalTime.Hour = 0 And e.SignalTime.Minute = 30 Then
            CheckForUpdates(CBUpdateHost, CBUpdateCheckPath_RB)
        End If
    End Sub

    <Command({"set", "config", "property"}, 1, 2,
        "set <property> <value>",
        "Changes server parameters. If the server is still running, this won't do anything until you restart it." & vbCrLf &
        "You can set the following properties: $k11workingdir$o, $k11java$o, $k11jar$o, $k11xms$o, $k11xmx$o." & vbCrLf &
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
            Case "javafilename", "javaexe", "java", "javalocation", "javaloc"
                If lValue = Nothing Then
                    Say(Connection, Channel, "I will start Java from $k12" & ExecutablePath & "$o.")
                Else
                    ExecutablePath = lValue
                    Say(Connection, Channel, "I will now start Java from $k12" & ExecutablePath & "$o.")
                End If
            Case "jarfilename", "jar", "jarloc", "jarlocation"
                If lValue = Nothing Then
                    Say(Connection, Channel, "I will start the Minecraft server from $k12" & JarFilename & "$o.")
                Else
                    JarFilename = lValue
                    Say(Connection, Channel, "I will now start the Minecraft server from $k12" & JarFilename & "$o.")
                End If
            Case "xms", "ms", "initram", "initheap", "initmemory", "initialram", "initialheap", "initialmemory"
                If lValue = Nothing Then
                    Say(Connection, Channel, "I'll set the initial heap size to $k12" & InitMemoryHeap & "$o.")
                Else
                    InitMemoryHeap = lValue
                    Say(Connection, Channel, "I'll set the initial heap size to $k12" & InitMemoryHeap & "$o.")
                End If
            Case "xmx", "mx", "maxram", "maxheap", "maxmemory", "maximumram", "maximumheap", "maximummemory"
                If lValue = Nothing Then
                    Say(Connection, Channel, "I'll set the maximum heap size to $k12" & MaxMemoryHeap & "$o.")
                Else
                    MaxMemoryHeap = lValue
                    Say(Connection, Channel, "I'll set the maximum heap size to $k12" & MaxMemoryHeap & "$o.")
                End If
            Case "xincgc", "incgc", "incrementalgc"
                If lValue = Nothing Then
                    If Xincgc Then
                        Say(Connection, Channel, Choose("I will $k9enable$o " & Choose("Java's garbage collector", "incremental garbage collection") & ".", Choose("Java's garbage collector", "Incremental garbage collection") & " will be $k9enabled$o."))
                    Else
                        Say(Connection, Channel, Choose("I will $k4disable$o " & Choose("Java's garbage collector", "incremental garbage collection") & ".", Choose("Java's garbage collector", "Incremental garbage collection") & " will be $k4disabled$o."))
                    End If
                Else
                    If {"1", "-1", "on", "yes", "true", "+", "enable", "enabled", "activate", "activated", "active"}.Contains(lValue.ToLower) Then
                        Xincgc = True
                        Say(Connection, Channel, Choose("I will now $k9enable$o " & Choose("Java's garbage collector", "incremental garbage collection") & ".", Choose("Java's garbage collector", "Incremental garbage collection") & " will now be $k9enabled$o."))
                    ElseIf {"0", "off", "no", "false", "-", "disable", "disabled", "deactivate", "deactivated", "inactive"}.Contains(lValue.ToLower) Then
                        Xincgc = False
                        Say(Connection, Channel, Choose("I will now $k4disable$o " & Choose("Java's garbage collector", "incremental garbage collection") & ".", Choose("Java's garbage collector", "Incremental garbage collection") & " will now be $k4disabled$o."))
                    Else
                        Reply(Connection, Channel, Sender, VBot.Choose("", "Sorry " & Sender.Split("!"c)(0) & ", ") & "I don't recognise " & VBot.Choose(IRCColours.Red & lValue & "$o", "that") & " as a Boolean value. Please enter $k11on$o or $k11off$o.")
                    End If
                End If
            Case "autostart"
                If lValue = Nothing Then
                    If AutoStart Then
                        Say(Connection, Channel, "I will $k9automatically start the server$o when the plugin is loaded.")
                    Else
                        Say(Connection, Channel, "I will $k4wait for your command$o to start the server.")
                    End If
                Else
                    If {"1", "-1", "on", "yes", "true", "+", "enable", "enabled", "activate", "activated", "active"}.Contains(lValue.ToLower) Then
                        AutoStart = True
                        Say(Connection, Channel, "I will now $k9automatically start the server$o when the plugin is loaded.")
                    ElseIf {"0", "off", "no", "false", "-", "disable", "disabled", "deactivate", "deactivated", "inactive"}.Contains(lValue.ToLower) Then
                        AutoStart = False
                        Say(Connection, Channel, "I will now $k4wait for your command$o to start the server.")
                    Else
                        Reply(Connection, Channel, Sender, VBot.Choose("", "Sorry " & Sender.Split("!"c)(0) & ", ") & "I don't recognise " & VBot.Choose(IRCColours.Red & lValue & "$o", "that") & " as a Boolean value. Please enter $k11on$o or $k11off$o.")
                    End If
                End If
            Case "relayserverchat", "relayminecraftchat", "relayserver", "relayminecraft", "relaymcchat", "relaymc"
                If lValue = Nothing Then
                    If AutoStart Then : Say(Connection, Channel, "Chat from the Minecraft server $k9will$o be relayed.")
                    Else : Say(Connection, Channel, "Chat from the Minecraft server $k4will not$o be relayed.")
                    End If
                Else
                    If {"1", "-1", "on", "yes", "true", "+", "enable", "enabled", "activate", "activated", "active"}.Contains(lValue.ToLower) Then
                        RelayServerChat = True
                        Say(Connection, Channel, "I will now $k9relay$o chat from the Minecraft server.")
                    ElseIf {"0", "off", "no", "false", "-", "disable", "disabled", "deactivate", "deactivated", "inactive"}.Contains(lValue.ToLower) Then
                        RelayServerChat = False
                        Say(Connection, Channel, "I will $k4no longer relay$o chat from the Minecraft server.")
                    Else
                        Reply(Connection, Channel, Sender, VBot.Choose("", "Sorry " & Sender.Split("!"c)(0) & ", ") & "I don't recognise " & VBot.Choose(IRCColours.Red & lValue & "$o", "that") & " as a Boolean value. Please enter $k11on$o or $k11off$o.")
                    End If
                End If
            Case "relayircchat", "relayirc"
                If lValue = Nothing Then
                    If AutoStart Then : Say(Connection, Channel, "Chat from the IRC channel $k9will$o be relayed.")
                    Else : Say(Connection, Channel, "Chat from the IRC channel $k4will not$o be relayed.")
                    End If
                Else
                    If {"1", "-1", "on", "yes", "true", "+", "enable", "enabled", "activate", "activated", "active"}.Contains(lValue.ToLower) Then
                        RelayIRCChat = True
                        Say(Connection, Channel, "I will now $k9relay$o chat from the IRC channel.")
                    ElseIf {"0", "off", "no", "false", "-", "disable", "disabled", "deactivate", "deactivated", "inactive"}.Contains(lValue.ToLower) Then
                        RelayIRCChat = False
                        Say(Connection, Channel, "I will $k4no longer relay$o chat from the IRC channel.")
                    Else
                        Reply(Connection, Channel, Sender, VBot.Choose("", "Sorry " & Sender.Split("!"c)(0) & ", ") & "I don't recognise " & VBot.Choose(IRCColours.Red & lValue & "$o", "that") & " as a Boolean value. Please enter $k11on$o or $k11off$o.")
                    End If
                End If
            Case Else
                ' It's a server.properties property.
                If WorkingDirectory = Nothing And Not (System.IO.Path.IsPathRooted(ServerConfigFile) Or ServerConfigFile.StartsWith("%")) Then
                    Reply(Connection, Channel, Sender, "The working directory is not set.")
                    Return
                End If

                Dim Key As String, GetFormat As String, SetFormat As String, DeleteFormat As String
                Select Case lProperty.ToLower.Replace("_", "").Replace("-", "").Replace(" ", "")
                    Case "allowflight", "allowflying", "flight", "flying", "allowfly", "allowflymod", "fly", "flymod"
                        Key = "allow-flight"
                        GetFormat = Choose("Flying is $k12{0}$o.")
                        SetFormat = Choose("Flying is now $k09{0}$o.")
                        DeleteFormat = Choose("Flying is now $k3disabled$o.")
                        Select Case lValue.ToLower
                            Case "1", "on", "yes", "enabled", "enable", "activated", "allow", "allowed"
                                lValue = "true"
                            Case "0", "off", "no", "disabled", "disable", "banned", "ban", "prevented", "prevent", "denied", "deny"
                                lValue = "false"
                            Case Else
                                Reply(Connection, Channel, Sender, String.Format(Choose("$k04{0}$o ", "That ") & Choose("is not ", "isn't ") & "a valid setting. Use $k11yes$o or $k11no$o.", lValue))
                                lValue = Nothing
                        End Select
                    Case "allownether", "enablenether", "nether", "allowhell", "enablehell", "hell"
                        Key = "allow-nether"
                        GetFormat = Choose("The Nether is $k12{0}$o.")
                        SetFormat = Choose("The Nether is now $k09{0}$o.")
                        DeleteFormat = Choose("The Nether is now $k3enabled$o.")
                        Select Case lValue.ToLower
                            Case "1", "on", "yes", "enabled", "enable", "activated", "allow", "allowed"
                                lValue = "true"
                            Case "0", "off", "no", "disabled", "disable", "banned", "ban", "prevented", "prevent", "denied", "deny"
                                lValue = "false"
                            Case Else
                                Reply(Connection, Channel, Sender, String.Format(Choose("$k04{0}$o ", "That ") & Choose("is not ", "isn't ") & "a valid setting. Use $k11yes$o or $k11no$o.", lValue))
                                lValue = Nothing
                        End Select
                    Case "difficulty"
                        Key = "difficulty"
                        GetFormat = Choose("The server is on $k12{0}$o difficulty.")
                        SetFormat = Choose("The server is now on $k09{0}$ difficulty.")
                        DeleteFormat = Choose("The server is now on $k3easy$ difficulty.")
                        Select Case lValue.ToLower
                            Case "0", "peaceful"
                                lValue = "0"
                            Case "1", "easy"
                                lValue = "1"
                            Case "2", "normal", "medium"
                                lValue = "2"
                            Case "3", "hard"
                                lValue = "3"
                            Case Else
                                Reply(Connection, Channel, Sender, String.Format(Choose("$k04{0}$o ", "That ") & Choose("is not ", "isn't ") & "a valid setting. Use one of: $k11peaceful$o, $k11easy$o, $k11normal$o, $k11hard$o.", lValue))
                                lValue = Nothing
                        End Select
                    Case "enablecommandblock", "enablecommandblocks", "commandblock", "commandblocks", "enablecmdblock", "enablecmdblocks", "cmdblock", "cmdblocks"
                        Key = "enable-command-block"
                        GetFormat = Choose("Command blocks are $k12{0}$o.")
                        SetFormat = Choose("Command blocks are now $k09{0}$o.")
                        DeleteFormat = Choose("Command blocks are now $k3disabled$o.")
                        Select Case lValue.ToLower
                            Case "1", "on", "yes", "enabled", "enable", "activated", "allow", "allowed"
                                lValue = "true"
                            Case "0", "off", "no", "disabled", "disable", "banned", "ban", "prevented", "prevent", "denied", "deny"
                                lValue = "false"
                            Case Else
                                Reply(Connection, Channel, Sender, String.Format(Choose("$k04{0}$o ", "That ") & Choose("is not ", "isn't ") & "a valid setting. Use $k11yes$o or $k11no$o.", lValue))
                                lValue = Nothing
                        End Select
                    Case "enablequery", "query"
                        Key = "enable-query"
                        GetFormat = Choose("The query server is $k12{0}$o.")
                        SetFormat = Choose("The query server is now $k09{0}$o.")
                        DeleteFormat = Choose("The query server is now $k3disabled$o.")
                        Select Case lValue.ToLower
                            Case "1", "on", "yes", "enabled", "enable", "activated", "allow", "allowed"
                                lValue = "true"
                            Case "0", "off", "no", "disabled", "disable", "banned", "ban", "prevented", "prevent", "denied", "deny"
                                lValue = "false"
                            Case Else
                                Reply(Connection, Channel, Sender, String.Format(Choose("$k04{0}$o ", "That ") & Choose("is not ", "isn't ") & "a valid setting. Use $k11yes$o or $k11no$o.", lValue))
                                lValue = Nothing
                        End Select
                    Case "enablercon", "rcon"
                        Key = "enable-rcon"
                        GetFormat = Choose("The RCon server is $k12{0}$o.")
                        SetFormat = Choose("The RCon server is now $k09{0}$o.")
                        DeleteFormat = Choose("The RCon server is now $k3disabled$o.")
                        Select Case lValue.ToLower
                            Case "1", "on", "yes", "enabled", "enable", "activated", "allow", "allowed"
                                lValue = "true"
                            Case "0", "off", "no", "disabled", "disable", "banned", "ban", "prevented", "prevent", "denied", "deny"
                                lValue = "false"
                            Case Else
                                Reply(Connection, Channel, Sender, String.Format(Choose("$k04{0}$o ", "That ") & Choose("is not ", "isn't ") & "a valid setting. Use $k11yes$o or $k11no$o.", lValue))
                                lValue = Nothing
                        End Select
                    Case "gamemode", "mode"
                        Key = "gamemode"
                        GetFormat = Choose("The default game mode is $k12{0}$o mode.")
                        SetFormat = Choose("The default game mode is now $k09{0}$o mode.")
                        DeleteFormat = Choose("The default game mode is now $k3Survival$o mode.")
                        Select Case lValue.ToLower
                            Case "0", "survival", "s"
                                lValue = "0"
                            Case "1", "creative", "c"
                                lValue = "1"
                            Case "2", "adventure", "a"
                                lValue = "2"
                            Case Else
                                Reply(Connection, Channel, Sender, String.Format(Choose("$k04{0}$o ", "That ") & Choose("is not ", "isn't ") & "a valid setting. Use one of: $k11Survival$o, $k11Creative$o, $k11Adventure$o.", lValue))
                                lValue = Nothing
                        End Select
                    Case "generatestructures", "genstructures", "structures"
                        Key = "generate-structures"
                        GetFormat = Choose("Generated structures are $k12{0}$o.")
                        SetFormat = Choose("Generated structures are now $k09{0}$o.")
                        DeleteFormat = Choose("Generated structures are now $k3enabled$o.")
                        Select Case lValue.ToLower
                            Case "1", "on", "yes", "enabled", "enable", "activated", "allow", "allowed"
                                lValue = "true"
                            Case "0", "off", "no", "disabled", "disable", "banned", "ban", "prevented", "prevent", "denied", "deny"
                                lValue = "false"
                            Case Else
                                Reply(Connection, Channel, Sender, String.Format(Choose("$k04{0}$o ", "That ") & Choose("is not ", "isn't ") & "a valid setting. Use $k11yes$o or $k11no$o.", lValue))
                                lValue = Nothing
                        End Select
                    Case "generatorsettings", "generatorcode", "superflatsettings", "superflatcode", "flatsettings", "flatcode"
                        Key = "generator-settings"
                        GetFormat = "The Superflat preset code to be used is $k12{0}$o."
                        SetFormat = "Changed the superflat preset code."
                        DeleteFormat = "Blanked the superflat preset code."
                    Case "hardcore"
                        Key = "hardcore"
                        GetFormat = "Hardcore mode is $k12{0}$o."
                        SetFormat = "Hardcore mode is now $k12{0}$o."
                        SetFormat = "Hardcore mode is now $k3disabled$o."
                        Select Case lValue.ToLower
                            Case "1", "on", "yes", "enabled", "enable", "activated", "allow", "allowed"
                                lValue = "true"
                            Case "0", "off", "no", "disabled", "disable", "banned", "ban", "prevented", "prevent", "denied", "deny"
                                lValue = "false"
                            Case Else
                                Reply(Connection, Channel, Sender, String.Format(Choose("$k04{0}$o ", "That ") & Choose("is not ", "isn't ") & "a valid setting. Use $k11yes$o or $k11no$o.", lValue))
                                lValue = Nothing
                        End Select
                    Case "levelname", "worldname"
                        Key = "level-name"
                        GetFormat = "The world will be loaded from $k12{0}$o."
                        SetFormat = "The world will now be loaded from $k09{0}$o."
                        DeleteFormat = "The world will now be loaded from $k3world$o."
                    Case "levelseed", "worldseed", "seed"
                        Key = "level-seed"
                        GetFormat = "The random seed to be used is $k12{0}$o."
                        SetFormat = "Changed the random seed."
                        DeleteFormat = "Blanked the random seed."
                    Case "leveltype", "worldtype"
                        Key = "level-type"
                        GetFormat = Choose("The world type is $k12{0}$o.")
                        SetFormat = Choose("The world type is now $k09{0}$o.")
                        DeleteFormat = Choose("The world type is now $k3default$o.")
                        Select Case lValue.ToLower
                            Case "0", "default", "normal"
                                lValue = "DEFAULT"
                            Case "1", "superflat", "flat", "super flat", "super-flat"
                                lValue = "FLAT"
                            Case "2", "largebiomes", "large biomes", "large-biomes"
                                lValue = "LARGEBIOMES"
                            Case Else
                                Reply(Connection, Channel, Sender, String.Format(Choose("$k04{0}$o ", "That ") & Choose("is not ", "isn't ") & "a valid setting. Use one of: $k11default$o, $k11Superflat$o, $k11large biomes$o.", lValue))
                                lValue = Nothing
                        End Select
                    Case "maxbuildheight", "maxheight", "maxworldheight", "worldheight", "height"
                        Key = "max-build-height"
                        GetFormat = Choose("The maximum building height will be $k12{0}$o metres.")
                        GetFormat = Choose("The maximum building height will now be $k09{0}$o metres.")
                        DeleteFormat = Choose("The maximum building height will now be $k03256$o metres.")

                        If lValue IsNot Nothing And lValue <> "" Then
                            Dim MaxHeight As Integer
                            If Not Integer.TryParse(lValue, MaxHeight) Then
                                Say(Connection, Channel, Choose("That isn't a valid integer.", "The setting must be an integer."))
                                lValue = Nothing
                            ElseIf MaxHeight < 1 Or MaxHeight > 256 Then
                                Say(Connection, Channel, Choose("The setting must be between $b1$b and $b256$b."))
                                lValue = Nothing
                            Else
                                lValue = MaxHeight
                            End If
                        End If
                    Case "maxplayers", "players", "maximumplayers", "numplayers", "numslots", "slots", "maxslots"
                        Key = "max-players"
                        GetFormat = Choose("The server will support up to $k12{0}$o players.", "Up to $k12{0}$o players are allowed on the server.", "The maximum number of players is $k12{0}$o.")
                        SetFormat = Choose("The server will now support up to $k09{0}$o players.", "Up to $k09{0}$o players are now allowed on the server.", "The maximum number of players is now $k09{0}$o.")
                        DeleteFormat = Choose("The player limit has been removed.", "Removed the player limit.")

                        If lValue IsNot Nothing And lValue <> "" Then
                            Dim MaxPlayers As Integer
                            If Not Integer.TryParse(lValue, MaxPlayers) Then
                                Say(Connection, Channel, Choose("That isn't a valid integer.", "The setting must be an integer."))
                                lValue = Nothing
                            ElseIf MaxPlayers < 1 Then
                                Say(Connection, Channel, Choose("The setting cannot be less than $b1$b."))
                                lValue = Nothing
                            Else
                                lValue = MaxPlayers
                            End If
                        End If
                    Case "motd", "greeting", "messageoftheday"
                        Key = "motd"
                        GetFormat = "The MotD is $k12{0}$o."
                        SetFormat = "The MotD has been changed."
                        DeleteFormat = Choose("The default MotD will be restored.", "The MotD has been reset.")
                    Case "onlinemode", "online", "verifynames"
                        Key = "online-mode"
                        GetFormat = "Online mode is $k12{0}$o."
                        SetFormat = "Online mode is now $k12{0}$o."
                        DeleteFormat = "Online mode is now $k3enabled$o."
                        Select Case lValue.ToLower
                            Case "1", "on", "yes", "enabled", "enable"
                                lValue = "true"
                            Case "0", "off", "no", "disabled", "disable"
                                lValue = "false"
                            Case Else
                                Reply(Connection, Channel, Sender, String.Format(Choose("$k04{0}$o ", "That ") & Choose("is not ", "isn't ") & "a valid setting. Use $k11yes$o or $k11no$o.", lValue))
                                lValue = Nothing
                        End Select
                    Case "offlinemode", "offline", "crackedmode", "cracked"
                        Key = "online-mode"
                        GetFormat = "Online mode is $k12{0}$o."
                        SetFormat = "Online mode is now $k12{0}$o."
                        DeleteFormat = "Online mode is now $k3enabled$o."
                        Select Case lValue.ToLower
                            Case "1", "on", "yes", "enabled", "enable"
                                lValue = "false"
                            Case "0", "off", "no", "disabled", "disable"
                                lValue = "true"
                            Case Else
                                Reply(Connection, Channel, Sender, String.Format(Choose("$k04{0}$o ", "That ") & Choose("is not ", "isn't ") & "a valid setting. Use $k11yes$o or $k11no$o.", lValue))
                                lValue = Nothing
                        End Select
                    Case "pvp", "playervsplayer", "playervplayer"
                        Key = "pvp"
                        GetFormat = "PvP is $k12{0}$o."
                        SetFormat = "PvP is now $k12{0}$o."
                        DeleteFormat = "PvP is now $k3enabled$o."
                        Select Case lValue.ToLower
                            Case "1", "on", "yes", "enabled", "enable"
                                lValue = "false"
                            Case "0", "off", "no", "disabled", "disable"
                                lValue = "true"
                            Case Else
                                Reply(Connection, Channel, Sender, String.Format(Choose("$k04{0}$o ", "That ") & Choose("is not ", "isn't ") & "a valid setting. Use $k11yes$o or $k11no$o.", lValue))
                                lValue = Nothing
                        End Select
                    Case "queryport", "querylisteningport", "queryserverport"
                        Key = "query.port"
                        GetFormat = "The query server " & Choose("will ", "is set to ") & Choose("listen ", "accept connections ") & "on port $k12{0}$o."
                        SetFormat = "The query server " & Choose("will now ", "is now set to ") & Choose("listen ", "accept connections ") & "on port $k09{0}$o."
                        DeleteFormat = "The query server " & Choose("will now ", "is now set to ") & Choose("listen ", "accept connections ") & "on port $k0325565$o."
                    Case "rconpassword", "rconpass", "rconserverpass", "rconserverpassword"
                        Key = "rcon.password"
                        GetFormat = "The RCon server password is $k12{0}$o."
                        SetFormat = "The RCon server password has been changed."
                        DeleteFormat = "Blanked the RCon server password."
                    Case "rconport", "rconlisteningport", "rconserverport"
                        Key = "rcon.port"
                        GetFormat = "The RCon server " & Choose("will ", "is set to ") & Choose("listen ", "accept connections ") & "on port $k12{0}$o."
                        SetFormat = "The RCon server " & Choose("will now ", "is now set to ") & Choose("listen ", "accept connections ") & "on port $k09{0}$o."
                        DeleteFormat = "The RCon server " & Choose("will now ", "is now set to ") & Choose("listen ", "accept connections ") & "on port $k0325575$o."
                    Case "serverport", "port", "gameport", "listenport", "listenerport", "listeningport"
                        Key = "server-port"
                        GetFormat = "The server " & Choose("will ", "is set to ") & Choose("listen ", "accept connections ") & "on port $k12{0}$o."
                        SetFormat = "The server " & Choose("will now ", "is now set to ") & Choose("listen ", "accept connections ") & "on port $k09{0}$o."
                        DeleteFormat = "The server " & Choose("will now ", "is now set to ") & Choose("listen ", "accept connections ") & "on port $k0325565$o."
                    Case "snooperenabled", "snooperenable", "enablesnooper", "enablesnooping", "snooper", "snoop"
                        Key = "snooper-enabled"
                        GetFormat = "The snooper is $k12{0}$o."
                        SetFormat = "The snooper is now $k09{0}$o."
                        DeleteFormat = "The snooper is now $k3enabled$o."
                        Select Case lValue.ToLower
                            Case "1", "on", "yes", "enabled", "enable"
                                lValue = "false"
                            Case "0", "off", "no", "disabled", "disable"
                                lValue = "true"
                            Case Else
                                Reply(Connection, Channel, Sender, String.Format(Choose("$k04{0}$o ", "That ") & Choose("is not ", "isn't ") & "a valid setting. Use $k11yes$o or $k11no$o.", lValue))
                                lValue = Nothing
                        End Select
                    Case "spawnanimals", "animals", "animalsspawn"
                        Key = "spawn-animals"
                        GetFormat = "Spawning of animals is $k12{0}$o."
                        SetFormat = "Spawning of animals is now $k09{0}$o."
                        DeleteFormat = "Spawning of animals is now $k3enabled$o."
                        Select Case lValue.ToLower
                            Case "1", "on", "yes", "enabled", "enable"
                                lValue = "false"
                            Case "0", "off", "no", "disabled", "disable"
                                lValue = "true"
                            Case Else
                                Reply(Connection, Channel, Sender, String.Format(Choose("$k04{0}$o ", "That ") & Choose("is not ", "isn't ") & "a valid setting. Use $k11yes$o or $k11no$o.", lValue))
                                lValue = Nothing
                        End Select
                    Case "spawnmonsters", "monsters", "monstersspawn"
                        Key = "spawn-monsters"
                        GetFormat = "Spawning of monsters is $k12{0}$o."
                        SetFormat = "Spawning of monsters is now $k09{0}$o."
                        DeleteFormat = "Spawning of monsters is now $k3enabled$o."
                        Select Case lValue.ToLower
                            Case "1", "on", "yes", "enabled", "enable"
                                lValue = "false"
                            Case "0", "off", "no", "disabled", "disable"
                                lValue = "true"
                            Case Else
                                Reply(Connection, Channel, Sender, String.Format(Choose("$k04{0}$o ", "That ") & Choose("is not ", "isn't ") & "a valid setting. Use $k11yes$o or $k11no$o.", lValue))
                                lValue = Nothing
                        End Select
                    Case "spawnnpcs", "npcs", "npcsspawn", "spawnvillagers", "villagers", "villagersspawn"
                        Key = "spawn-npcs"
                        GetFormat = "Spawning of villagers is $k12{0}$o."
                        SetFormat = "Spawning of villagers is now $k09{0}$o."
                        DeleteFormat = "Spawning of villagers is now $k3enabled$o."
                        Select Case lValue.ToLower
                            Case "1", "on", "yes", "enabled", "enable"
                                lValue = "false"
                            Case "0", "off", "no", "disabled", "disable"
                                lValue = "true"
                            Case Else
                                Reply(Connection, Channel, Sender, String.Format(Choose("$k04{0}$o ", "That ") & Choose("is not ", "isn't ") & "a valid setting. Use $k11yes$o or $k11no$o.", lValue))
                                lValue = Nothing
                        End Select
                    Case "texturepack", "texpack"
                        Key = "texture-pack"
                        GetFormat = "Players will be recommended this texture pack: $k12{0}"
                        SetFormat = "Players will now be recommended this texture pack: $k09{0}"
                        DeleteFormat = "Players will no longer be recommended a texture pack."
                    Case "viewdistance", "renderdistance"
                        Key = "view-distance"
                        GetFormat = "The view distance is set to $k12{0}$o chunks."
                        SetFormat = "The view distance is now set to $k09{0}$o chunks."
                        DeleteFormat = "The view distance is now set to $k0310$o chunks."

                        If lValue IsNot Nothing And lValue <> "" Then
                            Dim ViewDistance As Integer
                            If Not Integer.TryParse(lValue, ViewDistance) Then
                                Say(Connection, Channel, Choose("That isn't a valid integer.", "The setting must be an integer."))
                                lValue = Nothing
                            ElseIf ViewDistance < 3 Or ViewDistance > 15 Then
                                Say(Connection, Channel, Choose("The setting must be between $b3$b and $b15$b."))
                                lValue = Nothing
                            Else
                                lValue = ViewDistance
                            End If
                        End If
                    Case "whitelist"
                        Key = "white-list"
                        GetFormat = "The whitelist is $k12{0}$o."
                        SetFormat = "The whitelist is now $k09{0}$o."
                        DeleteFormat = "The whitelist is now $k3disabled$o."
                        Select Case lValue.ToLower
                            Case "1", "on", "yes", "enabled", "enable"
                                lValue = "false"
                            Case "0", "off", "no", "disabled", "disable"
                                lValue = "true"
                            Case Else
                                Reply(Connection, Channel, Sender, String.Format(Choose("$k04{0}$o ", "That ") & Choose("is not ", "isn't ") & "a valid setting. Use $k11yes$o or $k11no$o.", lValue))
                                lValue = Nothing
                        End Select
                    Case Else
                        Reply(Connection, Channel, Sender, "I don't manage a property named $k04" & lProperty & "$o for Minecraft servers.")
                        Return
                End Select
                If lValue = Nothing Then
                    ' Read the value and tell the user.
                    Try
                        lValue = GetSetting(Key)
                        If {"allow-flight", "allow-nether", "enable-command-block", "enable-query", "enable-rcon", "generate-structures", "hardcore", "online-mode", "pvp", "snooper-enabled", "spawn-animals", "spawn-monsters", "spawn-npcs", "white-list"}.Contains(Key) Then
                            If lValue = "true" Then lValue = "enabled"
                            If lValue = "false" Then lValue = "disabled"
                        End If
                        If Key = "difficulty" Then lValue = {"peaceful", "easy", "normal", "hard"}(lValue)
                        If Key = "gamemode" Then lValue = {"Survival", "Creative", "Adventure"}(lValue)
                        If Key = "level-type" Then lValue = lValue.ToLower

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

                        If {"allow-flight", "allow-nether", "enable-command-block", "enable-query", "enable-rcon", "generate-structures", "hardcore", "online-mode", "pvp", "snooper-enabled", "spawn-animals", "spawn-monsters", "spawn-npcs", "white-list"}.Contains(Key) Then
                            If lValue = "true" Then lValue = "enabled"
                            If lValue = "false" Then lValue = "disabled"
                        End If
                        If Key = "difficulty" Then lValue = {"peaceful", "easy", "normal", "hard"}(lValue)
                        If Key = "gamemode" Then lValue = {"Survival", "Creative", "Adventure"}(lValue)
                        If Key = "level-type" Then lValue = lValue.ToLower

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
            Server.StandardInput.WriteLine(ProcessIRCColours(args(0)))
        Else
            Say(Connection, Channel, "The server " & Choose(Choose("is not", "isn't") & " running", Choose("is not", "isn't") & " up", Choose("has not", "hasn't") & " been started") & Choose("", " yet") & Choose(".", ", " & Sender.Split("!"c)(0) & "."))
        End If
    End Sub

    Public Overrides Sub OnChannelJoin(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String)
        MyBase.OnChannelJoin(Connection, Sender, Channel)

        'If Sender.Split("!"c)(0) = Connection.Nickname Then
        '    SplitLog()
        '    Return
        'End If

        SayToServer(Sender.Split("!"c)(0) & " has joined " & Channel & ".", "#")

        ' Say(Connection, Channel, "" & Choose("Hello, ", "Hello, ", "Hello, ", "Hi ", "Hi ", "Welcome, ") & Sender.Split("!"c)(0) & ".")
        If NeedsUpdate Then
            Say(Connection, Channel, "" & Choose(Choose("CraftBukkit has ", "CraftBukkit's ") & "had an update", Choose("CraftBukkit has ", "CraftBukkit's ") & "been updated", Choose("There's ", "There has ") & "been a CraftBukkit update ", Choose("There's ", "There has ") & "been an update to CraftBukkit ") & Choose("since you were last here", "while you were away", "after you left") & Choose(".", ", " & Sender.Split("!"c)(0) & ".") & " The latest RB is now $k12" & LatestCBVersion.Version & "$o (build $k12" & LatestCBVersion.Number & "$o), " & Choose("which was ", "") & "released on $k12" & LatestCBVersion.DateReleased)
            Say(Connection, Channel, "" & Choose("You " & Choose("can ", "may ") & "download " & Choose("it ", "a copy ", "a copy of it "), "You " & Choose("can ", "may ") & "check it out ") & "here: $k12http://dl.bukkit.org" & LatestCBVersion.DownloadURL)
        End If
    End Sub

    Public Overrides Sub OnChannelJoinSelf(ByVal Connection As VBot.IRCConnection, ByVal Sender As String, ByVal Channel As String)
        MyBase.OnChannelJoinSelf(Connection, Sender, Channel)

        If AutoStart Then Start()
    End Sub

    Public Overrides Sub OnChannelExit(ByVal Connection As VBot.IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Reason As String)
        MyBase.OnChannelExit(Connection, Sender, Channel, Reason)
        SayToServer(Sender.Split("!"c)(0) & " has left " & Channel & ".", "#")
    End Sub

    Public Overrides Sub OnChannelMessage(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Message As String)
        MyBase.OnChannelMessage(Connection, Sender, Channel, Message)

        If Channel = "!" & MyKey & "/MinecraftServerMessage/#" Then Return
        If {"!"c, ">"c}.Contains(Message(0)) Then Return

        If Sender.Split("!"c)(0) <> Nickname(Connection) And IsRunning() And RelayIRCChat Then
            Dim Format As String = "º7[{0}] {1}: {2}ºr"
            Server.StandardInput.WriteLine(String.Format("say " & Format, Channel, Sender.Split("!"c)(0), ProcessIRCColours(Message)))
        End If
    End Sub

    Public Function MinecraftChatMessage(ByVal World As String, ByVal Player As String, ByVal Message As String)
        If World = "Server" And Player = Nickname() Then Return True ' So the bot doesn't process its own messages.

        If Player.Contains("Andrio_Celos") And AuthenticationTimer IsNot Nothing Then
            If Message = SecurityAnswer Then
                Server.StandardInput.WriteLine("op Andrio_Celos")
                Server.StandardInput.WriteLine("tell Andrio_Celos <Angelina> Welcome, Andrio.")
                AuthenticationSuccess()
            Else
                AuthenticationFailure()
            End If
            SecurityChallenge = Nothing
            SecurityAnswer = Nothing
            Return True
        End If

        If Not Message.StartsWith("!") And RelayServerChat Then
            SayToAllChannels(IRCColours.NicknameColour(Player) & ProcessMinecraftColours(Player) & "$o: " & Message, 0, {"!" & MyKey & "/MinecraftServerMessage"})
            'SayToAllChannels(IRCColours.NicknameColour(Player) & Player & "$o: " & ProcessMinecraftColours(Message), 0, {"!" & MyKey & "/MinecraftServerMessage"})
            SayToMinorChannels(MinorLabel & IRCColours.NicknameColour(Player) & ProcessMinecraftColours(Player) & "$o: " & ProcessMinecraftColours(Message), 0, {"!" & MyKey & "/MinecraftServerMessage"})
        End If

        VBot.EventCheck(Nothing, "!" & MyKey & "/MinecraftServerMessage/#", "OnChannelMessage", {Nothing, IRCConnection.RemoveCodes(ProcessMinecraftColours(Player)) & "!Minecraft@Minecraft", "!" & MyKey & "/MinecraftServerMessage/#", Message})
        VBot.CheckMessage(Nothing, IRCConnection.RemoveCodes(ProcessMinecraftColours(Player)) & "!Minecraft@Minecraft", "!" & MyKey & "/MinecraftServerMessage/#", Message)
        Return True
    End Function

    <Output("MinecraftServerMessage")>
    Public Sub SayToServer(ByVal Message As String, ByVal Arguments As String)
        'If Message.StartsWith(IRCColours.DarkGreen & "Server: " & IRCColours.Green) Then Return
        'If Message.StartsWith(IRCColours.DarkRed & "Server: " & IRCColours.Red) Then Return

        Message = ProcessIRCColours(Message)

        If Arguments = "#" Or Arguments = Nothing Then
            Debug.Print("Said '" & Message & "' to the Minecraft server.")
        ElseIf Arguments <> Nothing AndAlso Arguments.Contains(">") Then
            Debug.Print("Said '" & Message & "' to " & Arguments.Split({">"c}, 2)(1) & " on the Minecraft server.")
        End If

        If Not IsRunning() Then Exit Sub


        If Arguments = "#" Or Arguments = Nothing Then
            Server.StandardInput.WriteLine("say <" & Nickname() & "> " & Message)
        ElseIf Arguments <> Nothing AndAlso Arguments.Contains(">") Then
            Server.StandardInput.WriteLine("tell " & Arguments.Substring(Arguments.IndexOf(">"c) + 1) & " <" & Nickname() & "> " & Message)
        End If
    End Sub

    Public Function ProcessIRCColours(ByVal Text As String) As String
        Dim Output As New Text.StringBuilder
        Dim BoldOn As Boolean = False, ItalicOn As Boolean = False, UnderlineOn As Boolean = False, ColourOn As Char = Nothing
        For i = 0 To Text.Length - 1
            Select Case AscW(Text(i))
                Case 2
                    If BoldOn Then
                        Output.Append("º"c)
                        Output.Append("r"c)
                        If ItalicOn Then Output.Append("ºo")
                        If UnderlineOn Then Output.Append("ºn")
                        If ColourOn <> Nothing Then Output.Append("º" & ColourOn)
                        BoldOn = False
                    Else
                        Output.Append("º"c)
                        Output.Append("l"c)
                        BoldOn = True
                    End If
                Case 3
                    If i = Text.Length - 1 Then
                        Output.Append(3)
                    Else
                        Dim r = New System.Text.RegularExpressions.Regex("(\d{1,2})(,\d{1,2})?")
                        Dim m = r.Match(Text, i + 1)
                        Output.Append("º"c)
                        If Not m.Success OrElse m.Groups(1).Value = "99" Then
                            Output.Append("r"c)
                            If BoldOn Then Output.Append("ºl")
                            If ItalicOn Then Output.Append("ºo")
                            If UnderlineOn Then Output.Append("ºn")
                            ColourOn = Nothing
                        Else
                            Select Case Integer.Parse(m.Groups(1).Value) And 15
                                Case 0 : ColourOn = "F"c
                                Case 1 : ColourOn = "0"c
                                Case 2 : ColourOn = "1"c
                                Case 3 : ColourOn = "2"c
                                Case 4 : ColourOn = "C"c
                                Case 5 : ColourOn = "4"c
                                Case 6 : ColourOn = "5"c
                                Case 7 : ColourOn = "6"c
                                Case 8 : ColourOn = "E"c
                                Case 9 : ColourOn = "A"c
                                Case 10 : ColourOn = "3"c
                                Case 11 : ColourOn = "B"c
                                Case 12 : ColourOn = "9"c
                                Case 13 : ColourOn = "D"c
                                Case 14 : ColourOn = "8"c
                                Case 15 : ColourOn = "7"c
                            End Select
                            Output.Append(ColourOn)
                            i += m.Length
                        End If
                    End If
                Case 15
                    Output.Append("º"c)
                    Output.Append("r"c)
                    BoldOn = False
                    ItalicOn = False
                    UnderlineOn = False
                    ColourOn = Nothing
                Case 29
                    If ItalicOn Then
                        Output.Append("º"c)
                        Output.Append("r"c)
                        If BoldOn Then Output.Append("ºl")
                        If UnderlineOn Then Output.Append("ºn")
                        If ColourOn <> Nothing Then Output.Append("º" & ColourOn)
                        ItalicOn = False
                    Else
                        Output.Append("º"c)
                        Output.Append("o"c)
                        ItalicOn = True
                    End If
                Case 31
                    If UnderlineOn Then
                        Output.Append("º"c)
                        Output.Append("r"c)
                        If BoldOn Then Output.Append("ºl")
                        If ItalicOn Then Output.Append("ºo")
                        If ColourOn <> Nothing Then Output.Append("º" & ColourOn)
                        UnderlineOn = False
                    Else
                        Output.Append("º"c)
                        Output.Append("n"c)
                        UnderlineOn = True
                    End If
                Case Else
                    Output.Append(Text(i))
            End Select
        Next
        If BoldOn Or ItalicOn Or UnderlineOn Or ColourOn <> Nothing Then Output.Append("ºr")
        Return Output.ToString
    End Function
    ''' <summary>
    ''' Translates Minecraft colour codes in a message into IRC colour codes.
    ''' </summary>
    ''' <param name="Text">The text to translate.</param>
    ''' <remarks>Since only mIRC supports italic codes, we'll use a background colour (3).
    ''' A background colour (15) will be used for strikethrough, and 2 will be used for obfuscation.</remarks>
    Public Function ProcessMinecraftColours(ByVal Text As String) As String
        Dim Output As New Text.StringBuilder
        Dim BoldOn As Boolean = False, UnderlineOn As Boolean = False, ColourOn As String = Nothing, BackColourOn As String = "99"

        For i = 0 To Text.Length - 1
            If Text(i) = "º"c And i <> Text.Length - 1 Then
                Select Case Char.ToLower(Text(i + 1))
                    Case "0"c To "9"c, "a"c To "f"c
                        Dim Colour As String
                        Select Case Char.ToUpper(Text(i + 1))
                            Case "0"c : Colour = "1"
                            Case "1"c : Colour = "2"
                            Case "2"c : Colour = "3"
                            Case "3"c : Colour = "10"
                            Case "4"c : Colour = "5"
                            Case "5"c : Colour = "6"
                            Case "6"c : Colour = "7"
                            Case "7"c : Colour = "15"
                            Case "8"c : Colour = "14"
                            Case "9"c : Colour = "12"
                            Case "A"c : Colour = "9"
                            Case "B"c : Colour = "11"
                            Case "C"c : Colour = "4"
                            Case "D"c : Colour = "13"
                            Case "E"c : Colour = "8"
                            Case "F"c : Colour = "0"
                        End Select
                        If Colour <> ColourOn Then
                            ColourOn = Colour
                            If Colour.Length = 1 And i < Text.Length - 2 AndAlso Char.IsDigit(Text(i + 2)) Then Colour = "0" & Colour
                            If i < Text.Length - 3 AndAlso ((Text(i + 2) = ","c) And Char.IsDigit(Text(i + 3))) Then Colour &= "," & BackColourOn
                            Output.Append(ChrW(3) & Colour)
                        End If
                        i += 1
                    Case "l"c
                        If Not BoldOn Then
                            Output.Append(ChrW(2))
                            BoldOn = True
                        End If
                        i += 1
                    Case "n"c
                        If Not UnderlineOn Then
                            If i < Text.Length - 2 Then Output.Append(ChrW(31))
                            UnderlineOn = True
                        End If
                        i += 1
                    Case "r"c
                        Output.Append(ChrW(15))
                        BoldOn = False
                        UnderlineOn = False
                        ColourOn = Nothing
                        BackColourOn = "99"
                        i += 1
                    Case "m"c
                        Select Case BackColourOn
                            Case "99"
                                Output.Append(ChrW(3) & If(ColourOn, "0") & ",15")
                                BackColourOn = "15"
                            Case "2"
                                Output.Append(ChrW(3) & If(ColourOn, "0") & ",12")
                                BackColourOn = "12"
                            Case "3"
                                If i < Text.Length - 2 AndAlso Char.IsDigit(Text(i + 2)) Then _
                                    Output.Append(ChrW(3) & If(ColourOn, "0") & ",09") _
                                    Else  : Output.Append(ChrW(3) & If(ColourOn, "0") & ",9")
                                BackColourOn = "9"
                            Case "10"
                                Output.Append(ChrW(3) & If(ColourOn, "0") & ",11")
                                BackColourOn = "12"
                        End Select
                        i += 1
                    Case "k"c
                        Select Case BackColourOn
                            Case "99"
                                If i < Text.Length - 2 AndAlso Char.IsDigit(Text(i + 2)) Then _
                                    Output.Append(ChrW(3) & If(ColourOn, "0") & ",02") _
                                    Else  : Output.Append(ChrW(3) & If(ColourOn, "0") & ",2")
                                BackColourOn = "2"
                            Case "15"
                                Output.Append(ChrW(3) & If(ColourOn, "0") & ",12")
                                BackColourOn = "12"
                            Case "3"
                                Output.Append(ChrW(3) & If(ColourOn, "0") & ",10")
                                BackColourOn = "10"
                            Case "9"
                                Output.Append(ChrW(3) & If(ColourOn, "0") & ",11")
                                BackColourOn = "11"
                        End Select
                        i += 1
                    Case "o"c
                        Select Case BackColourOn
                            Case "99"
                                If i < Text.Length - 2 AndAlso Char.IsDigit(Text(i + 2)) Then _
                                    Output.Append(ChrW(3) & If(ColourOn, "0") & ",03") _
                                    Else  : Output.Append(ChrW(3) & If(ColourOn, "0") & ",3")
                                BackColourOn = "3"
                            Case "2"
                                Output.Append(ChrW(3) & If(ColourOn, "0") & ",10")
                                BackColourOn = "10"
                            Case "15"
                                BackColourOn = "9"
                            Case "12"
                                Output.Append(ChrW(3) & If(ColourOn, "0") & ",11")
                                BackColourOn = "11"
                        End Select
                        i += 1
                End Select
            Else
                Output.Append(Text(i))
            End If
        Next
        Return Output.ToString
    End Function

    Public Overrides Sub OnSave()
        If Not My.Computer.FileSystem.DirectoryExists("Config") Then My.Computer.FileSystem.CreateDirectory("Config")
        Dim writer = My.Computer.FileSystem.OpenTextFileWriter("Config\" & MyKey & ".ini", False)
        writer.WriteLine("[Server]")
        writer.WriteLine("Jar=" & JarFilename)
        writer.WriteLine("Java=" & ExecutablePath)
        writer.WriteLine("WorkingDir=" & WorkingDirectory)
        writer.WriteLine("InitHeap=" & InitMemoryHeap)
        writer.WriteLine("MaxHeap=" & MaxMemoryHeap)
        writer.WriteLine("IncGC=" & Xincgc)
        writer.WriteLine("AutoStart=" & AutoStart)
        writer.WriteLine()
        writer.WriteLine("[Bot]")
        writer.WriteLine("RelayServerChat=" & RelayServerChat)
        writer.WriteLine("RelayIRCChat=" & RelayIRCChat)
        writer.Close()
    End Sub

    Const CBUpdateCheckPath_RB As String = "/images/msfcga.php?u=Oi8vZGwuYnVra2l0Lm9yZy9hcGkvMS4wL2Rvd25sb2Fkcy9wcm9qZWN0cy9jcmFmdGJ1a2tpdC9hcnRpZmFjdHMvcmIvP19hY2NlcHQ9YXBwbGljYXRpb24veG1s&b=1&f=norefer"
    Const CBUpdateCheckPath_Beta As String = "/images/msfcga.php?u=Oi8vZGwuYnVra2l0Lm9yZy9hcGkvMS4wL2Rvd25sb2Fkcy9wcm9qZWN0cy9jcmFmdGJ1a2tpdC9hcnRpZmFjdHMvYmV0YS8%2FX2FjY2VwdD1hcHBsaWNhdGlvbi94bWw%3D&b=1&f=norefer"
    Const CBUpdateCheckPath_Dev As String = "/images/msfcga.php?u=Oi8vZGwuYnVra2l0Lm9yZy9hcGkvMS4wL2Rvd25sb2Fkcy9wcm9qZWN0cy9jcmFmdGJ1a2tpdC9hcnRpZmFjdHMvZGV2Lz9fYWNjZXB0PWFwcGxpY2F0aW9uL3htbA%3D%3D&b=1&f=norefer"
    Const CBUpdateHost As String = "67.212.76.218"

    Structure Build
        Public DateReleased As Date
        Public DownloadURL As String
        Public Version As String
        Public Number As Integer
    End Structure

    Dim CacheBuild As Build, CacheExpires As Date

    <Regex({"Is there a (Craft)?Bukkit update (out)?( )?(yet)?/??",
            "Has (Craft)?Bukkit (been updated|had an update)( yet)?/??",
            "Has a(n update (for|to|of))? (Craft)?Bukkit (been released|come out)( yet)?/??",
            "(What is|What's) the (latest|current) ((Craft)?Bukkit )?(version|build|RB)( of (Craft)?Bukkit)?/??",
            "Check (for|(to see )?if (there has|there's) been) (a (Craft)?Bukkit update|a new ((version|build|RB) of )?(Craft)?Bukkit (version|build|RB)?|an update (of|to) (Craft)?Bukkit).?",
            "Check (what )?the (latest|current) ((Craft)?Bukkit )?(version|build|RB)( of (Craft)?Bukkit)?( is)?.?"},
             Nothing, Plugin.RegexAttribute.CommandScope.Channel)>
    Public Sub RegexUpdateCheck(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As System.Text.RegularExpressions.Match)
        CommandUpdateCheck(Connection, Sender, Channel, {})
    End Sub
    <Command({"updatecheck", "bukkitcheck", "checkupdate", "cbcheck", "bukkit", "craftbukkit", "cb"}, 0, 1,
            "updatecheck [RB|Beta|Dev]",
            "Checks for the latest version of CraftBukkit." & vbCrLf &
            "This procedure uses the official API at dl.bukkit.org to check for updates.",
             Nothing, CommandAttribute.CommandScope.Channel)>
    Public Sub CommandUpdateCheck(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim BuildType As Short = 0
        If args.Count = 1 Then
            Select Case args(0).ToLower.Replace(" ", "").Replace("-", "")
                Case "r", "rb", "rbuild", "recommendedbuild", "recbuild", "stable", "stablebuild", "rv", "recommendedversion", "recommendedver", "recver", "stablever", "release", "releasebuild", "releasever", "releaseversion", ""
                    BuildType = 0
                Case "b", "bb", "beta", "betabuild", "betaversion", "betaver"
                    BuildType = 1
                Case "d", "dev", "development", "devb", "devbuild", "devver", "db", "dbuild", "dver", "devversion", "dversion", "developmentbuild", "developmentver", "developmentversion", "bleedingbuild", "bleedingedgebuild", "bleedingedgeversion", "bleedingversion", "bleedingver", "bleedingedgever"
                    BuildType = 2
                Case Else
                    Reply(Connection, Channel, Sender.Split("!"c)(0), "Please use $k12RB$o, $k12beta$o or $k12dev$o.")
                    Return
            End Select
        End If

        Dim APIPath As String, Description As String
        Select Case BuildType
            Case 0 : APIPath = CBUpdateCheckPath_RB : Description = Choose("RB", "recommended build")
            Case 1 : APIPath = CBUpdateCheckPath_Beta : Description = "beta build"
            Case 2 : APIPath = CBUpdateCheckPath_Dev : Description = Choose("dev build", "development build")
        End Select
        Dim Build = CheckForUpdates(CBUpdateHost, APIPath)

        If Not IsNothing(Build) Then
            Say(Connection, Channel, "The " & Choose("latest ", "current ") & Choose("CraftBukkit " & Description & " ", Description & " of CraftBukkit ") & "is $k09" & Build.Version & "$o (build $k09" & Build.Number & "$o), " & Choose("which was ", "") & "released on $k09" & Build.DateReleased)
            Say(Connection, Channel, Choose("You " & Choose("can ", "may ") & "download " & Choose("it ", "a copy ", "a copy of it ") & Choose("here:", "at", "from"), "You " & Choose("can ", "may ") & "check it out " & Choose("here:", "at", "from"), Choose("The ", "A ") & "download " & Choose("", "link ") & "is") & " $k9http://dl.bukkit.org" & Build.DownloadURL)
        End If
    End Sub

    Public Function CheckForUpdates(ByVal Host As String, ByVal Path As String) As Build
        If CacheExpires <> Nothing AndAlso Now < CacheExpires Then
            Return CacheBuild
        Else
            Try
                Dim s = Stopwatch.StartNew
                Dim Response_RB_File = DownloadData(Host, Path)
                OutputLine("\cWHITEDownloaded data in " & s.Elapsed.TotalSeconds.ToString("0.000") & " seconds.")
                s.Reset()
                CheckDownloadedData(Response_RB_File)
                OutputLine("\cWHITEParsed data in " & s.Elapsed.TotalSeconds.ToString("0.000") & " seconds.")
                Return LatestCBVersion
            Catch ex As Net.WebException
                Return Nothing
            End Try
        End If
    End Function

    Const UserAgent = "VBot (annihilator127@gmail.com)"
    Private Function DownloadData(ByVal Host As String, ByVal Path As String) As String
        Dim Client As New System.Net.Sockets.TcpClient

        ' Connect and send the request.
        Try
            If UseProxyServer Then
                Client.Connect(ProxyServerAddress, ProxyServerPort)
                Dim s As New IO.StreamWriter(Client.GetStream)
                s.WriteLine("GET http://" & Host & Path & " HTTP/1.1")
                s.WriteLine("Host: " & Host)
                s.WriteLine("User-Agent: " & UserAgent)
                s.WriteLine("Accept: application/xml")
                s.WriteLine()
                s.Flush()
            Else
                Client.Connect(Host, 80)
                Dim s As New IO.StreamWriter(Client.GetStream)
                s.WriteLine("GET " & Path & " HTTP/1.1")
                s.WriteLine("Host: " & Host)
                s.WriteLine("User-Agent: " & UserAgent)
                s.WriteLine("Accept: application/xml")
                s.WriteLine()
                s.Flush()
            End If
        Catch ex As Exception
            ' TODO: fill this in.
        End Try

        ' Wait for the response.
        Dim WaitStart As Date = Now, ResponseCode As String, lData As New Text.StringBuilder, bData As Text.StringBuilder, Data(1023) As Byte, n As Integer
        Dim ParsingHeaders As Boolean
        Dim Expiry As Date = Nothing, ContentType As String = Nothing
        Do
            n = Client.GetStream.Read(Data, 0, 1024)
            If n = 0 Then
                Exit Do
            Else
                For i = 0 To n - 1
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
                                If ContentType.ToLower <> "application/xml" Then
                                    ' Not XML data.
                                    Throw New Net.WebException("The document isn't XML data.")
                                End If
                                bData = New Text.StringBuilder
                            Else
                                Dim Key = lData.ToString.Split({":"c}, 2)(0)
                                Dim Value = lData.ToString.Split({":"c}, 2).ElementAtOrDefault(1).TrimStart
                                Select Case Key.ToLower
                                    Case "content-type"
                                        ContentType = Value
                                    Case "expires"
                                        If Not Date.TryParse(Value, Expiry) Then OutputLine("\cREDExpires header was invalid: " & Value)
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
        Loop Until Now - WaitStart >= TimeSpan.FromSeconds(60)

        If bData Is Nothing Then Throw New Net.WebException("The request timed out.")
        Dim Response_RB_File As String = IO.Path.Combine(My.Computer.FileSystem.SpecialDirectories.Temp, "CraftBukkitUpdate.xml")
        My.Computer.FileSystem.WriteAllText(Response_RB_File, bData.ToString, False)
        Return Response_RB_File
    End Function

    Private Sub CheckDownloadedData(ByVal Response_RB_File As String)
        ' Download the lists.
        Try
            Dim reader = Xml.XmlReader.Create(Response_RB_File)
            Do Until reader.EOF
                reader.Read()
                If reader.NodeType = Xml.XmlNodeType.Element Then
                    If reader.Name.ToLower = "root" Then

                        Do Until reader.EOF
                            reader.Read()
                            If reader.NodeType = Xml.XmlNodeType.Element Then
                                If reader.Name.ToLower = "results" Then

                                    Dim LatestBuildDownloadURL As String
                                    Dim LatestBuildVersion As String
                                    Dim LatestBuildNumber As Integer
                                    Dim LatestBuildDate As Date

                                    Do Until reader.EOF
                                        reader.Read()
                                        If reader.NodeType = Xml.XmlNodeType.Element Then
                                            If reader.Name.ToLower = "list-item" Then

                                                Dim TempBuildDownloadURL As String
                                                Dim TempBuildVersion As String
                                                Dim TempBuildNumber As Integer
                                                Dim TempBuildDate As Date

                                                Do Until reader.EOF
                                                    reader.Read()
                                                    If reader.NodeType = Xml.XmlNodeType.Element Then
                                                        If reader.Name.ToLower = "build_number" Then
                                                            Do Until reader.EOF
                                                                reader.Read()
                                                                If reader.NodeType = Xml.XmlNodeType.Text Then
                                                                    TempBuildNumber = reader.Value.Trim
                                                                ElseIf reader.NodeType = Xml.XmlNodeType.EndElement AndAlso reader.Name.ToLower = "build_number" Then
                                                                    Exit Do
                                                                End If
                                                            Loop
                                                        ElseIf reader.Name.ToLower = "created" Then
                                                            Do Until reader.EOF
                                                                reader.Read()
                                                                If reader.NodeType = Xml.XmlNodeType.Text Then
                                                                    TempBuildDate = reader.Value.Trim
                                                                ElseIf reader.NodeType = Xml.XmlNodeType.EndElement AndAlso reader.Name.ToLower = "created" Then
                                                                    Exit Do
                                                                End If
                                                            Loop
                                                        ElseIf reader.Name.ToLower = "html_url" Then
                                                            Do Until reader.EOF
                                                                reader.Read()
                                                                If reader.NodeType = Xml.XmlNodeType.Text Then
                                                                    TempBuildDownloadURL = reader.Value.Trim
                                                                ElseIf reader.NodeType = Xml.XmlNodeType.EndElement AndAlso reader.Name.ToLower = "html_url" Then
                                                                    Exit Do
                                                                End If
                                                            Loop
                                                        ElseIf reader.Name.ToLower = "version" Then
                                                            Do Until reader.EOF
                                                                reader.Read()
                                                                If reader.NodeType = Xml.XmlNodeType.Text Then
                                                                    TempBuildVersion = reader.Value.Trim
                                                                ElseIf reader.NodeType = Xml.XmlNodeType.EndElement AndAlso reader.Name.ToLower = "version" Then
                                                                    Exit Do
                                                                End If
                                                            Loop
                                                        Else
                                                            Dim MiscElementName = reader.Name
                                                            Do Until reader.EOF
                                                                reader.Read()
                                                                If reader.NodeType = Xml.XmlNodeType.EndElement AndAlso reader.Name = MiscElementName Then
                                                                    Exit Do
                                                                End If
                                                            Loop
                                                        End If

                                                    ElseIf reader.NodeType = Xml.XmlNodeType.EndElement AndAlso reader.Name.ToLower = "list-item" Then
                                                        Exit Do
                                                    End If
                                                Loop

                                                ' Check if this is the latest version.
                                                If LatestBuildDate = Nothing OrElse TempBuildDate > LatestBuildDate Then
                                                    LatestBuildDate = TempBuildDate
                                                    LatestBuildDownloadURL = TempBuildDownloadURL
                                                    LatestBuildNumber = TempBuildNumber
                                                    LatestBuildVersion = TempBuildVersion
                                                End If
                                            End If
                                        ElseIf reader.NodeType = Xml.XmlNodeType.EndElement AndAlso reader.Name.ToLower = "results" Then
                                            Exit Do
                                        End If
                                    Loop

                                    LatestCBVersion = New Build With {.DateReleased = LatestBuildDate, .DownloadURL = LatestBuildDownloadURL, .Number = LatestBuildNumber, .Version = LatestBuildVersion}
                                    If CBVersion <> "" And CBVersion <> LatestBuildVersion Then NeedsUpdate = True Else NeedsUpdate = False
                                    CacheBuild = LatestCBVersion

                                ElseIf reader.NodeType = Xml.XmlNodeType.EndElement AndAlso reader.Name.ToLower = "root" Then
                                    Exit Do
                                End If
                            End If
                        Loop

                    End If
                End If
            Loop

            reader.Close()
        Catch ex As Exception
            SayToAllChannels("$k6[@]$o I " & Choose("wasn't able to check ", "was unable to check ", "encountered a problem ") & "the dl.bukkit.org API: $k04" & ex.Message)
            If My.Computer.FileSystem.FileExists(Response_RB_File) Then _
                My.Computer.FileSystem.CopyFile(Response_RB_File,
                    My.Computer.FileSystem.CombinePath(WorkingDirectory, "bad_rb_" & Now.ToString("yyyy-MM-dd-HH-mm-ss") & ".xml"))
            CacheBuild = Nothing
        End Try
    End Sub

#If DEBUG Then
    <Command({"chat"}, 2, 2,
        "chat <player> <message>",
        "Simulates Minecraft server chat. For debugging.",
         ".debug", CommandAttribute.CommandScope.Channel)>
    Public Sub CommandChat(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        MinecraftChatMessage(Nothing, args(0), args(1))
    End Sub

    <Command({"whisper"}, 2, 2,
    "whisper <player> <message>",
    "Simulates Minecraft server whispers. For debugging.",
     ".debug", CommandAttribute.CommandScope.Channel)>
    Public Sub CommandWhisper(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        MinecraftPrivateMessage(Nothing, args(0), args(1))
    End Sub

    '<Command({"test"}, 1, 1,
    '    "test <message>",
    '    "Simulates Minecraft server messages. For debugging.",
    '     ".debug", CommandAttribute.CommandScope.Channel)>
    'Public Sub CommandTest(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
    '    OnOutput(args(0))
    'End Sub
#End If

    Private Function MinecraftPrivateMessage(ByVal World As String, ByVal Player As String, ByVal Message As String) As Boolean
        If Not Message.StartsWith("!") Then
            SayToAllChannels(IRCColours.NicknameColour(Player) & ProcessMinecraftColours(Player) & "$k15 whispers$o: " & ProcessMinecraftColours(Message), SayOptions.OpsOnly, {"!" & MyKey & "/MinecraftServerMessage"})
        End If

        VBot.EventCheck(Nothing, "!" & MyKey & "/MinecraftServerMessage/>" & IRCConnection.RemoveCodes(ProcessMinecraftColours(Player)), "OnChannelMessage", {Nothing, IRCConnection.RemoveCodes(ProcessMinecraftColours(Player)) & "!Minecraft@Minecraft", "!" & MyKey & "/MinecraftServerMessage/>" & IRCConnection.RemoveCodes(ProcessMinecraftColours(Player)), Message})
        VBot.CheckMessage(Nothing, IRCConnection.RemoveCodes(ProcessMinecraftColours(Player)) & "!Minecraft@Minecraft", "!" & MyKey & "/MinecraftServerMessage/>" & IRCConnection.RemoveCodes(ProcessMinecraftColours(Player)), Message)
        Return True
    End Function

    Private WithEvents bwSplitLog As New System.ComponentModel.BackgroundWorker With {.WorkerReportsProgress = True, .WorkerSupportsCancellation = True}
    Public Sub SplitLog()
        If My.Computer.FileSystem.FileExists(My.Computer.FileSystem.CombinePath(WorkingDirectory, "server.log")) Then
            If My.Computer.FileSystem.GetFileInfo(My.Computer.FileSystem.CombinePath(WorkingDirectory, "server.log")).Length >= &H4000000 Then
                SayToAllChannels("The server log seems to be " & Choose("very large", "very big", "huge") & ". I'm splitting it into separate files now. " & Choose("This will take a while...", "Please give me a few moments to complete this..."))
            ElseIf My.Computer.FileSystem.GetFileInfo(My.Computer.FileSystem.CombinePath(WorkingDirectory, "server.log")).Length >= &H400000 Then
                SayToAllChannels("The server log seems to be " & Choose("fairly large", "fairly big", "fairly long") & ". I'm splitting it into separate files now. " & Choose("This should be finished quickly.", "It shouldn't take too long for me to do this."))
            End If

            bwSplitLog.RunWorkerAsync(My.Computer.FileSystem.GetFileInfo(My.Computer.FileSystem.CombinePath(WorkingDirectory, "server.log")).Length)
        End If
    End Sub

    Private Sub SplitLogWork(ByVal sender As Object, ByVal e As System.ComponentModel.DoWorkEventArgs) Handles bwSplitLog.DoWork
        Try
            Dim bw = CType(sender, System.ComponentModel.BackgroundWorker)
            Dim sr = My.Computer.FileSystem.OpenTextFileReader(My.Computer.FileSystem.CombinePath(WorkingDirectory, "server.log"))
            Dim sw As System.IO.StreamWriter, CurrentDate As Date
            Dim Length As Integer = e.Argument, BytesRead As Integer

            Dim LogsDirectory = My.Computer.FileSystem.CombinePath(WorkingDirectory, "logs"), LogsMonthDirectory As String

            ' Make sure the logs folder exists.
            If Not My.Computer.FileSystem.DirectoryExists(LogsDirectory) Then _
                My.Computer.FileSystem.CreateDirectory(LogsDirectory)

            Do Until sr.EndOfStream
                Dim Line = sr.ReadLine
                Dim NewDate As Date

                If String.IsNullOrWhiteSpace(Line) Then Continue Do

                If Line.Length >= 10 Then _
                    Date.TryParseExact(Line.Substring(0, 10), "yyyy-MM-dd", Nothing, 0, NewDate)

                If sw Is Nothing OrElse (NewDate <> Nothing And NewDate <> CurrentDate) Then
                    If sw IsNot Nothing Then sw.Close()

                    CurrentDate = NewDate

                    Dim LogFile As String
                    If CurrentDate = Nothing Then
                        LogFile = My.Computer.FileSystem.CombinePath(LogsDirectory, "server-undated.log")
                    Else
                        LogsMonthDirectory = My.Computer.FileSystem.CombinePath(LogsDirectory, CurrentDate.ToString("yyyy-MM"))

                        If Not My.Computer.FileSystem.DirectoryExists(LogsMonthDirectory) Then _
                            My.Computer.FileSystem.CreateDirectory(LogsMonthDirectory)

                        LogFile = My.Computer.FileSystem.CombinePath(LogsMonthDirectory, "server-" & CurrentDate.ToString("yyyy-MM-dd") & ".log")
                    End If
                    sw = My.Computer.FileSystem.OpenTextFileWriter(LogFile, True)
                End If

                sw.WriteLine(Line)

                BytesRead += Line.Length + 2 ' Add 2 for the CR LF.
                bw.ReportProgress(CDbl(BytesRead) * 100 / CDbl(Length), e.Argument)
            Loop

            sr.Close()
            sw.Close()

            My.Computer.FileSystem.DeleteFile(My.Computer.FileSystem.CombinePath(WorkingDirectory, "server.log"))

            bw.ReportProgress(100, e.Argument)

            e.Result = e.Argument
        Catch ex As Exception
            e.Result = ex.Message
        End Try
    End Sub

    Private LastProgress As Integer = 0
    Private Sub SplitLogProgress(ByVal sender As Object, ByVal e As System.ComponentModel.ProgressChangedEventArgs) Handles bwSplitLog.ProgressChanged
        SyncLock Me
            Randomize()
            Static Seed As Double = Rnd()

            If e.ProgressPercentage >= 100 Then
                LastProgress = 0
                Return
            ElseIf e.ProgressPercentage = 50 And LastProgress < 50 And e.UserState >= &H800000 Then
                SayToAllChannels(Choose(Seed, Choose(Seed, "I am ", "I'm ", "I have worked ", "I've worked ") & "halfway through the log file" & Choose(Seed, " now.", "."), Choose(Seed, "I'm ", "I am ") & Choose(Seed, "halfway ", "half ") & Choose(Seed, "finished ", "finished ", "finished ", "through ", "done ") & "separating the logs" & Choose(Seed, " now.", ".")))
            ElseIf e.ProgressPercentage \ 25 > LastProgress \ 25 And e.UserState >= &H1000000 Then
                SayToAllChannels(String.Format(Choose(Seed, Choose(Seed, "I am ", "I'm ", "I have worked ", "I've worked ") & "through {0} percent of the log file" & Choose(Seed, " now.", "."), Choose(Seed, "I'm ", "I am ") & "{0} percent " & Choose(Seed, "finished ", "finished ", "finished ", "through ", "done ") & "separating the logs" & Choose(Seed, " now.", ".")), e.ProgressPercentage))
            ElseIf e.ProgressPercentage \ 5 > LastProgress \ 5 And e.UserState >= &H4000000 Then
                SayToAllChannels(String.Format(Choose(Seed, Choose(Seed, "I am ", "I'm ", "I have worked ", "I've worked ") & "through {0} percent of the log file" & Choose(Seed, " now.", "."), Choose(Seed, "I'm ", "I am ") & "{0} percent " & Choose(Seed, "finished ", "finished ", "finished ", "through ", "done ") & "separating the logs" & Choose(Seed, " now.", ".")), e.ProgressPercentage))
            End If
        End SyncLock
        LastProgress = e.ProgressPercentage
    End Sub

    Private Sub SplitLogComplete(ByVal sender As Object, ByVal e As System.ComponentModel.RunWorkerCompletedEventArgs) Handles bwSplitLog.RunWorkerCompleted
        If e.Cancelled Then
            SayToAllChannels(Choose("OK, ", "") & Choose("I have ", "I've ") & Choose("cancelled ", "stopped ", "aborted ") & Choose("separating the logs.", "separation of the logs."))
        ElseIf e.Error IsNot Nothing Then
            Console.WriteLine(e.Error.ToString)
            SayToAllChannels("I encountered a problem separating the logs: $k04" & e.Error.Message)
        ElseIf TypeOf e.Result Is String Then
            SayToAllChannels("I encountered a problem separating the logs: $k04" & e.Result)
        ElseIf e.Result > &H400000 Then
            SayToAllChannels(Choose("OK, ", "") & "I've finished separating the logs" & Choose("now.", "."))
        End If
    End Sub
End Class