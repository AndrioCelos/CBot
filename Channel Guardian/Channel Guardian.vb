Imports VBot
Imports System.Text.RegularExpressions

Public Class ChannelGuardianPlugin
    Inherits Plugin

    Public Structure Ban
        ''' <summary>The mask that is banned.</summary>
        Public Mask As String
        ''' <summary>The nickname that the ban is targeted at.</summary>
        Public Nickname As String
        ''' <summary>The time when the ban is due to expire.</summary>
        Public Expires As Date
    End Structure
    Public Bans As New Dictionary(Of String, Dictionary(Of String, Ban))

    Public Overrides ReadOnly Property Name As String
        Get
            Return "Channel Guardian"
        End Get
    End Property

    Public Overrides Sub OnChannelExit(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Reason As String)
        MyBase.OnChannelPart(Connection, Sender, Channel, Reason)

        If Sender.Split("!")(0) = Connection.Nickname Then Return
        If Connection.Channels(Channel).Users(Connection.Nickname).ChannelAccess >= IRCConnection.ChannelAccessModes.Op Then Return

        ' Attempt to gain ops in a channel in which the bot is the only member. This won't work for registered channels.
        If Connection.Channels(Channel).Users.Count = 1 Or (Connection.Channels(Channel).Users.Count = 2 And Connection.Channels(Channel).Users.ContainsKey(Sender.Split("!")(0))) Then
            Connection.Send("PART " & Channel)
            Connection.Send("JOIN " & Channel)
        End If
    End Sub

    Public Overrides Sub OnChannelMessage(ByVal Connection As VBot.IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Message As String)
        MyBase.OnChannelMessage(Connection, Sender, Channel, Message)

        MassHighlightCheck(Connection, Sender, Channel, Message)
    End Sub

    Public Sub MassHighlightCheck(ByVal Connection As VBot.IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Message As String)
        Dim Highlights As Integer

        If Connection Is Nothing Then Return
        For Each User In Connection.Channels(Channel).Users
            Dim r As New Regex("\b" & Regex.Escape(User.Key) & "\b", RegexOptions.IgnoreCase)
            If r.IsMatch(Message) Then Highlights += 1
        Next

        If Highlights >= 5 Then Connection.Send("KICK " & Channel & " " & Sender.Split("!"c)(0) & " :Mass highlighting")
    End Sub

#Region "Operator nickname commands"
    Private Sub FindTargets(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal Sender As String, ByVal args() As String, ByRef TargetAddress As String, ByRef TargetChannel As String, ByRef TargetNicknames As String)
        If args.Count = 0 Then  ' No channel was specified. Use the current channel and the sender.
            TargetAddress = Connection.Address
            TargetChannel = If(Channel.Contains("#"), Channel, Nothing)
        ElseIf args(0).Contains("/#") Then  ' The first argument contains an address and a channel. Use that channel.
            TargetAddress = args(0).Split({"/"c}, 2)(0)
            TargetChannel = args(0).Split({"/"c}, 2)(1)
            TargetNicknames = String.Join(",", args.Skip(1))
        ElseIf args(0).StartsWith("#") Then  ' The first argument contains a channel name. Use that channel on the current network.
            TargetAddress = Connection.Address
            TargetChannel = args(0)
            TargetNicknames = String.Join(",", args.Skip(1))
        Else  ' There are arguments specified that are not channel names. Assume they're the target nicknames.
            TargetAddress = Connection.Address
            TargetChannel = Channel
            TargetNicknames = String.Join(",", args)
        End If
        If TargetNicknames = "" Then TargetNicknames = Sender.Split("!"c)(0)
    End Sub

    Private Function SetNicknameModes(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal Sender As String, ByVal TargetAddress As String, ByVal TargetChannel As String, ByVal TargetNicknames() As String, ByVal Mode As String, ByVal ChanServCommand As String, Optional ByVal IsGlobal As Boolean = False) As Boolean
        If TargetChannel = Nothing Then
            Dim Delta As Boolean = False
            For Each lConnection In Connections
                If Not Connection.IsRegistered Then Continue For
                If TargetAddress = Nothing OrElse lConnection.Address.ToLower = TargetAddress.ToLower Then
                    For Each lChannel In lConnection.Channels
                        ' The sender must be on the channel.
                        If lChannel.Value.Users.ContainsKey(Sender.Split("!"c)(0)) Then
                            Dim lTargetNicknames As New List(Of String)
                            For Each lNickname In TargetNicknames
                                If lChannel.Value.Users.ContainsKey(lNickname) Then lTargetNicknames.Add(lNickname)
                            Next
                            If lTargetNicknames.Count > 0 Then _
                                Delta = SetNicknameModes(lConnection, lChannel.Key, Sender, lConnection.Address, lChannel.Key, lTargetNicknames.ToArray, Mode, ChanServCommand, True) Or Delta
                        End If
                    Next
                End If
                If TargetAddress <> Nothing Then Return True
            Next
            If TargetAddress <> Nothing Then Say(Connection, Channel, "I'm not connected to that network.")
            If Not Delta Then Say(Connection, Channel, "Nothing happens.")
            Return False
        End If

        For Each lConnection In Connections
            If lConnection.Address.ToLower = TargetAddress.ToLower Then
                If Not lConnection.IsRegistered Then
                    If Not IsGlobal Then Say(Connection, Channel, "My connection to $k13" & TargetAddress & "$o is currently down.")
                    Return False
                Else
                    If Not Connection.StatusPrefix.ContainsKey(Mode(1)) Then
                        If Not IsGlobal Then Say(Connection, Channel, "$k13" & TargetAddress & "$o does not support that command.")
                        Return False
                    End If
                    For Each lChannel In lConnection.Channels
                        If lChannel.Key.ToLower = TargetChannel.ToLower Then
                            ' Check for permissions.
                            Dim Permission As String
                            If TargetNicknames.Count > 1 OrElse TargetNicknames(0) <> Sender.Split("!"c)(0) Then
                                Select Case Mode(1)
                                    Case "V" : Permission = "halfop"
                                    Case "v" : Permission = "halfop"
                                    Case "h" : Permission = "op"
                                    Case "o" : Permission = "op"
                                    Case "a" : Permission = "owner"
                                End Select
                            Else
                                Select Case Mode(1)
                                    Case "V" : Permission = "halfvoice"
                                    Case "v" : Permission = "voice"
                                    Case "h" : Permission = "halfop"
                                    Case "o" : Permission = "op"
                                    Case "a" : Permission = "admin"
                                End Select
                            End If
                            If Not UserHasPermission(Connection, Channel, Sender, "irc.auto" & Permission & "." & TargetAddress.Replace("."c, "-"c) & "." & Channel.Replace("."c, "-"c)) Then
                                If Not IsGlobal Then Say(Connection, Channel, "You don't have permission for $k04" & TargetChannel & "$o.")
                                Return False
                            End If

                            If ("Vv".Contains(Mode(1)) And lChannel.Value.Users(lConnection.Nickname).ChannelAccess < IRCConnection.ChannelAccessModes.HalfOp) Or
                              ("ho".Contains(Mode(1)) And lChannel.Value.Users(lConnection.Nickname).ChannelAccess < IRCConnection.ChannelAccessModes.Op) Or
                              ("a".Contains(Mode(1)) And lChannel.Value.Users(lConnection.Nickname).ChannelAccess < IRCConnection.ChannelAccessModes.Owner) Then
                                For Each User In TargetNicknames
                                    Say(Connection, "ChanServ", String.Format(ChanServCommand, lChannel.Key, User), SayOptions.NoticeNever)
                                Next
                            Else
                                Dim Parameter2 As String = Mode(0), Parameter3 As String = ""
                                For i = 0 To TargetNicknames.Count - 1
                                    Dim Target = TargetNicknames(i)
                                    If Not lChannel.Value.Users.ContainsKey(Target) Then
                                        If Not IsGlobal Then Say(Connection, Channel, "$b" & Target & "$b isn't on that channel.")
                                        Return False
                                    Else
                                        Parameter2 &= Mode(1)
                                        Parameter3 &= If(Parameter3 = "", "", " ") & Target
                                    End If

                                    If Parameter2.Length = lConnection.Modes + 1 Then
                                        lConnection.Send("MODE " & lChannel.Key & " " & Parameter2 & " " & Parameter3)
                                        Parameter2 = Mode(0)
                                        Parameter3 = ""
                                    End If
                                Next
                                If Parameter2.Length > 1 Then lConnection.Send("MODE " & lChannel.Key & " " & Parameter2 & " " & Parameter3)
                            End If
                            Return True
                        End If
                    Next
                    Say(Connection, Channel, "I'm not on that channel.")
                End If
            End If
            Return False
        Next
        Say(Connection, Channel, "I'm not connected to that network.")
        Return False
    End Function

    <Command({"halfvoice", "hvoice"}, 0, 2,
    "halfvoice [[server]\channel] [nickname]",
    "Gives semi-voice to a user, default yourself.",
    "")>
    Public Sub CommandHalfVoice(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim TargetAddress As String, TargetChannel As String, TargetNicknames As String
        FindTargets(Connection, Channel, Sender, args, TargetAddress, TargetChannel, TargetNicknames)
        SetNicknameModes(Connection, Channel, Sender, TargetAddress, TargetChannel, TargetNicknames.Split({","c, " "c}, StringSplitOptions.RemoveEmptyEntries),
                 "+V", "HALFVOICE {0} {1}")
    End Sub
    <Command("voice", 0, 2,
    "voice [[server]\channel] [nickname]",
    "Gives voice to a user, default yourself.",
    "")>
    Public Sub CommandVoice(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim TargetAddress As String, TargetChannel As String, TargetNicknames As String
        FindTargets(Connection, Channel, Sender, args, TargetAddress, TargetChannel, TargetNicknames)
        SetNicknameModes(Connection, Channel, Sender, TargetAddress, TargetChannel, TargetNicknames.Split({","c, " "c}, StringSplitOptions.RemoveEmptyEntries),
                 "+v", "VOICE {0} {1}")
    End Sub
    <Command({"halfop", "semiop"}, 0, 2,
    "halfop [[server]\channel] [nickname]",
    "Gives channel semi-operator status to a user, default yourself.",
    "")>
    Public Sub CommandHalfOp(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim TargetAddress As String, TargetChannel As String, TargetNicknames As String
        FindTargets(Connection, Channel, Sender, args, TargetAddress, TargetChannel, TargetNicknames)
        SetNicknameModes(Connection, Channel, Sender, TargetAddress, TargetChannel, TargetNicknames.Split({","c, " "c}, StringSplitOptions.RemoveEmptyEntries),
                 "+h", "HALFOP {0} {1}")
    End Sub
    <Command("op", 0, 2,
    "op [[server]\channel] [nickname]",
    "Gives channel operator status to a user, default yourself.",
    "")>
    Public Sub CommandOp(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim TargetAddress As String, TargetChannel As String, TargetNicknames As String
        FindTargets(Connection, Channel, Sender, args, TargetAddress, TargetChannel, TargetNicknames)
        SetNicknameModes(Connection, Channel, Sender, TargetAddress, TargetChannel, TargetNicknames.Split({","c, " "c}, StringSplitOptions.RemoveEmptyEntries),
                 "+o", "OP {0} {1}")
    End Sub
    <Command("admin", 0, 2,
    "admin [[server]\channel] [nickname]",
    "Gives channel administrator status to a user, default yourself.",
    "")>
    Public Sub CommandAdmin(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim TargetAddress As String, TargetChannel As String, TargetNicknames As String
        FindTargets(Connection, Channel, Sender, args, TargetAddress, TargetChannel, TargetNicknames)
        SetNicknameModes(Connection, Channel, Sender, TargetAddress, TargetChannel, TargetNicknames.Split({","c, " "c}, StringSplitOptions.RemoveEmptyEntries),
                 "+a", "ADMIN {0} {1}")
    End Sub

    <Command({"dehalfvoice", "dehvoice"}, 0, 2,
"dehalfvoice [[server]\channel] [nickname]",
"Removes semi-voice from a user, default yourself.",
"")>
    Public Sub CommandDeHalfVoice(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim TargetAddress As String, TargetChannel As String, TargetNicknames As String
        FindTargets(Connection, Channel, Sender, args, TargetAddress, TargetChannel, TargetNicknames)
        SetNicknameModes(Connection, Channel, Sender, TargetAddress, TargetChannel, TargetNicknames.Split({","c, " "c}, StringSplitOptions.RemoveEmptyEntries),
                 "-V", "DEHALFVOICE {0} {1}")
    End Sub
    <Command("devoice", 0, 2,
"devoice [[server]\channel] [nickname]",
"Removes voice from a user, default yourself.",
"")>
    Public Sub CommandDevoice(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim TargetAddress As String, TargetChannel As String, TargetNicknames As String
        FindTargets(Connection, Channel, Sender, args, TargetAddress, TargetChannel, TargetNicknames)
        SetNicknameModes(Connection, Channel, Sender, TargetAddress, TargetChannel, TargetNicknames.Split({","c, " "c}, StringSplitOptions.RemoveEmptyEntries),
                 "-v", "DEVOICE {0} {1}")
    End Sub
    <Command({"dehalfop", "desemiop"}, 0, 2,
    "dehalfop [[server]\channel] [nickname]",
    "Removes channel semi-operator status from a user, default yourself.",
    "")>
    Public Sub CommandDeHalfOp(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim TargetAddress As String, TargetChannel As String, TargetNicknames As String
        FindTargets(Connection, Channel, Sender, args, TargetAddress, TargetChannel, TargetNicknames)
        SetNicknameModes(Connection, Channel, Sender, TargetAddress, TargetChannel, TargetNicknames.Split({","c, " "c}, StringSplitOptions.RemoveEmptyEntries),
                 "-h", "DEHALFOP {0} {1}")
    End Sub
    <Command("deop", 0, 2,
    "deop [[server]\channel] [nickname]",
    "Removes channel operator status from a user, default yourself.",
    "")>
    Public Sub CommandDeop(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim TargetAddress As String, TargetChannel As String, TargetNicknames As String
        FindTargets(Connection, Channel, Sender, args, TargetAddress, TargetChannel, TargetNicknames)
        SetNicknameModes(Connection, Channel, Sender, TargetAddress, TargetChannel, TargetNicknames.Split({","c, " "c}, StringSplitOptions.RemoveEmptyEntries),
                 "-o", "DEOP {0} {1}")
    End Sub
    <Command("deadmin", 0, 2,
    "deadmin [[server]\channel] [nickname]",
    "Removes channel administrator status from a user, default yourself.",
    "")>
    Public Sub CommandDeAdmin(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim TargetAddress As String, TargetChannel As String, TargetNicknames As String
        FindTargets(Connection, Channel, Sender, args, TargetAddress, TargetChannel, TargetNicknames)
        SetNicknameModes(Connection, Channel, Sender, TargetAddress, TargetChannel, TargetNicknames.Split({","c, " "c}, StringSplitOptions.RemoveEmptyEntries),
                 "-a", "DEADMIN {0} {1}")
    End Sub
#End Region

    <Command("grant", 2, 2,
"grant <account> <permission>",
"Gives a user a permission.",
"me.grant")>
    Public Sub CommandGrant(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        ReDim Accounts(args(0)).Permissions(UBound(Accounts(args(0)).Permissions))
        Accounts(args(0)).Permissions(UBound(Accounts(args(0)).Permissions)) = args(1)
        Reply(Connection, Channel, Sender, "Permission granted to $k09" & args(0) & "$o.")
    End Sub

End Class
