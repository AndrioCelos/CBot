' General to-do list:
'   - Make it so that CTCP messages aren't relayed.

Imports VBot

Public Class Class1
    Inherits VBot.Plugin

    Public Sub SendCheck(Message As String)
        For Each Channel In MinorChannels
            If Channel.StartsWith("!") Then
                If Channel = "!*" Then
                    For Each m In VBot.Plugins
                        SendCheckSub(Nothing, m.Key & "/*", Message)
                    Next
                ElseIf Channel.StartsWith("!*") Then
                    For Each m In VBot.Plugins
                        SendCheckSub(Nothing, m.Key & "/" & Channel.Split({"/"c}, 2)(1), Message)
                    Next
                Else
                    SendCheckSub(Nothing, Channel, Message)
                End If
            Else
                If Channel = "*" Or Channel = "*/*" Then
                    For Each m In VBot.Plugins
                        SendCheckSub(Nothing, m.Key & "/" & Channel.Split({"/"c}, 2)(1), Message)
                    Next
                End If
                For Each Connection In VBot.Connections
                    If Channel = "*" OrElse (Connection.Address = Channel.Split({"/"c}, 2)(0) Or Channel.Split({"/"c}, 2)(0) = "*") Then
                        If Channel = "*" OrElse Channel.Split({"/"c}, 2)(1) = "*" Then
                            For Each eChannel In Connection.Channels.Values
                                SendCheckSub(Connection, eChannel.Name, Message)
                            Next
                        Else
                            SendCheckSub(Connection, Channel.Split("/"c)(1), Message)
                        End If
                    End If
                Next
            End If
        Next
    End Sub

    Public Sub SendCheckSub(TargetConnection As IRCConnection, Target As String, Message As String)
        If Target.StartsWith("#") OrElse UserHasPermission(TargetConnection, Target, Target & "!*@*", MyKey & ".receive") Then
            Say(TargetConnection, Target, Message, SayOptions.NoticeNever)
        End If
    End Sub

    Public Overrides Sub OnChannelJoin(Connection As IRCConnection, Sender As String, Channel As String)
        MyBase.OnChannelJoin(Connection, Sender, Channel)
        SendCheck("$k15[$o" & Channel & "$k15] " & IRCColours.NicknameColour(Sender.Split("!"c)(0)) & Sender.Split("!"c)(0) & "$k2 (" & Sender.Split("!"c)(1) & ") joined.")
    End Sub

    Public Overrides Sub OnChannelJoinSelf(Connection As IRCConnection, Sender As String, Channel As String)
        MyBase.OnChannelJoin(Connection, Sender, Channel)
        SendCheck("$k15[$o" & Channel & "$k15] " & IRCColours.NicknameColour(Sender.Split("!"c)(0)) & Sender.Split("!"c)(0) & "$k2 (" & Sender.Split("!"c)(1) & ") joined.")
    End Sub

    Public Overrides Sub OnChannelExit(Connection As IRCConnection, Sender As String, Channel As String, Reason As String)
        MyBase.OnChannelExit(Connection, Sender, Channel, Reason)
        SendCheck("$k15[$o" & Channel & "$k15] " & IRCColours.NicknameColour(Sender.Split("!"c)(0)) & Sender.Split("!"c)(0) & "$k15 (" & Sender.Split("!"c)(1) & ") left: " & Reason)
    End Sub

    Public Overrides Sub OnChannelExitSelf(Connection As IRCConnection, Sender As String, Channel As String, Reason As String)
        MyBase.OnChannelExit(Connection, Sender, Channel, Reason)
        SendCheck("$k15[$o" & Channel & "$k15] " & IRCColours.NicknameColour(Sender.Split("!"c)(0)) & Sender.Split("!"c)(0) & "$k15 (" & Sender.Split("!"c)(1) & ") left: " & Reason)
    End Sub

    Public Overrides Sub OnChannelMessage(ByVal Connection As VBot.IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Message As String)
        MyBase.OnChannelMessage(Connection, Sender, Channel, Message)
        SendCheck("$k15[$o" & Channel & "$k15] " & IRCColours.NicknameColour(Sender.Split("!"c)(0)) & Sender.Split("!"c)(0) & "$o: " & Message)
    End Sub

    Public Overrides Sub OnPrivateMessage(ByVal Connection As VBot.IRCConnection, ByVal Sender As String, ByVal Message As String)
        MyBase.OnPrivateMessage(Connection, Sender, Message)
        If Not Message.StartsWith("!") Then _
               SendCheck("$k15[$k12PM$k15] " & IRCColours.NicknameColour(Sender.Split("!"c)(0)) & Sender.Split("!"c)(0) & "$o: " & Message)
    End Sub

    Public Overrides Sub OnPrivateAction(Connection As IRCConnection, Sender As String, Message As String)
        MyBase.OnPrivateAction(Connection, Sender, Message)
        SendCheck("$k15[$k12PM$k15] " & IRCColours.NicknameColour(Sender.Split("!"c)(0)) & Sender.Split("!"c)(0) & "$k13 " & Message)
    End Sub

    Public Overrides Sub OnPrivateNotice(Connection As IRCConnection, Sender As String, Message As String)
        MyBase.OnPrivateNotice(Connection, Sender, Message)
        SendCheck("$k15[$k12PM$k15] " & IRCColours.NicknameColour(Sender.Split("!"c)(0)) & Sender.Split("!"c)(0) & "$k8: " & Message)
    End Sub
End Class
