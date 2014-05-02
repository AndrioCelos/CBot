Imports VBot

Public Class AI
    Public Nicknames As String()

    Public Connection As IRCConnection

    Public Sub Connect(ByVal Address As String)
        Connection = New IRCConnection With {.Address = Address, .Port = 6667, .Nickname = Me.Nicknames(0), .Nicknames = Me.Nicknames, .Username = "AI", .FullName = Nicknames(0)}
        Connection.Connect()

        AddHandler Connection.RawLineReceived, Sub(sender As IRCConnection, message As String) OutputLine(String.Format("\cBLUE{0} \cGREEN>>\r {1}", sender.Address, message))
        AddHandler Connection.RawLineSent, Sub(sender As IRCConnection, message As String) OutputLine(String.Format("\cBLUE{0} \cRED<<\r {1}", sender.Address, message))

    End Sub

    Public Sub Join(ByVal Channel As String)
        Connection.Send("JOIN " & Channel)
        Connection.Send("PRIVMSG " & Channel & " :" & Chr(1) & "ACTION enters the game." & Chr(1))
    End Sub

    Public Sub Play(ByVal Channel As String, ByVal Game As GreedyDicePlugin.ChannelGame)
        Dim Position As Integer = 1
        Dim MyScore As Integer = 0

        For i = 0 To Game.Players.Count - 1
            If Game.Players(i).Split("/"c)(1) <> Connection.Nickname And Game.Score(i) > Game.CurrentScore Then Position += 1
        Next

        Threading.Thread.Sleep(667)

        If Game.CurrentScore >= 700 + 200 * Position Then
            Connection.Send("PRIVMSG " & Channel & " :" & Chr(1) & "ACTION passes." & Chr(1))
        Else
            Connection.Send("PRIVMSG " & Channel & " :" & Chr(1) & "ACTION rolls the dice." & Chr(1))
        End If
    End Sub

    Public Sub Part()
        Connection.Send("QUIT")
        Connection.Disconnect()
    End Sub
End Class