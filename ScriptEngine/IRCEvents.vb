Imports VBot


Public Class ListEventArgs
    Inherits EventArgs
    Public Parameters As Dictionary(Of String, Object)

    Public Sub New(Parameters As Dictionary(Of String, Object))
        Me.Parameters = Parameters
    End Sub
End Class

Partial Public Class ScriptEngine
    Public Overrides Sub OnChannelMessage(Connection As IRCConnection, Sender As String, Channel As String, Message As String)
        Dim e = New ListEventArgs(New Dictionary(Of String, Object)(StringComparer.OrdinalIgnoreCase) From {{"connection", Connection}, {"nick", Sender.Split("!"c)(0)}, {"channel", Channel}, {"message", Message}})
        Dim Result = RunEvent(Connection, Channel, Sender, "TEXT", Message, e, False)
        If Result = Script.ExecuteResults.OK Then MyBase.OnChannelMessage(Connection, Sender, Channel, Message)
    End Sub

    Public Overrides Sub OnChannelAction(Connection As IRCConnection, Sender As String, Channel As String, Message As String)
        Dim e = New ListEventArgs(New Dictionary(Of String, Object)(StringComparer.OrdinalIgnoreCase) From {{"connection", Connection}, {"nick", Sender.Split("!"c)(0)}, {"channel", Channel}, {"message", Message}})
        Dim Result = RunEvent(Connection, Channel, Sender, "ACTION", Message, e, False)
        If Result = Script.ExecuteResults.OK Then MyBase.OnChannelAction(Connection, Sender, Channel, Message)
    End Sub
    Public Overrides Sub OnChannelActionHighlight(Connection As IRCConnection, Sender As String, Channel As String, Message As String)
        Dim e = New ListEventArgs(New Dictionary(Of String, Object)(StringComparer.OrdinalIgnoreCase) From {{"connection", Connection}, {"nick", Sender.Split("!"c)(0)}, {"channel", Channel}, {"message", Message}})
        Dim Result = RunEvent(Connection, Channel, Sender, "ACTION", Message, e, False)
        If Result = Script.ExecuteResults.OK Then MyBase.OnChannelActionHighlight(Connection, Sender, Channel, Message)
    End Sub

    Public Overrides Sub OnChannelJoin(Connection As IRCConnection, Sender As String, Channel As String)
        Dim e = New ListEventArgs(New Dictionary(Of String, Object)(StringComparer.OrdinalIgnoreCase) From {{"connection", Connection}, {"nick", Sender.Split("!"c)(0)}, {"channel", Channel}})
        Dim Result = RunEvent(Connection, Channel, Sender, "JOIN", Nothing, e, False)
        If Result = Script.ExecuteResults.OK Then MyBase.OnChannelJoin(Connection, Sender, Channel)
    End Sub
    Public Overrides Sub OnChannelJoinSelf(Connection As IRCConnection, Sender As String, Channel As String)
        Dim e = New ListEventArgs(New Dictionary(Of String, Object)(StringComparer.OrdinalIgnoreCase) From {{"connection", Connection}, {"nick", Sender.Split("!"c)(0)}, {"channel", Channel}})
        Dim Result = RunEvent(Connection, Channel, Sender, "JOIN", Nothing, e, False)
        If Result = Script.ExecuteResults.OK Then MyBase.OnChannelJoinSelf(Connection, Sender, Channel)
    End Sub

    Public Overrides Sub OnChannelPart(Connection As IRCConnection, Sender As String, Channel As String, Reason As String)
        Dim e = New ListEventArgs(New Dictionary(Of String, Object)(StringComparer.OrdinalIgnoreCase) From {{"connection", Connection}, {"nick", Sender.Split("!"c)(0)}, {"channel", Channel}, {"message", Reason}})
        Dim Result = RunEvent(Connection, Channel, Sender, "PART", Reason, e, False)
        If Result = Script.ExecuteResults.OK Then MyBase.OnChannelPart(Connection, Sender, Channel, Reason)
    End Sub
    Public Overrides Sub OnChannelPartSelf(Connection As IRCConnection, Sender As String, Channel As String, Reason As String)
        Dim e = New ListEventArgs(New Dictionary(Of String, Object)(StringComparer.OrdinalIgnoreCase) From {{"connection", Connection}, {"nick", Sender.Split("!"c)(0)}, {"channel", Channel}, {"message", Reason}})
        Dim Result = RunEvent(Connection, Channel, Sender, "PART", Reason, e, False)
        If Result = Script.ExecuteResults.OK Then MyBase.OnChannelPartSelf(Connection, Sender, Channel, Reason)
    End Sub

    Public Overrides Sub OnQuit(Connection As IRCConnection, Sender As String, Reason As String)
        Dim e = New ListEventArgs(New Dictionary(Of String, Object)(StringComparer.OrdinalIgnoreCase) From {{"connection", Connection}, {"nick", Sender.Split("!"c)(0)}, {"message", Reason}})
        Dim Result = RunEvent(Connection, Nothing, Sender, "QUIT", Reason, e, False)
        If Result = Script.ExecuteResults.OK Then MyBase.OnQuit(Connection, Sender, Reason)
    End Sub
    Public Overrides Sub OnQuitSelf(Connection As IRCConnection, Sender As String, Reason As String)
        Dim e = New ListEventArgs(New Dictionary(Of String, Object)(StringComparer.OrdinalIgnoreCase) From {{"connection", Connection}, {"nick", Sender.Split("!"c)(0)}, {"message", Reason}})
        Dim Result = RunEvent(Connection, Nothing, Sender, "QUIT", Reason, e, False)
        If Result = Script.ExecuteResults.OK Then MyBase.OnQuitSelf(Connection, Sender, Reason)
    End Sub

    Public Overrides Sub OnChannelExit(Connection As IRCConnection, Sender As String, Channel As String, Reason As String)
        Dim e = New ListEventArgs(New Dictionary(Of String, Object)(StringComparer.OrdinalIgnoreCase) From {{"connection", Connection}, {"nick", Sender.Split("!"c)(0)}, {"channel", Channel}, {"message", Reason}})
        Dim Result = RunEvent(Connection, Channel, Sender, "EXIT", Reason, e, False)
        If Result = Script.ExecuteResults.OK Then MyBase.OnChannelExit(Connection, Sender, Channel, Reason)
    End Sub
    Public Overrides Sub OnChannelExitSelf(Connection As IRCConnection, Sender As String, Channel As String, Reason As String)
        Dim e = New ListEventArgs(New Dictionary(Of String, Object)(StringComparer.OrdinalIgnoreCase) From {{"connection", Connection}, {"nick", Sender.Split("!"c)(0)}, {"channel", Channel}, {"message", Reason}})
        Dim Result = RunEvent(Connection, Channel, Sender, "EXIT", Reason, e, False)
        If Result = Script.ExecuteResults.OK Then MyBase.OnChannelExitSelf(Connection, Sender, Channel, Reason)
    End Sub
End Class