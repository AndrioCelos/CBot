' **** CompuChat - IRC Connection Module ****
' by Andrea Giannone (Andrio Celos)
' Monday 20 August 2012
'
' This module contains the IRCConnection and related classes, which manage a connection
' with an IRC server.

Imports System.Net
Imports System.Net.Sockets
Imports System.Net.Security
Imports System.Security.Cryptography.X509Certificates
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Security.Authentication

''' <summary>Manages a session on an IRC network.</summary>
Public Class IRCConnection

    ' Server information
    ''' <summary>The IP address of the server.</summary>
    Public IP As IPAddress
    ''' <summary>The port number to connect on.</summary>
    Public Port As Integer
    ''' <summary>The address to connect to.</summary>
    Public Address As String
    ''' <summary>The given name of the server.</summary>
    Public ServerName As String

    ''' <summary>A list of user modes that the server supports.</summary>
    Public SupportedUserModes As Char()
    ''' <summary>A list of channel modes that the server supports.</summary>
    Public SupportedChannelModes As Char()

#Region "005 reply parameters"
    Private pCaseMapping As String = "rfc1459"
    Private pChanLimit As New Dictionary(Of Char, Integer) From {{"#"c, Integer.MaxValue}}
    Private pChanModes As ChannelModes
    Private pChannelLength As Integer = 200
    Private pChannelTypes As Char() = {"#"c, "&"c}
    Private pSupportsBanExceptions As Boolean = False
    Private pBanExceptionsMode As Char
    Private pSupportsInviteExceptions As Boolean = False
    Private pInviteExceptionsMode As Char
    Private pKickMessageLength As Integer = Integer.MaxValue
    Private pMaxListModeLength As New Dictionary(Of Char, Integer)
    Private pModes As Integer = 3
    Private pNetworkName As String
    Private pNicknameLength As Integer = 9
    Private pPrefix As New Dictionary(Of Char, Char) From {{"o"c, "@"c}, {"v"c, "+"c}}
    Private pStatusMessage As Char() = {}
    Private pMaxTargets As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
    Private pTopicLength As Integer = Integer.MaxValue

    ''' <summary>
    ''' Specifies which method the server uses to resolve equality in case-insensitive string comparisons.
    ''' 
    ''' The CASEMAPPING parameter allows the server to specify which method
    ''' it uses to compare equality of case-insensitive strings. Possible
    ''' values are:
    ''' 
    ''' - "ascii": The ASCII characters 97 to 122 (decimal) are defined as
    ''' the lower-case characters of ASCII 65 to 90 (decimal). No other
    ''' character equivalency is defined.
    ''' - "rfc1459": The ASCII characters 97 to 126 (decimal) are defined as
    ''' the lower-case characters of ASCII 65 to 94 (decimal). No other
    ''' character equivalency is defined.
    ''' - "strict-rfc1459": The ASCII characters 97 to 125 (decimal) are
    ''' defined as the lower-case characters of ASCII 65 to 93 (decimal).
    ''' No other character equivalency is defined.
    ''' </summary>
    Public ReadOnly Property CaseMapping As String
        Get
            Return pCaseMapping
        End Get
    End Property

    ''' <summary>
    ''' The maximum number of channels that a client may join.
    ''' 
    ''' The value is a [comma-separated] series of "pfx:num" pairs, where 'pfx'
    ''' refers to one or more channel prefix characters (as specified in
    ''' CHANTYPES), and 'num' indicates how many of these types of channel
    ''' the client may join in total. If there is no limit to the number of
    ''' certain channel type(s) a client may join, the limit should be
    ''' specified as the empty string, for example "#:".
    ''' </summary>
    Public ReadOnly Property ChanLimit As Dictionary(Of Char, Integer)
        Get
            Return pChanLimit
        End Get
    End Property

    ''' <summary>
    ''' Channel modes which are supported by the server.
    ''' Not listed in this property are modes that are specified in PREFIX.
    ''' </summary>
    Public ReadOnly Property ChanModes As ChannelModes
        Get
            Return pChanModes
        End Get
    End Property

    ''' <summary>
    ''' The maximum length of a channel name that can be created by a client.
    ''' </summary>
    Public ReadOnly Property ChannelLength As Integer
        Get
            Return pChannelLength
        End Get
    End Property

    ''' <summary>
    ''' A list of characters that channel names may start with.
    ''' </summary>
    Public ReadOnly Property ChannelTypes As Char()
        Get
            Return pChannelTypes
        End Get
    End Property

    ''' <summary>
    ''' True if the server supports ban exceptions; False otherwise.
    ''' </summary>
    Public ReadOnly Property SupportsBanExceptions As Boolean
        Get
            Return pSupportsBanExceptions
        End Get
    End Property

    ''' <summary>
    ''' The mode character for channel ban exceptions.
    ''' </summary>
    Public ReadOnly Property BanExceptionsMode As Char
        Get
            Return pBanExceptionsMode
        End Get
    End Property

    ''' <summary>
    ''' True if the server supports invite exceptions; False otherwise.
    ''' </summary>
    Public ReadOnly Property SupportsInviteExceptions As Boolean
        Get
            Return pSupportsInviteExceptions
        End Get
    End Property

    ''' <summary>
    ''' The mode character for channel invite exceptions.
    ''' </summary>
    Public ReadOnly Property InviteExceptionsMode As Char
        Get
            Return pInviteExceptionsMode
        End Get
    End Property

    ''' <summary>
    ''' The maximum length of a KICK message that can be received from a client.
    ''' </summary>
    Public ReadOnly Property KickMessageLength As Integer
        Get
            Return pKickMessageLength
        End Get
    End Property

    ''' <summary>
    ''' The maximum number of entries that can be in a channel mode list.
    ''' </summary>
    Public ReadOnly Property MaxListModeLength As Dictionary(Of Char, Integer)
        Get
            Return pMaxListModeLength
        End Get
    End Property

    ''' <summary>
    ''' The maximum number of channel modes that can be set at once by a client.
    ''' </summary>
    Public ReadOnly Property Modes As Integer
        Get
            Return pModes
        End Get
    End Property

    ''' <summary>The given name of the network.</summary>
    Public ReadOnly Property NetworkName As String
        Get
            Return pNetworkName
        End Get
    End Property

    ''' <summary>The maximum allowed length of a client's nickname.</summary>
    Public ReadOnly Property NicknameLength As Integer
        Get
            Return pNicknameLength
        End Get
    End Property

    ''' <summary>
    ''' A list of status flags that clients can have on channels, along with corresponding channel modes.
    ''' The dictionary keys represent mode characters; the associated value represents the associated prefix.
    ''' 
    ''' The PREFIX parameter specifies a list of channel status flags (the
    ''' "modes" section) that clients may have on channels, followed by a
    ''' mapping to the equivalent channel status flags ("prefixes"), which
    ''' are used in NAMES and WHO replies. There is a one to one mapping
    ''' between each mode and prefix.
    ''' 
    ''' The order of the modes is from that which gives most privileges on
    ''' the channel, to that which gives the least.
    ''' </summary>
    Public ReadOnly Property StatusPrefix As Dictionary(Of Char, Char)
        Get
            Return pPrefix
        End Get
    End Property

    ''' <summary>
    ''' Indicates support by the server for sending a message to only users in a channel with at least a certain privilege level.
    '''
    ''' The server supports a method of sending a NOTICE message to only
    ''' those people on a channel with the specified status. This is done
    ''' via a NOTICE command, with the channel prefixed by the desired status
    ''' flag as the target.
    ''' 
    ''' Example: NOTICE @#channel :Hi there
    ''' 
    ''' The server should deliver the message to all users on the specified
    ''' channel with equal or higher status on the channel as the status flag
    ''' indicates.
    ''' </summary>
    Public ReadOnly Property StatusMessage As Char()
        Get
            Return pStatusMessage
        End Get
    End Property

    ''' <summary>
    ''' The maximum number of targets a command can be targeted at.
    '''
    ''' The TARGMAX parameter specifies the maximum number of targets
    ''' allowable for commands which accept multiple targets. It consists of
    ''' a series of cmd:lim pairs, where each command 'cmd' allows up to
    ''' 'lim' targets (generally either channels or nicks). In the case of
    ''' the KICK command, the limit indicates the maximum number of (user,
    ''' channel) pairs which may be specified in any one KICK command.
    ''' 
    ''' Example: TARGMAX=PRIVMSG:4,NOTICE:3 would allow "PRIVMSG A,B,C,D
    ''' :message" and "NOTICE A,B,C :message", but not "PRIVMSG A,B,C,D,E
    ''' :message" or "NOTICE A,B,C,D :message".
    ''' 
    ''' If no argument is given for a particular command (e.g. "WHOIS:"),
    ''' that command does not have a limit on the number of targets.
    ''' </summary>
    Public ReadOnly Property MaxTargets As Dictionary(Of String, Integer)
        Get
            Return pMaxTargets
        End Get
    End Property

    ''' <summary>
    ''' The maximum length of a channel topic that can be set by a client.
    ''' </summary>
    Public ReadOnly Property TopicLength As Integer
        Get
            Return pTopicLength
        End Get
    End Property
#End Region

    ''' <summary>Whether the local user is marked as away.</summary>
    Public Away As Boolean
    Public AwayReason As String
    Public AwaySince As Date

    Public LastSpoke As Date = Nothing

    ' User information
    ''' <summary>The local user's nickname.</summary>
    Public Nickname As String
    ''' <summary>Nicknames that we will attempt to use.</summary>
    Public Nicknames() As String
    ''' <summary>The local user's identd username.</summary>
    Public Username As String
    ''' <summary>The local user's full name (displayed in WhoIs).</summary>
    Public FullName As String
    ''' <summary>The modes of the local user. Example: "+ix"</summary>
    Public UserModes As String

    ''' <summary>The channels that we are connected to.</summary>
    Public ReadOnly Channels As New ChannelCollection

    '''' <summary>A log of raw IRC messages to and from the server.</summary>
    'Public ReadOnly RawLog As New Queue(Of String)

    ''' <summary>Number of milliseconds to wait after losing connection before reconnecting.</summary>
    Public ReconnectInterval As Integer = 30000

    ''' <summary>Number of times to attempt to reconnect. Specify -1 to attempt to reconnect indefinitely.</summary>
    Public ReconnectMaxAttempts As Integer = 10

    Private ReconnectAttempts As Integer = 0
    Private WithEvents ReconnectTimer As Timers.Timer
    Public IsRegistered As Boolean
    Public IsConnected As Boolean
    Public VoluntarilyQuit As Boolean

    ' Low-level network stuff
    Private mobjClient As TcpClient          ' The TCP client.
    Private pIsUsingSSL As Boolean
    Public AllowInvalidCertificate As Boolean
    Private SSLStream As SslStream
    Private marData(511) As Byte            ' A buffer for received data.
    Private mobjText As New StringBuilder()  ' Builds the buffer into a string.
    Private WithEvents bgwRead As System.ComponentModel.BackgroundWorker

    Private _PingTimeout As Integer
    Private Pinged As Boolean
    Private WithEvents PingTimer As Timers.Timer

    Public Property IsUsingSSL As Boolean
        Get
            Return pIsUsingSSL
        End Get
        Set(ByVal value As Boolean)
            If IsConnected Then Throw New InvalidOperationException("This property cannot be set while the client is connected.")
            pIsUsingSSL = value
        End Set
    End Property

    Public Property PingTimeout As Integer
        Get
            Return _PingTimeout
        End Get
        Set(value As Integer)
            _PingTimeout = value
            If value = 0 Then
                PingTimer.Enabled = False
            Else
                PingTimer.Interval = value * 1000
                If IsConnected Then PingTimer.Enabled = True
            End If
        End Set
    End Property

    Private Sub PingTimeout_Elapsed(sender As Object, e As Timers.ElapsedEventArgs) Handles PingTimer.Elapsed
        SyncLock PingTimer
            If Pinged Then
                ' Ping timeout.
                RaiseEvent TimeOut(Me)
                Send("QUIT :Ping timeout; reconnecting.")
                PingTimer.Stop()
                Disconnect()
            Else
                Send("PING :Keep-alive")
                Pinged = True
            End If
        End SyncLock
    End Sub

    ''' <summary>
    ''' Connects to an IRC server.
    ''' </summary>
    Public Sub Connect()

        If Not System.Net.IPAddress.TryParse(Address, IP) Then
            Try
                RaiseEvent LookingUpHost(Me, Address)
                IP = System.Net.Dns.GetHostEntry(Address).AddressList(0)
            Catch ex As System.Net.Sockets.SocketException
                RaiseEvent LookingUpHostFailed(Me, Address, ex.Message)
                Return
            End Try
        End If

        RaiseEvent Connecting(Me, Address, New IPEndPoint(IP, Port))

        ' Make the actual connection.
        Try
            mobjClient = New TcpClient(IP.ToString, Port)
        Catch ex As Exception
            RaiseEvent ConnectingFailed(Me, ex)
            ReconnectTimer = New Timers.Timer(ReconnectInterval) With {.AutoReset = False, .Enabled = True}
            Return
        End Try

        If IsUsingSSL Then
            ' Set up an SSL stream.
            SSLStream = New SslStream(mobjClient.GetStream, False, AddressOf ValidateServerCertificate, Nothing)

            Try
                SSLStream.AuthenticateAsClient(Address)
            Catch e As AuthenticationException
                'OutputLine("\cREDAuthentication failed: " & e.Message)
                'If e.InnerException IsNot Nothing Then
                '    Console.WriteLine("Inner exception: {0}", e.InnerException.Message)
                'End If
                mobjClient.Close()
                Return
            End Try

        Else
            'mobjClient.GetStream.BeginRead(marData, 0, 1024, AddressOf DoRead, Nothing)
        End If

        marData = New Byte(511) {}

        bgwRead = New System.ComponentModel.BackgroundWorker()
        bgwRead.WorkerReportsProgress = True
        bgwRead.RunWorkerAsync()

        ReconnectAttempts = 0
        LastSpoke = Now
        VoluntarilyQuit = False
        IsConnected = True
        If _PingTimeout <> 0 Then PingTimer.Start()
        Pinged = False
        RaiseEvent Connected(Me)

        Send(String.Format("NICK {0}", Nickname))
        Send(String.Format("USER {0} {2} {3} :{1}", Username, FullName, "0", Address))
    End Sub

    Private Function ValidateServerCertificate(ByVal sender As Object, ByVal certificate As X509Certificate, ByVal chain As X509Chain, ByVal sslPolicyErrors As SslPolicyErrors) As Boolean

        If sslPolicyErrors = sslPolicyErrors.None Then
            Return True
        End If

        Console.WriteLine("Failed to validate the server's certificate: {0}", sslPolicyErrors)

        If AllowInvalidCertificate Then Return True
        ' Do not allow this client to communicate with unauthenticated servers.
        Return False
    End Function

    ''' <summary>
    ''' Immediately closes the connection to an IRC server.
    ''' </summary>
    Public Sub Disconnect()
        If IsUsingSSL Then SSLStream.Close()
        mobjClient.Close()
        PingTimer.Stop()
    End Sub

    Private Sub BeginRead(ByVal sender As Object, ByVal e As System.ComponentModel.DoWorkEventArgs) Handles bgwRead.DoWork
        Do
            Dim intCount As Integer
            Try
                If _PingTimeout <> 0 Then PingTimer.Start()
                If IsUsingSSL Then
                    intCount = SSLStream.Read(marData, 0, 512)
                Else
                    intCount = mobjClient.GetStream.Read(marData, 0, 512)
                End If
            Catch ex As IO.IOException 'When TypeOf ex.InnerException Is SocketException
                RaiseEvent Disconnected(Me, ex.Message)
                e.Result = ex.Message
                PingTimer.Stop()
                Return
            Catch ex As SocketException 'When TypeOf ex.InnerException Is SocketException
                RaiseEvent Disconnected(Me, ex.Message)
                e.Result = ex.Message
                PingTimer.Stop()
                Return
            End Try

            If CType(sender, System.ComponentModel.BackgroundWorker).CancellationPending Then
                e.Result = ""
                e.Cancel = True
                Return
            End If
            'Try
            'intCount = mobjClient.GetStream.EndRead(ar)
            If intCount < 1 Then
                'MarkAsDisconnected()
                RaiseEvent Disconnected(Me, "The server closed the connection.")
                e.Result = "The server closed the connection."
                PingTimer.Stop()
                Return
            End If

            Try
                Dim intIndex As Integer

                For intIndex = 0 To intCount - 1
                    If marData(intIndex) = 10 Or marData(intIndex) = 13 Then
                        'mobjText.Append(vbLf)

                        If mobjText.Length > 0 Then
                            SyncLock PingTimer
                                Pinged = False
                                If _PingTimeout <> 0 Then PingTimer.Stop()
                                Dim params() As Object = {mobjText.ToString}
                                'OutputLine("\cGREEN>>\r " & mobjText.ToString)
                                ReceivedLine(mobjText.ToString)
                                'CType(sender, System.ComponentModel.BackgroundWorker).ReportProgress(-1, mobjText.ToString)
                                'Me.Invoke(New DisplayInvoker(AddressOf Me.MessageReceived), params)
                            End SyncLock
                        End If

                        mobjText = New StringBuilder()
                    Else
                        mobjText.Append(ChrW(marData(intIndex)))
                    End If
                Next

                'Catch e As Exception
                'Throw e
                'MarkAsDisconnected()
                '        End Try
            Catch ex As Exception
                RaiseEvent Exception(Me, ex)
                ' Flush the buffer.
                mobjText = New StringBuilder()
            End Try

        Loop

    End Sub

    Private Sub NamesDirty(ByVal sender As IRCChannel)
        Send("NAMES :" & sender.Name)
    End Sub

    ''' <summary>
    ''' Parses an IRC command line.
    ''' </summary>
    ''' <param name="Data">The command line to process.</param>
    ''' <param name="Prefix">The prefix part of the line.</param>
    ''' <param name="Command">The command part of the line.</param>
    ''' <param name="Parameters">The parameters of the line.</param>
    ''' <param name="Trail">The trailing part of the line.</param>
    ''' <param name="IncludeTrail">If True, the trail will be included as a Parameter.</param>
    ''' <remarks>The three outputs will return Nothing if the line doesn't contain the corresponding parts.</remarks>
    Public Shared Sub ParseIRCLine(ByVal Data As String, ByRef Prefix As String, ByRef Command As String, ByRef Parameters As String(), ByRef Trail As String, Optional ByVal IncludeTrail As Boolean = True)
        Dim p As Integer = 0, ps As Integer = 0
        If Data.Length = 0 Then
            Prefix = Nothing : Command = Nothing : Parameters = Nothing
            Return
        End If
        ' Locate the prefix.
        If Data(0) = ":"c Then
            ' The leading colon indicates the presence of a prefix.
            p = Data.IndexOf(" "c)
            If p < 0 Then
                ' This case probably isn't legal, but anyway...
                Prefix = Data.Substring(1)
                Command = Nothing
                Parameters = Nothing
                Return
            End If
            Prefix = Data.Substring(1, p - 1)
            ps = p + 1
        Else
            Prefix = Nothing
        End If

        Dim tParameters As New List(Of String)
        Do Until ps >= Data.Length
            If Data(ps) = ":"c Then
                ' Parameter starting with : indicates the trail.
                Trail = Data.Substring(ps + 1)
                If IncludeTrail Then tParameters.Add(Trail)
                Exit Do
            Else
                p = Data.IndexOf(" "c, ps)
                If p < 0 Then
                    ' Final argument.
                    tParameters.Add(Data.Substring(ps))
                    Exit Do
                Else
                    Dim tP As String = Data.Substring(ps, p - ps)
                    If tP.Length > 0 Then tParameters.Add(tP)
                    ps = p + 1
                End If
            End If
        Loop

        Command = tParameters(0)
        tParameters.RemoveAt(0)
        Parameters = tParameters.ToArray
    End Sub

    Private Sub ReceivedLine(ByVal Data As String)
        SyncLock Me
            RaiseEvent RawLineReceived(Me, Data)

            Dim Prefix As String, Command As String, Parameters() As String, Trail As String
            ParseIRCLine(Data, Prefix, Command, Parameters, Trail)

            ' Great list of IRC replies: http://www.mirc.net/raws/
            ' Cue humungous Select Case.

            Select Case Command.ToUpper
                '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' 001
                Case "001" ' :Welcome to the [network] IRC network [user]
                    ' Verify the server name from the prefix.
                    ServerName = Prefix
                    If Nickname <> Parameters(0) Then
                        RaiseEvent NicknameChangeSelf(Me, New IRCUser(Nickname, "*", "*"), Parameters(0))
                        Nickname = Parameters(0)
                    End If
                    IsRegistered = True
                    RaiseEvent ServerMessage(Me, Prefix, Command, Parameters(1))
                    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' 005
                Case "005"  ' settings :are supported by this server
                    For i = 1 To UBound(Parameters)
                        Dim Setting = Parameters(i)
                        If i = UBound(Parameters) AndAlso Setting = Trail Then Exit For
                        Dim Key As String, Value As String
                        If Setting.Contains("=") Then
                            Key = Setting.Split({"="c}, 2)(0)
                            Value = Setting.Split({"="c}, 2)(1)
                        Else
                            Key = Setting
                            Value = Nothing
                        End If
                        Try
                            Select Case Key ' Parameter names are case-sensitive.
                                Case "CASEMAPPING"
                                    pCaseMapping = Value
                                Case "CHANLIMIT"
                                    pChanLimit = New Dictionary(Of Char, Integer)
                                    For Each Field In Value.Split(","c)
                                        pChanLimit.Add(Field.Split({":"c}, 2)(0), Field.Split({":"c}, 2)(1))
                                    Next
                                Case "CHANMODES"
                                    pChanModes = New ChannelModes(Value.Split({","c}, 4)(0), Value.Split({","c}, 4)(1), Value.Split({","c}, 4)(2), Value.Split({","c}, 4)(3))
                                Case "CHANNELLEN"
                                    pChannelLength = Value
                                Case "CHANTYPES"
                                    pChannelTypes = Value
                                Case "EXCEPTS"
                                    pSupportsBanExceptions = True
                                    pBanExceptionsMode = If(Value, "e"c)
                                Case "INVEX"
                                    pSupportsInviteExceptions = True
                                    pInviteExceptionsMode = If(Value, "I"c)
                                Case "KICKLEN"
                                    pKickMessageLength = Value
                                Case "MAXLIST"
                                    For Each Field In Value.Split(","c)
                                        For Each Mode In Field.Split({":"c}, 2)(0)
                                            pMaxListModeLength.Add(Mode, Field.Split({":"c}, 2)(1))
                                        Next
                                    Next
                                Case "MODES"
                                    pModes = Value
                                Case "NETWORK"
                                    pNetworkName = Value
                                Case "NICKLEN"
                                    pNicknameLength = Value
                                Case "PREFIX"
                                    pPrefix = New Dictionary(Of Char, Char)
                                    If Value <> Nothing Then
                                        Dim Match = System.Text.RegularExpressions.Regex.Match(Value, "\((?<Modes>[a-zA-Z]*)\)(?<Prefixes>.*)")
                                        For j = 0 To Match.Groups("Modes").Value.Length - 1
                                            pPrefix.Add(Match.Groups("Modes").Value(j), Match.Groups("Prefixes").Value(j))
                                        Next
                                    End If
                                Case "STATUSMSG"
                                    pStatusMessage = Value
                                Case "TARGMAX"
                                    For Each Field In Value.Split(","c)
                                        pMaxTargets.Add(Field.Split({":"c}, 2)(0), If(Field.Split({":"c}, 2)(1) = "", Integer.MaxValue, Field.Split({":"c}, 2)(1)))
                                    Next
                                Case "TOPICLEN"
                                    pTopicLength = Value
                            End Select
                        Catch ex As ArgumentException
                        End Try
                    Next
                    RaiseEvent ServerMessage(Me, Prefix, Command, String.Join(" ", Parameters))
                    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' 221
                Case "221"  ' mode
                    If Parameters(0) = Nickname Then UserModes = Parameters(1)
                    RaiseEvent ServerMessage(Me, Prefix, Command, RemoveColon(Data.Split({" "c}, 3)(2)))
                    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' 301
                Case "301"  ' WhoIs away line
                    RaiseEvent WhoIsAwayLine(Me, Parameters(1), Trail)
                    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' 303
                Case "303"  ' ISON response
                    'TODO: This will be trapped as part of a notify feature.
                    RaiseEvent ServerMessage(Me, Prefix, Command, RemoveColon(Data.Split({" "c}, 3)(2)))
                    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' 305
                Case "305"  ' AWAY cancellation
                    Away = False
                    RaiseEvent AwayCancelled(Me, Trail)
                    RaiseEvent ServerMessage(Me, Prefix, Command, RemoveColon(Data.Split({" "c}, 3)(2)))
                    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' 306
                Case "306"  ' AWAY set
                    Away = True
                    If AwaySince = Nothing Then AwaySince = Now
                    RaiseEvent AwaySet(Me, Trail)
                    RaiseEvent ServerMessage(Me, Prefix, Command, RemoveColon(Data.Split({" "c}, 3)(2)))
                    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' 310
                Case "310"  ' WhoIs helper line
                    RaiseEvent WhoIsHelperLine(Me, Parameters(1), RemoveColon(Data.Split({" "c}, 5)(4)))
                    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' 311
                Case "311"  ' WhoIs name line
                    RaiseEvent WhoIsNameLine(Me, Parameters(1), Parameters(2), Parameters(3), Parameters(5))
                    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' 311
                Case "312"  ' WhoIs server line
                    RaiseEvent WhoIsServerLine(Me, Parameters(1), Parameters(2), Parameters(3))
                    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' 313
                Case "313"  ' WhoIs oper line
                    RaiseEvent WhoIsOperLine(Me, Parameters(1), RemoveColon(Data.Split({" "c}, 5)(4)))
                    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' 314
                Case "314" 'WhoWas list
                    RaiseEvent WhoWasNameLine(Me, Parameters(1), Parameters(2), Parameters(3), Parameters(5))
                    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' 315
                Case "315"  ' End of WHO list
                    'TODO: Respond to 315 similarly to 366.
                    RaiseEvent ServerMessage(Me, Prefix, Command, RemoveColon(Data.Split({" "c}, 3)(2)))
                    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' 317
                Case "317"  ' WhoIs idle line
                    RaiseEvent WhoIsIdleLine(Me, Parameters(1), TimeSpan.FromSeconds(Parameters(2)), New Date(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(Parameters(3)), Trail)
                    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' 318
                Case "318"  ' End of WhoIs list
                    RaiseEvent WhoIsEnd(Me, Parameters(1), RemoveColon(Data.Split({" "c}, 5)(4)))
                    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' 319
                Case "319"  ' WhoIs channels line
                    RaiseEvent WhoIsChannelLine(Me, Parameters(1), Parameters(2))
                    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' 321
                Case "321"  ' LIST header
                    RaiseEvent ServerMessage(Me, Prefix, Command, RemoveColon(Data.Split({" "c}, 3)(2)))
                    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' 322
                Case "322"  ' Channel list
                    RaiseEvent ChannelList(Me, Parameters(1), Parameters(2), RemoveColon(Data.Split({" "c}, 6)(5)))
                    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' 323
                Case "323"  ' End of channel list
                    RaiseEvent ServerMessage(Me, Prefix, Command, RemoveColon(Data.Split({" "c}, 3)(2)))
                    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' 324
                Case "324"  ' Channel modes
                    Dim ChannelName = Parameters(1), Modes = Parameters(2)
                    If Channels.ContainsKey(ChannelName) Then Channels(ChannelName).pModes = Modes
                    RaiseEvent ChannelModesGet(Me, ChannelName, Modes)
                    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' 329
                Case "329"  ' Channel timestamp
                    Dim ChannelName = Parameters(1), UnixTime = Parameters(2)
                    Dim Timestamp = New Date(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(UnixTime)
                    If Channels.ContainsKey(ChannelName) Then Channels(ChannelName).pTimestamp = Timestamp
                    RaiseEvent ChannelTimestamp(Me, ChannelName, Timestamp)
                    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' 332
                Case "332"  ' Channel topic
                    If Channels.ContainsKey(Parameters(1)) Then Channels(Parameters(1)).pTopic = Parameters(2)
                    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' 333
                Case "333"  ' Channel topic stamp
                    Dim Timestamp = New Date(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(Parameters(3))
                    Dim Channel = Parameters(1)

                    If Channels.ContainsKey(Channel) Then
                        Channels(Channel).pTopicSetter = Parameters(2)
                        Channels(Channel).pTopicStamp = Timestamp
                    End If

                    RaiseEvent ChannelTopicStamp(Me, Channel, Parameters(2), Timestamp)
                    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' 341
                Case "341"  ' Invite sent
                    RaiseEvent InviteSent(Me, Parameters(1), Parameters(2))
                    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' 346
                Case "346"  ' Invite list
                    RaiseEvent InviteList(Me, Parameters(1), Parameters(2), Parameters(3), New Date(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(Parameters(4)))
                    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' 347
                Case "347"  ' End of invite list
                    RaiseEvent InviteListEnd(Me, Parameters(1), RemoveColon(Data.Split({" "c}, 5)(4)))
                    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' 348
                Case "348"  ' Invite list
                    RaiseEvent ExemptList(Me, Parameters(1), Parameters(2), Parameters(3), New Date(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(Parameters(4)))
                    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' 349
                Case "349"  ' End of invite list
                    RaiseEvent ExemptListEnd(Me, Parameters(1), RemoveColon(Data.Split({" "c}, 5)(4)))
                    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' 352
                Case "352"  ' WHO list
                    'TODO: Populate the user list.
                    RaiseEvent WhoList(Me, Parameters(1), Parameters(2), Parameters(3), Parameters(4), Parameters(5), Parameters(6), Parameters(7), Parameters(8))
                    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' 353
                Case "353"  ' NAMES list
                    Dim Channel = Parameters(2), Message = Parameters(3)
                    If Channels.ContainsKey(Channel) Then
                        ' We are online in the channel. Mark all remembered users
                        If Channels(Channel).WaitingForNamesList Mod 2 = 0 Then
                            For Each user In Channels(Channel).Users.Values
                                user.ChannelAccess = -1
                            Next
                            'Channels(Channel).Users.Clear()
                            Channels(Channel).WaitingForNamesList += 1
                        End If

                        Dim Names = RemoveColon(Message).Split(" "c)
                        For Each Name In Names
                            Debug.Assert(Not Name.StartsWith(":"))
                            Dim newAccess As ChannelAccessModes = ChannelAccessModes.Normal
                            For i = 0 To Name.Length - 1
                                If StatusPrefix.ContainsKey("q"c) AndAlso Name(i) = StatusPrefix("q"c) Then
                                    newAccess += ChannelAccessModes.Owner
                                ElseIf StatusPrefix.ContainsKey("a"c) AndAlso Name(i) = StatusPrefix("a"c) Then
                                    newAccess += ChannelAccessModes.Admin
                                ElseIf StatusPrefix.ContainsKey("o"c) AndAlso Name(i) = StatusPrefix("o"c) Then
                                    newAccess += ChannelAccessModes.Op
                                ElseIf StatusPrefix.ContainsKey("h"c) AndAlso Name(i) = StatusPrefix("h"c) Then
                                    newAccess += ChannelAccessModes.HalfOp
                                ElseIf StatusPrefix.ContainsKey("v"c) AndAlso Name(i) = StatusPrefix("v"c) Then
                                    newAccess += ChannelAccessModes.Voice
                                ElseIf StatusPrefix.ContainsKey("V"c) AndAlso Name(i) = StatusPrefix("V"c) Then
                                    newAccess += ChannelAccessModes.HalfVoice
                                Else
                                    Dim newNickname = Name.Substring(i)
                                    If Channels(Channel).Users.ContainsKey(newNickname) Then
                                        Channels(Channel).Users(newNickname).ChannelAccess = newAccess
                                    Else
                                        Dim newUser = New IRCUser(Name.Substring(i), "", "") With {.ChannelAccess = newAccess}
                                        Channels(Channel).Users.Add(newUser)
                                    End If
                                    Exit For
                                End If
                            Next
                        Next
                    End If

                    RaiseEvent Names(Me, Channel, Message)
                    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' 366
                Case "366"  ' End of NAMES list
                    Dim Channel = Parameters(1), Message = Data.Split({" "c}, 5)(4)
                    If Channels.ContainsKey(Channel) Then
                        If Channels(Channel).WaitingForNamesList Mod 2 = 1 Then
                            For i = Channels(Channel).Users.Count - 1 To 0 Step -1
                                Dim user = Channels(Channel).Users.Values(i)
                                If user.ChannelAccess < 0 Then Channels(Channel).Users.Remove(user)
                            Next
                            Channels(Channel).WaitingForNamesList -= 3
                        End If
                    End If
                    RaiseEvent NamesEnd(Me, Channel, Message)
                    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' 367
                Case "367"  ' Ban list
                    RaiseEvent BanList(Me, Parameters(1), Parameters(2), Parameters(3), New Date(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(Parameters(4)))
                    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' 368
                Case "368"  ' End of ban list
                    RaiseEvent BanListEnd(Me, Parameters(1), RemoveColon(Data.Split({" "c}, 5)(4)))
                    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' 369
                Case "369"  ' End of WhoWas list
                    RaiseEvent WhoWasEnd(Me, Parameters(1), RemoveColon(Data.Split({" "c}, 5)(4)))
                    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' 404
                Case "404"  ' Cannot send to channel (Any similarity with HTTP 404 is purely coincidential. ^_^)
                    RaiseEvent ChannelMessageSendDenied(Me, Parameters(1), RemoveColon(Data.Split({" "c}, 5)(4)))
                    RaiseEvent ServerMessage(Me, Prefix, Command, RemoveColon(Data.Split({" "c}, 3)(2)))
                    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' 432
                Case "432"  ' Erroneous nickname
                    RaiseEvent NicknameInvalid(Me, Parameters(1), Parameters(2))
                    RaiseEvent ServerMessage(Me, Prefix, Command, RemoveColon(Data.Split({" "c}, 3)(2)))
                    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' 433
                Case "433"  ' Nickname already in use
                    RaiseEvent NicknameTaken(Me, Parameters(1), Parameters(2))
                    If Not IsRegistered Then
                        If Nicknames.Count > 1 Then
                            For i = 0 To Nicknames.Count - 2
                                If Nicknames(i) = Parameters(1) Then
                                    Nickname = Nicknames(i + 1)
                                    Send("NICK " & Nickname)
                                    Exit For
                                End If
                            Next
                        End If
                    End If
                    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' 436
                Case "436"  ' Nickname collision KILL (OMG PANIC! *stab*)
                    RaiseEvent Killed(Me, Prefix, Parameters(2))
                    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' ERROR
                Case "ERROR"
                    RaiseEvent ServerError(Me, Data.Split({" "c}, 2)(1))
                    ''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' INVITE
                Case "INVITE"  ' Channel invite
                    RaiseEvent Invite(Me, New IRCUser(Prefix), Parameters(1))
                    ''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' JOIN
                Case "JOIN"
                    If Prefix.Split("!")(0) = Nickname Then
                        RaiseEvent ChannelJoinSelf(Me, New IRCUser(Prefix), Parameters(0))

                        Dim newChannel As New IRCChannel(Parameters(0), Me)
                        newChannel.pOwnStatus = 0
                        newChannel.pUsers = New UserCollection
                        newChannel.pUsers.Add(New IRCUser(Prefix))
                        Channels.Add(newChannel)
                        AddHandler newChannel.NamesDirty, AddressOf NamesDirty
                    Else
                        OutputLine("\cWHITE" & New IRCUser(Prefix).Nickname & "\r joins \cWHITE" & Parameters(0) & "\r.")
                        RaiseEvent ChannelJoin(Me, New IRCUser(Prefix), Parameters(0))
                        Try
                            Channels.Item(Parameters(0)).Users.Add(New IRCUser(Prefix))
                        Catch ex As Exception
                        End Try
                    End If
                    ''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' KICK
                Case "KICK"  ' Channel kick
                    Dim Target As String = Parameters(1)
                    If Target.ToUpper() = Nickname.ToUpper() Then
                        Channels.Remove(Parameters(0))
                        RaiseEvent ChannelKickSelf(Me, New IRCUser(Prefix), Parameters(0), Target, If(Parameters.ElementAtOrDefault(2), ""))
                    Else
                        If Channels(Parameters(0)).Users.ContainsKey(Target) Then _
                            Channels.Item(Parameters(0)).Users.Remove(Target)
                        RaiseEvent ChannelKick(Me, New IRCUser(Prefix), Parameters(0), Target, If(Parameters.ElementAtOrDefault(2), ""))
                    End If
                    ''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' KILL
                Case "KILL"  ' Kill
                    If Parameters(0) = Nickname Then _
                        RaiseEvent Killed(Me, Prefix, Parameters(1))
                    ''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' MODE
                Case "MODE"  ' Mode/s
                    If Parameters(0).StartsWith("#") Then
                        Dim Modes = Parameters(1)
                        RaiseEvent ChannelMode(Me, Prefix, Parameters(0), Modes.StartsWith("+"), If(Modes.Length >= 2, Modes.Substring(2), ""))

                        Dim ModeChanges As New Dictionary(Of Char, String)
                        Dim Direction As Boolean = True, ParameterIndex As Integer = 2
                        For Each c In Modes
                            If c = "+" Then
                                Direction = True
                            ElseIf c = "-" Then
                                Direction = False
                            ElseIf ChanModes.TypeAModes.Contains(c) Then  ' Type A modes always take a parameter.
                                OnChannelMode(Prefix, Parameters(0), Direction, c, Parameters(ParameterIndex))
                                ParameterIndex += 1
                            ElseIf ChanModes.TypeBModes.Contains(c) Then  ' Type B modes always take a parameter.
                                OnChannelMode(Prefix, Parameters(0), Direction, c, Parameters(ParameterIndex))
                                ParameterIndex += 1
                            ElseIf ChanModes.TypeCModes.Contains(c) Then  ' Type C modes take a parameter when set; not when unset.
                                If Direction Then
                                    OnChannelMode(Prefix, Parameters(0), Direction, c, Parameters(ParameterIndex))
                                    ParameterIndex += 1
                                Else
                                    OnChannelMode(Prefix, Parameters(0), Direction, c, Nothing)
                                End If
                            ElseIf ChanModes.TypeDModes.Contains(c) Then  ' Type D modes never take a parameter.
                                OnChannelMode(Prefix, Parameters(0), Direction, c, Nothing)
                            ElseIf StatusPrefix.ContainsKey(c) Then
                                OnChannelMode(Prefix, Parameters(0), Direction, c, Parameters(ParameterIndex))
                                ParameterIndex += 1
                            End If
                        Next
                    End If
                    ''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' NICK
                Case "NICK"  ' Nickname change
                    Dim User = New IRCUser(Prefix)
                    If User.Nickname = Nickname Then
                        Nickname = Parameters(0)
                        RaiseEvent NicknameChangeSelf(Me, User, Nickname)
                    Else
                        OutputLine("\cWHITE" & New IRCUser(Prefix).Nickname & "\r is now known as \cWHITE" & Parameters(0) & "\r.")
                        RaiseEvent NicknameChange(Me, User, Parameters(0))
                    End If
                    For Each Channel In Channels
                        If Channel.Value.Users.ContainsKey(User.Nickname) Then
                            Channel.Value.Users.Remove(User.Nickname)
                            Channel.Value.Users.Add(New IRCUser(Parameters(0), User.Username, User.Host) With {.ChannelAccess = User.ChannelAccess})
                        End If
                    Next
                    ''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' NOTICE
                Case "NOTICE"  ' Notice
                    Dim Message = RemoveColon(Data.Split({" "c}, 4)(3))
                    If If(ServerName = Nothing, Not Prefix.Contains("@"), Prefix.ToLower = ServerName.ToLower) Then
                        ' It's a server notice.
                        RaiseEvent ServerNotice(Me, Prefix, Message)
                    ElseIf Parameters(0).StartsWith("#") Then
                        ' It's a channel notice.
                        RaiseEvent ChannelNotice(Me, New IRCUser(Prefix), Parameters(0), Message)
                    Else
                        ' It's a user notice.
                        RaiseEvent PrivateNotice(Me, New IRCUser(Prefix), Message)
                    End If
                    ''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' PART
                Case "PART"
                    If Prefix.Split("!")(0) = Nickname Then
                        Channels.Remove(Parameters(0))
                        RaiseEvent ChannelPartSelf(Me, New IRCUser(Prefix), Parameters(0), If(Parameters.ElementAtOrDefault(1), ""))
                    Else
                        If Channels(Parameters(0)).Users.ContainsKey(Prefix.Split("!")(0)) Then _
                            Channels.Item(Parameters(0)).Users.Remove(Prefix.Split("!")(0))
                        OutputLine("\cWHITE" & New IRCUser(Prefix).Nickname & "\r leaves \cWHITE" & Parameters(0) & "\r.")
                        RaiseEvent ChannelPart(Me, New IRCUser(Prefix), Parameters(0), If(Parameters.ElementAtOrDefault(1), ""))
                    End If
                    ''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' PING
                Case "PING"
                    RaiseEvent Ping(Me, ServerName)
                    Send("PONG :" & If(Parameters.ElementAtOrDefault(0), ""))
                    ''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' PONG
                Case "PONG"  ' Ping reply
                    Dim Source As String = If(Parameters.ElementAtOrDefault(0), "")
                    Dim Message As String = If(Parameters.ElementAtOrDefault(1), "")
                    RaiseEvent PingReply(Me, Prefix)
                    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' PRIVMSG
                Case "PRIVMSG"
                    Dim Message = Parameters(1)
                    If Parameters(0).Contains("#") Then
                        ' It's a channel message.
                        If Message.StartsWith(Chr(1)) And Message.EndsWith(Chr(1)) And Message.Length >= 3 Then
                            If Message.Substring(1).Split(" "c)(0).ToUpper = "ACTION" Then
                                RaiseEvent ChannelAction(Me, New IRCUser(Prefix), Parameters(0), If(Message.Split({" "c}, 2).ElementAtOrDefault(1), "").TrimEnd(Chr(1)))
                            Else
                                RaiseEvent ChannelCTCP(Me, New IRCUser(Prefix), Parameters(0), Message.Trim(Chr(1)))
                            End If
                        Else
                            RaiseEvent ChannelMessage(Me, New IRCUser(Prefix), Parameters(0), Message)
                        End If
                    Else
                        ' It's a private message.
                        If Message.StartsWith(Chr(1)) And Message.EndsWith(Chr(1)) And Message.Length >= 3 Then
                            If Message.Substring(1).Split(" "c)(0).ToUpper = "ACTION" Then
                                RaiseEvent PrivateAction(Me, New IRCUser(Prefix), If(Message.Split({" "c}, 2).ElementAtOrDefault(1), "").TrimEnd(Chr(1)))
                            Else
                                RaiseEvent PrivateCTCP(Me, New IRCUser(Prefix), Message.Trim(Chr(1)))
                            End If
                        Else
                            RaiseEvent PrivateMessage(Me, New IRCUser(Prefix), Message)
                        End If
                    End If
                    ''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' QUIT
                Case "QUIT"
                    If Prefix.Split("!")(0) = Nickname Then
                        RaiseEvent QuitSelf(Me, New IRCUser(Prefix), Parameters(0))
                        Channels.Clear()
                    Else
                        OutputLine("\cWHITE" & New IRCUser(Prefix).Nickname & "\r quits.")
                        RaiseEvent Quit(Me, New IRCUser(Prefix), Parameters(0))
                        For Each Channel In Channels.Values
                            If Channel.Users.ContainsKey(Prefix.Split("!")(0)) Then
                                Channel.Users.Remove(Prefix.Split("!")(0))
                            End If
                        Next
                    End If
                    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' TOPIC
                Case "TOPIC"  ' Channel topic change
                    RaiseEvent ChannelTopicChange(Me, New IRCUser(Prefix), Parameters(0), Parameters(1))
                    ''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
                Case Else
                    RaiseEvent ServerMessage(Me, Prefix, Command, RemoveColon(Data.Split({" "c}, 3)(2)))
            End Select
        End SyncLock
    End Sub

    Public Sub Send(ByVal t As String)
        Try
            If Not mobjClient.Connected Then Return
            RaiseEvent RawLineSent(Me, t)
            'OutputLine("\cRED<<\r " & t)

            Dim w As IO.StreamWriter
            If SSLStream IsNot Nothing Then
                w = New IO.StreamWriter(SSLStream)
            Else
                w = New IO.StreamWriter(mobjClient.GetStream)
            End If
            w.Write(t & vbCrLf)
            w.Flush()

            If t.Split(" "c)(0).ToUpper = "QUIT" And t <> "QUIT :Ping timeout; reconnecting." Then VoluntarilyQuit = True
            If t.Split(" "c)(0).ToUpper = "PRIVMSG" Then LastSpoke = Now
        Catch ex As Exception
        End Try
    End Sub

    Public Shared Function RemoveCodes(ByVal Message As String) As String
        Dim regex = New System.Text.RegularExpressions.Regex("\x03(\d{0,2}(,\d{1,2})?)?")
        Message = regex.Replace(Message.Trim, "")
        Message = Message.Replace(Chr(2), "")
        Message = Message.Replace(Chr(15), "")
        Message = Message.Replace(Chr(22), "")
        Message = Message.Replace(Chr(31), "")
        Return Message
    End Function

    Public Shared Function RemoveColon(ByVal Data As String) As String
        If Data.StartsWith(":") Then
            If Data = ":" Then Return "" Else Return Data.Substring(1)
        Else : Return Data
        End If
    End Function

    ''' <summary>
    ''' Represents a nickname, username and host.
    ''' </summary>
    Public Class IRCUser
        Private pNickname As String
        Private pUsername As String
        Private pHost As String

        Public ChannelAccess As ChannelAccessModes

        'Public ReadOnly CommonChannels As New ChannelCollection
        'Public ReadOnly ChannelAccessModes As New Dictionary(Of Channel, ChannelAccessModes)

        ''' <summary>
        ''' Creates a new user specification.
        ''' </summary>
        ''' <param name="Nickname">The user's nickname.</param>
        ''' <param name="Username">The user's identd username.</param>
        ''' <param name="Host">The host from whence the user is connecting.</param>
        Public Sub New(ByVal Nickname As String, ByVal Username As String, ByVal Host As String)
            Me.pNickname = Nickname
            Me.pUsername = Username
            Me.pHost = Host
        End Sub
        ''' <summary>
        ''' Creates a new user specification.
        ''' </summary>
        ''' <param name="Nickname">The user's nickname.</param>
        ''' <param name="UserAndHost">The user's identd username and host, in the format user@host.</param>
        Public Sub New(ByVal Nickname As String, ByVal UserAndHost As String)
            If Not UserAndHost.Contains("@") Then Throw New ArgumentException("UserAndHost must contain a delimiter (@).")
            Me.pNickname = Nickname
            Me.pUsername = UserAndHost.Split({"@"c}, 2)(0)
            Me.pHost = UserAndHost.Split({"@"c}, 2)(1)
        End Sub
        ''' <summary>
        ''' Creates a new user specification.
        ''' </summary>
        ''' <param name="User">The user's details, in the format nick!user@host.</param>
        Public Sub New(ByVal User As String)
            If Not User.Contains("!") Then
                Me.pNickname = User
                Me.pUsername = "*"
                Me.pHost = "*"
            ElseIf Not User.Split({"!"c}, 2)(1).Contains("@") Then
                Me.pNickname = User.Split({"!"c}, 2)(0)
                Me.pUsername = User.Split({"!"c}, 2)(1)
                Me.pHost = "*"
            Else
                Me.pNickname = User.Split({"!"c}, 2)(0)
                Me.pUsername = User.Split({"!"c}, 2)(1).Split({"@"c}, 2)(0)
                Me.pHost = User.Split({"!"c}, 2)(1).Split({"@"c}, 2)(1)
            End If
        End Sub

        ''' <summary>
        ''' Represents this specification in a string, in the format nick!user@host.
        ''' </summary>
        ''' <param name="User">The user specification to convert.</param>
        ''' <returns>A string containing this specification in the format nick!user@host.</returns>
        Shared Widening Operator CType(ByVal User As IRCUser) As String
            Return User.ToString()
        End Operator

        ''' <summary>
        ''' Converts a string to this class, assuming it's in the correct format.
        ''' </summary>
        ''' <param name="User">The string to convert.</param>
        ''' <returns>An IRCUser class.</returns>
        Shared Narrowing Operator CType(ByVal User As String) As IRCUser
            Return New IRCUser(User)
        End Operator

        Public ReadOnly Property Nickname As String
            Get
                Return pNickname
            End Get
        End Property

        Public ReadOnly Property Username As String
            Get
                Return pUsername
            End Get
        End Property

        Public ReadOnly Property Host As String
            Get
                Return pHost
            End Get
        End Property

        Public ReadOnly Property UserAndHost As String
            Get
                Return pUsername & "@" & pHost
            End Get
        End Property

        ''' <summary>\Represents this specification in a string, in the format nick!user@host.\</summary>
        ''' <returns>A string containing this specification in the format nick!user@host.</returns>
        Public Overrides Function ToString() As String
            Return Nickname & "!" & Username & "@" & Host
        End Function

        Shared Operator =(ByVal user1 As IRCUser, ByVal user2 As IRCUser)
            Return (user1.ToString = user2.ToString)
        End Operator
        Shared Operator =(ByVal user1 As String, ByVal user2 As IRCUser)
            Return (user1 = user2.ToString)
        End Operator
        Shared Operator =(ByVal user1 As IRCUser, ByVal user2 As String)
            Return (user1.ToString = user2)
        End Operator

        Shared Operator <>(ByVal user1 As IRCUser, ByVal user2 As IRCUser)
            Return (user1.ToString <> user2.ToString)
        End Operator
        Shared Operator <>(ByVal user1 As String, ByVal user2 As IRCUser)
            Return (user1 <> user2.ToString)
        End Operator
        Shared Operator <>(ByVal user1 As IRCUser, ByVal user2 As String)
            Return (user1.ToString <> user2)
        End Operator
    End Class

    ''' <summary>
    ''' Represents a channel, which can be an IRC channel or something else.
    ''' </summary>
    Public Interface IChannel
        ReadOnly Property SupportsJoin As Boolean
        ReadOnly Property SupportsPart As Boolean

        ReadOnly Property SupportsSay As Boolean
        ReadOnly Property SupportsSayPrivate As Boolean

        ReadOnly Property SupportsUserList As Boolean
        ReadOnly Property SupportsTimestamp As Boolean
        ReadOnly Property SupportsTopic As Boolean
        ReadOnly Property SupportsTopicChange As Boolean
        ReadOnly Property SupportsKey As Boolean
        ReadOnly Property SupportsKeyChange As Boolean
        ReadOnly Property SupportsUserLimit As Boolean
        ReadOnly Property SupportsUserLimitChange As Boolean

        ReadOnly Property SupportsHalfVoice As Boolean
        ReadOnly Property SupportsVoice As Boolean
        ReadOnly Property SupportsHalfOp As Boolean
        ReadOnly Property SupportsOp As Boolean
        ReadOnly Property SupportsAdmin As Boolean

        ReadOnly Property SupportsKick As Boolean  ' Kick online users.
        ReadOnly Property SupportsKickReason As Boolean  ' Kick online users with a reason.
        ReadOnly Property SupportsBan As Boolean  ' Ban online users (hostmasks).
        ReadOnly Property SupportsBanReason As Boolean  ' Ban online users with a reason.
        ReadOnly Property SupportsBanIP As Boolean  ' Ban an IP address.
        ReadOnly Property SupportsBanIPReason As Boolean
        ReadOnly Property SupportsOfflineBan As Boolean  ' Ban offline users.
        ReadOnly Property SupportsBanExceptions As Boolean
        ReadOnly Property SupportsQuiet As Boolean

        ReadOnly Property SupportsBanList As Boolean

        ReadOnly Property SupportsInvite As Boolean
        ReadOnly Property SupportsInviteExceptions As Boolean

        ReadOnly Property IsPrivate As Boolean
        ReadOnly Property IsOnline As Boolean

        ReadOnly Property CanSetTopic As Boolean
        ReadOnly Property CanSetKey As Boolean
        ReadOnly Property CanSetUserLimit As Boolean

        ReadOnly Property CanHalfVoice As Boolean
        ReadOnly Property CanVoice As Boolean
        ReadOnly Property CanHalfOp As Boolean
        ReadOnly Property CanOp As Boolean
        ReadOnly Property CanAdmin As Boolean

        ReadOnly Property CanDeHalfVoice As Boolean
        ReadOnly Property CanDeVoice As Boolean
        ReadOnly Property CanDeHalfOp As Boolean
        ReadOnly Property CanDeOp As Boolean
        ReadOnly Property CanDeAdmin As Boolean

        ReadOnly Property CanKick As Boolean
        ReadOnly Property CanKick(User As String) As Boolean
        ReadOnly Property CanBan As Boolean
        ReadOnly Property CanBan(User As String) As Boolean
        ReadOnly Property CanBanIP As Boolean
        ReadOnly Property CanOfflineBan As Boolean
        ReadOnly Property CanSetBanExceptions As Boolean
        ReadOnly Property CanQuiet As Boolean
        ReadOnly Property CanQuiet(User As String) As Boolean

        ReadOnly Property CanSeeBanList As Boolean
        ReadOnly Property CanSeeBanExceptionList As Boolean
        ReadOnly Property CanSeeInviteExceptionList As Boolean

        ReadOnly Property CanJoin As Boolean

        ReadOnly Property CanSay As Boolean
        ReadOnly Property CanSayPrivate As Boolean

        ReadOnly Property CanInvite As Boolean
        ReadOnly Property CanSetInviteExceptions As Boolean

        ReadOnly Property Name As String
        ReadOnly Property Users As UserCollection
        ReadOnly Property Timestamp As Date
        Property HasKey As Boolean
        Property Key As String
        Property HasLimit As Boolean
        Property UserLimit As Integer
        Property Topic As String
        ReadOnly Property TopicSetter As String
        ReadOnly Property TopicStamp As Date

        Sub Join()
        Sub Join(Key As String)
        Sub Part()
        Sub Part(Message As String)

        Sub Say(Message As String)
        Sub SayPrivate(Target As String, Message As String, Optional Notice As Boolean = True)

        Sub DeHalfVoice(ParamArray Targets() As String)
        Sub DeVoice(ParamArray Targets() As String)
        Sub DeHalfOp(ParamArray Targets() As String)
        Sub DeOp(ParamArray Targets() As String)
        Sub DeAdmin(ParamArray Targets() As String)

        Sub HalfVoice(ParamArray Targets() As String)
        Sub Voice(ParamArray Targets() As String)
        Sub HalfOp(ParamArray Targets() As String)
        Sub Op(ParamArray Targets() As String)
        Sub Admin(ParamArray Targets() As String)

        Sub Kick(Target As String)
        Sub Kick(Target As String, Message As String)
        Sub Kick(Targets() As String)
        Sub Kick(Targets() As String, Message As String)

        Sub Ban(Target As String)
        Sub Ban(Target As String, Message As String)
        Sub Ban(Targets() As String)
        Sub Ban(Targets() As String, Message As String)

        Sub BanIP(Target As String)
        Sub BanIP(Target As String, Message As String)
        Sub BanIP(Targets() As String)
        Sub BanIP(Targets() As String, Message As String)

        Sub Unban(Target As String)
        Sub Unban(Targets() As String)
        Sub UnbanIP(Target As String)
        Sub UnbanIP(Targets() As String)

        Sub BanExcept(Target As String)
        Sub BanExcept(Targets() As String)
        Sub BanUnExcept(Target As String)
        Sub BanUnExcept(Targets() As String)

        Sub InviteExcept(Target As String)
        Sub InviteExcept(Targets() As String)
        Sub InviteUnExcept(Target As String)
        Sub InviteUnExcept(Targets() As String)

        Sub Quiet(Target As String)
        Sub Quiet(Targets() As String)
        Sub UnQuiet(Target As String)
        Sub UnQuiet(Targets() As String)
    End Interface

    Public Enum ChannelAccessModes As Short
        Normal = 0     ' Normal user             Nick
        HalfVoice = 1  ' Half-voice             -Nick
        Voice = 2      ' Voice                  +Nick
        HalfOp = 4     ' Channel Half-operator  %Nick
        Op = 8         ' Channel Operator       @Nick
        Admin = 16     ' Channel administrator  &Nick or !Nick
        Owner = 32     ' Channel owner          ~Nick
        ServerOperator = 64  ' Server operator (mode +Y)
    End Enum

    Public Class ChannelCollection
        Inherits SortedDictionary(Of String, IRCChannel)

        Public Shadows Sub Add(ByVal Channel As IRCChannel)
            If MyBase.ContainsKey(Channel.Name) Then Return
            MyBase.Add(Channel.Name, Channel)
        End Sub

        Public Shadows Sub Remove(ByVal Channel As IRCChannel)
            MyBase.Remove(Channel.Name)
        End Sub
        Public Shadows Sub Remove(ByVal ChannelName As String)
            MyBase.Remove(ChannelName)
        End Sub

        Default Public Shadows ReadOnly Property Item(ByVal ChannelName As String) As IRCChannel
            Get
                Return MyBase.Item(ChannelName)
            End Get
        End Property

        Public Shadows ReadOnly Property Count() As Integer
            Get
                Return MyBase.Count
            End Get
        End Property
    End Class

    Public Class UserCollection
        Inherits SortedDictionary(Of String, IRCUser)

        Public Shadows Sub Add(ByVal User As IRCUser)
            MyBase.Add(User.Nickname, User)
        End Sub

        Public Shadows Sub Remove(ByVal User As IRCUser)
            MyBase.Remove(User.Nickname)
        End Sub
        Public Shadows Sub Remove(ByVal Nickname As String)
            MyBase.Remove(Nickname)
        End Sub

        Default Public Shadows ReadOnly Property Item(ByVal Nickname As String) As IRCUser
            Get
                Return MyBase.Item(Nickname)
            End Get
        End Property

        Public Shadows ReadOnly Property Count() As Integer
            Get
                Return MyBase.Count
            End Get
        End Property

        Public Function AccessCount(Access As ChannelAccessModes) As Integer
            Dim lCount As Integer = 0
            For Each User In Me
                If (User.Value.ChannelAccess And Access) = Access Then lCount += 1
            Next
            Return lCount
        End Function
        Public ReadOnly Property HalfVoiceCount() As Integer
            Get
                Return AccessCount(ChannelAccessModes.HalfVoice)
            End Get
        End Property
        Public ReadOnly Property VoiceCount() As Integer
            Get
                Return AccessCount(ChannelAccessModes.Voice)
            End Get
        End Property
        Public ReadOnly Property HalfOpCount() As Integer
            Get
                Return AccessCount(ChannelAccessModes.HalfOp)
            End Get
        End Property
        Public ReadOnly Property OpCount() As Integer
            Get
                Return AccessCount(ChannelAccessModes.Op)
            End Get
        End Property
        Public ReadOnly Property AdminCount() As Integer
            Get
                Return AccessCount(ChannelAccessModes.Admin)
            End Get
        End Property
        Public ReadOnly Property OwnerCount() As Integer
            Get
                Return AccessCount(ChannelAccessModes.Owner)
            End Get
        End Property

        Private Function UserHasAccess(Nickname As String, Access As ChannelAccessModes) As Boolean
            Dim User As IRCUser
            If TryGetValue(Nickname, User) Then
                Return User.ChannelAccess >= Access
            Else
                Return False
            End If
        End Function
        Public Function IsHalfVoice(Nickname As String) As Boolean
            Return UserHasAccess(Nickname, ChannelAccessModes.HalfVoice)
        End Function
        Public Function IsVoice(Nickname As String) As Boolean
            Return UserHasAccess(Nickname, ChannelAccessModes.Voice)
        End Function
        Public Function IsHalfOp(Nickname As String) As Boolean
            Return UserHasAccess(Nickname, ChannelAccessModes.HalfOp)
        End Function
        Public Function IsOp(Nickname As String) As Boolean
            Return UserHasAccess(Nickname, ChannelAccessModes.Op)
        End Function
        Public Function IsAdmin(Nickname As String) As Boolean
            Return UserHasAccess(Nickname, ChannelAccessModes.Admin)
        End Function
        Public Function IsOwner(Nickname As String) As Boolean
            Return UserHasAccess(Nickname, ChannelAccessModes.Owner)
        End Function
    End Class

    Private Sub bgwRead_RunWorkerCompleted(ByVal sender As Object, ByVal e As System.ComponentModel.RunWorkerCompletedEventArgs) Handles bgwRead.RunWorkerCompleted
        IsRegistered = False
        IsConnected = False
        Channels.Clear()
        mobjClient.Close()
        If Not e.Cancelled And ReconnectAttempts <> ReconnectMaxAttempts And Not VoluntarilyQuit Then
            ' There was a connection failure. Attempt to reconnect.
            ReconnectTimer = New Timers.Timer(ReconnectInterval) With {.AutoReset = False, .Enabled = True}
            RaiseEvent WaitingToReconnect(Me, ReconnectTimer.Interval / 1000, ReconnectAttempts, ReconnectMaxAttempts)
        End If
    End Sub

    Private Sub Reconnect(ByVal sender As Object, ByVal e As Timers.ElapsedEventArgs) Handles ReconnectTimer.Elapsed
        ReconnectAttempts += 1
        Connect()
    End Sub

    Public Structure ChannelModes
        ''' <summary>
        ''' Type A modes supported by the server:
        ''' Modes that add or remove an address to or from a list.
        ''' These modes always take a parameter when sent by the server to a
        ''' client; when sent by a client, they may be specified without a
        ''' parameter, which requests the server to display the current
        ''' contents of the corresponding list on the channel to the client.
        '''  </summary>
        Public TypeAModes As Char()
        ''' <summary>
        ''' Type B modes supported by the server:
        ''' Modes that change a setting on the channel. These modes
        ''' always take a parameter.
        '''  </summary>
        Public TypeBModes As Char()
        ''' <summary>
        ''' Type C modes supported by the server:
        ''' Modes that change a setting on the channel. These modes
        ''' take a parameter only when set; the parameter is absent when the
        ''' mode is removed both in the client's and server's MODE command.
        '''  </summary>
        Public TypeCModes As Char()
        ''' <summary>
        ''' Type D modes supported by the server:
        ''' Modes that change a setting on the channel. These modes
        ''' never take a parameter.
        '''  </summary>
        Public TypeDModes As Char()

        ''' <summary>
        ''' Initialises a new instance of the ChannelModes structure.
        ''' </summary>
        ''' <param name="A">A list of Type A modes that the server supports.</param>
        ''' <param name="B">A list of Type B modes that the server supports.</param>
        ''' <param name="C">A list of Type C modes that the server supports.</param>
        ''' <param name="D">A list of Type D modes that the server supports.</param>
        Public Sub New(ByVal A As Char(), ByVal B As Char(), ByVal C As Char(), ByVal D As Char())
            TypeAModes = A
            TypeBModes = B
            TypeCModes = C
            TypeDModes = D
        End Sub
    End Structure

    Private Sub OnChannelMode(ByVal Sender As String, ByVal Target As String, ByVal Direction As Boolean, ByVal Mode As Char, ByVal Parameter As String)
        Select Case Mode
            Case "a"c
                If Not StatusPrefix.ContainsKey(Mode) Then Return
                If Direction Then
                    Channels(Target).Users(Parameter).ChannelAccess =
                        Channels(Target).Users(Parameter).ChannelAccess Or ChannelAccessModes.Admin
                    If Parameter = Nickname Then : RaiseEvent ChannelAdminSelf(Me, Sender, Target, Parameter) _
                        : Else : RaiseEvent ChannelAdmin(Me, Sender, Target, Parameter) : End If
                Else
                    Channels(Target).Users(Parameter).ChannelAccess =
                        Channels(Target).Users(Parameter).ChannelAccess And Not ChannelAccessModes.Admin
                    If Parameter = Nickname Then : RaiseEvent ChannelDeAdminSelf(Me, Sender, Target, Parameter) _
                        : Else : RaiseEvent ChannelDeAdmin(Me, Sender, Target, Parameter) : End If
                End If
            Case "b"c
                If Not ChanModes.TypeAModes.Contains(Mode) Then Return
                Dim MatchedUsers As String() = FindMatchingUsers(Target, Parameter)
                If Direction Then
                    If MatchedUsers.Contains(Nickname) Then : RaiseEvent ChannelBanSelf(Me, Sender, Target, Parameter, MatchedUsers) _
                            : Else : RaiseEvent ChannelBan(Me, Sender, Target, Parameter, MatchedUsers) : End If
                Else
                    If MatchedUsers.Contains(Nickname) Then : RaiseEvent ChannelUnBanSelf(Me, Sender, Target, Parameter, MatchedUsers) _
                            : Else : RaiseEvent ChannelUnBan(Me, Sender, Target, Parameter, MatchedUsers) : End If
                End If
            Case "e"c
                If Not ChanModes.TypeAModes.Contains(Mode) Then Return
                Dim MatchedUsers As String() = FindMatchingUsers(Target, Parameter)
                If Direction Then
                    If MatchedUsers.Contains(Nickname) Then : RaiseEvent ChannelExemptSelf(Me, Sender, Target, Parameter, MatchedUsers) _
                              : Else : RaiseEvent ChannelExempt(Me, Sender, Target, Parameter, MatchedUsers) : End If
                Else
                    If MatchedUsers.Contains(Nickname) Then : RaiseEvent ChannelRemoveExemptSelf(Me, Sender, Target, Parameter, MatchedUsers) _
                              : Else : RaiseEvent ChannelRemoveExempt(Me, Sender, Target, Parameter, MatchedUsers) : End If
                End If
            Case "h"c
                If Not StatusPrefix.ContainsKey(Mode) Then Return
                If Direction Then
                    Channels(Target).Users(Parameter).ChannelAccess =
                        Channels(Target).Users(Parameter).ChannelAccess Or ChannelAccessModes.HalfOp
                    If Parameter = Nickname Then : RaiseEvent ChannelHalfOpSelf(Me, Sender, Target, Parameter) _
                        : Else : RaiseEvent ChannelHalfOp(Me, Sender, Target, Parameter) : End If
                Else
                    Channels(Target).Users(Parameter).ChannelAccess =
                        Channels(Target).Users(Parameter).ChannelAccess And Not ChannelAccessModes.HalfOp
                    If Parameter = Nickname Then : RaiseEvent ChannelDeHalfOpSelf(Me, Sender, Target, Parameter) _
                        : Else : RaiseEvent ChannelDeHalfOp(Me, Sender, Target, Parameter) : End If
                End If
            Case "I"c
                If Not ChanModes.TypeAModes.Contains(Mode) Then Return
                Dim MatchedUsers As String() = FindMatchingUsers(Target, Parameter)
                If Direction Then
                    If MatchedUsers.Contains(Nickname) Then : RaiseEvent ChannelInviteExemptSelf(Me, Sender, Target, Parameter, MatchedUsers) _
                                : Else : RaiseEvent ChannelInviteExempt(Me, Sender, Target, Parameter, MatchedUsers) : End If
                Else
                    If MatchedUsers.Contains(Nickname) Then : RaiseEvent ChannelRemoveInviteExemptSelf(Me, Sender, Target, Parameter, MatchedUsers) _
                                : Else : RaiseEvent ChannelRemoveInviteExempt(Me, Sender, Target, Parameter, MatchedUsers) : End If
                End If
            Case "o"c
                If Not StatusPrefix.ContainsKey(Mode) Then Return
                If Direction Then
                    Channels(Target).Users(Parameter).ChannelAccess =
                        Channels(Target).Users(Parameter).ChannelAccess Or ChannelAccessModes.Op
                    If Parameter = Nickname Then : RaiseEvent ChannelOpSelf(Me, Sender, Target, Parameter) _
                        : Else : RaiseEvent ChannelOp(Me, Sender, Target, Parameter) : End If
                Else
                    Channels(Target).Users(Parameter).ChannelAccess =
                        Channels(Target).Users(Parameter).ChannelAccess And Not ChannelAccessModes.Op
                    If Parameter = Nickname Then : RaiseEvent ChannelDeOpSelf(Me, Sender, Target, Parameter) _
                        : Else : RaiseEvent ChannelDeOp(Me, Sender, Target, Parameter) : End If
                End If
            Case "q"c
                If ChanModes.TypeAModes.Contains(Mode) Then
                    Dim MatchedUsers As String() = FindMatchingUsers(Target, Parameter)
                    If Direction Then
                        If MatchedUsers.Contains(Nickname) Then : RaiseEvent ChannelQuietSelf(Me, Sender, Target, Parameter, MatchedUsers) _
                                      : Else : RaiseEvent ChannelQuiet(Me, Sender, Target, Parameter, MatchedUsers) : End If
                    Else
                        If MatchedUsers.Contains(Nickname) Then : RaiseEvent ChannelUnQuietSelf(Me, Sender, Target, Parameter, MatchedUsers) _
                                      : Else : RaiseEvent ChannelUnQuiet(Me, Sender, Target, Parameter, MatchedUsers) : End If
                    End If
                End If
                If Not StatusPrefix.ContainsKey(Mode) Then Return
                If Direction Then
                    Channels(Target).Users(Parameter).ChannelAccess =
                        Channels(Target).Users(Parameter).ChannelAccess Or ChannelAccessModes.Owner
                    If Parameter = Nickname Then : RaiseEvent ChannelOwnerSelf(Me, Sender, Target, Parameter) _
                        : Else : RaiseEvent ChannelOwner(Me, Sender, Target, Parameter) : End If
                Else
                    Channels(Target).Users(Parameter).ChannelAccess =
                        Channels(Target).Users(Parameter).ChannelAccess And Not ChannelAccessModes.Owner
                    If Parameter = Nickname Then : RaiseEvent ChannelDeOwnerSelf(Me, Sender, Target, Parameter) _
                        : Else : RaiseEvent ChannelDeOwner(Me, Sender, Target, Parameter) : End If
                End If
            Case "v"c
                If Not StatusPrefix.ContainsKey(Mode) Then Return
                If Direction Then
                    Channels(Target).Users(Parameter).ChannelAccess =
                        Channels(Target).Users(Parameter).ChannelAccess Or ChannelAccessModes.Voice
                    If Parameter = Nickname Then : RaiseEvent ChannelVoiceSelf(Me, Sender, Target, Parameter) _
                        : Else : RaiseEvent ChannelVoice(Me, Sender, Target, Parameter) : End If
                Else
                    Channels(Target).Users(Parameter).ChannelAccess =
                        Channels(Target).Users(Parameter).ChannelAccess And Not ChannelAccessModes.Voice
                    If Parameter = Nickname Then : RaiseEvent ChannelDeVoiceSelf(Me, Sender, Target, Parameter) _
                        : Else : RaiseEvent ChannelDeVoice(Me, Sender, Target, Parameter) : End If
                End If
            Case "V"c
                If Not StatusPrefix.ContainsKey(Mode) Then Return
                If Direction Then
                    Channels(Target).Users(Parameter).ChannelAccess =
                        Channels(Target).Users(Parameter).ChannelAccess Or ChannelAccessModes.HalfVoice
                    If Parameter = Nickname Then : RaiseEvent ChannelHalfVoiceSelf(Me, Sender, Target, Parameter) _
                          : Else : RaiseEvent ChannelHalfVoice(Me, Sender, Target, Parameter) : End If
                Else
                    Channels(Target).Users(Parameter).ChannelAccess =
                        Channels(Target).Users(Parameter).ChannelAccess And Not ChannelAccessModes.HalfVoice
                    If Parameter = Nickname Then : RaiseEvent ChannelDeHalfVoiceSelf(Me, Sender, Target, Parameter) _
                        : Else : RaiseEvent ChannelDeHalfVoice(Me, Sender, Target, Parameter) : End If
                End If
        End Select
    End Sub

    Public Function FindMatchingUsers(ByVal Channel As String, ByVal Mask As String) As String()
        Dim MatchedUsers As New List(Of String)
        For Each User In Channels(Channel).Users
            Dim ex As String = ""
            For Each s In Mask.Split("*"c)
                ex &= ".*" & Regex.Escape(s)
            Next
            If Regex.IsMatch(User.Value, ex.Substring(2)) Then MatchedUsers.Add(User.Key)
        Next
        Return MatchedUsers.ToArray
    End Function

    Public Sub New()
        MyClass.New(60)
    End Sub

    Public Sub New(PingTimeout As Integer)
        _PingTimeout = PingTimeout
        If PingTimeout <= 0 Then
            PingTimer = New Timers.Timer
        Else
            PingTimer = New Timers.Timer(PingTimeout * 1000)
        End If
    End Sub
End Class

