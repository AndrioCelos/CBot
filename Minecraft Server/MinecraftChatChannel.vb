Imports VBot
Imports VBot.IRCConnection

Public Class MinecraftChatChannel
    Implements IChannel
    Dim lPlugin As MinecraftServerPlugin

    Public Sub Admin(ByVal ParamArray Targets() As String) Implements IChannel.Admin
        Throw New NotSupportedException
    End Sub

    Public Sub Ban(ByVal Target As String) Implements IChannel.Ban
        lPlugin.Server.StandardInput.WriteLine("ban " & Target)
    End Sub

    Public Sub Ban(ByVal Target As String, ByVal Message As String) Implements IChannel.Ban

    End Sub

    Public Sub Ban(ByVal Targets() As String) Implements IChannel.Ban

    End Sub

    Public Sub Ban(ByVal Targets() As String, ByVal Message As String) Implements IChannel.Ban

    End Sub

    Public Sub BanExcept(ByVal Target As String) Implements IChannel.BanExcept
        Throw New NotSupportedException
    End Sub

    Public Sub BanExcept(ByVal Targets() As String) Implements IChannel.BanExcept
        Throw New NotSupportedException
    End Sub

    Public Sub BanIP(ByVal Target As String) Implements IChannel.BanIP

    End Sub

    Public Sub BanIP(ByVal Target As String, ByVal Message As String) Implements IChannel.BanIP

    End Sub

    Public Sub BanIP(ByVal Targets() As String) Implements IChannel.BanIP

    End Sub

    Public Sub BanIP(ByVal Targets() As String, ByVal Message As String) Implements IChannel.BanIP

    End Sub

    Public Sub BanUnExcept(ByVal Target As String) Implements IChannel.BanUnExcept
        Throw New NotSupportedException
    End Sub

    Public Sub BanUnExcept(ByVal Targets() As String) Implements IChannel.BanUnExcept
        Throw New NotSupportedException
    End Sub

    Public ReadOnly Property CanAdmin As Boolean Implements IChannel.CanAdmin
        Get
            Return False
        End Get
    End Property

    Public ReadOnly Property CanBan As Boolean Implements IChannel.CanBan
        Get
            Return True
        End Get
    End Property

    Public ReadOnly Property CanBan(ByVal User As String) As Boolean Implements IChannel.CanBan
        Get
            Return True
        End Get
    End Property

    Public ReadOnly Property CanBanIP As Boolean Implements IChannel.CanBanIP
        Get
            Return True
        End Get
    End Property

    Public ReadOnly Property CanDeAdmin As Boolean Implements IChannel.CanDeAdmin
        Get
            Return False
        End Get
    End Property

    Public ReadOnly Property CanDeHalfOp As Boolean Implements IChannel.CanDeHalfOp
        Get
            Return False
        End Get
    End Property

    Public ReadOnly Property CanDeHalfVoice As Boolean Implements IChannel.CanDeHalfVoice
        Get
            Return False
        End Get
    End Property

    Public ReadOnly Property CanDeOp As Boolean Implements IChannel.CanDeOp
        Get
            Return True
        End Get
    End Property

    Public ReadOnly Property CanDeVoice As Boolean Implements IChannel.CanDeVoice
        Get
            Return False
        End Get
    End Property

    Public ReadOnly Property CanHalfOp As Boolean Implements IChannel.CanHalfOp
        Get
            Return False
        End Get
    End Property

    Public ReadOnly Property CanHalfVoice As Boolean Implements IChannel.CanHalfVoice
        Get
            Return False
        End Get
    End Property

    Public ReadOnly Property CanInvite As Boolean Implements IChannel.CanInvite
        Get
            Return False
        End Get
    End Property

    Public ReadOnly Property CanJoin As Boolean Implements IChannel.CanJoin
        Get
            Return False
        End Get
    End Property

    Public ReadOnly Property CanKick As Boolean Implements IChannel.CanKick
        Get
            Return True
        End Get
    End Property

    Public ReadOnly Property CanKick(ByVal User As String) As Boolean Implements IChannel.CanKick
        Get
            Return True
        End Get
    End Property

    Public ReadOnly Property CanOfflineBan As Boolean Implements IChannel.CanOfflineBan
        Get
            Return True
        End Get
    End Property

    Public ReadOnly Property CanOp As Boolean Implements IChannel.CanOp
        Get
            Return True
        End Get
    End Property

    Public ReadOnly Property CanQuiet As Boolean Implements IChannel.CanQuiet
        Get
            Return False
        End Get
    End Property

    Public ReadOnly Property CanQuiet(ByVal User As String) As Boolean Implements IChannel.CanQuiet
        Get
            Return False
        End Get
    End Property

    Public ReadOnly Property CanSay As Boolean Implements IChannel.CanSay
        Get
            Return True
        End Get
    End Property

    Public ReadOnly Property CanSayPrivate As Boolean Implements IChannel.CanSayPrivate
        Get
            Return True
        End Get
    End Property

    Public ReadOnly Property CanSeeBanExceptionList As Boolean Implements IChannel.CanSeeBanExceptionList
        Get
            Return False
        End Get
    End Property

    Public ReadOnly Property CanSeeBanList As Boolean Implements IChannel.CanSeeBanList
        Get
            Return True
        End Get
    End Property

    Public ReadOnly Property CanSeeInviteExceptionList As Boolean Implements IChannel.CanSeeInviteExceptionList
        Get
            Return False
        End Get
    End Property

    Public ReadOnly Property CanSetBanExceptions As Boolean Implements IChannel.CanSetBanExceptions
        Get
            Return False
        End Get
    End Property

    Public ReadOnly Property CanSetInviteExceptions As Boolean Implements IChannel.CanSetInviteExceptions
        Get
            Return False
        End Get
    End Property

    Public ReadOnly Property CanSetKey As Boolean Implements IChannel.CanSetKey
        Get
            Return False
        End Get
    End Property

    Public ReadOnly Property CanSetTopic As Boolean Implements IChannel.CanSetTopic
        Get
            Return False
        End Get
    End Property

    Public ReadOnly Property CanSetUserLimit As Boolean Implements IChannel.CanSetUserLimit
        Get
            Return False
        End Get
    End Property

    Public ReadOnly Property CanVoice As Boolean Implements IChannel.CanVoice
        Get
            Return False
        End Get
    End Property

    Public Sub DeAdmin(ByVal ParamArray Targets() As String) Implements IChannel.DeAdmin
        Throw New NotSupportedException
    End Sub

    Public Sub DeHalfOp(ByVal ParamArray Targets() As String) Implements IChannel.DeHalfOp
        Throw New NotSupportedException
    End Sub

    Public Sub DeHalfVoice(ByVal ParamArray Targets() As String) Implements IChannel.DeHalfVoice
        Throw New NotSupportedException
    End Sub

    Public Sub DeOp(ByVal ParamArray Targets() As String) Implements IChannel.DeOp

    End Sub

    Public Sub DeVoice(ByVal ParamArray Targets() As String) Implements IChannel.DeVoice
        Throw New NotSupportedException
    End Sub

    Public Sub HalfOp(ByVal ParamArray Targets() As String) Implements IChannel.HalfOp
        Throw New NotSupportedException
    End Sub

    Public Sub HalfVoice(ByVal ParamArray Targets() As String) Implements IChannel.HalfVoice
        Throw New NotSupportedException
    End Sub

    Public Property HasKey As Boolean Implements IChannel.HasKey
        Get
            Return False
        End Get
        Set(ByVal value As Boolean)
            Throw New NotSupportedException
        End Set
    End Property

    Public Property HasLimit As Boolean Implements IChannel.HasLimit
        Get

        End Get
        Set(ByVal value As Boolean)

        End Set
    End Property

    Public Sub InviteExcept(ByVal Target As String) Implements IChannel.InviteExcept
        Throw New NotSupportedException
    End Sub

    Public Sub InviteExcept(ByVal Targets() As String) Implements IChannel.InviteExcept
        Throw New NotSupportedException
    End Sub

    Public Sub InviteUnExcept(ByVal Target As String) Implements IChannel.InviteUnExcept
        Throw New NotSupportedException
    End Sub

    Public Sub InviteUnExcept(ByVal Targets() As String) Implements IChannel.InviteUnExcept
        Throw New NotSupportedException
    End Sub

    Public ReadOnly Property IsOnline As Boolean Implements IChannel.IsOnline
        Get

        End Get
    End Property

    Public ReadOnly Property IsPrivate As Boolean Implements IChannel.IsPrivate
        Get
            Return False
        End Get
    End Property

    Public Sub Join() Implements IChannel.Join
        Throw New NotSupportedException
    End Sub

    Public Sub Join(ByVal Key As String) Implements IChannel.Join
        Throw New NotSupportedException
    End Sub

    Public Property Key As String Implements IChannel.Key
        Get
            Throw New NotSupportedException
        End Get
        Set(ByVal value As String)
            Throw New NotSupportedException
        End Set
    End Property

    Public Sub Kick(ByVal Target As String) Implements IChannel.Kick

    End Sub

    Public Sub Kick(ByVal Target As String, ByVal Message As String) Implements IChannel.Kick

    End Sub

    Public Sub Kick(ByVal Targets() As String) Implements IChannel.Kick

    End Sub

    Public Sub Kick(ByVal Targets() As String, ByVal Message As String) Implements IChannel.Kick

    End Sub

    Public ReadOnly Property Name As String Implements IChannel.Name
        Get

        End Get
    End Property

    Public Sub Op(ByVal ParamArray Targets() As String) Implements IChannel.Op

    End Sub

    Public Sub Part() Implements IChannel.Part
        Throw New NotSupportedException
    End Sub

    Public Sub Part(ByVal Message As String) Implements IChannel.Part
        Throw New NotSupportedException
    End Sub

    Public Sub Quiet(ByVal Target As String) Implements IChannel.Quiet
        Throw New NotSupportedException
    End Sub

    Public Sub Quiet(ByVal Targets() As String) Implements IChannel.Quiet
        Throw New NotSupportedException
    End Sub

    Public Sub Say(ByVal Message As String) Implements IChannel.Say

    End Sub

    Public Sub SayPrivate(ByVal Target As String, ByVal Message As String, Optional ByVal Notice As Boolean = True) Implements IChannel.SayPrivate

    End Sub

    Public ReadOnly Property SupportsAdmin As Boolean Implements IChannel.SupportsAdmin
        Get
            Return False
        End Get
    End Property

    Public ReadOnly Property SupportsBan As Boolean Implements IChannel.SupportsBan
        Get
            Return True
        End Get
    End Property

    Public ReadOnly Property SupportsBanExceptions As Boolean Implements IChannel.SupportsBanExceptions
        Get
            Return False
        End Get
    End Property

    Public ReadOnly Property SupportsBanIP As Boolean Implements IChannel.SupportsBanIP
        Get
            Return True
        End Get
    End Property

    Public ReadOnly Property SupportsBanIPReason As Boolean Implements IChannel.SupportsBanIPReason
        Get

        End Get
    End Property

    Public ReadOnly Property SupportsBanList As Boolean Implements IChannel.SupportsBanList
        Get
            Return True
        End Get
    End Property

    Public ReadOnly Property SupportsBanReason As Boolean Implements IChannel.SupportsBanReason
        Get

        End Get
    End Property

    Public ReadOnly Property SupportsHalfOp As Boolean Implements IChannel.SupportsHalfOp
        Get
            Return False
        End Get
    End Property

    Public ReadOnly Property SupportsHalfVoice As Boolean Implements IChannel.SupportsHalfVoice
        Get
            Return False
        End Get
    End Property

    Public ReadOnly Property SupportsInvite As Boolean Implements IChannel.SupportsInvite
        Get
            Return False
        End Get
    End Property

    Public ReadOnly Property SupportsInviteExceptions As Boolean Implements IChannel.SupportsInviteExceptions
        Get
            Return False
        End Get
    End Property

    Public ReadOnly Property SupportsJoin As Boolean Implements IChannel.SupportsJoin
        Get
            Return False
        End Get
    End Property

    Public ReadOnly Property SupportsKey As Boolean Implements IChannel.SupportsKey
        Get
            Return False
        End Get
    End Property

    Public ReadOnly Property SupportsKeyChange As Boolean Implements IChannel.SupportsKeyChange
        Get
            Return False
        End Get
    End Property

    Public ReadOnly Property SupportsKick As Boolean Implements IChannel.SupportsKick
        Get
            Return True
        End Get
    End Property

    Public ReadOnly Property SupportsKickReason As Boolean Implements IChannel.SupportsKickReason
        Get

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
            Return False
        End Get
    End Property

    Public ReadOnly Property SupportsQuiet As Boolean Implements IChannel.SupportsQuiet
        Get
            Return False
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
            Return False
        End Get
    End Property

    Public ReadOnly Property SupportsUserLimit As Boolean Implements IChannel.SupportsUserLimit
        Get
            Return True
        End Get
    End Property

    Public ReadOnly Property SupportsUserLimitChange As Boolean Implements IChannel.SupportsUserLimitChange
        Get
            Return False
        End Get
    End Property

    Public ReadOnly Property SupportsUserList As Boolean Implements IChannel.SupportsUserList
        Get
            Return True
        End Get
    End Property

    Public ReadOnly Property SupportsVoice As Boolean Implements IChannel.SupportsVoice
        Get
            Return False
        End Get
    End Property

    Public ReadOnly Property Timestamp As Date Implements IChannel.Timestamp
        Get

        End Get
    End Property

    Public Property Topic As String Implements IChannel.Topic
        Get

        End Get
        Set(ByVal value As String)
            Throw New NotSupportedException
        End Set
    End Property

    Public ReadOnly Property TopicSetter As String Implements IChannel.TopicSetter
        Get
            Throw New NotSupportedException
        End Get
    End Property

    Public ReadOnly Property TopicStamp As Date Implements IChannel.TopicStamp
        Get
            Throw New NotSupportedException
        End Get
    End Property

    Public Sub Unban(ByVal Target As String) Implements IChannel.Unban

    End Sub

    Public Sub Unban(ByVal Targets() As String) Implements IChannel.Unban

    End Sub

    Public Sub UnbanIP(ByVal Target As String) Implements IChannel.UnbanIP

    End Sub

    Public Sub UnbanIP(ByVal Targets() As String) Implements IChannel.UnbanIP

    End Sub

    Public Sub UnQuiet(ByVal Target As String) Implements IChannel.UnQuiet
        Throw New NotSupportedException
    End Sub

    Public Sub UnQuiet(ByVal Targets() As String) Implements IChannel.UnQuiet
        Throw New NotSupportedException
    End Sub

    Public Property UserLimit As Integer Implements IChannel.UserLimit
        Get

        End Get
        Set(ByVal value As Integer)
            Throw New NotSupportedException
        End Set
    End Property

    Public ReadOnly Property Users As UserCollection Implements IChannel.Users
        Get

        End Get
    End Property

    Public Sub Voice(ByVal ParamArray Targets() As String) Implements IChannel.Voice
        Throw New NotSupportedException
    End Sub
End Class
