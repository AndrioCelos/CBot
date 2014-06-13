' General to-do list:
'   TODO: Spam proof commands.

Imports System.IO
'Imports System.Net.Sockets
Imports System.Text

''' <summary>The main module for VBot.</summary>
Public Module Bot
    ''' <summary>The version of the bot. This is sent in reply to a CTCP VERSION or CLIENTINFO.</summary>
    Public ReadOnly Version = String.Format("Visual Basic Bot by {1} : version {2}.{3}", My.Application.Info.Title, My.Application.Info.CompanyName, My.Application.Info.Version.Major, My.Application.Info.Version.Minor, My.Application.Info.Version.Revision, My.Application.Info.Version.Build)

    ''' <summary>The list of all the IRC connections that the bot manages.</summary>
    Public Connections As New List(Of IRCConnection)
    ''' <summary>The list of channels the bot will join, by address.</summary>
    Public AutoJoinChannels As New Dictionary(Of String, String())(StringComparer.OrdinalIgnoreCase)
    ''' <summary>NickServ related settings for each network, by address.</summary>
    Public NickServ As New Dictionary(Of String, NickServData)(StringComparer.OrdinalIgnoreCase)
    ''' <summary>The list of all the plugins that the bot has loaded.</summary>
    Public Plugins As New Dictionary(Of String, PluginData)(StringComparer.OrdinalIgnoreCase)

    ''' <summary>The list of users that have been identified using !id or some other means.</summary>
    Public Identifications As New Dictionary(Of String, Identification)(StringComparer.OrdinalIgnoreCase)
    ''' <summary>The list of all registered users.</summary>
    Public Accounts As New Dictionary(Of String, Account)(StringComparer.OrdinalIgnoreCase)

    ''' <summary>The list of allowed command prefixes.</summary>
    Public DefaultCommandPrefixes As String()
    ''' <summary>The list of all channels in which to ignore certain command prefixes.</summary>
    Public ChannelCommandPrefixes As New Dictionary(Of String, String())(StringComparer.OrdinalIgnoreCase)

    ' These are the default identity settings that will be used when the bot connects to a server for which it doesn't have
    ' specific identity settings.
    ' They will be overwritten by the LoadConfig procedure.
    Private dNicknames() As String = {"VBot"}
    Private dUsername As String = "VBot"
    Private dFullName As String = "VBot by Andrio Celos"
    Private dUserInfo As String = "VBot by Andrio Celos"
    Private dAvatar As String = Nothing

    Private ConfigFileFound As Boolean
    Private UsersFileFound As Boolean
    Private PluginsFileFound As Boolean

    ''' <summary>The settings for NickServ identifications.</summary>
    Public Class NickServData
        ''' <summary>The list of registered or grouped nicknames. The bot will attempt to identify to any one of them.</summary>
        Public RegisteredNicknames As String() = {}
        ''' <summary>Set to true if the network's NickServ supports entering a username in the IDENTIFY command.</summary>
        Public AnyNickname As Boolean = False
        ''' <summary>Set to true if you want the bot to use the GHOST command.</summary>
        Public UseGhostCommand As Boolean = True
        ''' <summary>The syntax of the GHOST command, as a raw IRC line.
        ''' Supports identifiers:
        '''     $target : The nickname of the NickServ bot.
        '''     $nickname : The nickname that is to be ghosted.
        '''     $password : The configured NickServ password.</summary>
        Public GhostCommand As String = "PRIVMSG $target :GHOST $nickname $password"
        ''' <summary>The password to use to identify.</summary>
        Public Password As String
        ''' <summary>The syntax of the IDENTIFY command, as a raw IRC line.
        ''' Supports identifiers:
        '''     $target : The nickname of the NickServ bot.
        '''     $nickname : The nickname that is to be identified to.
        '''     $password : The configured NickServ password.</summary>
        Public IdentifyCommand As String = "PRIVMSG $target :IDENTIFY $password"
        ''' <summary>A hostmask that NickServ must match for the bot to respond to it.
        ''' This is usually along the lines of NickServ!NickServ@services.example.net</summary>
        Public Hostmask As String = "NickServ!*@*"
        ''' <summary>The bot will attempt to identify when it receives a message from NickServ matching this mask. The default of *IDENTIFY* is not recommended.</summary>
        Public RequestMask As String = "*IDENTIFY*"
        ''' <summary>The time when the bot last attempted to identify. Used for rate limiting.</summary>
        Public IdentifyTime As Date = Nothing
    End Class

    ''' <summary>Prepares a new IRC connection, and sets up event handlers for it. This does not actually connect to a server; to do that, call the Connect method on the returned object.</summary>
    ''' <param name="Address">The address to connect to.</param>
    ''' <param name="Port">The port number to connect to.</param>
    ''' <param name="Nicknames">The list of nicknames to use.</param>
    ''' <param name="Username">The identd username to use.</param>
    ''' <param name="FullName">The full name to use.</param>
    ''' <returns>An IRCConnection instance tied to this connection.</returns>
    Function NewConnection(ByVal Address As String, ByVal Port As Integer, ByVal Nicknames() As String, ByVal Username As String, ByVal FullName As String) As IRCConnection
        Dim lnewConnection As New IRCConnection With {.Address = Address, .Port = Port, .Nickname = Nicknames(0), .Nicknames = Nicknames, .Username = Username, .FullName = FullName, .ReconnectInterval = 30000, .ReconnectMaxAttempts = 30}

        AddHandler lnewConnection.LookingUpHost, Sub(sender As IRCConnection, Hostname As String) OutputLine("\cGREENLooking up " & Hostname & "...\r")
        AddHandler lnewConnection.LookingUpHostFailed, Sub(sender As IRCConnection, Hostname As String, ErrorMessage As String) OutputLine("\cREDFailed to look up '" & Hostname & "': " & ErrorMessage & "\r")
        AddHandler lnewConnection.Connecting, Sub(sender As IRCConnection, Host As String, Endpoint As System.Net.IPEndPoint) OutputLine(String.Format("\cGREENConnecting to {0} ({1}) on port {2}...\r", Host, Endpoint.Address, Endpoint.Port))
        AddHandler lnewConnection.ConnectingFailed, Sub(sender As IRCConnection, Exception As Exception) OutputLine("\cREDI was unable to connect to " & sender.Address & ": " & Exception.Message & "\r")
        AddHandler lnewConnection.Connected, Sub(sender As IRCConnection) OutputLine("\cGREENConnected to " & sender.Address & ".\r")
        AddHandler lnewConnection.Disconnected, Sub(sender As IRCConnection, Message As String) OutputLine("\cREDDisconnected from " & sender.Address & ": " & Message & "\r")
        AddHandler lnewConnection.WaitingToReconnect, Sub(sender As IRCConnection, Interval As Decimal, Attempts As Integer, MaxAttempts As Integer) OutputLine(String.Format("\cREDWaiting {0} seconds before reconnecting... (Attempt number {1}" & If(MaxAttempts < 0, ")\r", " of {2})\r"), Interval, Attempts + 1, MaxAttempts))
        AddHandler lnewConnection.RawLineReceived, Sub(sender As IRCConnection, message As String) OutputLine(String.Format("\cDKGRAY{0} \cDKGREEN>>\cDKGRAY {1}\r", sender.Address, message))
        AddHandler lnewConnection.RawLineSent, Sub(sender As IRCConnection, message As String) OutputLine(String.Format("\cDKGRAY{0} \cDKRED<<\cDKGRAY {1}\r", sender.Address, message))
        AddHandler lnewConnection.Exception, AddressOf LogConnectionError

        ' Events
        AddHandler lnewConnection.AwayCancelled, AddressOf OnAwayCancelled
        AddHandler lnewConnection.AwaySet, AddressOf OnAway
        AddHandler lnewConnection.BanList, AddressOf OnBanList
        AddHandler lnewConnection.BanListEnd, AddressOf OnBanListEnd
        AddHandler lnewConnection.ChannelAction, AddressOf OnChannelAction
        AddHandler lnewConnection.ChannelActionHighlight, AddressOf OnChannelActionHighlight
        AddHandler lnewConnection.ChannelAdmin, AddressOf OnChannelAdmin
        AddHandler lnewConnection.ChannelAdminSelf, AddressOf OnChannelAdminSelf
        AddHandler lnewConnection.ChannelBan, AddressOf OnChannelBan
        AddHandler lnewConnection.ChannelBanSelf, AddressOf OnChannelBanSelf
        AddHandler lnewConnection.ChannelTimestamp, AddressOf OnChannelTimestamp
        AddHandler lnewConnection.ChannelCTCP, AddressOf OnChannelCTCP
        AddHandler lnewConnection.ChannelDeAdmin, AddressOf OnChannelDeAdmin
        AddHandler lnewConnection.ChannelDeAdminSelf, AddressOf OnChannelDeAdminSelf
        AddHandler lnewConnection.ChannelDeHalfOp, AddressOf OnChannelDeHalfOp
        AddHandler lnewConnection.ChannelDeHalfOpSelf, AddressOf OnChannelDeHalfOpSelf
        AddHandler lnewConnection.ChannelDeHalfVoice, AddressOf OnChannelDeHalfVoice
        AddHandler lnewConnection.ChannelDeHalfVoiceSelf, AddressOf OnChannelDeHalfVoiceSelf
        AddHandler lnewConnection.ChannelDeOp, AddressOf OnChannelDeOp
        AddHandler lnewConnection.ChannelDeOpSelf, AddressOf OnChannelDeOpSelf
        AddHandler lnewConnection.ChannelDeOwner, AddressOf OnChannelDeOwner
        AddHandler lnewConnection.ChannelDeOwnerSelf, AddressOf OnChannelDeOwnerSelf
        AddHandler lnewConnection.ChannelDeVoice, AddressOf OnChannelDeVoice
        AddHandler lnewConnection.ChannelDeVoiceSelf, AddressOf OnChannelDeVoiceSelf
        AddHandler lnewConnection.ChannelExempt, AddressOf OnChannelExempt
        AddHandler lnewConnection.ChannelExemptSelf, AddressOf OnChannelExemptSelf
        AddHandler lnewConnection.ChannelHalfOp, AddressOf OnChannelHalfOp
        AddHandler lnewConnection.ChannelHalfOpSelf, AddressOf OnChannelHalfOpSelf
        AddHandler lnewConnection.ChannelHalfVoice, AddressOf OnChannelHalfVoice
        AddHandler lnewConnection.ChannelHalfVoiceSelf, AddressOf OnChannelHalfVoiceSelf
        AddHandler lnewConnection.ChannelInviteExempt, AddressOf OnChannelInviteExempt
        AddHandler lnewConnection.ChannelInviteExemptSelf, AddressOf OnChannelInviteExemptSelf
        AddHandler lnewConnection.ChannelJoin, AddressOf OnChannelJoin
        AddHandler lnewConnection.ChannelJoinSelf, AddressOf OnChannelJoinSelf
        AddHandler lnewConnection.ChannelJoinDeniedBanned, AddressOf OnChannelJoinDeniedBanned
        AddHandler lnewConnection.ChannelJoinDeniedFull, AddressOf OnChannelJoinDeniedFull
        AddHandler lnewConnection.ChannelJoinDeniedInvite, AddressOf OnChannelJoinDeniedInvite
        AddHandler lnewConnection.ChannelJoinDeniedKey, AddressOf OnChannelJoinDeniedKey
        AddHandler lnewConnection.ChannelKick, AddressOf OnChannelKick
        AddHandler lnewConnection.ChannelKickSelf, AddressOf OnChannelKickSelf
        AddHandler lnewConnection.ChannelList, AddressOf OnChannelList
        AddHandler lnewConnection.ChannelMessage, AddressOf OnChannelMessage
        AddHandler lnewConnection.ChannelMessageSendDenied, AddressOf OnChannelMessageSendDenied
        AddHandler lnewConnection.ChannelMessageHighlight, AddressOf OnChannelMessageHighlight
        AddHandler lnewConnection.ChannelMode, AddressOf OnChannelMode
        AddHandler lnewConnection.ChannelModesGet, AddressOf OnChannelModesGet
        AddHandler lnewConnection.ChannelOp, AddressOf OnChannelOp
        AddHandler lnewConnection.ChannelOpSelf, AddressOf OnChannelOpSelf
        AddHandler lnewConnection.ChannelOwner, AddressOf OnChannelOwner
        AddHandler lnewConnection.ChannelOwnerSelf, AddressOf OnChannelOwnerSelf
        AddHandler lnewConnection.ChannelPart, AddressOf OnChannelPart
        AddHandler lnewConnection.ChannelPartSelf, AddressOf OnChannelPartSelf
        AddHandler lnewConnection.ChannelQuiet, AddressOf OnChannelQuiet
        AddHandler lnewConnection.ChannelQuietSelf, AddressOf OnChannelQuietSelf
        AddHandler lnewConnection.ChannelRemoveExempt, AddressOf OnChannelRemoveExempt
        AddHandler lnewConnection.ChannelRemoveExemptSelf, AddressOf OnChannelRemoveExemptSelf
        AddHandler lnewConnection.ChannelRemoveInviteExempt, AddressOf OnChannelRemoveInviteExempt
        AddHandler lnewConnection.ChannelRemoveInviteExemptSelf, AddressOf OnChannelRemoveInviteExemptSelf
        AddHandler lnewConnection.ChannelRemoveKey, AddressOf OnChannelRemoveKey
        AddHandler lnewConnection.ChannelRemoveLimit, AddressOf OnChannelRemoveLimit
        AddHandler lnewConnection.ChannelSetKey, AddressOf OnChannelSetKey
        AddHandler lnewConnection.ChannelSetLimit, AddressOf OnChannelSetLimit
        AddHandler lnewConnection.ChannelTopic, AddressOf OnChannelTopic
        AddHandler lnewConnection.ChannelTopicChange, AddressOf OnChannelTopicChange
        AddHandler lnewConnection.ChannelTopicStamp, AddressOf OnChannelTopicStamp
        AddHandler lnewConnection.ChannelUsers, AddressOf OnChannelUsers
        AddHandler lnewConnection.ChannelUnBan, AddressOf OnChannelUnBan
        AddHandler lnewConnection.ChannelUnBanSelf, AddressOf OnChannelUnBanSelf
        AddHandler lnewConnection.ChannelUnQuiet, AddressOf OnChannelUnQuiet
        AddHandler lnewConnection.ChannelUnQuietSelf, AddressOf OnChannelUnQuietSelf
        AddHandler lnewConnection.ChannelVoice, AddressOf OnChannelVoice
        AddHandler lnewConnection.ChannelVoiceSelf, AddressOf OnChannelVoiceSelf
        AddHandler lnewConnection.ExemptList, AddressOf OnExemptList
        AddHandler lnewConnection.ExemptListEnd, AddressOf OnExemptListEnd
        AddHandler lnewConnection.Invite, AddressOf OnInvite
        AddHandler lnewConnection.InviteExemptList, AddressOf OnInviteExemptList
        AddHandler lnewConnection.InviteExemptListEnd, AddressOf OnInviteExemptListEnd
        AddHandler lnewConnection.Killed, AddressOf OnKilled
        AddHandler lnewConnection.Names, AddressOf OnNames
        AddHandler lnewConnection.NamesEnd, AddressOf OnNamesEnd
        AddHandler lnewConnection.NicknameChange, AddressOf OnNicknameChange
        AddHandler lnewConnection.NicknameChangeSelf, AddressOf OnNicknameChangeSelf
        AddHandler lnewConnection.PrivateMessage, AddressOf OnPrivateMessage
        AddHandler lnewConnection.PrivateAction, AddressOf OnPrivateAction
        AddHandler lnewConnection.PrivateNotice, AddressOf OnPrivateNotice
        AddHandler lnewConnection.PrivateCTCP, AddressOf OnPrivateCTCP
        AddHandler lnewConnection.Quit, AddressOf OnQuit
        AddHandler lnewConnection.QuitSelf, AddressOf OnQuitSelf
        AddHandler lnewConnection.RawLineReceived, AddressOf OnRawLineReceived
        AddHandler lnewConnection.ServerNotice, AddressOf OnServerNotice
        AddHandler lnewConnection.ServerError, AddressOf OnServerError
        AddHandler lnewConnection.ServerMessage, AddressOf OnServerMessage
        AddHandler lnewConnection.ServerMessageUnhandled, AddressOf OnServerMessageUnhandled
        AddHandler lnewConnection.TimeOut, AddressOf OnTimeOut
        AddHandler lnewConnection.UserModesSet, AddressOf OnUserModesSet
        AddHandler lnewConnection.WhoList, AddressOf OnWhoList

        Connections.Add(lnewConnection)
        Return lnewConnection
    End Function

    ''' <summary>The program's entry point.</summary>
    Sub Main()
        Console.ForegroundColor = ConsoleColor.Gray
        'Console.TreatControlCAsInput = True  ' Buggy
        DefaultCommandPrefixes = {"!"}
        Console.Write("Loading configuration file...")
        If File.Exists("VBotConfig.ini") Then
            ConfigFileFound = True
            Try
                LoadConfig()
                Console.WriteLine(" OK")
            Catch ex As Exception
                OutputLine(" \cREDFailed\r")
                OutputLine("\cREDI couldn't load the configuration file: " & ex.Message & "\r")
                OutputLine("\cWHITEPress any key to continue, or close this window to cancel initialisation . . .")
                Console.ReadKey(True)
            End Try
        Else
            OutputLine(" \cBLUEFile VBotConfig.ini is missing.\r")
        End If
        Console.Write("Loading user configuration file...")
        If File.Exists("VBotUsers.ini") Then
            UsersFileFound = True
            Try
                LoadUsers()
                Console.WriteLine(" OK")
            Catch ex As Exception
                OutputLine(" \cREDFailed\r")
                OutputLine("\cREDI couldn't load the user configuration file: " & ex.Message & "\r")
                OutputLine("\cWHITEPress any key to continue, or close this window to cancel initialisation . . .")
                Console.ReadKey(True)
            End Try
        Else
            OutputLine(" \cBLUEFile VBotUsers.ini is missing.\r")
        End If
        Console.WriteLine("Loading plugins...")
        If File.Exists("VBotPlugins.ini") Then
            PluginsFileFound = True
            Try
                LoadPlugins()
            Catch ex As Exception
                Console.WriteLine()
                OutputLine("\cREDI couldn't load the plugins: " & ex.Message & "\r")
                OutputLine("\cWHITEPress any key to continue, or close this window to cancel initialisation . . .")
                Console.ReadKey(True)
            End Try
        Else
            OutputLine("\cBLUEFile VBotPlugins.ini is missing.\r")
        End If
        FirstRun()

        For Each Connection In Connections
            Try
                Connection.Connect()
            Catch ex As Exception
                OutputLine("\cREDI could not initialise an IRC connection to " & Connection.Address & ": " & ex.Message & "\r")
            End Try
        Next

        Do
            'Dim input = InputLine("", Console.WindowWidth, False, Nothing, {ConsoleKey.Enter})
            Dim input = Console.ReadLine
            Try
                Select Case input.Split({" "c})(0).ToLower
                    Case "load"
                        Try
                            LoadPlugin(input.Split({" "c}, 3)(1), input.Split({" "c}, 3)(2), {"*"})
                            If Plugins.ContainsKey(input.Split({" "c}, 3)(1)) Then _
                            OutputLine("\cBLUELoaded " & Plugins(input.Split({" "c}, 3)(1)).Obj.Name & ".\r")
                        Catch ex As Exception
                            OutputLine("\cBLUEI couldn't load a plugin from " & input.Split({" "c}, 3)(2) & ": " & ex.Message & "\r")
                        End Try
                    Case "send"
                        Connections(input.Split({" "c}, 3)(1)).Send(input.Split({" "c}, 3)(2))
                    Case "connect"
                        NewConnection(input.Split({" "c}, 5)(1), 6667,
                                      If(input.Split({" "c}, 5).Count > 2, {input.Split({" "c}, 5)(2)}, dNicknames),
                                      If(input.Split({" "c}, 5).ElementAtOrDefault(3), dUsername),
                                      If(input.Split({" "c}, 5).ElementAtOrDefault(4), dFullName)).Connect()
                    Case "stop"
                        Stop
                    Case Else
                        For Each m In Plugins
                            Try
                                m.Value.Obj.OnConsoleInput(input)
                            Catch ex As Exception
                                OutputLine("\cREDThere was a problem processing your request with plugin " & m.Key & ": " & ex.Message & "\r")
                                OutputLine("\cDKRED" & ex.StackTrace & "\r")
                            End Try
                        Next
                        CheckMessage(Nothing, "user!User@Console", "!MainCommands/Console/", input)
                End Select
            Catch ex As Exception
                OutputLine("\cREDThere was a problem processing your request: " & ex.Message & "\r")
                OutputLine("\cDKRED" & ex.StackTrace & "\r")
            End Try
        Loop
    End Sub

    ''' <summary>Loads a plugin from a file.</summary>
    ''' <param name="Key">The key to use in the Plugins list. This is useful if you want to load multiple instances of the same plugin.</param>
    ''' <param name="Filename">The DLL file to load a plugin from.</param>
    ''' <param name="Channels">The channels that this plugin will be active in.</param>
    Public Sub LoadPlugin(ByVal Key As String, ByVal Filename As String, ByVal ParamArray Channels() As String)
        If Filename = "*" Then
            Dim plugin As New Plugin
            plugin.Channels = If(Channels, {})
            Output(" Empty plugin", plugin.Name)
            Plugins.Add(Key, New PluginData With {.Filename = Filename, .Obj = plugin})
        ElseIf My.Computer.FileSystem.FileExists(Filename) Then
            Static PluginsLoaded As UInteger
            Dim tempFile As String
            For i = 0 To 15
                Try
                    tempFile = My.Computer.FileSystem.CombinePath(IO.Path.GetTempPath, IO.Path.GetFileNameWithoutExtension(Filename) & (PluginsLoaded + i) & ".dll")
                    My.Computer.FileSystem.CopyFile(Filename, tempFile, True)
                    Exit For
                Catch ex As IO.IOException
                End Try
            Next
            PluginsLoaded += 1
            Dim asm As Reflection.Assembly = Reflection.Assembly.LoadFrom(tempFile)
            Dim asmName = asm.GetName()

            Dim myType As System.Type
            For Each myType In asm.GetTypes
                If GetType(Plugin).IsAssignableFrom(myType) Then
                    GoTo FoundPluginType
                End If
            Next

            Throw New EntryPointNotFoundException("This is not a valid plugin (no class was found that inherits from the base plugin class).")
            'myType = asm.GetType(IO.Path.GetFileNameWithoutExtension(Filename).Replace(" ", "") + ".Plugin", True, True)


            'Dim implementsIPlugin As Boolean = GetType(bModule).IsAssignableFrom(myType)
            'If implementsIPlugin Then
FoundPluginType:
            Dim ConstructorType = -1

            Dim plugin As Plugin
            For Each Constructor In myType.GetConstructors
                Select Case Constructor.GetParameters.Count = 1
                    Case 0
                        ConstructorType = Math.Max(ConstructorType, 0)
                    Case 1
                        If Constructor.GetParameters(0).ParameterType = GetType(String) Then
                            ConstructorType = Math.Max(ConstructorType, 1)
                        End If
                End Select
            Next
            Select Case ConstructorType
                Case 0
                    plugin = CType(Activator.CreateInstance(myType), Plugin)
                Case 1
                    plugin = CType(Activator.CreateInstance(myType, {Key}), Plugin)
                Case Else
                    Throw New InvalidCastException("This is not a valid plugin (no compatible constructor was found).")
            End Select

            plugin.Channels = If(Channels, {})
            Output(" {0} ({1})", plugin.Name, asmName.Version)
            Plugins.Add(Key, New PluginData With {.Filename = Filename, .Obj = plugin})
            'plugin.Calculate(5, 4)
            'Else
            '    Throw New InvalidCastException("This is not a valid plugin (does not inherit from the base plugin class).")
            'End If
        Else
            Throw New IO.FileNotFoundException("The specified file doesn't seem to exist.", Filename)
        End If
    End Sub

    Public Sub FirstRun()
        Dim Input As String
        If Not ConfigFileFound Then
            Console.WriteLine()
            Console.WriteLine("This appears to be the first time I have been run here. Let us take a moment to set up.")
            Console.WriteLine("Please enter the identity details I should use on IRC.")
            dNicknames = {}
            Do While dNicknames.Count = 0
                Console.Write("Nicknames (comma- or space-separated, in order of preference): ")
                Input = Console.ReadLine()
                dNicknames = Input.Split({","c, " "c}, System.StringSplitOptions.RemoveEmptyEntries)
                For Each s In dNicknames
                    If s = "" Then Continue For
                    If Char.IsDigit(s(0)) Then
                        Console.WriteLine("A nickname may not begin with a number.")
                        dNicknames = {}
                        Continue Do
                    End If
                    For Each c In s
                        If c < "A"c Or c > "}"c Then
                            Console.WriteLine("Nickname '" & s & "' contains invalid characters.")
                            dNicknames = {}
                            Continue Do
                        End If
                    Next
                Next
            Loop
            dUsername = ""
            Do While dUsername = ""
                Console.Write("Username: ")
                dUsername = Console.ReadLine()
                For Each c In dUsername
                    If c < "A"c Or c > "}"c Then
                        Console.WriteLine("That username contains invalid characters.")
                        dUsername = ""
                        Continue Do
                    End If
                Next
            Loop
            dFullName = ""
            Do While dFullName = ""
                Console.Write("Full name: ")
                dFullName = Console.ReadLine()
            Loop
            dUserInfo = ""
            Do While dUserInfo = ""
                Console.Write("User info: ")
                dUserInfo = Console.ReadLine()
            Loop

            DefaultCommandPrefixes = Nothing
            Do While DefaultCommandPrefixes Is Nothing
                Console.Write("What do you want my command prefix to be? ")
                Input = Console.ReadLine()
                If Input.Length <> 1 Then
                    Console.WriteLine("It must be a single character.")
                Else
                    DefaultCommandPrefixes = {Input}
                End If
            Loop

            Dim SetUpNetwork As Boolean
            Console.WriteLine()
            Do
                Console.Write("Shall I connect to an IRC network? ")
                Input = Console.ReadLine()
                If Input = "" Then Continue Do
                If Input(0) = "Y" Or Input(0) = "y" Or Input(0) = "S" Or Input(0) = "s" Or Input(0) = "O" Or Input(0) = "o" Or Input(0) = "J" Or Input(0) = "j" Then
                    SetUpNetwork = True
                    Exit Do
                End If
                If Input(0) = "N" Or Input(0) = "n" Or Input(0) = "A" Or Input(0) = "a" Or Input(0) = "P" Or Input(0) = "p" Then
                    SetUpNetwork = False
                    Exit Do
                End If
            Loop

            If SetUpNetwork Then
                Dim NetworkName As String = "", NetworkAddress As String, NetworkPort As UShort = 0, UseSSL As Boolean = False, AcceptInvalidSSLCertificate As Boolean = False, AutoJoinChannels() As String
                Do While NetworkName = ""
                    Console.Write("What is the name of the IRC network? ")
                    NetworkName = Console.ReadLine()
                Loop
                Do While NetworkAddress Is Nothing
                    Console.Write("What is the address of the server? ")
                    Input = Console.ReadLine()
                    If Input = "" Then Continue Do
                    Dim m = System.Text.RegularExpressions.Regex.Match(Input, "^(?>([^:]*):(\d{1,5}))$", RegularExpressions.RegexOptions.Singleline)
                    If m.Success Then
                        ' A port number is provided.
                        NetworkAddress = m.Groups(1).Value
                        If Not UShort.TryParse(m.Groups(2).Value, NetworkPort) OrElse NetworkPort = 0 Then
                            Console.WriteLine("That is not a valid port number.")
                            Continue Do
                        End If
                    Else
                        NetworkAddress = Input
                    End If
                Loop
                Do While NetworkPort = 0
                    Console.Write("What port number should I connect to? ")
                    Input = Console.ReadLine()
                    If Input = "" Then Continue Do
                    If Input(0) = "+"c Then
                        UseSSL = True
                        Input = Input.Substring(1)
                    End If
                    If Not UShort.TryParse(Input, NetworkPort) OrElse NetworkPort = 0 Then
                        Console.WriteLine("That is not a valid port number.")
                        UseSSL = False
                        Continue Do
                    End If
                Loop
                If Not UseSSL Then
                    Do
                        Console.Write("Shall I use SSL? ")
                        Input = Console.ReadLine()
                        If Input = "" Then Continue Do
                        If Input(0) = "Y" Or Input(0) = "y" Or Input(0) = "S" Or Input(0) = "s" Or Input(0) = "O" Or Input(0) = "o" Or Input(0) = "J" Or Input(0) = "j" Then
                            UseSSL = True
                            Exit Do
                        End If
                        If Input(0) = "N" Or Input(0) = "n" Or Input(0) = "A" Or Input(0) = "a" Or Input(0) = "P" Or Input(0) = "p" Then
                            UseSSL = False
                            Exit Do
                        End If
                    Loop
                End If
                If UseSSL Then
                    Do
                        Console.Write("Shall I connect if the server's certificate is invalid? ")
                        Input = Console.ReadLine()
                        If Input = "" Then Continue Do
                        If Input(0) = "Y" Or Input(0) = "y" Or Input(0) = "S" Or Input(0) = "s" Or Input(0) = "O" Or Input(0) = "o" Or Input(0) = "J" Or Input(0) = "j" Then
                            AcceptInvalidSSLCertificate = True
                            Exit Do
                        End If
                        If Input(0) = "N" Or Input(0) = "n" Or Input(0) = "A" Or Input(0) = "a" Or Input(0) = "P" Or Input(0) = "p" Then
                            AcceptInvalidSSLCertificate = False
                            Exit Do
                        End If
                    Loop
                End If
                Console.WriteLine()
                Dim NickServData As NickServData
                Do
                    Console.Write("Is there a NickServ registration for me on " & NetworkName & "? ")
                    Input = Console.ReadLine()
                    If Input = "" Then Continue Do
                    If Input(0) = "Y" Or Input(0) = "y" Or Input(0) = "S" Or Input(0) = "s" Or Input(0) = "O" Or Input(0) = "o" Or Input(0) = "J" Or Input(0) = "j" Then
                        NickServData = New NickServData()
                        Exit Do
                    End If
                    If Input(0) = "N" Or Input(0) = "n" Or Input(0) = "A" Or Input(0) = "a" Or Input(0) = "P" Or Input(0) = "p" Then
                        Exit Do
                    End If
                Loop
                If NickServData IsNot Nothing Then
                    Do While NickServData.RegisteredNicknames.Count = 0
                        Console.Write("Grouped nicknames (comma- or space-separated): ")
                        Input = Console.ReadLine()
                        NickServData.RegisteredNicknames = Input.Split({","c, " "c}, System.StringSplitOptions.RemoveEmptyEntries)
                        For Each s In NickServData.RegisteredNicknames
                            If s = "" Then Continue For
                            If Char.IsDigit(s(0)) Then
                                Console.WriteLine("A nickname cannot begin with a number.")
                                NickServData.RegisteredNicknames = {}
                                Continue Do
                            End If
                            For Each c In s
                                If c < "A"c Or c > "}"c Then
                                    Console.WriteLine("Nickname '" & s & "' contains invalid characters.")
                                    NickServData.RegisteredNicknames = {}
                                    Continue Do
                                End If
                            Next
                        Next
                    Loop
                    NickServData.Password = ""
                    Do While NickServData.Password = ""
                        Console.Write("NickServ account password: ")
                        NickServData.Password = Console.ReadLine()
                    Loop
                    Do
                        Console.Write("Can I log in from any nickname by including '" & NickServData.RegisteredNicknames(0) & "' in the identify command? ")
                        Input = Console.ReadLine()
                        If Input = "" Then Continue Do
                        If Input(0) = "Y" Or Input(0) = "y" Or Input(0) = "S" Or Input(0) = "s" Or Input(0) = "O" Or Input(0) = "o" Or Input(0) = "J" Or Input(0) = "j" Then
                            NickServData.AnyNickname = True
                            Exit Do
                        End If
                        If Input(0) = "N" Or Input(0) = "n" Or Input(0) = "A" Or Input(0) = "a" Or Input(0) = "P" Or Input(0) = "p" Then
                            NickServData.AnyNickname = False
                            Exit Do
                        End If
                    Loop
                End If

                Console.WriteLine()
                Do While AutoJoinChannels Is Nothing
                    Console.Write("What channels (comma- or space-separated) should I join upon connecting? ")
                    Input = Console.ReadLine()
                    AutoJoinChannels = Input.Split({","c, " "c}, StringSplitOptions.RemoveEmptyEntries)
                Loop

                ' Set up the connection.
                Dim Connection = NewConnection(NetworkAddress, NetworkPort, dNicknames, dUsername, dFullName)
                Connection.IsUsingSSL = UseSSL
                Connection.AllowInvalidCertificate = AcceptInvalidSSLCertificate
                If NickServData IsNot Nothing Then NickServ.Add(NetworkAddress.ToLower, NickServData)
                If AutoJoinChannels.Length <> 0 Then VBot.AutoJoinChannels.Add(NetworkAddress.ToLower, AutoJoinChannels)

                Console.WriteLine("OK, that's the IRC connection configuration done.")
                Console.WriteLine("Press any key to continue . . .")
                Console.ReadKey(True)
            End If
        End If

        If Not UsersFileFound Then
            Dim AccountName As String, Password As String
            Console.WriteLine()
            Input = Nothing
            Do
                Console.WriteLine("What do you want your account name to be?")
                If Input Is Nothing Then Console.Write("For simplicity, we recommend you use your IRC nickname. ")
                Input = Console.ReadLine()
                If Input = "" Then Continue Do
                If Input.Contains(" ") Then
                    Console.WriteLine("It can't contain spaces.")
                Else
                    AccountName = Input
                    Exit Do
                End If
            Loop

            Dim RNG = New System.Security.Cryptography.RNGCryptoServiceProvider()
            Dim SHA256M As New System.Security.Cryptography.SHA256Managed
            Do While Password Is Nothing
                Console.Write("Please enter a password.      ")
                Input = ""
                Do
                    Dim c = Console.ReadKey(True)
                    If c.Key = ConsoleKey.Enter Then
                        Console.WriteLine()
                        Exit Do
                    Else
                        Input &= c.KeyChar
                        Console.Write("*"c)
                    End If
                Loop
                If Input = "" Then Continue Do
                If Input.Contains(" ") Then
                    Console.WriteLine("It can't contain spaces.")
                    Continue Do
                End If

                ' Hash the password.
                Dim Salt(31) As Byte, Hash() As Byte, ConfirmHash() As Byte
                RNG.GetBytes(Salt)
                Hash = SHA256M.ComputeHash(Salt.Concat(System.Text.Encoding.UTF8.GetBytes(Input)).ToArray)

                Console.Write("Please confirm your password. ")
                Input = ""
                Do
                    Dim c = Console.ReadKey(True)
                    If c.Key = ConsoleKey.Enter Then
                        Console.WriteLine()
                        Exit Do
                    Else
                        Input &= c.KeyChar
                        Console.Write("*"c)
                    End If
                Loop
                If Input = "" OrElse Input.Contains(" ") Then
                    Console.WriteLine("The passwords do not match.")
                    Continue Do
                End If

                ConfirmHash = SHA256M.ComputeHash(Salt.Concat(System.Text.Encoding.UTF8.GetBytes(Input)).ToArray)
                If Not Hash.SequenceEqual(ConfirmHash) Then
                    Console.WriteLine("The passwords do not match.")
                    Continue Do
                End If

                Dim passwordBuilder As New StringBuilder
                For i = 0 To 31
                    passwordBuilder.Append(Salt(i).ToString("x2"))
                Next
                For i = 0 To 31
                    passwordBuilder.Append(Hash(i).ToString("x2"))
                Next
                Accounts.Add(AccountName, New Account With {.Password = passwordBuilder.ToString, .Permissions = {"*"}})
                OutputLine("Thank you. To log in from IRC, enter \cWHITE/msg " & Nickname() & " !id <password>\r or \cWHITE/msg " & Nickname() & " !id " & AccountName & " <password>\r, without the brackets.")
                Console.WriteLine("Press any key to continue . . .")
                Console.ReadKey(True)
                Exit Do
            Loop

        End If
    End Sub

#Region "Filing"

    ''' <summary>Loads configuration data from the VBotConfig.ini general configuration file.</summary>
    Public Sub LoadConfig()
        If Not My.Computer.FileSystem.FileExists("VBotConfig.ini") Then
        Else
            Try
                Dim Reader = My.Computer.FileSystem.OpenTextFileReader("VBotConfig.ini")

                Dim Section As String = "", Field As String = "", Value As String = ""
                Dim GotNicknames As Boolean, Connection As IRCConnection

                Do Until Reader.EndOfStream
                    Dim s = Reader.ReadLine
                    ' Check for comments.
                    If s.TrimStart.StartsWith(";") Then Continue Do

                    Dim Match As System.Text.RegularExpressions.Match
                    Match = System.Text.RegularExpressions.Regex.Match(s, "^\s*\[(?<Section>.*?)\]?\s*$")
                    If Match.Success Then
                        Section = Match.Groups("Section").Value
                        If Section.ToLower <> "me" And Section.ToLower <> "prefixes" Then
                            Dim Host As String, Port As UShort = 6667
                            Dim ss = Section.Split({":"c}, 2)
                            Host = ss(0)
                            If ss.Count > 1 Then
                                If Not UShort.TryParse(ss(1), Port) Then
                                    OutputLine("\cREDPort number for " & ss(0) & " is invalid.")
                                End If
                            End If
                            Connection = NewConnection(Host, Port, dNicknames, dUsername, dFullName)
                        End If
                        GotNicknames = False
                        Continue Do
                    End If

                    Match = System.Text.RegularExpressions.Regex.Match(s, "^\s*(?<Field>(?>[^=]*))=(?<Value>.*)$")
                    If Match.Success Then
                        Field = Match.Groups("Field").Value
                        Value = Match.Groups("Value").Value

                        If Section.ToLower = "me" Then
                            Select Case Field.ToLower
                                Case "nickname", "nicknames", "name", "names", "nick", "nicks"
                                    If GotNicknames Then
                                        dNicknames = dNicknames.Concat(Value.Split({","c, " "c}, StringSplitOptions.RemoveEmptyEntries)).ToArray
                                    Else
                                        dNicknames = Value.Split({","c, " "c}, StringSplitOptions.RemoveEmptyEntries)
                                        GotNicknames = True
                                    End If
                                Case "username", "user", "identname", "ident"
                                    dUsername = Value
                                Case "fullname", "realname", "gecos", "full"
                                    dFullName = Value
                                Case "userinfo", "ctcpinfo", "info"
                                    dUserInfo = Value
                                Case "avatar", "avatarurl", "avatar-url"
                                    dAvatar = Value
                            End Select
                        ElseIf Section.ToLower = "prefixes" Then
                            If Not ChannelCommandPrefixes.ContainsKey(Field.ToLower) Then
                                ChannelCommandPrefixes.Add(Field.ToLower, Value.Split({" "c}, StringSplitOptions.RemoveEmptyEntries))
                            End If
                        Else
                            If Field.ToLower.StartsWith("nickserv") Or Field.ToLower.StartsWith("ns") Then
                                If Not NickServ.ContainsKey(Connection.Address.ToLower) Then
                                    NickServ.Add(Connection.Address.ToLower, New NickServData)
                                End If
                            End If
                            Select Case Field.ToLower
                                Case "nickname", "nicknames", "name", "names", "nick", "nicks"
                                    If GotNicknames Then
                                        Connection.Nicknames = Connection.Nicknames.Concat(Value.Split({","c, " "c}, StringSplitOptions.RemoveEmptyEntries)).ToArray
                                    Else
                                        Connection.Nicknames = Value.Split({","c, " "c}, StringSplitOptions.RemoveEmptyEntries)
                                        Connection.Nickname = Connection.Nicknames(0)
                                        GotNicknames = True
                                    End If
                                Case "username", "user", "identname", "ident"
                                    Connection.Username = Value
                                Case "fullname", "realname", "gecos", "full"
                                    Connection.FullName = Value
                                Case "autojoin", "join", "ajoin", "channels", "autojoinchannels", "ajoinchannels", "joinchannels", "autojoinchans", "ajoinchans", "joinchans"
                                    If AutoJoinChannels.ContainsKey(Connection.Address.ToLower) Then
                                        Dim NewList = AutoJoinChannels(Connection.Address.ToLower)
                                        For Each Channel In Value.Split({","c, " "c}, StringSplitOptions.RemoveEmptyEntries)
                                            If Not NewList.Contains(Channel) Then
                                                AppendArray(NewList, Channel)
                                            End If
                                        Next
                                    Else
                                        AutoJoinChannels.Add(Connection.Address.ToLower, Value.Split({","c, " "c}, StringSplitOptions.RemoveEmptyEntries))
                                    End If
                                Case "ssl", "usessl", "encrypt", "encrypted"
                                    If {"yes", "true", "on"}.Contains(Value.ToLower) Then Connection.IsUsingSSL = True
                                    If {"no", "false", "off"}.Contains(Value.ToLower) Then Connection.IsUsingSSL = False
                                Case "allowinvalidcertificate"
                                    If {"yes", "true", "on"}.Contains(Value.ToLower) Then Connection.AllowInvalidCertificate = True
                                    If {"no", "false", "off"}.Contains(Value.ToLower) Then Connection.AllowInvalidCertificate = False
                                Case "nickserv-nicknames", "ns-nicknames", "nickserv-nicks", "ns-nicks", "nickserv-names", "ns-names"
                                    NickServ(Connection.Address.ToLower).RegisteredNicknames = Value.Split({","c, " "c}, StringSplitOptions.RemoveEmptyEntries)
                                Case "nickserv-password", "ns-password", "nickserv-pass", "ns-pass"
                                    NickServ(Connection.Address.ToLower).Password = Value
                                Case "nickserv-anynickname", "ns-anynickname", "nickserv-anynick", "ns-anynick"
                                    If {"yes", "true", "on"}.Contains(Value.ToLower) Then NickServ(Connection.Address.ToLower).AnyNickname = True
                                    If {"no", "false", "off"}.Contains(Value.ToLower) Then NickServ(Connection.Address.ToLower).AnyNickname = False
                                Case "nickserv-useghostcommand", "ns-useghostcommand", "nickserv-useghost", "ns-useghost", "nickserv-ghost", "ns-ghost"
                                    If {"yes", "true", "on"}.Contains(Value.ToLower) Then NickServ(Connection.Address.ToLower).UseGhostCommand = True
                                    If {"no", "false", "off"}.Contains(Value.ToLower) Then NickServ(Connection.Address.ToLower).UseGhostCommand = False
                                Case "nickserv-identifycommand", "ns-identifycommand", "nickserv-idcmd", "ns-idcmd"
                                    NickServ(Connection.Address.ToLower).IdentifyCommand = Value
                                Case "nickserv-ghostcommand", "ns-ghostcommand", "nickserv-ghostcmd", "ns-ghostcmd"
                                    NickServ(Connection.Address.ToLower).GhostCommand = Value
                                Case "nickserv-hostmask", "ns-hostmask", "nickserv-host", "ns-host"
                                    NickServ(Connection.Address.ToLower).Hostmask = Value
                                Case "nickserv-requestmask", "ns-requestmask", "nickserv-request", "ns-request"
                                    NickServ(Connection.Address.ToLower).RequestMask = Value
                            End Select
                        End If
                    End If
                Loop
                Reader.Close()

            Catch ex As Exception
                OutputLine("\cGRAY[\cREDERROR\cGRAY] \cWHITEI was unable to retrieve config data from the file: $k04" & ex.Message & "\r")
            End Try
        End If
    End Sub

    ''' <summary>Loads configuration data from the (deprecated) VBotConfig.xml general configuration file.</summary>
    <Obsolete("Using XML to store configuration is a mess and resulted in crashes, so we're not doing it any more. Use LoadConfig() instead.")>
    Public Sub LoadConfigXML()
        If My.Computer.FileSystem.FileExists("VBotConfig.xml") Then
            Dim Reader = Xml.XmlReader.Create("VBotConfig.xml")

            Do Until Reader.EOF : Reader.Read()
                If Reader.NodeType = Xml.XmlNodeType.Element Then
                    If Reader.Name.ToLower = "vbot" Then
                        ''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' vbot
                        If Not Reader.IsEmptyElement Then : Do Until Reader.EOF : Reader.Read()
                                If Reader.NodeType = Xml.XmlNodeType.Element Then
                                    If Reader.Name.ToLower = "me" Then
                                        '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' vbot/me
                                        dUsername = Reader.GetAttribute("username")
                                        dFullName = If(Reader.GetAttribute("fullname"), "").Replace("$1", Chr(1)).Replace("$b", Chr(2)).Replace("$k", Chr(3)).Replace("$o", Chr(15)).Replace("$r", Chr(22)).Replace("$u", Chr(31)).Replace("$$", "$")
                                        dUserInfo = If(Reader.GetAttribute("userinfo"), "").Replace("$1", Chr(1)).Replace("$b", Chr(2)).Replace("$k", Chr(3)).Replace("$o", Chr(15)).Replace("$r", Chr(22)).Replace("$u", Chr(31)).Replace("$$", "$")

                                        If Not Reader.IsEmptyElement Then : Do Until Reader.EOF : Reader.Read()
                                                If Reader.NodeType = Xml.XmlNodeType.Element Then
                                                    If Reader.Name.ToLower = "userinfo" Then
                                                        ''''''''''''''''''''''''''''''''''''''''''''''''''' vbot/me/userinfo
                                                        If Not Reader.IsEmptyElement Then : Do Until Reader.EOF : Reader.Read()
                                                                If Reader.NodeType = Xml.XmlNodeType.Element Then
                                                                    If Not Reader.IsEmptyElement Then : Dim MiscElementName = Reader.Name : Do Until Reader.EOF : Reader.Read() : If Reader.NodeType = Xml.XmlNodeType.EndElement AndAlso Reader.Name = MiscElementName Then : Exit Do : End If : Loop
                                                                    End If
                                                                ElseIf Reader.NodeType = Xml.XmlNodeType.Text Then
                                                                    dUserInfo &= Reader.Value
                                                                ElseIf Reader.NodeType = Xml.XmlNodeType.EndElement AndAlso Reader.Name = "userinfo" Then : Exit Do
                                                                End If
                                                            Loop : End If

                                                    ElseIf Reader.Name.ToLower = "nickname" Then
                                                        Dim CurrentText As String = ""
                                                        ''''''''''''''''''''''''''''''''''''''''''''''''''' vbot/me/nickname
                                                        If Not Reader.IsEmptyElement Then : Do Until Reader.EOF : Reader.Read()
                                                                If Reader.NodeType = Xml.XmlNodeType.Element Then
                                                                    If Not Reader.IsEmptyElement Then : Dim MiscElementName = Reader.Name : Do Until Reader.EOF : Reader.Read() : If Reader.NodeType = Xml.XmlNodeType.EndElement AndAlso Reader.Name = MiscElementName Then : Exit Do : End If : Loop
                                                                    End If
                                                                ElseIf Reader.NodeType = Xml.XmlNodeType.Text Then
                                                                    CurrentText &= Reader.Value
                                                                ElseIf Reader.NodeType = Xml.XmlNodeType.EndElement AndAlso Reader.Name = "nickname" Then : Exit Do
                                                                End If
                                                            Loop : End If
                                                        AppendArray(dNicknames, CurrentText)

                                                    ElseIf Not Reader.IsEmptyElement Then : Dim MiscElementName = Reader.Name : Do Until Reader.EOF : Reader.Read() : If Reader.NodeType = Xml.XmlNodeType.EndElement AndAlso Reader.Name = MiscElementName Then : Exit Do : End If : Loop
                                                    End If
                                                ElseIf Reader.NodeType = Xml.XmlNodeType.Text Then
                                                    dUserInfo &= Reader.Value
                                                ElseIf Reader.NodeType = Xml.XmlNodeType.EndElement AndAlso Reader.Name = "me" Then : Exit Do
                                                End If
                                            Loop : End If

                                    ElseIf Reader.Name.ToLower = "connections" Then
                                        ''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' vbot/connections
                                        If Not Reader.IsEmptyElement Then : Do Until Reader.EOF : Reader.Read()
                                                If Reader.NodeType = Xml.XmlNodeType.Element Then
                                                    If Reader.Name.ToLower = "connection" Then
                                                        '''''''''''''''''''''''''''''''''''''''' vbot/connections/connection
                                                        Dim cAddress = Reader.GetAttribute("address")
                                                        Dim cPort = Reader.GetAttribute("port")
                                                        Dim cUsername = Reader.GetAttribute("username")
                                                        Dim cFullName = If(Reader.GetAttribute("fullname"), "").Replace("$1", Chr(1)).Replace("$b", Chr(2)).Replace("$k", Chr(3)).Replace("$o", Chr(15)).Replace("$r", Chr(22)).Replace("$u", Chr(31)).Replace("$$", "$")
                                                        Dim cNicknames As String() = {}
                                                        If Not Reader.IsEmptyElement Then : Do Until Reader.EOF : Reader.Read()
                                                                If Reader.NodeType = Xml.XmlNodeType.Element Then
                                                                    If Reader.Name.ToLower = "nickname" Then
                                                                        Dim CurrentText As String = ""
                                                                        ''''''''''''''' vbot/connections/connection/nickname
                                                                        If Not Reader.IsEmptyElement Then : Do Until Reader.EOF : Reader.Read()
                                                                                If Reader.NodeType = Xml.XmlNodeType.Element Then
                                                                                    If Not Reader.IsEmptyElement Then : Dim MiscElementName = Reader.Name : Do Until Reader.EOF : Reader.Read() : If Reader.NodeType = Xml.XmlNodeType.EndElement AndAlso Reader.Name = MiscElementName Then : Exit Do : End If : Loop
                                                                                    End If
                                                                                ElseIf Reader.NodeType = Xml.XmlNodeType.Text Then
                                                                                    CurrentText &= Reader.Value
                                                                                ElseIf Reader.NodeType = Xml.XmlNodeType.EndElement AndAlso Reader.Name = "nickname" Then : Exit Do
                                                                                End If
                                                                            Loop : End If
                                                                        AppendArray(cNicknames, CurrentText)

                                                                    ElseIf Reader.Name.ToLower = "autojoin" Then
                                                                        Dim CurrentText As String = ""
                                                                        ''''''''''''''' vbot/connections/connection/autojoin
                                                                        If Not Reader.IsEmptyElement Then : Do Until Reader.EOF : Reader.Read()
                                                                                If Reader.NodeType = Xml.XmlNodeType.Element Then
                                                                                    If Not Reader.IsEmptyElement Then : Dim MiscElementName = Reader.Name : Do Until Reader.EOF : Reader.Read() : If Reader.NodeType = Xml.XmlNodeType.EndElement AndAlso Reader.Name = MiscElementName Then : Exit Do : End If : Loop
                                                                                    End If
                                                                                ElseIf Reader.NodeType = Xml.XmlNodeType.Text Then
                                                                                    CurrentText &= Reader.Value
                                                                                ElseIf Reader.NodeType = Xml.XmlNodeType.EndElement AndAlso Reader.Name = "autojoin" Then : Exit Do
                                                                                End If
                                                                            Loop : End If
                                                                        If AutoJoinChannels.ContainsKey(cAddress.ToLower) Then
                                                                            AppendArray(AutoJoinChannels(cAddress.ToLower), CurrentText)
                                                                        Else
                                                                            AutoJoinChannels.Add(cAddress.ToLower, {CurrentText})
                                                                        End If

                                                                    ElseIf Not Reader.IsEmptyElement Then : Dim MiscElementName = Reader.Name : Do Until Reader.EOF : Reader.Read() : If Reader.NodeType = Xml.XmlNodeType.EndElement AndAlso Reader.Name = MiscElementName Then : Exit Do : End If : Loop
                                                                    End If
                                                                ElseIf Reader.NodeType = Xml.XmlNodeType.EndElement AndAlso Reader.Name = "connection" Then
                                                                    Exit Do
                                                                End If
                                                            Loop : End If
                                                        NewConnection(cAddress, If(cPort, 6667), If(cNicknames, dNicknames), If(cUsername, dUsername), If(cFullName, dFullName))

                                                    ElseIf Not Reader.IsEmptyElement Then : Dim MiscElementName = Reader.Name : Do Until Reader.EOF : Reader.Read() : If Reader.NodeType = Xml.XmlNodeType.EndElement AndAlso Reader.Name = MiscElementName Then : Exit Do : End If : Loop
                                                    End If
                                                ElseIf Reader.NodeType = Xml.XmlNodeType.EndElement AndAlso Reader.Name = "connections" Then : Exit Do
                                                End If
                                            Loop : End If

                                    ElseIf Reader.Name.ToLower = "users" Then
                                        ''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' vbot/users
                                        If Not Reader.IsEmptyElement Then : Do Until Reader.EOF : Reader.Read()
                                                If Reader.NodeType = Xml.XmlNodeType.Element Then
                                                    If Reader.Name.ToLower = "user" Then
                                                        '''''''''''''''''''''''''''''''''''''''''''''''''''' vbot/users/user
                                                        Dim newUser As New Account
                                                        Dim newUserName = Reader.GetAttribute("name")
                                                        newUser.Password = Reader.GetAttribute("password")
                                                        'newUser.Owner = Reader.GetAttribute("owner")
                                                        newUser.Permissions = {}
                                                        If Not Reader.IsEmptyElement Then : Do Until Reader.EOF : Reader.Read()
                                                                If Reader.NodeType = Xml.XmlNodeType.Element Then
                                                                    If Reader.Name.ToLower = "permissions" Then
                                                                        '''''''''''''''''''''''' vbot/users/user/permissions
                                                                        Dim CurrentText As String = ""
                                                                        If Not Reader.IsEmptyElement Then : Do Until Reader.EOF : Reader.Read()
                                                                                If Reader.NodeType = Xml.XmlNodeType.Element Then
                                                                                    If Not Reader.IsEmptyElement Then : Dim MiscElementName = Reader.Name : Do Until Reader.EOF : Reader.Read() : If Reader.NodeType = Xml.XmlNodeType.EndElement AndAlso Reader.Name = MiscElementName Then : Exit Do : End If : Loop
                                                                                    End If
                                                                                ElseIf Reader.NodeType = Xml.XmlNodeType.Text Then
                                                                                    CurrentText &= Reader.Value
                                                                                ElseIf Reader.NodeType = Xml.XmlNodeType.EndElement AndAlso Reader.Name = "permissions" Then : Exit Do
                                                                                End If
                                                                            Loop : End If
                                                                        For Each Line In CurrentText.Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries)
                                                                            AppendArray(newUser.Permissions, Line.Trim)
                                                                        Next

                                                                    ElseIf Not Reader.IsEmptyElement Then : Dim MiscElementName = Reader.Name : Do Until Reader.EOF : Reader.Read() : If Reader.NodeType = Xml.XmlNodeType.EndElement AndAlso Reader.Name = MiscElementName Then : Exit Do : End If : Loop
                                                                    End If
                                                                ElseIf Reader.NodeType = Xml.XmlNodeType.Text Then
                                                                    dUserInfo &= Reader.Value
                                                                ElseIf Reader.NodeType = Xml.XmlNodeType.EndElement AndAlso Reader.Name = "user" Then
                                                                    Accounts.Add(newUserName, newUser)
                                                                    Exit Do
                                                                End If
                                                            Loop : End If

                                                    ElseIf Not Reader.IsEmptyElement Then : Dim MiscElementName = Reader.Name : Do Until Reader.EOF : Reader.Read() : If Reader.NodeType = Xml.XmlNodeType.EndElement AndAlso Reader.Name = MiscElementName Then : Exit Do : End If : Loop
                                                    End If
                                                ElseIf Reader.NodeType = Xml.XmlNodeType.Text Then
                                                    dUserInfo &= Reader.Value
                                                ElseIf Reader.NodeType = Xml.XmlNodeType.EndElement AndAlso Reader.Name = "users" Then : Exit Do
                                                End If
                                            Loop : End If

                                    ElseIf Reader.Name.ToLower = "prefixes" Then
                                        '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' vbot/prefixes
                                        DefaultCommandPrefixes = {}
                                        ChannelCommandPrefixes.Clear()
                                        If Not Reader.IsEmptyElement Then : Do Until Reader.EOF : Reader.Read()
                                                If Reader.NodeType = Xml.XmlNodeType.Element Then
                                                    If Reader.Name.ToLower = "default" Then
                                                        ''''''''''''''''''''''''''''''''''''''''''''''''''' vbot/prefixes/default
                                                        Dim CurrentText As String = ""
                                                        If Not Reader.IsEmptyElement Then : Do Until Reader.EOF : Reader.Read()
                                                                If Reader.NodeType = Xml.XmlNodeType.Element Then
                                                                    If Not Reader.IsEmptyElement Then : Dim MiscElementName = Reader.Name : Do Until Reader.EOF : Reader.Read() : If Reader.NodeType = Xml.XmlNodeType.EndElement AndAlso Reader.Name = MiscElementName Then : Exit Do : End If : Loop
                                                                    End If
                                                                ElseIf Reader.NodeType = Xml.XmlNodeType.Text Then
                                                                    CurrentText &= Reader.Value
                                                                ElseIf Reader.NodeType = Xml.XmlNodeType.EndElement AndAlso Reader.Name = "default" Then
                                                                    For Each Prefix In CurrentText.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.RemoveEmptyEntries)
                                                                        AppendArray(DefaultCommandPrefixes, Prefix.Trim)
                                                                    Next
                                                                    Exit Do
                                                                End If
                                                            Loop : End If

                                                    ElseIf Reader.Name.ToLower = "except" Then
                                                        ''''''''''''''''''''''''''''''''''''''''''''''' vbot/prefixes/except
                                                        Dim Channel = Reader.GetAttribute("channel")
                                                        Dim CurrentText As String = ""
                                                        If Not Reader.IsEmptyElement Then : Do Until Reader.EOF : Reader.Read()
                                                                If Reader.NodeType = Xml.XmlNodeType.Element Then
                                                                    If Not Reader.IsEmptyElement Then : Dim MiscElementName = Reader.Name : Do Until Reader.EOF : Reader.Read() : If Reader.NodeType = Xml.XmlNodeType.EndElement AndAlso Reader.Name = MiscElementName Then : Exit Do : End If : Loop
                                                                    End If
                                                                ElseIf Reader.NodeType = Xml.XmlNodeType.Text Then
                                                                    CurrentText &= Reader.Value
                                                                ElseIf Reader.NodeType = Xml.XmlNodeType.EndElement AndAlso Reader.Name = "except" Then
                                                                    ChannelCommandPrefixes.Add(Channel, {})
                                                                    For Each Prefix In CurrentText.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.RemoveEmptyEntries)
                                                                        AppendArray(ChannelCommandPrefixes(Channel), Prefix.Trim)
                                                                    Next
                                                                    Exit Do
                                                                End If
                                                            Loop : End If

                                                    ElseIf Not Reader.IsEmptyElement Then : Dim MiscElementName = Reader.Name : Do Until Reader.EOF : Reader.Read() : If Reader.NodeType = Xml.XmlNodeType.EndElement AndAlso Reader.Name = MiscElementName Then : Exit Do : End If : Loop
                                                    End If
                                                ElseIf Reader.NodeType = Xml.XmlNodeType.EndElement AndAlso Reader.Name = "prefixes" Then : Exit Do
                                                End If
                                            Loop : End If

                                    ElseIf Not Reader.IsEmptyElement Then : Dim MiscElementName = Reader.Name : Do Until Reader.EOF : Reader.Read() : If Reader.NodeType = Xml.XmlNodeType.EndElement AndAlso Reader.Name = MiscElementName Then : Exit Do : End If : Loop
                                    End If
                                ElseIf Reader.NodeType = Xml.XmlNodeType.EndElement AndAlso Reader.Name = "vbot" Then : Exit Do
                                End If
                            Loop : End If

                    ElseIf Not Reader.IsEmptyElement Then : Dim MiscElementName = Reader.Name : Do Until Reader.EOF : Reader.Read() : If Reader.NodeType = Xml.XmlNodeType.EndElement AndAlso Reader.Name = MiscElementName Then : Exit Do : End If : Loop
                    End If
                End If
            Loop

            Reader.Close()
        End If
    End Sub

    ''' <summary>Loads user account data from the VBotUsers.ini configuration file.</summary>
    Public Sub LoadUsers()
        If Not My.Computer.FileSystem.FileExists("VBotUsers.ini") Then
        Else
            Try
                Dim Reader = My.Computer.FileSystem.OpenTextFileReader("VBotUsers.ini")

                Dim Section As String = "", Field As String = "", Value As String = ""
                Dim newUser As Account

                Do Until Reader.EndOfStream
                    Dim s = Reader.ReadLine
                    ' Check for comments.
                    If s.TrimStart.StartsWith(";") Then Continue Do

                    Dim Match As System.Text.RegularExpressions.Match
                    Match = System.Text.RegularExpressions.Regex.Match(s, "^\s*\[(?<Section>.*?)\]?\s*$")
                    If Match.Success Then
                        If Section <> "" Then Accounts.Add(Section, newUser)
                        Section = Match.Groups("Section").Value
                        If Not Accounts.ContainsKey(Section) Then
                            newUser = New Account With {.Permissions = New String() {}}
                        End If
                        Continue Do
                    End If
                    If Section = "" Then Continue Do

                    Match = System.Text.RegularExpressions.Regex.Match(s, "^\s*(?<Field>(?>[^=]*))=(?<Value>.*)$")
                    If Match.Success Then
                        Field = Match.Groups("Field").Value
                        Value = Match.Groups("Value").Value

                        Select Case Field.ToLower
                            Case "password", "pass"
                                newUser.Password = Value
                        End Select
                    ElseIf s.Trim <> "" Then
                        AppendArray(newUser.Permissions, s.Trim)
                    End If
                Loop
                Accounts.Add(Section, newUser)
                Reader.Close()

            Catch ex As Exception
                OutputLine("\cGRAY[\cREDERROR\cGRAY] \cWHITEI was unable to retrieve user data from the file: $k04" & ex.Message & "\r")
            End Try
        End If

    End Sub

    ''' <summary>Loads plugin information from the VBotPlugins.ini configuration file.</summary>
    Public Sub LoadPlugins()
        Dim Errors As Boolean
        If Not My.Computer.FileSystem.FileExists("VBotPlugins.ini") Then
        Else
            Try
                Dim Reader = My.Computer.FileSystem.OpenTextFileReader("VBotPlugins.ini")

                Dim Section As String = "", Field As String = "", Value As String = ""
                Dim Filename As String, Channels As String(), MinorChannels As String(), Label As String

                Do Until Reader.EndOfStream
                    Dim s = Reader.ReadLine
                    ' Check for comments.
                    If s.TrimStart.StartsWith(";") Then Continue Do

                    Dim Match As System.Text.RegularExpressions.Match
                    Match = System.Text.RegularExpressions.Regex.Match(s, "^\s*\[(?<Section>.*?)\]?\s*$")
                    If Match.Success Then
                        If Filename <> Nothing Then
                            Try
                                Output("  Loading plugin \cWHITE" & Section & "\r...")
                                LoadPlugin(Section, Filename, Channels)
                                Plugins(Section).Obj.MinorChannels = MinorChannels
                                Plugins(Section).Obj.MinorLabel = Label
                                OutputLine(" OK")
                            Catch ex As Exception
                                OutputLine("\cRED Failed\r")
                                LogError(Section, "Initialisation", ex)
                                Errors = True
                            End Try
                        End If

                        Section = Match.Groups("Section").Value
                        Continue Do
                    End If
                    If Section = "" Then Continue Do

                    Match = System.Text.RegularExpressions.Regex.Match(s, "^\s*(?<Field>(?>[^=]*))=(?<Value>.*)$")
                    If Match.Success Then
                        Field = Match.Groups("Field").Value
                        Value = Match.Groups("Value").Value

                        Select Case Field.ToLower
                            Case "filename", "file"
                                Filename = Value
                            Case "channels", "chans", "major"
                                Channels = Value.Split({","c}, StringSplitOptions.RemoveEmptyEntries)
                            Case "minorchannels", "minorchans", "minor"
                                MinorChannels = Value.Split({","c}, StringSplitOptions.RemoveEmptyEntries)
                            Case "minorlabel", "label", "prefix"
                                Label = Value
                        End Select
                    Else
                    End If
                Loop
                If Filename <> Nothing Then
                    Try
                        Output("  Loading plugin \cWHITE" & Section & "\r...")
                        LoadPlugin(Section, Filename, Channels)
                        Plugins(Section).Obj.MinorChannels = MinorChannels
                        Plugins(Section).Obj.MinorLabel = Label
                        OutputLine(" OK")
                    Catch ex As Exception
                        OutputLine("\cRED Failed\r")
                        LogError(Section, "Initialisation", ex)
                        Errors = True
                    End Try
                End If

                Reader.Close()

            Catch ex As Exception
                OutputLine("\cGRAY[\cREDERROR\cGRAY] \cWHITEI was unable to retrieve plugin data from the file: $k04" & ex.Message & "\r")
                Errors = True
            End Try
        End If

        If Errors Then
            OutputLine("\cREDSome plugins failed to load.\r")
            OutputLine("\cWHITEPress any key to continue, or close this window to cancel initialisation . . .")
            Console.ReadKey(True)
        End If
    End Sub

    ''' <summary>Loads plugin information from the VBotPlugins.xml configuration file.</summary>
    <Obsolete("Using XML to store configuration is a mess and resulted in crashes, so we're not doing it any more. Use LoadPlugins() instead.")>
    Public Sub LoadPluginsXML()
        Plugins.Clear()

        If My.Computer.FileSystem.FileExists("VBotPlugins.xml") Then
            Dim Reader = Xml.XmlReader.Create("VBotPlugins.xml")

            Do Until Reader.EOF : Reader.Read()
                If Reader.NodeType = Xml.XmlNodeType.Element Then
                    If Reader.Name.ToLower = "vbotplugins" Then
                        ''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' vbotplugins
                        If Not Reader.IsEmptyElement Then : Do Until Reader.EOF : Reader.Read()
                                If Reader.NodeType = Xml.XmlNodeType.Element Then
                                    If Reader.Name.ToLower = "plugin" Then
                                        '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' vbotplugins/plugin
                                        Dim Key = Reader.GetAttribute("key")
                                        Dim Filename = Reader.GetAttribute("filename")
                                        Dim Channels = If(Reader.GetAttribute("channels"), "").Split({","c}, StringSplitOptions.RemoveEmptyEntries)
                                        Dim MinorChannels = If(Reader.GetAttribute("minorchannels"), "").Split({","c}, StringSplitOptions.RemoveEmptyEntries)
                                        Dim MinorLabel = If(Reader.GetAttribute("minorlabel"), "").Replace("$1", Chr(1)).Replace("$b", Chr(2)).Replace("$k", Chr(3)).Replace("$o", Chr(15)).Replace("$r", Chr(22)).Replace("$u", Chr(31)).Replace("$$", "$")

                                        If Not Reader.IsEmptyElement Then : Do Until Reader.EOF : Reader.Read()
                                                If Reader.NodeType = Xml.XmlNodeType.Element Then
                                                    If Not Reader.IsEmptyElement Then : Dim MiscElementName = Reader.Name : Do Until Reader.EOF : Reader.Read() : If Reader.NodeType = Xml.XmlNodeType.EndElement AndAlso Reader.Name = MiscElementName Then : Exit Do : End If : Loop : End If
                                                ElseIf Reader.NodeType = Xml.XmlNodeType.EndElement AndAlso Reader.Name = "plugin" Then
                                                    If Filename = Nothing Then Filename = "VBotPlugins\" & Key & ".dll"
                                                    Exit Do
                                                End If
                                            Loop : End If
                                        Try
                                            LoadPlugin(Key, Filename, Channels)
                                            Plugins(Key).Obj.MinorChannels = MinorChannels
                                            Plugins(Key).Obj.MinorLabel = MinorLabel
                                        Catch ex As Exception
                                            LogError(Key, "Initialisation", ex)
                                        End Try

                                    ElseIf Not Reader.IsEmptyElement Then : Dim MiscElementName = Reader.Name : Do Until Reader.EOF : Reader.Read() : If Reader.NodeType = Xml.XmlNodeType.EndElement AndAlso Reader.Name = MiscElementName Then : Exit Do : End If : Loop
                                    End If
                                ElseIf Reader.NodeType = Xml.XmlNodeType.EndElement AndAlso Reader.Name = "vbotplugins" Then : Exit Do
                                End If
                            Loop : End If

                    ElseIf Not Reader.IsEmptyElement Then : Dim MiscElementName = Reader.Name : Do Until Reader.EOF : Reader.Read() : If Reader.NodeType = Xml.XmlNodeType.EndElement AndAlso Reader.Name = MiscElementName Then : Exit Do : End If : Loop
                    End If
                End If
            Loop

            Reader.Close()
        End If
    End Sub

    ''' <summary>Saves configuration data to the VBotConfig.ini general configuration file.</summary>
    Public Sub SaveConfig()
        Dim Writer = My.Computer.FileSystem.OpenTextFileWriter("VBotConfig.ini", False)

        Writer.WriteLine("[Me]")
        Writer.WriteLine("Nicknames=" & String.Join(",", dNicknames))
        Writer.WriteLine("Username=" & dUsername)
        Writer.WriteLine("FullName=" & dFullName)
        Writer.WriteLine("UserInfo=" & dUserInfo)
        If dAvatar <> Nothing Then Writer.WriteLine("Avatar=" & dAvatar)

        For Each Connection In Connections
            Writer.WriteLine()
            Writer.WriteLine("[" & Connection.Address & "]")
            Writer.WriteLine("Nicknames=" & String.Join(",", Connection.Nicknames))
            Writer.WriteLine("Username=" & Connection.Username)
            Writer.WriteLine("FullName=" & Connection.FullName)
            If AutoJoinChannels.ContainsKey(Connection.Address.ToLower) Then _
                Writer.WriteLine("Autojoin=" & String.Join(",", AutoJoinChannels(Connection.Address.ToLower)))
            Writer.WriteLine("SSL=" & If(Connection.IsUsingSSL, "Yes", "No"))
            Writer.WriteLine("AllowInvalidCertificate=" & If(Connection.AllowInvalidCertificate, "Yes", "No"))
            If NickServ.ContainsKey(Connection.Address.ToLower) Then
                Writer.WriteLine("NickServ-Nicknames=" & String.Join(",", NickServ(Connection.Address.ToLower).RegisteredNicknames))
                Writer.WriteLine("NickServ-Password=" & NickServ(Connection.Address.ToLower).Password)
                Writer.WriteLine("NickServ-AnyNickname=" & If(NickServ(Connection.Address.ToLower).AnyNickname, "Yes", "No"))
                Writer.WriteLine("NickServ-UseGhostCommand=" & If(NickServ(Connection.Address.ToLower).UseGhostCommand, "Yes", "No"))
                Writer.WriteLine("NickServ-GhostCommand=" & NickServ(Connection.Address.ToLower).GhostCommand)
                Writer.WriteLine("NickServ-IdentifyCommand=" & NickServ(Connection.Address.ToLower).IdentifyCommand)
                Writer.WriteLine("NickServ-Hostmask=" & NickServ(Connection.Address.ToLower).Hostmask)
                Writer.WriteLine("NickServ-RequestMask=" & NickServ(Connection.Address.ToLower).RequestMask)
            End If
        Next
        Writer.WriteLine()
        Writer.WriteLine("[Prefixes]")
        For Each Connection In ChannelCommandPrefixes
            Writer.WriteLine(Connection.Key & "=" & String.Join(" ", Connection.Value))
        Next

        Writer.Close()
    End Sub

    ''' <summary>Saves user account data to the VBotUsers.ini configuration file.</summary>
    Public Sub SaveUsers()
        Dim Writer = My.Computer.FileSystem.OpenTextFileWriter("VBotUsers.ini", False)

        For Each User In Accounts
            Writer.WriteLine("[" & User.Key & "]")
            Writer.WriteLine("Password=" & User.Value.Password)
            For Each Permission In User.Value.Permissions
                Writer.WriteLine(Permission)
            Next
            Writer.WriteLine()
        Next

        Writer.Close()
    End Sub

    ''' <summary>Saves plugin information to the VBotPlugins.ini configuration file.</summary>
    Public Sub SavePlugins()
        Dim Writer = My.Computer.FileSystem.OpenTextFileWriter("VBotPlugins.ini", False)

        For Each Plugin In Plugins
            Writer.WriteLine("[" & Plugin.Key & "]")
            Writer.WriteLine("Filename=" & Plugin.Value.Filename)
            If Plugin.Value.Obj.Channels IsNot Nothing Then Writer.WriteLine("Channels=" & String.Join(",", Plugin.Value.Obj.Channels))
            If Plugin.Value.Obj.MinorChannels IsNot Nothing Then Writer.WriteLine("MinorChannels=" & String.Join(",", Plugin.Value.Obj.MinorChannels))
            If Plugin.Value.Obj.MinorLabel IsNot Nothing Then Writer.WriteLine("Label=" & Plugin.Value.Obj.MinorLabel)
            Writer.WriteLine()

            Plugin.Value.Obj.OnSave()
        Next

        Writer.Close()
    End Sub

    ''' <summary>Saves configuration data to the VBotConfig.xml general configuration file.</summary>
    <Obsolete("Using XML to store configuration is a mess and resulted in crashes, so we're not doing it any more. Use SaveConfig() instead.")>
    Public Sub SaveConfigXML()
        Dim Writer = Xml.XmlWriter.Create("VBotConfig.xml", New Xml.XmlWriterSettings With {.Indent = True, .IndentChars = vbTab, .NewLineOnAttributes = False})
        Writer.WriteStartElement("vbot")

        Writer.WriteStartElement("me")

        Writer.WriteAttributeString("username", dUsername)
        Writer.WriteAttributeString("fullname", dFullName.Replace("$", "$$").Replace(Chr(1), "$1").Replace(Chr(2), "$b").Replace(Chr(3), "$k").Replace(Chr(15), "$o").Replace(Chr(22), "$r").Replace(Chr(31), "$u"))
        If Not dUserInfo.Contains(vbCr) And Not dUserInfo.Contains(vbLf) Then
            Writer.WriteAttributeString("userinfo", dUserInfo.Replace("$", "$$").Replace(Chr(1), "$1").Replace(Chr(2), "$b").Replace(Chr(3), "$k").Replace(Chr(15), "$o").Replace(Chr(22), "$r").Replace(Chr(31), "$u"))
        Else
            Writer.WriteElementString("userinfo", dUserInfo.Replace("$", "$$").Replace(Chr(1), "$1").Replace(Chr(2), "$b").Replace(Chr(3), "$k").Replace(Chr(15), "$o").Replace(Chr(22), "$r").Replace(Chr(31), "$u"))
        End If

        For Each lNickname In dNicknames
            Writer.WriteAttributeString("nickname", lNickname)
        Next
        Writer.WriteEndElement()


        Writer.WriteStartElement("connections")

        For Each Connection In Connections
            Writer.WriteStartElement("connection")
            Writer.WriteAttributeString("address", Connection.Address)
            Writer.WriteAttributeString("port", Connection.Port)
            Writer.WriteAttributeString("username", Connection.Username)
            Writer.WriteAttributeString("fullname", Connection.FullName.Replace("$", "$$").Replace(Chr(1), "$1").Replace(Chr(2), "$b").Replace(Chr(3), "$k").Replace(Chr(15), "$o").Replace(Chr(22), "$r").Replace(Chr(31), "$u"))
            'If Not Connection.UserInfo.Contains(vbCr) And Not Connection.UserInfo.Contains(vbLf) Then
            '    Writer.WriteAttributeString("userinfo", Connection.UserInfo.Replace("$", "$$").Replace(Chr(1), "$1").Replace(Chr(2), "$b").Replace(Chr(3), "$k").Replace(Chr(15), "$o").Replace(Chr(22), "$r").Replace(Chr(31), "$u")))
            'Else
            '    Writer.WriteElementString("userinfo", Connection.UserInfo.Replace("$", "$$").Replace(Chr(1), "$1").Replace(Chr(2), "$b").Replace(Chr(3), "$k").Replace(Chr(15), "$o").Replace(Chr(22), "$r").Replace(Chr(31), "$u")))
            'End If

            For Each lNickname In Connection.Nicknames
                Writer.WriteElementString("nickname", lNickname)
            Next
            If AutoJoinChannels.ContainsKey(Connection.Address.ToLower) Then
                For Each Channel In AutoJoinChannels(Connection.Address.ToLower)
                    Writer.WriteElementString("autojoin", Channel)
                Next
            End If
            Writer.WriteEndElement()
        Next
        Writer.WriteEndElement()

        Writer.WriteStartElement("users")

        For Each User In Accounts
            Writer.WriteStartElement("user")
            Writer.WriteAttributeString("name", User.Key)
            Writer.WriteAttributeString("password", User.Value.Password)
            'Writer.WriteAttributeString("owner", If(User.Value.Owner, "true", "false"))
            'If Not Connection.UserInfo.Contains(vbCr) And Not Connection.UserInfo.Contains(vbLf) Then
            '    Writer.WriteAttributeString("userinfo", Connection.UserInfo)
            'Else
            '    Writer.WriteElementString("userinfo", Connection.UserInfo)
            'End If

            Writer.WriteElementString("permissions", String.Join(vbCrLf, User.Value.Permissions))
            Writer.WriteEndElement()
        Next
        Writer.WriteEndElement()

        Writer.WriteStartElement("prefixes")

        Writer.WriteElementString("default", String.Join(vbCrLf, DefaultCommandPrefixes))

        For Each Channel In ChannelCommandPrefixes
            Writer.WriteStartElement("except")
            Writer.WriteAttributeString("channel", Channel.Key)
            Writer.WriteValue(String.Join(vbCrLf, Channel.Value))
            Writer.WriteEndElement()
        Next

        Writer.WriteEndElement()

        Writer.WriteEndElement()

        Writer.Close()
    End Sub

    ''' <summary>Saves plugin information to the VBotPlugins.xml configuration file.</summary>
    <Obsolete("Using XML to store configuration is a mess and resulted in crashes, so we're not doing it any more. Use SavePlugins() instead.")>
    Public Sub SavePluginsXML()
        Dim Writer = Xml.XmlWriter.Create("VBotPlugins.xml", New Xml.XmlWriterSettings With {.Indent = True, .IndentChars = vbTab, .NewLineOnAttributes = False})
        Writer.WriteStartElement("vbotplugins")

        For Each Plugin In Plugins
            Writer.WriteStartElement("plugin")

            Writer.WriteAttributeString("key", Plugin.Key)
            Writer.WriteAttributeString("filename", Plugin.Value.Filename)
            Writer.WriteAttributeString("channels", String.Join(",", Plugin.Value.Obj.Channels))
            Writer.WriteAttributeString("minorchannels", String.Join(",", Plugin.Value.Obj.MinorChannels))
            Writer.WriteAttributeString("minorlabel", Plugin.Value.Obj.MinorLabel.Replace("$", "$$").Replace(Chr(1), "$1").Replace(Chr(2), "$b").Replace(Chr(3), "$k").Replace(Chr(15), "$o").Replace(Chr(22), "$r").Replace(Chr(31), "$u"))

            Plugin.Value.Obj.OnSave()

            Writer.WriteEndElement()
        Next

        Writer.WriteEndElement()
        Writer.Close()
    End Sub

#End Region

    Public Sub CheckMessage(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal Message As String)
        ' Check for minor channels.
        For Each m In Plugins
            Dim Command = Message.Split(" "c)(0)
            For Each c In m.Value.Obj.CommandPrefixes(Connection, Channel)
                If Command.StartsWith(c) Then _
                    If Command = c Then Command = "" Else Command = Command.Substring(1)
            Next

            If Command.ToLower = m.Key.ToLower And m.Value.Obj.UseGlobalKeyCommand AndAlso (m.Value.Obj.IsMinorChannel(Connection, Channel)) Then
                Try
                    m.Value.Obj.RunCommand(Connection, Sender, Channel, Message(0) & Message.Split({" "c}, 2)(1), True)
                Catch ex As Exception
                    LogError(m.Key, "RunCommand", ex)
                End Try
                Return
            End If
        Next

        ' Check for global commands.

        Dim Names As String()
        Dim Scope As Plugin.CommandAttribute.CommandScope
        Dim Method As Reflection.MethodInfo, attr As Object

        For Each Plugin In Plugins
            Dim Command = Message.Split(" "c)(0)
            For Each c In Plugin.Value.Obj.CommandPrefixes(Connection, Channel)
                If Command.StartsWith(c) Then _
                    If Command = c Then Command = "" Else Command = Command.Substring(1)
            Next
            For Each Method In Plugin.Value.Obj.GetType.GetMethods
                For Each attr In Method.GetCustomAttributes(False)
                    If TypeOf attr Is Plugin.CommandAttribute Then
                        Names = CType(attr, Plugin.CommandAttribute).Names
                        Scope = CType(attr, Plugin.CommandAttribute).Scope

                        If (Scope And VBot.Plugin.CommandAttribute.CommandScope.GlobalCommand) AndAlso Names.Contains(Command) Then
                            Try
                                Plugin.Value.Obj.RunCommand(Connection, Sender, Channel, Message)
                            Catch ex As Exception
                                LogError(Plugin.Key, "RunCommand", ex)
                            End Try
                        End If
                    End If
                Next
            Next
        Next

    End Sub

    Private Sub NickServCheck(ByVal sender As IRCConnection, ByVal User As String, ByVal Message As String)
        ' Check if we need to identify.
        Dim Data As NickServData
        If NickServ.ContainsKey(sender.Address.ToLower) Then
            Data = NickServ(sender.Address.ToLower)
        Else
            Return
        End If

        If User.ToLower Like Data.Hostmask.ToLower And Message Like Data.RequestMask Then
            NickServIdentify(sender, User, Data)
        End If
    End Sub

    Private Sub NickServIdentify(ByVal sender As IRCConnection, ByVal User As String, ByVal Data As NickServData)
        If Data.IdentifyTime = Nothing OrElse (Now - Data.IdentifyTime > TimeSpan.FromSeconds(60)) Then
            sender.Send(Data.IdentifyCommand.Replace("$target", User.Split("!"c)(0)).Replace("$nickname", Data.RegisteredNicknames(0)).Replace("$password", Data.Password))
            Data.IdentifyTime = Now
        End If
    End Sub

    Private Sub OnCTCPMessage(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Message As String)
        Select Case Message.Split(" "c)(0).ToUpper
            Case "PING"
                Connection.Send(String.Format("NOTICE {0} :{1}PING{2}{1}", Sender, Chr(1), If(Message.Contains(" "c), " " & Message.Split({" "c}, 2)(1), "")))
            Case "ERRMSG"
                Connection.Send(String.Format("NOTICE {0} :{1}ERRMSG{2}{1}", Sender, Chr(1), If(Message.Contains(" "c), " " & Message.Split({" "c}, 2)(1), "")))
            Case "VERSION"
                Connection.Send(String.Format("NOTICE {0} :{1}VERSION {2}{1}", Sender, Chr(1), Version))
            Case "TIME"
                Connection.Send(String.Format("NOTICE {0} :{1}TIME {2}{1}", Sender, Chr(1), Now.ToString("dddd d MMMM yyyy HH:mm:ss")))
            Case "FINGER"
                Dim ReadableIdleTime As String = "", IdleTime As TimeSpan
                IdleTime = Now - Connection.LastSpoke
                If IdleTime.Days >= 1 Then ReadableIdleTime &= ", " & CStr(Int(IdleTime.Days)) & " day" & If(Int(IdleTime.Days) = 1, "", "s")
                If IdleTime.Hours >= 1 Then ReadableIdleTime &= ", " & CStr(Int(IdleTime.Hours)) & " hour" & If(Int(IdleTime.Hours) = 1, "", "s")
                If IdleTime.Minutes >= 1 Then ReadableIdleTime &= ", " & CStr(Int(IdleTime.Minutes)) & " minute" & If(Int(IdleTime.Minutes) = 1, "", "s")
                ReadableIdleTime &= ", " & CStr(Int(IdleTime.Seconds)) & " second" & If(Int(IdleTime.Seconds) = 1, "", "s")

                Connection.Send(String.Format("NOTICE {0} :{1}FINGER {4}: {2}; idle for {3}.{1}", Sender, Chr(1), dUserInfo, ReadableIdleTime.Substring(2), dNicknames(0)))
            Case "USERINFO"
                Connection.Send(String.Format("NOTICE {0} :{1}USERINFO {2}{1}", Sender, Chr(1), dUserInfo))
            Case "AVATAR"
                Connection.Send(String.Format("NOTICE {0} :{1}AVATAR {2}{1}", Sender, Chr(1), dAvatar))
            Case "CLIENTINFO"
                Dim Response As String
                Select Case If(Message.Split(" "c).ElementAtOrDefault(1), "").ToUpper
                    Case "PING"
                        Response = "PING <stamp>: Verifies that I am receiving your message. This is often used to establish the connection latency."
                    Case "ERRMSG"
                        Response = "ERRMSG <message>: This is the general response to an unknown query. A query of ERRMSG will return the same message back."
                    Case "VERSION"
                        Response = "VERSION: Returns the name and version of my client."
                    Case "TIME"
                        Response = "TIME: Returns my local date and time."
                    Case "FINGER"
                        Response = "FINGER: Returns my user info and the amount of time I have been idle for."
                    Case "USERINFO"
                        Response = "USERINFO: Returns information about me."
                    Case "CLIENTINFO"
                        Response = "CLIENTINFO [query]: Returns information about a CTCP query."
                    Case "AVATAR"
                        Response = "AVATAR: Returns a URL to my avatar, if applicable."
                    Case ""
                        Response = ":I recognise the following CTCP queries: CLENTINFO, ERRMSG, FINGER, PING, TIME, USERINFO, VERSION, AVATAR"
                    Case Else
                        Connection.Send(String.Format("NOTICE {0} :{1}ERRMSG :I do not recognise {2} as a CTCP query.{1}", Sender, Chr(1), If(Message.Split(" "c).ElementAtOrDefault(1), "")))
                        Return
                End Select
                Connection.Send(String.Format("NOTICE {0} :{1}CLIENTINFO {2}{1}", Sender, Chr(1), Response))
        End Select
    End Sub

    Public Function Nickname()
        If dNicknames.Count = 0 Then Return "VBot"
        Return dNicknames(0)
    End Function
    Public Function Nickname(ByVal Connection As IRCConnection)
        If Connection Is Nothing Then Return Nickname()
        Return Connection.Nickname
    End Function
    Public Function Nickname(ByVal Index As Integer)
        If Connections.Count <= Index Then Return Nickname()
        Return Connections(Index).Nickname
    End Function

    ' TODO: Optimise this function.
    Public Function UserHasPermission(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal User As IRCConnection.IRCUser, ByVal Permission As String)
        If Permission Is Nothing OrElse Permission = "" Then Return True
        UserHasPermission = False

        Dim AccountName As String
        Dim UserPermissions As New List(Of String)

        If Connection Is Nothing Then
            If Identifications.ContainsKey(Channel.Split({"/"c}, 3)(0) & "/" & User.Nickname) Then
                AccountName = Identifications(Channel.Split({"/"c}, 3)(0) & "/" & User.Nickname).AccountName
            End If
        Else
            If Identifications.ContainsKey(Connection.Address & "/" & User.Nickname) Then
                AccountName = Identifications(Connection.Address & "/" & User.Nickname).AccountName
            End If
        End If

        For Each Account In Accounts
            If Account.Key = "*" Then
                UserPermissions.AddRange(Account.Value.Permissions)
            ElseIf Account.Key.Split(":"c)(0) = "$a" Then
                ' TODO: These are NYI.
            ElseIf {"$q", "$p", "$o", "$h", "$v", "$V"}.Contains(Account.Key.Split(":"c)(0)) Then
                Dim lChannel As IRCConnection.IRCChannel = Nothing
                If Connection Is Nothing Then Continue For
                If Account.Key.Split(":"c).Count = 1 Then
                    If Not Connection.Channels.ContainsKey(Channel) Then Continue For
                    lChannel = Connection.Channels(Channel)
                ElseIf Account.Key.Split({":"c}, 2)(1).Contains("/") Then
                    For Each Connection In Connections
                        If Connection.Address.ToLower = Account.Key.Split({":"c}, 2)(1).Split({"/"c}, 2)(0).ToLower Then
                            If Not Connection.Channels.TryGetValue(Channel, lChannel) Then Exit For
                        End If
                    Next
                Else
                    If Not Connection.Channels.TryGetValue(Channel, lChannel) Then Continue For
                End If
                If lChannel Is Nothing Then Continue For
                If Not lChannel.Users.ContainsKey(User.Nickname) Then Continue For
                Select Case Account.Key(1)
                    Case "q"c
                        If lChannel.Users(User.Nickname).ChannelAccess >= IRCConnection.ChannelAccessModes.Owner Then UserPermissions.AddRange(Account.Value.Permissions)
                    Case "p"c
                        If lChannel.Users(User.Nickname).ChannelAccess >= IRCConnection.ChannelAccessModes.Admin Then UserPermissions.AddRange(Account.Value.Permissions)
                    Case "o"c
                        If lChannel.Users(User.Nickname).ChannelAccess >= IRCConnection.ChannelAccessModes.Op Then UserPermissions.AddRange(Account.Value.Permissions)
                    Case "h"c
                        If lChannel.Users(User.Nickname).ChannelAccess >= IRCConnection.ChannelAccessModes.HalfOp Then UserPermissions.AddRange(Account.Value.Permissions)
                    Case "v"c
                        If lChannel.Users(User.Nickname).ChannelAccess >= IRCConnection.ChannelAccessModes.Voice Then UserPermissions.AddRange(Account.Value.Permissions)
                    Case "V"c
                        If lChannel.Users(User.Nickname).ChannelAccess >= IRCConnection.ChannelAccessModes.HalfVoice Then UserPermissions.AddRange(Account.Value.Permissions)
                End Select
            ElseIf Account.Key.Contains("!") Then
                If Account.Key.EndsWith("!") Or Account.Key.EndsWith("!*") Then
                    If User.Nickname.ToLower Like Account.Key.Split("!"c)(0).ToLower Then _
                        UserPermissions.AddRange(Account.Value.Permissions)
                Else
                    If CStr(User).ToLower Like Account.Key.ToLower Then _
                        UserPermissions.AddRange(Account.Value.Permissions)
                End If
            ElseIf AccountName IsNot Nothing AndAlso Account.Key.ToLower = AccountName.ToLower Then
                UserPermissions.AddRange(Account.Value.Permissions)
            End If
        Next

        Return UserHasPermissionSub(UserPermissions.ToArray, Permission)
    End Function
    Public Function UserHasPermission(ByVal AccountName As String, ByVal Permission As String)
        Return UserHasPermissionSub(Accounts(AccountName).Permissions, Permission)
    End Function
    Public Function UserHasPermissionSub(ByVal Permissions() As String, ByVal Permission As String)
        If Permission Is Nothing OrElse Permission = "" Then Return True
        UserHasPermissionSub = False

        If Permission.Split("."c)(0) <> "irc" AndAlso Permissions.Contains("*", System.StringComparer.OrdinalIgnoreCase) Then UserHasPermissionSub = True

        Dim fields = Permission.Split("."c)
        Dim pos As Integer = -1
        Do While pos < Permission.Length
            pos = Permission.IndexOf(".", pos + 1)
            If pos = -1 Then Exit Do

            If Permissions.Contains(Permission.Substring(0, pos) & ".*", System.StringComparer.OrdinalIgnoreCase) Then UserHasPermissionSub = True
            If Permissions.Contains("-" & Permission.Substring(0, pos) & ".*", System.StringComparer.OrdinalIgnoreCase) Then UserHasPermissionSub = False

            pos += 1
        Loop

        If Permissions.Contains(Permission, System.StringComparer.OrdinalIgnoreCase) Then UserHasPermissionSub = True
        If Permissions.Contains("-" & Permission, System.StringComparer.OrdinalIgnoreCase) Then UserHasPermissionSub = False
    End Function

    Public Function Choose(ByVal ParamArray args() As Object)
        Randomize()
        Return args(Int(Rnd() * args.Count))
    End Function
    Public Function Choose(ByVal Seed As Double, ByVal ParamArray args() As Object)
        Rnd(-1)
        Randomize(Seed)
        Return args(Int(Rnd() * args.Count))
    End Function

    Public Sub Die()
        End
    End Sub

    Friend Sub LogConnectionError(ByVal Server As IRCConnection, ByVal ex As Exception)
        Dim RealException As Exception
        If TypeOf ex Is System.Reflection.TargetInvocationException Then
            RealException = ex.InnerException
        Else
            RealException = ex
        End If

        OutputLine("\cGRAY[\cREDERROR\cGRAY] occurred in the connection to '\cWHITE" & Server.Address & "\cGRAY!")
        OutputLine("\cGRAY[\cDKREDERROR\cGRAY] \cWHITE" & RealException.GetType.FullName & " :\cGRAY " & RealException.Message & "\r")
        For Each Line In RealException.StackTrace.Split({vbCrLf, vbCr, vbLf}, 0)
            OutputLine("\cGRAY[\cDKREDERROR\cGRAY] \cGRAY" & Line & "\r")
        Next

        Dim ErrorLogWriter = My.Computer.FileSystem.OpenTextFileWriter("VBotErrorLog.txt", True)
        ErrorLogWriter.WriteLine("[" & Now.ToString & "] ERROR occurred in the connection to '" & Server.Address & "!")
        ErrorLogWriter.WriteLine("        " & RealException.Message)
        For Each Line In RealException.StackTrace.Split({vbCrLf, vbCr, vbLf}, 0)
            ErrorLogWriter.WriteLine("        " & Line)
        Next
        ErrorLogWriter.WriteLine()
        ErrorLogWriter.Close()
    End Sub

    Friend Sub LogError(ByVal PluginKey As String, ByVal Procedure As String, ByVal ex As Exception)
        Dim RealException As Exception
        If TypeOf ex Is System.Reflection.TargetInvocationException Then
            RealException = ex.InnerException
        Else
            RealException = ex
        End If

        OutputLine("\cGRAY[\cREDERROR\cGRAY] occurred in plugin '\cWHITE" & PluginKey & "\cGRAY' in procedure \cWHITE" & Procedure & "\cGRAY!")
        OutputLine("\cGRAY[\cDKREDERROR\cGRAY] \cWHITE" & RealException.GetType.FullName & " :\cGRAY " & RealException.Message & "\r")
        For Each Line In RealException.StackTrace.Split({vbCrLf, vbCr, vbLf}, 0)
            OutputLine("\cGRAY[\cDKREDERROR\cGRAY] \cGRAY" & Line & "\r")
        Next

        Dim ErrorLogWriter = My.Computer.FileSystem.OpenTextFileWriter("VBotErrorLog.txt", True)
        ErrorLogWriter.WriteLine("[" & Now.ToString & "] ERROR occurred in plugin '" & PluginKey & "' in procedure " & Procedure & "!")
        ErrorLogWriter.WriteLine("        " & RealException.Message)
        For Each Line In RealException.StackTrace.Split({vbCrLf, vbCr, vbLf}, 0)
            ErrorLogWriter.WriteLine("        " & Line)
        Next
        ErrorLogWriter.WriteLine()
        ErrorLogWriter.Close()
    End Sub

    Public Class WaitData
        Public Response As String
    End Class

    Dim Waiting As New Dictionary(Of String, WaitData)(StringComparer.OrdinalIgnoreCase)
    Public Function WaitForMessage(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal Nickname As String, Optional ByVal Timeout As UInteger = 0) As String
        Dim data = New WaitData With {.Response = Nothing}

        If Channel = "@" Or Channel = "PM" Then Channel = Nickname

        Waiting.Add(If(Connection Is Nothing, "", Connection.Address & "/") & Channel & "/" & Nickname, data)

        Dim stwWait As Stopwatch = Nothing
        If Timeout > 0 Then stwWait = Stopwatch.StartNew

        Do Until (Timeout > 0 AndAlso stwWait.ElapsedMilliseconds >= Timeout * 1000) Or data.Response IsNot Nothing
            Threading.Thread.Sleep(500)
        Loop

        Waiting.Remove(If(Connection Is Nothing, "", Connection.Address & "/") & Channel & "/" & Nickname)
        Return data.Response
    End Function

    Public Function Identify(ByVal Target As String, ByVal AccountName As String, ByVal Password As String, ByRef Identification As Identification) As Boolean
        Return Identify(Target, AccountName, Password, Identification, Nothing)
    End Function
    Public Function Identify(ByVal Target As String, ByVal AccountName As String, ByVal Password As String, ByRef Identification As Identification, ByRef Message As String) As Boolean
        ' A lot of this procedure is based on the information here: https://crackstation.net/hashing-security.htm
        ' I recommend giving it a read.

        ' Check for an account.
        Dim Account As Account
        If Not Accounts.TryGetValue(AccountName, Account) Then
            Message = "The account name or password is invalid."
            Identification = Nothing
            Return False
        Else
            If Identifications.TryGetValue(Target, Identification) Then
                Message = "You are already identified as $k12" & Identifications(Target).AccountName & "$o."
                Return False
            Else
                'Time to authenticate.
                Dim Salt(31) As Byte, OHash(31) As Byte
                Dim Hash() As Byte, sbHash As New StringBuilder

                ' Retrieve the salt from the account.
                For i = 0 To 31
                    Salt(i) = Convert.ToByte(Account.Password.Substring(i * 2, 2), 16)
                Next
                ' Retrieve the correct password hash from the account.
                For i = 0 To 31
                    OHash(i) = Convert.ToByte(Account.Password.Substring(i * 2 + 64, 2), 16)
                Next

                ' Compute the hash of the given password with the salt.
                Dim SHA256M As New System.Security.Cryptography.SHA256Managed
                Hash = SHA256M.ComputeHash(Salt.Concat(System.Text.Encoding.UTF8.GetBytes(Password)).ToArray)

                ' We use a very roundabout, but time-constant algorithm to check for equality. Even though I know timing attacks are basically impossible over IRC.
                'If there is no difference between the two hashes, the password is correct.
                If SlowEquals(OHash, Hash) Then
                    ' Authentication successful.
                    Identification = New Identification With {.AccountName = AccountName, .Channels = New List(Of String)}
                    Identifications.Add(Target, Identification)
                    Message = "You have identified successfully as $k09" & AccountName & "$o."
                    Return True
                Else
                    Message = "The account name or password is invalid."
                    Identification = Nothing
                    Return False
                End If
            End If
        End If
    End Function

    Public Function SlowEquals(ByVal Data1() As Byte, ByVal Data2() As Byte) As Boolean
        Dim Diff As Integer = Data1.Length Xor Data2.Length  ' The XOr operation returns zero only if the two operands are the same.
        For i = 0 To 31
            Diff = Diff Or (Data1(i) Xor Data2(i))
        Next
        Return (Diff = 0)
    End Function


#Region "IRC events"

    Public Sub EventCheck(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal ProcedureName As String, ByVal ParamArray Parameters As Object())
        Dim Handled As Boolean = False
        For Each m In Plugins
            If m.Value.Obj.IsMajorChannel(Connection, Channel) OrElse (m.Value.Obj.ListenInMinorChannels AndAlso m.Value.Obj.IsMinorChannel(Connection, Channel)) Then
                Try
                    CallByName(m.Value.Obj, ProcedureName, CallType.Method, Parameters)
                Catch ex As Exception
                    LogError(m.Key, ProcedureName, ex)
                End Try
            End If
            If Handled Then Exit For
        Next
    End Sub

    Public Sub OnAwayCancelled(ByVal Connection As IRCConnection, ByVal Message As String)
        EventCheck(Connection, "*", "OnAwayCancelled", {Connection, Message})
    End Sub
    Public Sub OnAway(ByVal Connection As IRCConnection, ByVal Message As String)
        EventCheck(Connection, "*", "OnAway", {Connection, Message})
    End Sub
    Public Sub OnBanList(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal BannedUser As String, ByVal BanningUser As String, ByVal Time As Date)
        EventCheck(Connection, "*", "OnBanList", {Connection, Channel, BannedUser, BanningUser, Time})
    End Sub
    Public Sub OnBanListEnd(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal Message As String)
        EventCheck(Connection, "*", "OnBanListEnd", {Connection, Channel, Message})
    End Sub
    Public Sub OnNicknameChange(ByVal Connection As IRCConnection, ByVal User As IRCConnection.IRCUser, ByVal NewNick As String)
        If Identifications.ContainsKey(Connection.Address & "/" & User.Nickname) Then
            Dim Identification = Identifications(Connection.Address & "/" & User.Nickname)
            Identifications.Remove(Connection.Address & "/" & User.Nickname)
            Identifications.Add(Connection.Address & "/" & NewNick, Identification)
        End If

        EventCheck(Connection, "*", "OnNicknameChange", {Connection, User, NewNick})
    End Sub
    Public Sub OnNicknameChangeSelf(ByVal Connection As IRCConnection, ByVal User As IRCConnection.IRCUser, ByVal NewNick As String)
        EventCheck(Connection, "*", "OnNicknameChangeSelf", {Connection, User, NewNick})
    End Sub
    Public Sub OnChannelAction(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Message As String)
        EventCheck(Connection, Channel, "OnChannelAction", {Connection, Sender, Channel, Message})
    End Sub
    Public Sub OnChannelActionHighlight(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Message As String)
        EventCheck(Connection, Channel, "OnChannelActionHighlight", {Connection, Sender, Channel, Message})
    End Sub
    Public Sub OnChannelAdmin(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal Target As String)
        EventCheck(Connection, Channel, "OnChannelAdmin", {Connection, Sender, Channel, Target})
    End Sub
    Public Sub OnChannelAdminSelf(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal Target As String)
        EventCheck(Connection, Channel, "OnChannelAdminSelf", {Connection, Sender, Channel, Target})
    End Sub
    Public Sub OnChannelBan(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal Target As String, ByVal MatchedUsers As String())
        EventCheck(Connection, Channel, "OnChannelBan", {Connection, Sender, Channel, Target, MatchedUsers})
    End Sub
    Public Sub OnChannelBanSelf(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal Target As String, ByVal MatchedUsers As String())
        EventCheck(Connection, Channel, "OnChannelBanSelf", {Connection, Sender, Channel, Target, MatchedUsers})
    End Sub
    Public Sub OnChannelTimestamp(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal Timestamp As Date)
        EventCheck(Connection, Channel, "OnChannelTimestamp", {Connection, Channel, Timestamp})
    End Sub
    Public Sub OnChannelCTCP(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal Message As String)
        OnCTCPMessage(Connection, Sender.Nickname, Message)
        EventCheck(Connection, Channel, "OnChannelCTCP", {Connection, Sender, Channel, Message})
    End Sub
    Public Sub OnChannelDeAdmin(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal Target As String)
        EventCheck(Connection, Channel, "OnChannelDeAdmin", {Connection, Sender, Channel, Target})
    End Sub
    Public Sub OnChannelDeAdminSelf(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal Target As String)
        EventCheck(Connection, Channel, "OnChannelDeAdminSelf", {Connection, Sender, Channel, Target})
    End Sub
    Public Sub OnChannelDeHalfOp(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal Target As String)
        EventCheck(Connection, Channel, "OnChannelDeHalfOp", {Connection, Sender, Channel, Target})
    End Sub
    Public Sub OnChannelDeHalfOpSelf(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal Target As String)
        EventCheck(Connection, Channel, "OnChannelDeHalfOpSelf", {Connection, Sender, Channel, Target})
    End Sub
    Public Sub OnChannelDeHalfVoice(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal Target As String)
        EventCheck(Connection, Channel, "OnChannelDeHalfVoice", {Connection, Sender, Channel, Target})
    End Sub
    Public Sub OnChannelDeHalfVoiceSelf(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal Target As String)
        EventCheck(Connection, Channel, "OnChannelDeHalfVoiceSelf", {Connection, Sender, Channel, Target})
    End Sub
    Public Sub OnChannelDeOp(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal Target As String)
        EventCheck(Connection, Channel, "OnChannelDeOp", {Connection, Sender, Channel, Target})
    End Sub
    Public Sub OnChannelDeOpSelf(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal Target As String)
        EventCheck(Connection, Channel, "OnChannelDeOpSelf", {Connection, Sender, Channel, Target})
    End Sub
    Public Sub OnChannelDeOwner(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal Target As String)
        EventCheck(Connection, Channel, "OnChannelDeOwner", {Connection, Sender, Channel, Target})
    End Sub
    Public Sub OnChannelDeOwnerSelf(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal Target As String)
        EventCheck(Connection, Channel, "OnChannelDeOwnerSelf", {Connection, Sender, Channel, Target})
    End Sub
    Public Sub OnChannelDeVoice(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal Target As String)
        EventCheck(Connection, Channel, "OnChannelDeVoice", {Connection, Sender, Channel, Target})
    End Sub
    Public Sub OnChannelDeVoiceSelf(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal Target As String)
        EventCheck(Connection, Channel, "OnChannelDeVoiceSelf", {Connection, Sender, Channel, Target})
    End Sub
    Public Sub OnChannelExempt(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal Target As String, ByVal MatchedUsers As String())
        EventCheck(Connection, Channel, "OnChannelExempt", {Connection, Sender, Channel, Target, MatchedUsers})
    End Sub
    Public Sub OnChannelExemptSelf(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal Target As String, ByVal MatchedUsers As String())
        EventCheck(Connection, Channel, "OnChannelExemptSelf", {Connection, Sender, Channel, Target, MatchedUsers})
    End Sub
    Public Sub OnChannelExit(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal Reason As String)
        If Identifications.ContainsKey(Connection.Address & "/" & Sender.Nickname) Then
            If Identifications(Connection.Address & "/" & Sender.Nickname).Channels.Contains(Channel) Then
                Identifications(Connection.Address & "/" & Sender.Nickname).Channels.Remove(Channel)
                OutputLine("\cGRAY" & Sender.Nickname & " just left " & Channel & ".")
                OutputLine("\cGRAYCommon channels with " & Sender.Nickname & ": " & String.Join(", ", Identifications(Connection.Address & "/" & Sender.Nickname).Channels))
                If Identifications(Connection.Address & "/" & Sender.Nickname).Channels.Count = 0 Then
                    OutputLine("\cGRAYInvalidated " & Sender.Nickname & ".")
                    Identifications.Remove(Connection.Address & "/" & Sender.Nickname)
                End If
            End If
        End If

        EventCheck(Connection, Channel, "OnChannelExit", {Connection, Sender, Channel, Reason})
    End Sub
    Public Sub OnChannelExitSelf(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal Reason As String)
        EventCheck(Connection, Channel, "OnChannelExitSelf", {Connection, Sender, Channel, Reason})
    End Sub
    Public Sub OnChannelHalfOp(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal Target As String)
        EventCheck(Connection, Channel, "OnChannelHalfOp", {Connection, Sender, Channel, Target})
    End Sub
    Public Sub OnChannelHalfOpSelf(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal Target As String)
        EventCheck(Connection, Channel, "OnChannelHalfOpSelf", {Connection, Sender, Channel, Target})
    End Sub
    Public Sub OnChannelHalfVoice(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal Target As String)
        EventCheck(Connection, Channel, "OnChannelHalfVoice", {Connection, Sender, Channel, Target})
    End Sub
    Public Sub OnChannelHalfVoiceSelf(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal Target As String)
        EventCheck(Connection, Channel, "OnChannelHalfVoiceSelf", {Connection, Sender, Channel, Target})
    End Sub
    Public Sub OnChannelInviteExempt(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal Target As String, ByVal MatchedUsers As String())
        EventCheck(Connection, Channel, "OnChannelInviteExempt", {Connection, Sender, Channel, Target, MatchedUsers})
    End Sub
    Public Sub OnChannelInviteExemptSelf(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal Target As String, ByVal MatchedUsers As String())
        EventCheck(Connection, Channel, "OnChannelInviteExemptSelf", {Connection, Sender, Channel, Target, MatchedUsers})
    End Sub
    Public Sub OnChannelJoin(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String)
        If Identifications.ContainsKey(Connection.Address & "/" & Sender.Nickname) Then
            Identifications(Connection.Address & "/" & Sender.Nickname).Channels.Add(Channel)
        End If

        If UserHasPermission(Connection, Channel, Sender, "irc.autohalfvoice." & Connection.Address.Replace("."c, "-"c) & "." & Channel.Replace("."c, "-"c)) Then _
            Connection.Send("MODE " & Channel & " +V " & Sender.Nickname)
        If UserHasPermission(Connection, Channel, Sender, "irc.autovoice." & Connection.Address.Replace("."c, "-"c) & "." & Channel.Replace("."c, "-"c)) Then _
            Connection.Send("MODE " & Channel & " +v " & Sender.Nickname)
        If UserHasPermission(Connection, Channel, Sender, "irc.autohalfop." & Connection.Address.Replace("."c, "-"c) & "." & Channel.Replace("."c, "-"c)) Then _
            Connection.Send("MODE " & Channel & " +h " & Sender.Nickname)
        If UserHasPermission(Connection, Channel, Sender, "irc.autoop." & Connection.Address.Replace("."c, "-"c) & "." & Channel.Replace("."c, "-"c)) Then _
            Connection.Send("MODE " & Channel & " +o " & Sender.Nickname)
        If UserHasPermission(Connection, Channel, Sender, "irc.autoadmin." & Connection.Address.Replace("."c, "-"c) & "." & Channel.Replace("."c, "-"c)) Then _
            Connection.Send("MODE " & Channel & " +ao " & Sender.Nickname & " " & Sender.Nickname)
        If UserHasPermission(Connection, Channel, Sender, "irc.autoquiet." & Connection.Address.Replace("."c, "-"c) & "." & Channel.Replace("."c, "-"c)) Then _
            Connection.Send("MODE " & Channel & " +q " & "*!*" & Sender.UserAndHost)
        If UserHasPermission(Connection, Channel, Sender, "irc.autoban." & Connection.Address.Replace("."c, "-"c) & "." & Channel.Replace("."c, "-"c)) Then
            Connection.Send("MODE " & Channel & " +b " & "*!*" & Sender.UserAndHost)
            Connection.Send("KICK " & Channel & " " & Sender.Nickname & " :You are banned from this channel.")
        End If

        EventCheck(Connection, Channel, "OnChannelJoin", {Connection, Sender, Channel})
    End Sub
    Public Sub OnChannelJoinSelf(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String)
        EventCheck(Connection, Channel, "OnChannelJoinSelf", {Connection, Sender, Channel})
    End Sub
    Public Sub OnChannelJoinDeniedBanned(ByVal Connection As IRCConnection, ByVal Channel As String)
        EventCheck(Connection, Channel, "OnChannelJoinDeniedBanned", {Connection, Channel})
    End Sub
    Public Sub OnChannelJoinDeniedFull(ByVal Connection As IRCConnection, ByVal Channel As String)
        EventCheck(Connection, Channel, "OnChannelJoinDeniedFull", {Connection, Channel})
    End Sub
    Public Sub OnChannelJoinDeniedInvite(ByVal Connection As IRCConnection, ByVal Channel As String)
        EventCheck(Connection, Channel, "OnChannelJoinDeniedInvite", {Connection, Channel})
    End Sub
    Public Sub OnChannelJoinDeniedKey(ByVal Connection As IRCConnection, ByVal Channel As String)
        EventCheck(Connection, Channel, "OnChannelJoinDeniedKey", {Connection, Channel})
    End Sub
    Public Sub OnChannelKick(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal Target As String, ByVal Reason As String)
        EventCheck(Connection, Channel, "OnChannelKick", {Connection, Sender, Channel, Target, Reason})
        OnChannelExit(Connection, New IRCConnection.IRCUser(Target, "*", "*"), Channel, "Kicked by " & Sender.Nickname & ": " & Reason)
    End Sub
    Public Sub OnChannelKickSelf(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal Target As String, ByVal Reason As String)
        EventCheck(Connection, Channel, "OnChannelKickSelf", {Connection, Sender, Channel, Target, Reason})
        OnChannelExitSelf(Connection, New IRCConnection.IRCUser(Target, "*", "*"), Channel, "Kicked by " & Sender.Nickname & ": " & Reason)
    End Sub
    Public Sub OnChannelList(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal Users As Integer, ByVal Topic As String)
        EventCheck(Connection, Channel, "OnChannelList", {Connection, Channel, Users, Topic})
    End Sub
    Public Sub OnChannelMessage(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal Message As String)
        If Waiting.ContainsKey(If(Sender Is Nothing, "", Connection.Address & "/") & Channel & "/" & Sender.Nickname) Then
            Waiting(If(Sender Is Nothing, "", Connection.Address & "/") & Channel & "/" & Sender.Nickname).Response = Message
        End If

        EventCheck(Connection, Channel, "OnChannelMessage", {Connection, Sender, Channel, Message})
        CheckMessage(Connection, Sender, Channel, Message)
    End Sub
    Public Sub OnChannelMessageSendDenied(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal Message As String)
        EventCheck(Connection, Channel, "OnChannelMessageSendDenied", {Connection, Channel, Message})
    End Sub
    Public Sub OnChannelMessageHighlight(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Message As String)
        EventCheck(Connection, Channel, "OnChannelMessageHighlight", {Connection, Sender, Channel, Message})
    End Sub
    Public Sub OnChannelMode(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Direction As Boolean, ByVal Mode As String)
        EventCheck(Connection, Channel, "OnChannelMode", {Connection, Sender, Channel, Direction, Mode})
    End Sub
    Public Sub OnChannelModesGet(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal Modes As String)
        EventCheck(Connection, Channel, "OnChannelModesGet", {Connection, Channel, Modes})
    End Sub
    Public Sub OnChannelNotice(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal Message As String)
        EventCheck(Connection, Channel, "OnChannelNotice", {Connection, Sender, Channel, Message})
    End Sub
    Public Sub OnChannelOp(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal Target As String)
        EventCheck(Connection, Channel, "OnChannelOp", {Connection, Sender, Channel, Target})
    End Sub
    Public Sub OnChannelOpSelf(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal Target As String)
        EventCheck(Connection, Channel, "OnChannelOpSelf", {Connection, Sender, Channel, Target})
    End Sub
    Public Sub OnChannelOwner(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal Target As String)
        EventCheck(Connection, Channel, "OnChannelOwner", {Connection, Sender, Channel, Target})
    End Sub
    Public Sub OnChannelOwnerSelf(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal Target As String)
        EventCheck(Connection, Channel, "OnChannelOwnerSelf", {Connection, Sender, Channel, Target})
    End Sub
    Public Sub OnChannelPart(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal Reason As String)
        EventCheck(Connection, Channel, "OnChannelPart", {Connection, Sender, Channel, Reason})
        OnChannelExit(Connection, Sender, Channel, If(Reason = Nothing, "Left", "Left: " & Reason))
    End Sub
    Public Sub OnChannelPartSelf(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Reason As String)
        EventCheck(Connection, Channel, "OnChannelPartSelf", {Connection, Sender, Channel, Reason})
        OnChannelExitSelf(Connection, Sender, Channel, If(Reason = Nothing, "Left", "Left: " & Reason))
    End Sub
    Public Sub OnChannelQuiet(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal Target As String, ByVal MatchedUsers As String())
        EventCheck(Connection, Channel, "OnChannelQuiet", {Connection, Sender, Channel, Target, MatchedUsers})
    End Sub
    Public Sub OnChannelQuietSelf(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal Target As String, ByVal MatchedUsers As String())
        EventCheck(Connection, Channel, "OnChannelQuietSelf", {Connection, Sender, Channel, Target, MatchedUsers})
    End Sub
    Public Sub OnChannelRemoveExempt(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal Target As String, ByVal MatchedUsers As String())
        EventCheck(Connection, Channel, "OnChannelRemoveExempt", {Connection, Sender, Channel, Target, MatchedUsers})
    End Sub
    Public Sub OnChannelRemoveExemptSelf(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal Target As String, ByVal MatchedUsers As String())
        EventCheck(Connection, Channel, "OnChannelRemoveExemptSelf", {Connection, Sender, Channel, Target, MatchedUsers})
    End Sub
    Public Sub OnChannelRemoveInviteExempt(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal Target As String, ByVal MatchedUsers As String())
        EventCheck(Connection, Channel, "OnChannelRemoveInviteExempt", {Connection, Sender, Channel, Target, MatchedUsers})
    End Sub
    Public Sub OnChannelRemoveInviteExemptSelf(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal Target As String, ByVal MatchedUsers As String())
        EventCheck(Connection, Channel, "OnChannelRemoveInviteExemptSelf", {Connection, Sender, Channel, Target, MatchedUsers})
    End Sub
    Public Sub OnChannelRemoveKey(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String)
        EventCheck(Connection, Channel, "OnChannelRemoveKey", {Connection, Sender, Channel})
    End Sub
    Public Sub OnChannelRemoveLimit(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String)
        EventCheck(Connection, Channel, "OnChannelRemoveLimit", {Connection, Sender, Channel})
    End Sub
    Public Sub OnChannelSetKey(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal Key As String)
        EventCheck(Connection, Channel, "OnChannelSetKey", {Connection, Sender, Channel, Key})
    End Sub
    Public Sub OnChannelSetLimit(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal Limit As Integer)
        EventCheck(Connection, Channel, "OnChannelSetLimit", {Connection, Sender, Channel, Limit})
    End Sub
    Public Sub OnChannelTopic(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal Topic As String)
        EventCheck(Connection, Channel, "OnChannelTopic", {Connection, Channel, Topic})
    End Sub
    Public Sub OnChannelTopicChange(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal NewTopic As String)
        EventCheck(Connection, Channel, "OnChannelTopicChange", {Connection, Sender, Channel, NewTopic})
    End Sub
    Public Sub OnChannelTopicStamp(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal Setter As String, ByVal SetDate As Date)
        EventCheck(Connection, Channel, "OnChannelTopicStamp", {Connection, Channel, Setter, SetDate})
    End Sub
    Public Sub OnChannelUsers(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal Names As String)
        EventCheck(Connection, Channel, "OnChannelUsers", {Connection, Channel, Names})
    End Sub
    Public Sub OnChannelUnBan(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal Target As String, ByVal MatchedUsers As String())
        EventCheck(Connection, Channel, "OnChannelUnBan", {Connection, Sender, Channel, Target, MatchedUsers})
    End Sub
    Public Sub OnChannelUnBanSelf(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal Target As String, ByVal MatchedUsers As String())
        EventCheck(Connection, Channel, "OnChannelUnBanSelf", {Connection, Sender, Channel, Target, MatchedUsers})
    End Sub
    Public Sub OnChannelUnQuiet(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal Target As String, ByVal MatchedUsers As String())
        EventCheck(Connection, Channel, "OnChannelUnQuiet", {Connection, Sender, Channel, Target, MatchedUsers})
    End Sub
    Public Sub OnChannelUnQuietSelf(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal Target As String, ByVal MatchedUsers As String())
        EventCheck(Connection, Channel, "OnChannelUnQuietSelf", {Connection, Sender, Channel, Target, MatchedUsers})
    End Sub
    Public Sub OnChannelVoice(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal Target As String)
        EventCheck(Connection, Channel, "OnChannelVoice", {Connection, Sender, Channel, Target})
    End Sub
    Public Sub OnChannelVoiceSelf(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String, ByVal Target As String)
        EventCheck(Connection, Channel, "OnChannelVoiceSelf", {Connection, Sender, Channel, Target})
    End Sub
    Public Sub OnExemptList(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal BannedUser As String, ByVal BanningUser As String, ByVal Time As Date)
        EventCheck(Connection, Channel, "OnExemptList", {Connection, Channel, BannedUser, BanningUser, Time})
    End Sub
    Public Sub OnExemptListEnd(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal Message As String)
        EventCheck(Connection, "*", "OnExemptListEnd", {Connection, Channel, Message})
    End Sub
    Public Sub OnInvite(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Channel As String)
        EventCheck(Connection, Channel, "OnInvite", {Connection, Sender, Channel})
    End Sub
    Public Sub OnInviteExemptList(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal BannedUser As String, ByVal BanningUser As String, ByVal Time As Date)
        EventCheck(Connection, Channel, "OnInviteExemptList", {Connection, Channel, BannedUser, BanningUser, Time})
    End Sub
    Public Sub OnInviteExemptListEnd(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal Message As String)
        EventCheck(Connection, "*", "OnInviteExemptListEnd", {Connection, Channel, Message})
    End Sub
    Public Sub OnKilled(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Reason As String)
        EventCheck(Connection, "*", "OnKilled", {Connection, Sender, Reason})
    End Sub
    Public Sub OnNames(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal Message As String)
        EventCheck(Connection, Channel, "OnNames", {Connection, Channel, Message})
    End Sub
    Public Sub OnNamesEnd(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal Message As String)
        EventCheck(Connection, Channel, "OnNamesEnd", {Connection, Channel, Message})
    End Sub
    Public Sub OnPrivateMessage(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Message As String)
        NickServCheck(Connection, Sender, Message)

        If Waiting.ContainsKey(If(Sender Is Nothing, "", Connection.Address & "/") & Sender.Nickname & "/" & Sender.Nickname) Then
            Waiting(If(Sender Is Nothing, "", Connection.Address & "/") & Sender.Nickname & "/" & Sender.Nickname).Response = Message
        End If

        EventCheck(Connection, "*", "OnPrivateMessage", {Connection, Sender, Message})
        CheckMessage(Connection, Sender, Sender.Nickname, Message)
    End Sub
    Public Sub OnPrivateAction(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Message As String)
        EventCheck(Connection, "*", "OnPrivateAction", {Connection, Sender, Message})
    End Sub
    Public Sub OnPrivateNotice(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Message As String)
        NickServCheck(Connection, Sender, Message)
        EventCheck(Connection, "*", "OnPrivateNotice", {Connection, Sender, Message})
    End Sub
    Public Sub OnPrivateCTCP(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Message As String)
        OnCTCPMessage(Connection, Sender.Nickname, Message)
        EventCheck(Connection, "*", "OnPrivateCTCP", {Connection, Sender, Message})
    End Sub
    Public Sub OnQuit(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Reason As String)
        If Identifications.ContainsKey(Connection.Address & "/" & Sender.Nickname) Then
            Identifications.Remove(Connection.Address & "/" & Sender.Nickname)
        End If

        For Each Channel In Connection.Channels
            If Channel.Value.Users.ContainsKey(Sender.Nickname) Then _
                OnChannelExit(Connection, Sender, Channel.Key, If(Reason.StartsWith("Quit:"), "Quit: ", "Disconnected: ") & Reason)
        Next
        EventCheck(Connection, "*", "OnQuit", {Connection, Sender, Reason})

        If Sender.Nickname = Connection.Nicknames(0) Then
            ' Regain the primary nickname.
            Connection.Send("NICK " & Connection.Nicknames(0))
        End If
    End Sub
    Public Sub OnQuitSelf(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Reason As String)
        For Each Channel In Connection.Channels
            OnChannelExitSelf(Connection, Sender, Channel.Key, If(Reason.StartsWith("Quit:"), "Quit: ", "Disconnected: ") & Reason)
        Next
        EventCheck(Connection, "*", "OnQuitSelf", {Connection, Sender, Reason})
    End Sub
    Public Sub OnRawLineReceived(ByVal Connection As IRCConnection, ByVal Message As String)
        EventCheck(Connection, "*", "OnRawLineReceived", {Connection, Message})
    End Sub
    Public Sub OnTimeOut(ByVal Connection As IRCConnection)
        OutputLine("\cREDPing timeout at " & Connection.Address & "\r")
        EventCheck(Connection, "*", "OnTimeOut", {Connection})
    End Sub
    Public Sub OnUserModesSet(ByVal Connection As IRCConnection, ByVal Sender As IRCConnection.IRCUser, ByVal Modes As String)
        EventCheck(Connection, "*", "OnUserModesSet", {Connection, Sender, Modes})
    End Sub
    Public Sub OnServerNotice(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Message As String)
        EventCheck(Connection, "*", "OnServerNotice", {Connection, Sender, Message})
    End Sub
    Public Sub OnServerError(ByVal Connection As IRCConnection, ByVal Message As String)
        EventCheck(Connection, "*", "OnServerError", {Connection, Message})
    End Sub
    Public Sub OnServerMessage(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Numeric As String, ByVal Message As String)
        Select Case Numeric
            Case "001"
                Dim Data As NickServData
                If NickServ.ContainsKey(Connection.Address.ToLower) Then
                    Data = NickServ(Connection.Address.ToLower)

                    ' Attempt to identify with NickServ.
                    If Data.AnyNickname Or Data.RegisteredNicknames.Contains(Connection.Nickname) Then _
                        NickServIdentify(Connection, If(Data.RequestMask.Split("!"c)(0) Like "[!A-}]", "NickServ", Data.Hostmask.Split("!"c)(0) & "!*@*"), Data)

                    ' If we're not on the primary nickname, try the GHOST command.
                    If Connection.Nickname <> Connection.Nicknames(0) And Data.UseGhostCommand Then
                        Dim NickServNickname As String = If(Data.Hostmask.Split("!"c)(0) Like "[!A-}]", "NickServ", Data.Hostmask.Split("!"c)(0))
                        Connection.Send(Data.GhostCommand.Replace("$target", NickServNickname).Replace("$nickname", Connection.Nicknames(0)).Replace("$password", Data.Password))
                        Threading.Thread.Sleep(1000)
                        Connection.Send("NICK " & Connection.Nicknames(0))
                    End If
                End If
                'Threading.Thread.Sleep(1500)

                'Try to join the channels.
                If AutoJoinChannels.ContainsKey(Connection.Address.ToLower) Then
                    For Each c In AutoJoinChannels(Connection.Address.ToLower)
                        OutputLine("\cGRAYTrying to join the channel \cWHITE" & c & "\cGRAY on \cWHITE" & Connection.Address & "\r")
                        Connection.Send("JOIN :" & c)
                    Next
                End If
        End Select

        EventCheck(Connection, "*", "OnServerMessage", {Connection, Sender, Numeric, Message})
    End Sub
    Public Sub OnServerMessageUnhandled(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Numeric As String, ByVal Message As String)
        EventCheck(Connection, "*", "OnServerMessageUnhandled", {Connection, Sender, Numeric, Message})
    End Sub
    Public Sub OnWhoList(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal Username As String, ByVal Address As String, ByVal Server As String, ByVal Nickname As String, ByVal Flags As String, ByVal Hops As Integer, ByVal FullName As String)
        EventCheck(Connection, Channel, "OnWhoList", {Connection, Channel, Username, Address, Server, Nickname, Flags, Hops, FullName})
    End Sub

#End Region

End Module

Public Module Globals
    Public Items As New System.Collections.Generic.Dictionary(Of String, Object)

    Public Sub AppendArray(Of T)(ByRef array() As T, ByVal obj As T)
        ReDim Preserve array(array.GetUpperBound(0) + 1)
        array(array.GetUpperBound(0)) = obj
    End Sub
End Module

''' <summary>Represents a user account.</summary>
Public Class Account
    ''' <summary>The hash of the user's password.</summary>
    Public Password As String
    ''' <summary>A list of permissions that the user has.</summary>
    Public Permissions As String()
End Class

''' <summary>Records a user who has identified to an account.</summary>
Public Class Identification
    ''' <summary>The account name that the user is logged in to</summary>
    Public AccountName As String
    ''' <summary>A list of common channels in which this user may be seen.</summary>
    Public Channels As List(Of String)
End Class

''' <summary>Records information about a loaded plugin.</summary>
Public Class PluginData
    ''' <summary>The location of the plugin's DLL file.</summary>
    Public Filename As String
    ''' <summary>The plugin instance</summary>
    Public Obj As Plugin
    ''' <summary>Events that this plugin will receive from other plugins.</summary>
    Public EventsEnabled As List(Of String)
End Class
