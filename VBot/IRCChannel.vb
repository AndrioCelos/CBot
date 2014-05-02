Imports System.Text

Partial Public Class IRCConnection

    ''' <summary>
    ''' Represents an IRC channel.
    ''' </summary>
    Public Class IRCChannel
        Implements IChannel

        Private pName As String
        Friend pOwnStatus As ChannelAccessModes
        Friend pUsers As New UserCollection
        Friend pModes As String
        Friend pTimestamp As Date
        Friend pTopic As String
        Friend pTopicSetter As String
        Friend pTopicStamp As Date

        Friend pKey As Integer
        Friend pUserLimit As String

        Private lConnection As IRCConnection

        Public WithEvents NamesTimer As New Timers.Timer(600000) With {.Enabled = False, .AutoReset = True}

        Public WaitingForNamesList As Short  ' 0 = not waiting  1 = waiting for user command  3 = waiting for other
        Public WaitingForWhoList As Short    ' 0 = not waiting  1 = waiting for user command  3 = waiting for other

        Public Event NamesDirty(ByVal sender As IRCChannel)

        Public Sub New(ByVal Name As String, ByVal Connection As IRCConnection)
            pName = Name
            lConnection = Connection
        End Sub

        Public Overridable ReadOnly Property Modes As String
            Get
                Return pModes
            End Get
        End Property

        Public Overridable ReadOnly Property OwnStatus As ChannelAccessModes
            Get
                Return pOwnStatus
            End Get
        End Property

#Region "Nickname modes"
        Public Sub NicknameMode(ByVal Direction As Char, ByVal ModeCharacter As Char, ByVal ParamArray Members() As String)
            Dim i As Integer, c As StringBuilder = Nothing, p As StringBuilder = Nothing
            Do Until i > UBound(Members)
                Do
                    If c Is Nothing Then
                        c = New StringBuilder(Direction)
                        c.Append(ModeCharacter)
                        p = New StringBuilder(Members(i))
                    Else
                        c.Append(ModeCharacter)
                        p.Append(" ")
                        p.Append(Members(i))
                    End If
                    i += 1
                Loop Until c.Length >= lConnection.Modes + 1 Or i > UBound(Members)
                lConnection.Send("MODE " & Name & " " & c.ToString & " " & p.ToString)
                c = Nothing
            Loop
        End Sub

        Public Sub DeHalfVoice(ByVal ParamArray Members() As String) Implements IChannel.DeHalfVoice
            NicknameMode("-"c, "V"c, Members)
        End Sub
        Public Sub DeVoice(ByVal ParamArray Members() As String) Implements IChannel.DeVoice
            NicknameMode("-"c, "v"c, Members)
        End Sub
        Public Sub DeHalfOp(ByVal ParamArray Members() As String) Implements IChannel.DeHalfOp
            NicknameMode("-"c, "h"c, Members)
        End Sub
        Public Sub DeOp(ByVal ParamArray Members() As String) Implements IChannel.DeOp
            NicknameMode("-"c, "o"c, Members)
        End Sub
        Public Sub DeAdmin(ByVal ParamArray Members() As String) Implements IChannel.DeAdmin
            NicknameMode("-"c, "a"c, Members)
        End Sub

        Public Sub HalfVoice(ByVal ParamArray Members() As String) Implements IChannel.HalfVoice
            NicknameMode("+"c, "V"c, Members)
        End Sub
        Public Sub Voice(ByVal ParamArray Members() As String) Implements IChannel.Voice
            NicknameMode("+"c, "v"c, Members)
        End Sub
        Public Sub HalfOp(ByVal ParamArray Members() As String) Implements IChannel.HalfOp
            NicknameMode("+"c, "h"c, Members)
        End Sub
        Public Sub Op(ByVal ParamArray Members() As String) Implements IChannel.Op
            NicknameMode("+"c, "o"c, Members)
        End Sub
        Public Sub Admin(ByVal ParamArray Members() As String) Implements IChannel.Admin
            NicknameMode("+"c, "a"c, Members)
        End Sub
#End Region

        Public Sub Ban(Target As String) Implements IChannel.Ban
            NicknameMode("+"c, "b"c, {Target})
        End Sub
        Public Sub Ban(Target As String, Message As String) Implements IChannel.Ban
            NicknameMode("+"c, "b"c, {Target})
        End Sub
        Public Sub Ban(Targets() As String) Implements IChannel.Ban
            NicknameMode("+"c, "b"c, Targets)
        End Sub
        Public Sub Ban(Targets() As String, Message As String) Implements IChannel.Ban
            NicknameMode("+"c, "b"c, Targets)
        End Sub
        Public Sub BanIP(Target As String) Implements IChannel.BanIP
            BanIP({Target})
        End Sub
        Public Sub BanIP(Target As String, Message As String) Implements IChannel.BanIP
            BanIP({Target})
        End Sub
        Public Sub BanIP(Targets() As String) Implements IChannel.BanIP
            Dim IPs As New List(Of String)
            For Each Target In Targets
                If pUsers.ContainsKey(Target) Then
                    IPs.Add("*!*@" & pUsers(Target).Host)
                End If
            Next
            NicknameMode("+"c, "b"c, IPs.ToArray)
        End Sub
        Public Sub BanIP(Targets() As String, Message As String) Implements IChannel.BanIP
            BanIP(Targets)
        End Sub

        Public ReadOnly Property CanAdmin As Boolean Implements IChannel.CanAdmin
            Get
                Return pOwnStatus >= ChannelAccessModes.Owner
            End Get
        End Property
        Public ReadOnly Property CanBan As Boolean Implements IChannel.CanBan
            Get
                Return pOwnStatus >= ChannelAccessModes.HalfOp
            End Get
        End Property
        Public ReadOnly Property CanBan(User As String) As Boolean Implements IChannel.CanBan
            Get
                Return pOwnStatus >= ChannelAccessModes.HalfOp
            End Get
        End Property
        Public ReadOnly Property CanBanIP As Boolean Implements IChannel.CanBanIP
            Get
                Return pOwnStatus >= ChannelAccessModes.HalfOp
            End Get
        End Property
        Public ReadOnly Property CanDeAdmin As Boolean Implements IChannel.CanDeAdmin
            Get
                Return pOwnStatus >= ChannelAccessModes.Owner
            End Get
        End Property
        Public ReadOnly Property CanDeHalfOp As Boolean Implements IChannel.CanDeHalfOp
            Get
                Return pOwnStatus >= ChannelAccessModes.Op
            End Get
        End Property
        Public ReadOnly Property CanDeHalfVoice As Boolean Implements IChannel.CanDeHalfVoice
            Get
                Return pOwnStatus >= ChannelAccessModes.HalfOp
            End Get
        End Property
        Public ReadOnly Property CanDeOp As Boolean Implements IChannel.CanDeOp
            Get
                Return pOwnStatus >= ChannelAccessModes.Op
            End Get
        End Property
        Public ReadOnly Property CanDeVoice As Boolean Implements IChannel.CanDeVoice
            Get
                Return pOwnStatus >= ChannelAccessModes.HalfOp
            End Get
        End Property
        Public ReadOnly Property CanHalfOp As Boolean Implements IChannel.CanHalfOp
            Get
                Return pOwnStatus >= ChannelAccessModes.Op
            End Get
        End Property
        Public ReadOnly Property CanHalfVoice As Boolean Implements IChannel.CanHalfVoice
            Get
                Return pOwnStatus >= ChannelAccessModes.HalfOp
            End Get
        End Property
        Public ReadOnly Property CanInvite As Boolean Implements IChannel.CanInvite
            Get
                Return pOwnStatus >= ChannelAccessModes.Op
            End Get
        End Property
        Public ReadOnly Property CanKick As Boolean Implements IChannel.CanKick
            Get
                Return pOwnStatus >= ChannelAccessModes.HalfOp
            End Get
        End Property
        Public ReadOnly Property CanKick(User As String) As Boolean Implements IChannel.CanKick
            Get
                Return pOwnStatus >= ChannelAccessModes.HalfOp
            End Get
        End Property
        Public ReadOnly Property CanOfflineBan As Boolean Implements IChannel.CanOfflineBan
            Get
                Return pOwnStatus >= ChannelAccessModes.HalfOp
            End Get
        End Property
        Public ReadOnly Property CanOp As Boolean Implements IChannel.CanOp
            Get
                Return pOwnStatus >= ChannelAccessModes.Op
            End Get
        End Property
        Public ReadOnly Property CanSetBanExceptions As Boolean Implements IChannel.CanSetBanExceptions
            Get
                Return pOwnStatus >= ChannelAccessModes.HalfOp
            End Get
        End Property
        Public ReadOnly Property CanSetInviteExceptions As Boolean Implements IChannel.CanSetInviteExceptions
            Get
                Return pOwnStatus >= ChannelAccessModes.HalfOp
            End Get
        End Property
        Public ReadOnly Property CanSetTopic As Boolean Implements IChannel.CanSetTopic
            Get
                Return pOwnStatus >= ChannelAccessModes.Op Or Not Me.pModes.Contains("t")
            End Get
        End Property
        Public ReadOnly Property CanVoice As Boolean Implements IChannel.CanVoice
            Get
                Return pOwnStatus >= ChannelAccessModes.HalfOp
            End Get
        End Property

        Public ReadOnly Property IsPrivate As Boolean Implements IChannel.IsPrivate
            Get
                Return False
            End Get
        End Property

        Public Sub Join() Implements IChannel.Join
            lConnection.Send("JOIN " & Name)
        End Sub
        Public Sub Join(Key As String) Implements IChannel.Join
            lConnection.Send("JOIN " & Name & " " & Key)
        End Sub

        Public Sub Kick(Target As String) Implements IChannel.Kick
            lConnection.Send("KICK " & Name & " " & Target)
        End Sub
        Public Sub Kick(Target As String, Message As String) Implements IChannel.Kick
            lConnection.Send("KICK " & Name & " " & Target & " :" & Message)
        End Sub
        Public Sub Kick(Targets() As String) Implements IChannel.Kick
            lConnection.Send("KICK " & Name & " " & String.Join(",", Targets))
        End Sub
        Public Sub Kick(Targets() As String, Message As String) Implements IChannel.Kick
            lConnection.Send("KICK " & Name & " " & String.Join(",", Targets) & " :" & Message)
        End Sub

        Public ReadOnly Property Name As String Implements IChannel.Name
            Get
                Return pName
            End Get
        End Property

        Public Sub Part() Implements IChannel.Part
            lConnection.Send("PART " & Name)
        End Sub
        Public Sub Part(Message As String) Implements IChannel.Part
            lConnection.Send("PART " & Name & " :" & Message)
        End Sub

        Public Sub Say(Message As String) Implements IChannel.Say
            lConnection.Send("PRIVMSG " & Name & " :" & Message)
        End Sub
        Public Sub SayPrivate(Target As String, Message As String, Optional Notice As Boolean = True) Implements IChannel.SayPrivate
            lConnection.Send(If(Notice, "NOTICE", "PRIVMSG") & " " & Target & " :" & Message)
        End Sub

        Public ReadOnly Property SupportsAdmin As Boolean Implements IChannel.SupportsAdmin
            Get
                Return lConnection.pPrefix.ContainsKey("a"c)
            End Get
        End Property
        Public ReadOnly Property SupportsBan As Boolean Implements IChannel.SupportsBan
            Get
                Return True
            End Get
        End Property
        Public ReadOnly Property SupportsBanExceptions As Boolean Implements IChannel.SupportsBanExceptions
            Get
                Return lConnection.SupportsBanExceptions
            End Get
        End Property
        Public ReadOnly Property SupportsBanIP As Boolean Implements IChannel.SupportsBanIP
            Get
                Return True
            End Get
        End Property
        Public ReadOnly Property SupportsBanIPReason As Boolean Implements IChannel.SupportsBanIPReason
            Get
                Return True
            End Get
        End Property
        Public ReadOnly Property SupportsBanReason As Boolean Implements IChannel.SupportsBanReason
            Get
                Return True
            End Get
        End Property
        Public ReadOnly Property SupportsHalfOp As Boolean Implements IChannel.SupportsHalfOp
            Get
                Return lConnection.pPrefix.ContainsKey("h"c)
            End Get
        End Property
        Public ReadOnly Property SupportsHalfVoice As Boolean Implements IChannel.SupportsHalfVoice
            Get
                Return lConnection.pPrefix.ContainsKey("V"c)
            End Get
        End Property
        Public ReadOnly Property SupportsInvite As Boolean Implements IChannel.SupportsInvite
            Get
                Return True
            End Get
        End Property
        Public ReadOnly Property SupportsInviteExceptions As Boolean Implements IChannel.SupportsInviteExceptions
            Get
                Return lConnection.SupportsInviteExceptions
            End Get
        End Property
        Public ReadOnly Property SupportsJoin As Boolean Implements IChannel.SupportsJoin
            Get
                Return True
            End Get
        End Property
        Public ReadOnly Property SupportsKick As Boolean Implements IChannel.SupportsKick
            Get
                Return True
            End Get
        End Property
        Public ReadOnly Property SupportsKickReason As Boolean Implements IChannel.SupportsKickReason
            Get
                Return True
            End Get
        End Property
        Public ReadOnly Property SupportsOfflineBan As Boolean Implements IChannel.SupportsOfflineBan
            Get
                Return True
            End Get
        End Property
        Public ReadOnly Property SupportsOp As Boolean Implements IChannel.SupportsOp
            Get
                Return True
            End Get
        End Property
        Public ReadOnly Property SupportsPart As Boolean Implements IChannel.SupportsPart
            Get
                Return True
            End Get
        End Property
        Public ReadOnly Property SupportsSay As Boolean Implements IChannel.SupportsSay
            Get
                Return True
            End Get
        End Property
        Public ReadOnly Property SupportsSayPrivate As Boolean Implements IChannel.SupportsSayPrivate
            Get
                Return True
            End Get
        End Property
        Public ReadOnly Property SupportsTimestamp As Boolean Implements IChannel.SupportsTimestamp
            Get
                Return True
            End Get
        End Property
        Public ReadOnly Property SupportsTopic As Boolean Implements IChannel.SupportsTopic
            Get
                Return True
            End Get
        End Property
        Public ReadOnly Property SupportsTopicChange As Boolean Implements IChannel.SupportsTopicChange
            Get
                Return True
            End Get
        End Property
        Public ReadOnly Property SupportsUserList As Boolean Implements IChannel.SupportsUserList
            Get
                Return True
            End Get
        End Property
        Public ReadOnly Property SupportsVoice As Boolean Implements IChannel.SupportsVoice
            Get
                Return True
            End Get
        End Property

        Public ReadOnly Property Timestamp As Date Implements IChannel.Timestamp
            Get
                Return pTimestamp
            End Get
        End Property

        Public Property Topic As String Implements IChannel.Topic
            Get
                Return pTopic
            End Get
            Set(value As String)
                lConnection.Send("TOPIC " & Name & " :" & value)
            End Set
        End Property
        Public ReadOnly Property TopicSetter As String Implements IChannel.TopicSetter
            Get
                Return pTopicSetter
            End Get
        End Property
        Public ReadOnly Property TopicStamp As Date Implements IChannel.TopicStamp
            Get
                Return pTopicStamp
            End Get
        End Property

        Public ReadOnly Property Users As UserCollection Implements IChannel.Users
            Get
                Return pUsers
            End Get
        End Property

        Public Sub BanExcept(Target As String) Implements IChannel.BanExcept
            BanExcept({Target})
        End Sub

        Public Sub BanExcept(Targets() As String) Implements IChannel.BanExcept
            NicknameMode("+"c, "e"c, Targets)
        End Sub

        Public Sub InviteExcept(Target As String) Implements IChannel.InviteExcept
            InviteExcept({Target})
        End Sub

        Public Sub InviteExcept(Targets() As String) Implements IChannel.InviteExcept
            NicknameMode("+"c, "I"c, Targets)
        End Sub

        Public Sub Unban(Target As String) Implements IChannel.Unban
            Unban({Target})
        End Sub

        Public Sub Unban(Targets() As String) Implements IChannel.Unban
            NicknameMode("-"c, "b"c, Targets)
        End Sub

        Public Sub UnbanIP(Target As String) Implements IChannel.UnbanIP
            UnbanIP({Target})
        End Sub

        Public Sub UnbanIP(Targets() As String) Implements IChannel.UnbanIP
            Dim IPs As New List(Of String)
            For Each Target In Targets
                If pUsers.ContainsKey(Target) Then
                    IPs.Add("*!*@" & pUsers(Target).Host)
                End If
            Next
            NicknameMode("-"c, "b"c, IPs.ToArray)
        End Sub

        Public Sub BanUnExcept(Target As String) Implements IChannel.BanUnExcept
            BanUnExcept({Target})
        End Sub

        Public Sub BanUnExcept(Targets() As String) Implements IChannel.BanUnExcept
            NicknameMode("-"c, "e"c, Targets)
        End Sub

        Public ReadOnly Property CanSay As Boolean Implements IChannel.CanSay
            Get
                If Not IsOnline And Modes.Contains("n") Then Return False
                If OwnStatus < ChannelAccessModes.Voice And Modes.Contains("m") Then Return False
                Return True
            End Get
        End Property

        Public ReadOnly Property CanSayPrivate As Boolean Implements IChannel.CanSayPrivate
            Get
                Return True
            End Get
        End Property

        Public Sub InviteUnExcept(Target As String) Implements IChannel.InviteUnExcept
            InviteUnExcept({Target})
        End Sub

        Public Sub InviteUnExcept(Targets() As String) Implements IChannel.InviteUnExcept
            NicknameMode("-"c, "I"c, Targets)
        End Sub

        Public ReadOnly Property IsOnline As Boolean Implements IChannel.IsOnline
            Get
                Return Users.ContainsKey(lConnection.Nickname)
            End Get
        End Property

        Public ReadOnly Property CanJoin As Boolean Implements IChannel.CanJoin
            Get
                Return Not (Modes.Contains("i"))
            End Get
        End Property

        Public ReadOnly Property CanQuiet As Boolean Implements IChannel.CanQuiet
            Get
                Return OwnStatus >= ChannelAccessModes.HalfOp AndAlso lConnection.ChanModes.TypeAModes.Contains("q"c)
            End Get
        End Property

        Public ReadOnly Property CanQuiet(User As String) As Boolean Implements IChannel.CanQuiet
            Get
                Return CanQuiet
            End Get
        End Property

        Public ReadOnly Property CanSeeBanExceptionList As Boolean Implements IChannel.CanSeeBanExceptionList
            Get
                Return OwnStatus >= ChannelAccessModes.HalfOp
            End Get
        End Property

        Public ReadOnly Property CanSeeBanList As Boolean Implements IChannel.CanSeeBanList
            Get
                Return True
            End Get
        End Property

        Public ReadOnly Property CanSeeInviteExceptionList As Boolean Implements IChannel.CanSeeInviteExceptionList
            Get
                Return OwnStatus >= ChannelAccessModes.HalfOp
            End Get
        End Property

        Public ReadOnly Property CanSetKey As Boolean Implements IChannel.CanSetKey
            Get
                Return OwnStatus >= ChannelAccessModes.HalfOp
            End Get
        End Property

        Public ReadOnly Property CanSetUserLimit As Boolean Implements IChannel.CanSetUserLimit
            Get
                Return OwnStatus >= ChannelAccessModes.HalfOp
            End Get
        End Property

        Public Property Key As String Implements IChannel.Key
            Get
                Return Key
            End Get
            Set(value As String)
                If HasKey Then
                    lConnection.Send("MODE " & Name & " -k+k " & Key & " " & value)
                Else
                    lConnection.Send("MODE " & Name & " +k " & value)
                End If
            End Set
        End Property

        Public Sub Quiet(Target As String) Implements IChannel.Quiet
            Quiet({Target})
        End Sub

        Public Sub Quiet(Targets() As String) Implements IChannel.Quiet
            NicknameMode("+"c, "q"c, Targets)
        End Sub

        Public ReadOnly Property SupportsBanList As Boolean Implements IChannel.SupportsBanList
            Get
                Return True
            End Get
        End Property

        Public ReadOnly Property SupportsKey As Boolean Implements IChannel.SupportsKey
            Get
                Return True
            End Get
        End Property

        Public ReadOnly Property SupportsKeyChange As Boolean Implements IChannel.SupportsKeyChange
            Get
                Return True
            End Get
        End Property

        Public ReadOnly Property SupportsQuiet As Boolean Implements IChannel.SupportsQuiet
            Get
                Return lConnection.ChanModes.TypeAModes.Contains("q"c)
            End Get
        End Property

        Public ReadOnly Property SupportsUserLimit As Boolean Implements IChannel.SupportsUserLimit
            Get
                Return True
            End Get
        End Property

        Public ReadOnly Property SupportsUserLimitChange As Boolean Implements IChannel.SupportsUserLimitChange
            Get
                Return True
            End Get
        End Property

        Public Sub UnQuiet(Target As String) Implements IChannel.UnQuiet
            Quiet({Target})
        End Sub

        Public Sub UnQuiet(Targets() As String) Implements IChannel.UnQuiet
            NicknameMode("-"c, "q"c, Targets)
        End Sub

        Public Property UserLimit As Integer Implements IChannel.UserLimit
            Get
                Return pUserLimit
            End Get
            Set(value As Integer)
                lConnection.Send("MODE " & Name & " +l " & value)
            End Set
        End Property

        Public Property HasKey As Boolean Implements IChannel.HasKey
            Get
                Return Modes.Contains("k")
            End Get
            Set(value As Boolean)
                If value Then
                    Throw New NotSupportedException("You cannot set the key with the HasKey property; set the Key property instead.")
                Else
                    lConnection.Send("MODE " & Name & " -k " & Key)
                End If
            End Set
        End Property

        Public Property HasLimit As Boolean Implements IChannel.HasLimit
            Get
                Return Modes.Contains("l")
            End Get
            Set(value As Boolean)
                If value Then
                    Throw New NotSupportedException("You cannot set the limit with the HasLimit property; set the Limit property instead.")
                Else
                    lConnection.Send("MODE " & Name & " -l")
                End If
            End Set
        End Property
    End Class

End Class