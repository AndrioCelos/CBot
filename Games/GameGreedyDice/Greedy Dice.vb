' General to-do list:
'   TODO: Have the game use standard dice.
'   TODO: Implement stats, like the UNO plugin has.
'   TODO: Make the AI a bit more variable.

Imports VBot

Public Class GreedyDicePlugin
    Inherits Plugin

    Public Class ChannelGame
        Public Connection As IRCConnection
        Public Channel As String

        Public IsOpen As Boolean
        Public GameTimer As Timers.Timer
        Public GameStartTime As Date

        Public Players() As String
        Public Score() As Integer
        Public TurnNumber As Short
        Public CurrentTurn As Short
        Public CurrentScore As Integer

        Public AIs As Dictionary(Of String, AI)
    End Class

    Public Games As New Dictionary(Of String, ChannelGame)(StringComparer.OrdinalIgnoreCase)
    Public TurnsPerGame As Short = 4
    Public PointsPerGame As Integer = 4000
    Public WinCondition As Short = 1
    Public UseAI As Integer = 1

    Public Overrides ReadOnly Property Name As String
        Get
            Return "Greedy Dice game"
        End Get
    End Property

    Public Overrides Function Help(ByVal Topic As String, ByVal IsMajorChannel As Boolean) As String
        Select Case Topic.ToLower
            Case "greedydice", "gdice"
                Return "This variant of Greedy Dice is a simple dice game." & vbCrLf &
                       "Players take turns rolling a pair of dice as many times as they dare." & vbCrLf &
                       "Each die has two X faces and four scoring faces." & vbCrLf &
                       "A double (that is, getting the same face on both dice) is worth double points." & vbCrLf &
                       "But don't get too greedy: if you get a double X, you lose all your points for that turn." & vbCrLf &
                       "The player with the most points after a certain number of turns is the winner. Good luck!"
            Case Else
                Return "The $bGreedy Dice game$b is hosted in this channel." & vbCrLf &
                       "Say $k11$cdjoin$o to start a game, or to join a game that someone else starts." & vbCrLf &
                       "For more information about the Greedy Dice Game, say $k11$chelp GreedyDice$o."
        End Select

    End Function

    <Regex({"\x01ACTION enters( the game( of Greedy Dice)?)?\.?\x01"})>
    Public Sub RegexJoin(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As System.Text.RegularExpressions.Match)
        CommandJoin(Connection, Sender, Channel, {})
    End Sub
    <Command({"djoin"}, 0, 0,
    "djoin",
    "Enters you into a game of Greedy Dice. You may use this command to open a game, even if someone else hasn't started one.")>
    Public Sub CommandJoin(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim CurrentGame As ChannelGame = Nothing
        ' If no game has started, start one now.
        If Not Games.TryGetValue(If(Connection IsNot Nothing, Connection.Address & "/", "") & Channel, CurrentGame) Then
            CurrentGame = New ChannelGame With {.Connection = Connection, .Channel = Channel, .IsOpen = True, .GameTimer = New Timers.Timer(10000) With {.AutoReset = False, .Enabled = True}, .GameStartTime = Now + TimeSpan.FromMinutes(1), .Players = New String() {}, .Score = New Integer() {}}
            Games.Add(If(Connection IsNot Nothing, Connection.Address & "/", "") & Channel, CurrentGame)
            AppendArray(CurrentGame.Players, If(Connection Is Nothing, Channel.Split("/"c)(0), Connection.Address) & "/" & Sender.Split("!"c)(0))
            AppendArray(CurrentGame.Score, 0)
            Say(Connection, Channel, "$k09$b" & Sender.Split("!")(0) & "$b is starting a game of Greedy Dice!")
            Say(Connection, Channel, "$k12Say $k11$cdjoin$k12 if you wish to join the game.")
            AddHandler CurrentGame.GameTimer.Elapsed, AddressOf PrepareAITimer
        ElseIf Not CurrentGame.IsOpen Then
            Say(Connection, Channel, "Sorry " & Sender.Split("!")(0) & ", but this game has already started. Feel free to join the next game, though.")
        ElseIf CurrentGame.Players.Contains(If(Connection Is Nothing, Channel.Split("/"c)(0), Connection.Address) & "/" & Sender.Split("!"c)(0)) Then
            Say(Connection, Channel, "You've already entered the game, " & Sender.Split("!")(0) & ".")
        ElseIf CurrentGame.AIs IsNot Nothing AndAlso Sender.Split("!")(0) = CurrentGame.AIs.Values(CurrentGame.AIs.Count - 1).Connection.Nickname Then
            AppendArray(CurrentGame.Players, If(Connection Is Nothing, Channel.Split("/"c)(0), Connection.Address) & "/" & Sender.Split("!"c)(0))
            AppendArray(CurrentGame.Score, 0)
            Say(Connection, Channel, "$k9$b" & Sender.Split("!")(0) & "$b has entered the game!")

            CurrentGame.IsOpen = False

            Say(Connection, Channel, "$k9The game of $bGreedy Dice$b has started!")
            CurrentGame.GameTimer.Interval = 30000 ' TODO: Make this configurable.
            CurrentGame.GameTimer.Start()
            RemoveHandler CurrentGame.GameTimer.Elapsed, AddressOf GameClose

            CurrentGame.CurrentTurn = 0
            Say(Connection, Channel, "$k2$b" & CurrentGame.Players(CurrentGame.CurrentTurn).Split({"/"c}, 2)(1) & "$b, it's your turn. Enter $k11$cdroll$k2 to roll the dice.")
            CheckForAI(CurrentGame.Players(CurrentGame.CurrentTurn).Split({"/"c}, 2)(1), CurrentGame)
        Else
            'CurrentGame.GameStartTime += TimeSpan.FromSeconds(5)
            'CurrentGame.GameTimer.Interval = (CurrentGame.GameStartTime - Now).TotalMilliseconds
            AppendArray(CurrentGame.Players, If(Connection Is Nothing, Channel.Split("/"c)(0), Connection.Address) & "/" & Sender.Split("!"c)(0))
            AppendArray(CurrentGame.Score, 0)
            Say(Connection, Channel, "$k9$b" & Sender.Split("!")(0) & "$b has entered the game!")
            'AddHandler CurrentGame.GameTimer.Elapsed, AddressOf GameClose
        End If
    End Sub

    <Command({"dquit"}, 0, 0,
   "dquit",
   "Exits you from the game.")>
    Public Sub CommandQuit(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Threading.Thread.Sleep(600)
        Dim CurrentGame As ChannelGame = Nothing
        If Not Games.TryGetValue(If(Connection IsNot Nothing, Connection.Address & "/", "") & Channel, CurrentGame) Then
            Say(Connection, Channel, "$k4There's no game going on right now.")
            Return
        Else
            Dim PlayerTag = If(Connection Is Nothing, Channel.Split("/"c)(0), Connection.Address) & "/" & Sender.Split("!"c)(0)
            Dim WasThisPlayersTurn = CurrentGame.Players(CurrentGame.CurrentTurn) = PlayerTag
            ' Remove the player who parted from the game.
            Dim PartingPlayerIndex = Array.IndexOf(CurrentGame.Players, PlayerTag)
            For i = PartingPlayerIndex + 1 To UBound(CurrentGame.Players)
                CurrentGame.Players(i - 1) = CurrentGame.Players(i)
                CurrentGame.Score(i - 1) = CurrentGame.Score(i)
            Next
            ReDim Preserve CurrentGame.Players(UBound(CurrentGame.Players) - 1)
            ReDim Preserve CurrentGame.Score(UBound(CurrentGame.Score) - 1)

            Say(Connection, Channel, IRCColours.Gray & PlayerTag.Split("/"c)(1) & "$k9 has quit the game.")
            If CurrentGame.IsOpen Then
                'If CurrentGame.AIs IsNot Nothing Then PrepareAI(CurrentGame, Connection, Channel)
            ElseIf WasThisPlayersTurn Then
                NextPlayer(CurrentGame, Connection, Channel)
            End If
        End If
    End Sub

    Public Overrides Sub OnChannelExit(ByVal Connection As VBot.IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Reason As String)
        Dim CurrentGame As ChannelGame = Nothing
        If Not Games.TryGetValue(If(Connection IsNot Nothing, Connection.Address & "/", "") & Channel, CurrentGame) Then
            Return
        ElseIf Not CurrentGame.Players.Contains(If(Connection Is Nothing, Channel.Split("/"c)(0), Connection.Address) & "/" & Sender.Split("!"c)(0)) Then
        Else
            Dim PlayerTag = If(Connection Is Nothing, Channel.Split("/"c)(0), Connection.Address) & "/" & Sender.Split("!"c)(0)
            Dim WasThisPlayersTurn = CurrentGame.Players(CurrentGame.CurrentTurn) = PlayerTag
            ' Remove the player who parted from the game.
            Dim PartingPlayerIndex = Array.IndexOf(CurrentGame.Players, PlayerTag)
            For i = PartingPlayerIndex + 1 To UBound(CurrentGame.Players)
                CurrentGame.Players(i - 1) = CurrentGame.Players(i)
                CurrentGame.Score(i - 1) = CurrentGame.Score(i)
            Next
            ReDim Preserve CurrentGame.Players(UBound(CurrentGame.Players) - 1)
            ReDim Preserve CurrentGame.Score(UBound(CurrentGame.Score) - 1)

            Say(Connection, Channel, IRCColours.Gray & PlayerTag.Split("/"c)(1) & "$k9 left the channel, and was removed from the game.")
            If CurrentGame.IsOpen Then
                'If CurrentGame.AIs IsNot Nothing Then PrepareAI(CurrentGame, Connection, Channel)
            ElseIf WasThisPlayersTurn Then
                NextPlayer(CurrentGame, Connection, Channel)
            End If
        End If
    End Sub

    Public Overrides Sub OnNicknameChange(ByVal Connection As VBot.IRCConnection, ByVal User As VBot.IRCConnection.IRCUser, ByVal NewNick As String)
        For Each channel In Connection.Channels
            Dim CurrentGame As ChannelGame = Nothing
            If Games.TryGetValue(Connection.Address & "/" & channel.Key, CurrentGame) Then
                Dim Index = Array.IndexOf(CurrentGame.Players, Connection.Address & "/" & User.Nickname)
                If Index >= 0 Then
                    CurrentGame.Players(Index) = Connection.Address & "/" & NewNick
                End If
            End If
        Next
    End Sub

    Private Sub PrepareAITimer(ByVal sender As Object, ByVal e As Timers.ElapsedEventArgs)
        Dim CurrentConnection As IRCConnection = Nothing, CurrentChannel As String = Nothing, CurrentGame As ChannelGame = Nothing
        For Each Game In Games
            If Game.Value.GameTimer Is sender Then
                CurrentGame = Game.Value
                CurrentChannel = Game.Key
                Exit For
            End If
        Next
        If IsNothing(CurrentGame) Then Return
        RemoveHandler CurrentGame.GameTimer.Elapsed, AddressOf PrepareAITimer

        If CurrentChannel.StartsWith("!") Then
            CurrentConnection = Nothing
        Else
            For Each Connection In Connections
                If Connection.Address = CurrentChannel.Split({"/"c}, 2)(0) Then
                    CurrentConnection = Connection
                    CurrentChannel = CurrentChannel.Split({"/"c}, 2)(1)
                    Exit For
                End If
            Next
        End If

        PrepareAI(CurrentGame, CurrentConnection, CurrentChannel)
    End Sub

    Private Sub PrepareAI(ByVal Game As ChannelGame, ByVal Connection As IRCConnection, ByVal Channel As String)
        If UseAI = 2 Then
            If Game.AIs Is Nothing Then Game.AIs = New Dictionary(Of String, AI)(StringComparer.OrdinalIgnoreCase)
            For i = Game.Players.Count + Game.AIs.Count To 1
                Dim NewAI As New AI
                ReDim NewAI.Nicknames(2)
                Do
                    NewAI.Nicknames(0) = Choose("George", "Jasmine", "Marcus", "Mitch", "Coraline", "Thelma", "Bianca", "Damien", "Cedric", "Vivian")
                Loop While Game.AIs.ContainsKey(NewAI.Nicknames(0))
                NewAI.Nicknames(1) = NewAI.Nicknames(0) & "-2"
                NewAI.Nicknames(2) = NewAI.Nicknames(0) & "-3"

                Game.AIs.Add(NewAI.Nicknames(0), NewAI)
                NewAI.Connect(Connection.Address)
            Next
        End If

        Game.GameTimer.Interval = 20000 ' TODO: Make this configurable.
        Game.GameTimer.Start()

        AddHandler Game.GameTimer.Elapsed, AddressOf GameClose
    End Sub

    <Command({"dset", "dconfig", "dproperty"}, 1, 2,
    "dset <property> <value>",
    "Changes plugin parameters for Greedy Dice." & vbCrLf &
    "You can set the following properties: $k11ai$o." & vbCrLf &
    "Alternatively, you can omit the $k11value$o parameter to just check a property's value.",
     ".set", CommandAttribute.CommandScope.Channel)>
    Public Sub CommandSet(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim lProperty = args(0)
        Dim lValue = args(1)

        Select Case lProperty.ToLower.Replace("_", "")
            Case "ai", "com", "cpu", "aiplayers", "computer", "complayers", "computerplayers", "cpuplayers"
                If {"allow", "allowed", "1", "-1", "on", "yes", "true", "+", "enable", "enabled", "activate", "activated", "active"}.Contains(lValue) Then
                    UseAI = 1
                    Reply(Connection, Channel, Sender, "AI players are now $k9enabled$o.")
                ElseIf {"disallow", "disallowed", "banned", "0", "off", "no", "false", "-", "disable", "disabled", "deactivate", "deactivated", "inactive"}.Contains(lValue) Then
                    UseAI = 0
                    Reply(Connection, Channel, Sender, "AI players are now $k4disabled$o.")
                Else
                    Reply(Connection, Channel, Sender, VBot.Choose("", "Sorry " & Sender.Split("!"c)(0) & ", ") & "I don't recognise " & VBot.Choose(IRCColours.Red & lValue & "$o", "that") & " as a Boolean value. Please enter $k11on$o or $k11off$o.")
                End If
            Case Else
                Say(Connection, Channel, "I don't manage a property named $k04" & lProperty & "$o for Greedy Dice.")
        End Select
    End Sub

    <Command({"aikill"}, 0, 0,
"aikill",
"Disconnects all computer player clients. Useful if they can't play for whatever reason.",
".aikill", CommandAttribute.CommandScope.Channel)>
    Public Sub CommandAIKill(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim currentGame As ChannelGame
        If Not Games.TryGetValue(If(Connection IsNot Nothing, Connection.Address & "/", "") & Channel, currentGame) Then
            Say(Connection, Channel, "$k4There's no game going on right now.")
            Return
        Else
            For Each AI In currentGame.AIs.Values
                AI.Part()
            Next
            currentGame.AIs.Clear()
        End If
    End Sub

    <Command({"ainudge"}, 0, 0,
"ainudge",
"Reminds a computer player whose turn it is to play. Useful if they couldn't speak in the channel.",
Nothing, CommandAttribute.CommandScope.Channel)>
    Public Sub CommandAINudge(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim currentGame As ChannelGame
        If Not Games.TryGetValue(If(Connection IsNot Nothing, Connection.Address & "/", "") & Channel, currentGame) Then
            Say(Connection, Channel, "$k4There's no game going on right now. Type $k11$cdjoin$k4 to start one.")
            Return
        ElseIf currentGame.IsOpen Then
            Say(Connection, Channel, "$k4This game hasn't started yet!")
            Return
        Else
            CheckForAI(currentGame.Players(currentGame.CurrentTurn).Split({"/"c}, 2)(1), currentGame)
        End If
    End Sub

    Private Sub GameClose(ByVal sender As Object, ByVal e As Timers.ElapsedEventArgs)
        Dim CurrentConnection As IRCConnection = Nothing, CurrentChannel As String = Nothing, CurrentGame As ChannelGame = Nothing
        For Each Game In Games
            If Game.Value.GameTimer Is sender Then
                CurrentGame = Game.Value
                CurrentChannel = Game.Key
                Exit For
            End If
        Next
        If IsNothing(CurrentGame) Then Return
        RemoveHandler CurrentGame.GameTimer.Elapsed, AddressOf GameClose

        If CurrentChannel.StartsWith("!") Then
            CurrentConnection = Nothing
        Else
            For Each Connection In Connections
                If Connection.Address = CurrentChannel.Split({"/"c}, 2)(0) Then
                    CurrentConnection = Connection
                    CurrentChannel = CurrentChannel.Split({"/"c}, 2)(1)
                    Exit For
                End If
            Next
        End If

        If CurrentGame.Players.Count < 2 Then
            If UseAI = 0 Then
                Say(CurrentConnection, CurrentChannel, "$k9Not enough players joined. Please enter $k11$cdjoin$k9 when you're ready for a game.")
                Games.Remove(If(CurrentConnection IsNot Nothing, CurrentConnection.Address & "/", "") & CurrentChannel)
            ElseIf UseAI = 1 Then
                AppendArray(CurrentGame.Players, If(CurrentConnection Is Nothing, CurrentChannel.Split("/"c)(0), CurrentConnection.Address) & "/" & Nickname(CurrentConnection))
                AppendArray(CurrentGame.Score, 0)
                Say(CurrentConnection, CurrentChannel, "$k9$b" & Nickname(CurrentConnection) & "$b has entered the game!")
            Else
                Say(CurrentConnection, CurrentChannel, "$k9Not enough players joined. Please enter $k11$cdjoin$k9 when you're ready for a game.")
                For i = 0 To 1 - CurrentGame.Players.Count
                    CurrentGame.AIs.Values(i).Join(CurrentChannel)
                Next i
                For i = 1 - CurrentGame.Players.Count + 1 To CurrentGame.AIs.Count - 1
                    CurrentGame.AIs.Values(i).Connection.Send("QUIT")
                Next
                Games.Remove(If(CurrentConnection IsNot Nothing, CurrentConnection.Address & "/", "") & CurrentChannel)
            End If
        End If
        If CurrentGame.Players.Count >= 2 Then
            CurrentGame.IsOpen = False

            Say(CurrentConnection, CurrentChannel, "$k9The game of $bGreedy Dice$b has started!")
            CurrentGame.GameTimer.Interval = 30000 ' TODO: Make this configurable.
            CurrentGame.GameTimer.Start()

            CurrentGame.CurrentTurn = 0
            Say(CurrentConnection, CurrentChannel, "$k2$b" & CurrentGame.Players(CurrentGame.CurrentTurn).Split({"/"c}, 2)(1) & "$b, it's your turn. Enter $k11$cdroll$k2 to roll the dice.")
            CheckForAI(CurrentGame.Players(CurrentGame.CurrentTurn).Split({"/"c}, 2)(1), CurrentGame)
        End If
    End Sub

    <Regex({"\x01ACTION rolls( the die| the dice)?( again)?\.?\x01"})>
    Public Sub RegexRoll(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As System.Text.RegularExpressions.Match)
        CommandRoll(Connection, Sender, Channel, {})
    End Sub
    <Command({"droll"}, 0, 0,
       "droll",
       "Rolls the dice.")>
    Public Sub CommandRoll(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Threading.Thread.Sleep(600)
        Dim CurrentGame As ChannelGame = Nothing
        If Not Games.TryGetValue(If(Connection IsNot Nothing, Connection.Address & "/", "") & Channel, CurrentGame) Then
            Say(Connection, Channel, "$k4There's no game going on right now. Type $k11$cdjoin$k4 to start one.")
            Return
        ElseIf CurrentGame.IsOpen Then
            Say(Connection, Channel, "$k4This game hasn't started yet!")
            Return
        ElseIf Not CurrentGame.Players.Contains(If(Connection Is Nothing, Channel.Split("/"c)(0), Connection.Address) & "/" & Sender.Split("!"c)(0)) Then
            Say(Connection, Channel, "$k4" & Sender.Split("!")(0) & ", you're not in this game.")
        ElseIf CurrentGame.Players(CurrentGame.CurrentTurn) <> If(Connection Is Nothing, Channel.Split("/"c)(0), Connection.Address) & "/" & Sender.Split("!"c)(0) Then
            Say(Connection, Channel, "$k4" & Sender.Split("!")(0) & ", it's not your turn.")
        Else
            Dim DieRoll1 As Short, DieRoll2 As Short, TotalScore As Short
            Randomize()
            DieRoll1 = Int(Rnd() * 6)
            DieRoll2 = Int(Rnd() * 6)

            If DieRoll1 < 2 And DieRoll2 < 2 Then ' A double X; you're out!
                If CurrentGame.CurrentScore = 0 Then
                    Say(Connection, Channel, "$k12$b" & Sender.Split("!")(0) & "$b rolled: $b$k1,8 X $o $b$k1,8 X $o $k12Since you haven't got any points, I won't count that.")
                    If Sender.Split("!")(0) <> VBot.Nickname(Connection) Then Say(Connection, Channel, "$k12Type $k11$cdroll$k12 to roll the dice again.")
                    CheckForAI(Sender.Split({"!"c})(0), CurrentGame)
                    Return
                Else
                    Say(Connection, Channel, "$k12$b" & Sender.Split("!")(0) & "$b rolled: $b$k1,8 X $o $b$k1,8 X $o $k12$bGot too greedy! " & Sender.Split("!")(0) & "$b loses their score this turn.")
                    Threading.Thread.Sleep(1000)
                    NextPlayer(CurrentGame, Connection, Channel)
                    Return
                End If
            ElseIf DieRoll1 = DieRoll2 Then
                TotalScore = {0, 0, 200, 200, 400, 600}(DieRoll1)
                Say(Connection, Channel, String.Format("$k12$b" & Sender.Split("!")(0) & "$b rolled: $b{0}$o $b{1}$o $k12$bDouble! " & Sender.Split("!")(0) & "$b earns $k11{2}$k2 points!",
                    {"$k1,8 X ", "$k1,8 X ", "$k8,7 O ", "$k11,10 I ", "$k12,2 ^ ", "$k9,3 * "}(DieRoll1),
                    {"$k1,8 X ", "$k1,8 X ", "$k8,7 O ", "$k11,10 I ", "$k12,2 ^ ", "$k9,3 * "}(DieRoll2),
                    TotalScore))
            Else
                TotalScore = {0, 0, 50, 50, 100, 150}(DieRoll1) + {0, 0, 50, 50, 100, 150}(DieRoll2)
                Say(Connection, Channel, String.Format("$k12$b" & Sender.Split("!")(0) & "$b rolled: $b{0}$o $b{1}$o $k12for $k11{2}$k2 points.",
                    {"$k1,8 X ", "$k1,8 X ", "$k8,7 O ", "$k11,10 I ", "$k12,2 ^ ", "$k9,3 * "}(DieRoll1),
                    {"$k1,8 X ", "$k1,8 X ", "$k8,7 O ", "$k11,10 I ", "$k12,2 ^ ", "$k9,3 * "}(DieRoll2),
                    TotalScore))
            End If
            CurrentGame.CurrentScore += TotalScore
            Threading.Thread.Sleep(1000)
            If CurrentGame.CurrentScore = TotalScore And Sender.Split("!")(0) <> VBot.Nickname(Connection) Then Say(Connection, Channel, "$k12Enter $k11$cdroll$k12 to roll again, or $k11!dpass$k12 to take your score.")
            CurrentGame.GameTimer.Interval = 30000 ' TODO: Make this configurable.
            CurrentGame.GameTimer.Start()
            CheckForAI(Sender.Split("!")(0), CurrentGame)
        End If
    End Sub

    <Regex({"\x01ACTION passes\.?\x01"})>
    Public Sub RegexPass(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As System.Text.RegularExpressions.Match)
        CommandPass(Connection, Sender, Channel, {})
    End Sub
    <Command({"dpass"}, 0, 0,
     "dpass",
     "Takes your score and passes to the next player.")>
    Public Sub CommandPass(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Threading.Thread.Sleep(600)
        Dim CurrentGame As ChannelGame = Nothing
        If Not Games.TryGetValue(If(Connection IsNot Nothing, Connection.Address & "/", "") & Channel, CurrentGame) Then
            Say(Connection, Channel, "$k4There's no game going on right now. Type $k11$cdjoin$k4 to start one.")
            Return
        ElseIf CurrentGame.IsOpen Then
            Say(Connection, Channel, "$k4This game hasn't started yet!")
            Return
        ElseIf Not CurrentGame.Players.Contains(If(Connection Is Nothing, Channel.Split("/"c)(0), Connection.Address) & "/" & Sender.Split("!"c)(0)) Then
            Say(Connection, Channel, "$k4" & Sender.Split("!")(0) & ", you're not in this game.")
        ElseIf CurrentGame.Players(CurrentGame.CurrentTurn) <> If(Connection Is Nothing, Channel.Split("/"c)(0), Connection.Address) & "/" & Sender.Split("!"c)(0) Then
            Say(Connection, Channel, "$k4" & Sender.Split("!")(0) & ", it's not your turn.")
        Else
            If CurrentGame.CurrentScore = 0 Then
                Say(Connection, Channel, "$k14You haven't rolled the dice yet!")
                Return
            End If

            ' Add the score.
            Say(Connection, Channel, "$k12$b" & Sender.Split("!")(0) & "$b passes, taking $k11" & CurrentGame.CurrentScore & "$k12 points.")
            CurrentGame.Score.SetValue(CurrentGame.Score(CurrentGame.CurrentTurn) + CurrentGame.CurrentScore, CurrentGame.CurrentTurn)

            ' Pass to the next player.
            Threading.Thread.Sleep(1000)
            NextPlayer(CurrentGame, Connection, Channel)
        End If
    End Sub

    Private Sub CheckForAI(ByVal Nickname As String, ByVal Game As ChannelGame)
        If UseAI = 0 Then Return

        If UseAI = 1 Then
            If Nickname = VBot.Nickname(Game.Connection) Then
                Dim Position As Integer = 1
                Dim MyScore As Integer = 0

                For i = 0 To Game.Players.Count - 1
                    If Not Game.Players(i).EndsWith("/" & Nickname) And Game.Score(i) > Game.CurrentScore Then Position += 1
                Next

                Threading.Thread.Sleep(667)

                If Game.CurrentScore >= 700 + 200 * Position Then
                    CommandPass(Game.Connection, Nickname, Game.Channel, {})
                Else
                    CommandRoll(Game.Connection, Nickname, Game.Channel, {})
                End If
            End If
        ElseIf UseAI = 2 Then
            For Each AI In Game.AIs.Values
                If AI.Connection.Nickname = Nickname Then
                    Dim CurrentConnection As IRCConnection = Nothing, CurrentChannel As String = Nothing, CurrentGame As ChannelGame = Nothing
                    For Each lGame In Games
                        If lGame.Value Is Game Then
                            CurrentGame = lGame.Value
                            CurrentChannel = lGame.Key
                            Exit For
                        End If
                    Next

                    If IsNothing(CurrentGame) Then Return

                    If CurrentChannel.StartsWith("!") Then
                        CurrentConnection = Nothing
                    Else
                        For Each Connection In Connections
                            If Connection.Address = CurrentChannel.Split({"/"c}, 2)(0) Then
                                CurrentConnection = Connection
                                CurrentChannel = CurrentChannel.Split({"/"c}, 2)(1)
                                Exit For
                            End If
                        Next
                    End If

                    AI.Play(CurrentChannel, Game)
                End If
            Next
        End If
    End Sub

    Private Sub AnnounceScores(ByVal Game As ChannelGame, ByVal Connection As IRCConnection, ByVal Channel As String, Optional ByVal IsFinal As Boolean = False)
        Dim TopScore As Short = 0
        For i = 0 To Game.Players.Count - 1
            TopScore = Math.Max(TopScore, Game.Score(i))
        Next

        Dim Message(Game.Players.Count - 1) As String
        For i = 0 To Game.Players.Count - 1
            Message(i) = If(i = Game.CurrentTurn And Not IsFinal, "$k11", "$k02")
            Message(i) &= Game.Players(i).Split({"/"c}, 2)(1) & "$k "
            Message(i) &= If(Game.Score(i) = 0, "$k15", If(Game.Score(i) = TopScore, "$k07", "$k03")) & Game.Score(i)
        Next

        If IsFinal Then
            Say(Connection, Channel, "$k12Final scores:  " & String.Join("$o | ", Message))
        Else
            Say(Connection, Channel, "$k12Current scores:  " & String.Join("$o | ", Message))
        End If
    End Sub

    Private Sub NextPlayer(ByVal Game As ChannelGame, ByVal Connection As IRCConnection, ByVal Channel As String)
        Game.CurrentScore = 0
        Game.CurrentTurn += 1
        If Game.Players.Count < 1 Then
            Threading.Thread.Sleep(5000)
            Say(Connection, Channel, "$k12Please say $k11$cdjoin$k12 when you're ready for another game.")
            Return
        ElseIf Game.Players.Count = 1 Then
            Say(Connection, Channel, "$k07" & Game.Players(0).Split({"/"c})(1) & "$k12 has won by default, with $k11" & Game.Score(0) & "$k12 points.")
            Threading.Thread.Sleep(3000)
            Say(Connection, Channel, "$k12Please say $k11$cdjoin$k12 when you're ready for another game.")
            Return
        ElseIf Game.CurrentTurn = Game.Players.Count Then
            Game.TurnNumber += 1
            Game.CurrentTurn = 0

            If (WinCondition And 1) Then
                If TurnsPerGame - Game.TurnNumber <= 0 Then
                    GoTo GameOver
                ElseIf TurnsPerGame - Game.TurnNumber = 1 Then
                    Say(Connection, Channel, "$k9This is the $blast turn$b! Make it count!")
                Else
                    Say(Connection, Channel, "$k11" & TurnsPerGame - Game.TurnNumber & "$k9 turns are left in this game!")
                End If
                Threading.Thread.Sleep(1000)
            End If
        End If
NextTurn:
        AnnounceScores(Game, Connection, Channel)
        Threading.Thread.Sleep(1000)
        If (Game.Players(Game.CurrentTurn).Split({"/"c}, 2)(1) <> VBot.Nickname(Game.Connection)) Then Say(Connection, Channel, "$k2$b" & Game.Players(Game.CurrentTurn).Split({"/"c}, 2)(1) & "$b, it's your turn. Enter $k11$cdroll$k2 to roll the dice.")
        Game.GameTimer.Interval = 30000 ' TODO: Make this configurable.
        Game.GameTimer.Start()
        CheckForAI(Game.Players(Game.CurrentTurn).Split({"/"c}, 2)(1), Game)
        Return
GameOver:
        EndGame(Game, Connection, Channel)
    End Sub

    Private Sub EndGame(ByVal Game As ChannelGame, ByVal Connection As IRCConnection, ByVal Channel As String)
        ' It's time to announce the results.
        Dim SortedScores As New SortedSet(Of Integer), winners As New List(Of Integer), pos As Short
        For Each i In Game.Score : SortedScores.Add(i) : Next i

        Say(Connection, Channel, "$k13The game is now $bfinished!")
        Threading.Thread.Sleep(1000)
        ' Say(Connection, Channel, "$k12The winner is...") ' That's too dramatic for a quick dice game.
        ' Threading.Thread.Sleep(4000)
        For i = 0 To UBound(Game.Players)
            If Game.Score(i) = SortedScores(SortedScores.Count - 1 - pos) Then
                winners.Add(i)
            End If
        Next
        If winners.Count = 1 Then
            Say(Connection, Channel, "$k07" & Game.Players(winners(0)).Split({"/"c})(1) & "$k12 has won the game, with $k11" & Game.Score(winners(0)) & "$k12 points!")
        ElseIf winners.Count > 1 Then
            Dim message As String
            For i = 0 To winners.Count - 1
                Select Case i
                    Case 0
                        message = "$k07" & Game.Players(winners(i)).Split({"/"c})(1) & "$k12"
                    Case winners.Count - 1
                        message = "and $k07" & Game.Players(winners(i)).Split({"/"c})(1) & "$k12"
                    Case Else
                        message = ", $k07" & Game.Players(winners(i)).Split({"/"c})(1) & "$k12"
                End Select
                Say(Connection, Channel, "$k12This game is tied between " & message & ", each with $k11" & Game.Score(winners(0)) & "$k12 points!")
            Next
        End If
        Threading.Thread.Sleep(2000)
        AnnounceScores(Game, Connection, Channel, True)
        Threading.Thread.Sleep(3000)
        Say(Connection, Channel, "$k12Well done, everyone. Please say $k11$cdjoin$k12 when you're ready for another game.")
        If Game.AIs IsNot Nothing Then
            For Each AI In Game.AIs.Values
                AI.Part()
            Next
        End If
        Games.Remove(If(Connection IsNot Nothing, Connection.Address & "/", "") & Channel)
    End Sub

    <Command({"statstest"}, 1, 1,
    "statstest <trials>",
    "Rolls the dice.", ".statstest")>
    Public Sub CommandStats(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim TotalRolls As Integer = 0, TotalPoints As Long = 0, TotalX As Integer = 0, TotalDisk = 0, TotalStick = 0, TotalArrow = 0, TotalStar = 0
        Dim Trials As Integer
        If Not Integer.TryParse(args(0), Trials) OrElse Trials < 0 Then
            Say(Connection, Channel, "That number of trials is not valid.")
            Return
        End If
        For i As Integer = 1 To Trials
            Dim DieRoll1 As Short, DieRoll2 As Short, RollScore As Integer, TotalScore As Integer = 0, RollCount As Integer = 0
            Do
                Randomize()
                DieRoll1 = Int(Rnd() * 6)
                DieRoll2 = Int(Rnd() * 6)

                If DieRoll1 < 2 And DieRoll2 < 2 Then
                    TotalX += 2
                    TotalPoints += TotalScore
                    TotalRolls += RollCount
                    Exit Do
                ElseIf DieRoll1 = DieRoll2 Then
                    RollScore = {0, 0, 200, 200, 400, 600}(DieRoll1)
                Else
                    RollScore = {0, 0, 50, 50, 100, 150}(DieRoll1) + {0, 0, 50, 50, 100, 150}(DieRoll2)
                End If
                Select Case DieRoll1
                    Case 0, 1
                        TotalX += 1
                    Case 2
                        TotalDisk += 1
                    Case 3
                        TotalStick += 1
                    Case 4
                        TotalArrow += 1
                    Case 5
                        TotalStar += 1
                End Select
                Select Case DieRoll2
                    Case 0, 1
                        TotalX += 1
                    Case 2
                        TotalDisk += 1
                    Case 3
                        TotalStick += 1
                    Case 4
                        TotalArrow += 1
                    Case 5
                        TotalStar += 1
                End Select
                TotalScore += RollScore
                RollCount += 1
            Loop
        Next
        Say(Connection, Channel, "$b$k3--- $k9Test Results $k3---")
        Say(Connection, Channel, "$k12Number of trials: $o" & Trials)
        Say(Connection, Channel, "$k12Average score accumulated before busting: $o" & Int(TotalPoints / Trials))
        Say(Connection, Channel, "$k12Average number of rolls before busting: $o" & Math.Round(TotalRolls / Trials, 2))
        Say(Connection, Channel, "$k12Total number of $b$k1,8 X $k12,99$b : $o" & TotalX)
        Say(Connection, Channel, "$k12Total number of $b$k8,7 O $k12,99$b : $o" & TotalDisk)
        Say(Connection, Channel, "$k12Total number of $b$k11,10 I $k12,99$b : $o" & TotalStick)
        Say(Connection, Channel, "$k12Total number of $b$k12,2 ^ $k12,99$b : $o" & TotalArrow)
        Say(Connection, Channel, "$k12Total number of $b$k9,3 * $k12,99$b : $o" & TotalStar)
        Say(Connection, Channel, "$b$k3--------------------------------")
    End Sub

End Class
