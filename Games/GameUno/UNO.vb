' General to-do list:
'   TODO: Improve the AI with regards to Wild Draw Fours.
'   TODO: Add 'easy' and 'hard' levels to the AI.
'   TODO: Add a notification when a player moves up in the scoreboard.

Imports VBot

Public Class UNOPlugin
    Inherits Plugin

    Enum Colour As Byte
        Red = 0
        Yellow = 1
        Green = 2
        Blue = 3
        None = 128
        Pending = 64
    End Enum
    Enum Card As Byte
        Zero = 0
        One = 1
        Two = 2
        Three = 3
        Four = 4
        Five = 5
        Six = 6
        Seven = 7
        Eight = 8
        Nine = 9
        Reverse = 10
        Skip = 11
        DrawTwo = 12
        Red = 0
        Yellow = 16
        Green = 32
        Blue = 48
        Wild = 64
        WildDrawFour = 65
        None = 255
    End Enum

    Public Class ChannelGame
        Friend Connection As IRCConnection
        Friend Channel As String

        Public IsOpen As Boolean
        Public GameTimer As Timers.Timer
        Public LongTimer As Boolean
        Public GameStartTime As Date

        Public Players As New Dictionary(Of String, Player)(StringComparer.OrdinalIgnoreCase)
        Public PlayerCount As Short
        Public PlayersOut As Short
        Public QuitPlayers As New Dictionary(Of String, Player)(StringComparer.OrdinalIgnoreCase)
        Public CurrentTurn As Short
        Public IdleTurn As Short = -1
        Public IsReversed As Boolean
        Public DrawnCard As Byte = 255
        Public DrawFourBadColour As Byte = 128
        Public DrawFourUser As String = Nothing
        Public DrawFourChallenger As String = Nothing
        Public GameEnded As Boolean = False

        Public CardsPlayed As Short

        Friend Deck As New List(Of Byte)
        Friend Discards As New Stack(Of Byte)
        Friend WildColour As Byte = Colour.None
        Public AIs As List(Of String)

        Public Sub New()
            ' Populate the deck.
            Deck.AddRange({0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10, 11, 11, 12, 12})
            Deck.AddRange({16, 17, 17, 18, 18, 19, 19, 20, 20, 21, 21, 22, 22, 23, 23, 24, 24, 25, 25, 26, 26, 27, 27, 28, 28})
            Deck.AddRange({32, 33, 33, 34, 34, 35, 35, 36, 36, 37, 37, 38, 38, 39, 39, 40, 40, 41, 41, 42, 42, 43, 43, 44, 44})
            Deck.AddRange({48, 49, 49, 50, 50, 51, 51, 52, 52, 53, 53, 54, 54, 55, 55, 56, 56, 57, 57, 58, 58, 59, 59, 60, 60})
            Deck.AddRange({64, 64, 64, 64, 65, 65, 65, 65})
        End Sub

        ''' <summary>
        ''' Returns the index of the next player.
        ''' </summary>
        ''' <remarks></remarks>
        Public Function NextPlayer(Optional ByVal Backward As Boolean = False, Optional ByVal Times As Integer = 1) As Integer
            NextPlayer = CurrentTurn

            For i As Integer = 1 To Times
                Do
                    If IsReversed Xor Backward Then
                        If NextPlayer <= 0 Then NextPlayer = Players.Count - 1 Else NextPlayer -= 1
                    Else
                        If NextPlayer >= Players.Count - 1 Then NextPlayer = 0 Else NextPlayer += 1
                    End If
                    If Players.Values(NextPlayer).Hand.Count > 0 And Not QuitPlayers.ContainsKey(Players.Keys(NextPlayer)) Then Exit Do
                Loop
            Next
        End Function

        Public Sub Advance(Optional ByVal Backward As Boolean = False, Optional ByVal Times As Integer = 1)
            CurrentTurn = NextPlayer(Backward, Times)
            DrawnCard = 255

            If IdleTurn <> -1 Then
                IdleTurn = -1
                For Each player In Players
                    player.Value.CanMove = False
                Next
            End If
        End Sub
    End Class

    Public Class Player
        Public BasePoints As Integer = 0
        Public HandPoints As Integer = 0
        Public Hand As New List(Of Byte)
        Public IdleCount As Short = 0
        Public MultipleCards As Boolean
        Public StreakMessage As String
        Public CanMove As Boolean
        Public LeftAt As Date
        Public Position As Short

        Public Sub SortHandByColour()
            SortHandByColourSub(Hand)
        End Sub

        Public Shared Sub SortHandByColourSub(ByVal List As List(Of Byte))
            ' Use the quicksort algorithm.
            If List.Count < 2 Then Return
            Dim l1 As New List(Of Byte), l2 As New List(Of Byte), Pivot As Byte = List(0)
            For i = 1 To List.Count - 1
                If List(i) <= Pivot Then l1.Add(List(i)) Else l2.Add(List(i))
            Next
            SortHandByColourSub(l1)
            SortHandByColourSub(l2)
            List.Clear()
            List.AddRange(l1)
            List.Add(Pivot)
            List.AddRange(l2)
        End Sub

        Public Sub SortHandByRank()
            SortHandByRankSub(Hand)
        End Sub

        Public Shared Sub SortHandByRankSub(ByVal List As List(Of Byte))
            ' Use the quicksort algorithm.
            If List.Count < 2 Then Return
            Dim l1 As New List(Of Byte), l2 As New List(Of Byte), Pivot As Byte = List(0)
            For i = 1 To List.Count - 1
                If (((Pivot And 64) = 64) And (List(i) <= Pivot)) Or
                   (((Pivot And 64) = 0) And ((List(i) And 64) = 0 And
                                              ((List(i) And 15) < (Pivot And 15) Or ((List(i) And 15) = (Pivot And 15) And List(i) <= Pivot)))) Then l1.Add(List(i)) Else l2.Add(List(i))
            Next
            SortHandByRankSub(l1)
            SortHandByRankSub(l2)
            List.Clear()
            List.AddRange(l1)
            List.Add(Pivot)
            List.AddRange(l2)
        End Sub
    End Class

    Public Class PlayerSettings
        Public AutoSort As Short = 1
        Public Highlight As Short = 0
    End Class

    ''' <summary>Contains information about extended stats of an individual player in UNO.</summary>
    Public Class PlayerStats
        ''' <summary>The player's name.</summary>
        Public Name As String
        ''' <summary>The number of points the player has won overall.</summary>
        Public Points As ULong
        ''' <summary>The player's rank.</summary>
        Public Rank As UShort
        ''' <summary>The number of games the player has entered.</summary>
        Public Plays As UInteger
        ''' <summary>The number of games the player has won (gone out first).</summary>
        Public Wins As UInteger
        ''' <summary>The number of games the player has lost (taken no cards).</summary>
        Public Losses As UInteger
        ''' <summary>The highest number of points this player has taken in a single game.</summary>
        Public RecordPoints As UInteger
        ''' <summary>The total number of turns this player has had.</summary>
        Public TurnsPlayed As UInteger
        ''' <summary>The total number of 1/10 seconds this player has taken for their turns. This will be used to calculate their average turn time.</summary>
        Public TotalTime As ULong
        ''' <summary>The player's score under a new system, whereby all points taken from them are deducted.</summary>
        Public ChallengePoints As Long

        Public Function AverageTimePerPlay() As Decimal
            Return (CDec(TotalTime) / 10) / TurnsPlayed
        End Function

        Public Function WinRate() As Decimal
            If Plays = 0 Then Return 0
            Return CDec(Wins) / Plays
        End Function

        Public Function LossRate() As Decimal
            If Plays = 0 Then Return 0
            Return CDec(Losses) / Plays
        End Function
    End Class

    Public Games As New Dictionary(Of String, ChannelGame)(StringComparer.OrdinalIgnoreCase)
    Public Players As New Dictionary(Of String, PlayerSettings)(StringComparer.OrdinalIgnoreCase)
    'Public Scores As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
    'Public LastScores As Dictionary(Of String, Integer)
    Public CurrentStats As Dictionary(Of String, PlayerStats)
    Public CurrentStreak As Dictionary(Of String, Short)
    Public BestStreak As Dictionary(Of String, Short)
    Public LastStats As Dictionary(Of String, PlayerStats)
    Public AllTimeStats As Dictionary(Of String, PlayerStats)
    Public ScoresPeriodEnd As Date
    Public WithEvents ScoreResetTimer As New Timers.Timer With {.Interval = 3600000, .Enabled = False} ' This will trigger once every hour. The event procedure will check if it's time to reset yet.

    Public AIEnabled As Boolean = True
    Public AllOut As Boolean = False
    Public EntryPeriod As Integer = 30
    Public TurnTimeLimit As Integer = 90
    Public WildDrawFour As Short = 1
    Public ShowHandOnChallenge As Boolean = True

    Public VictoryBonus As Boolean = True
    Public VictoryBonusValue As Integer() = {30, 10, 5}
    Public VictoryBonusLastPlace As Boolean = False
    Public VictoryBonusRepeat = False
    Public HandBonus As Boolean = True
    Public ParticipationBonus As Integer = 0
    Public QuitPenalty As Integer = 0

    Public Overrides ReadOnly Property Name As String
        Get
            Return "UNO game"
        End Get
    End Property

    Public Overrides Function Help(ByVal Topic As String, ByVal IsMajorChannel As Boolean) As String
        Select Case If(Topic, "").ToLower
            Case "uno"
                Return "The goal in UNO is to play all of your cards before your rivals do." & vbCrLf &
                       "When it's your turn, play a card of the same colour or with the same number or action as the last card played." & vbCrLf &
                       "For example, after a Red 9, you may play a Red 4, or a Yellow 9." & vbCrLf &
                       "To play a card, use $k11$cplay $k10<the name of the card you want to play>$o." & vbCrLf &
                       "If you don't have any cards that you can play, you must $k11$cdraw$o a card from the deck, then play that card or $k11$cpass$o." & vbCrLf &
                       "See also:  $k11$chelp $k10UNO-commands  $k11$chelp $k10UNO-cards  $k11$chelp $k10UNO-scoring"
            Case "uno-commands"
                Return "These are the commands that you can use in this game:" & vbCrLf &
                       "$k11$cplay $k10<card>$o    Lets you play a card from your hand." & vbCrLf &
                       "For example, you may enter $k11yellow 5$o, $k11y5$o, $k11yellow Skip$o, $k11ys$o (Skip), $k11yr$o (Reverse), $k11yd$o (Draw Two), $k11w$o (wild), $k11wd$o (Wild Draw Four) among others." & vbCrLf &
                       "$k11$cdraw$o    If you can't play, use this command to draw a card from the deck." & vbCrLf &
                       "$k11$cpass$o    If you draw and can't play the card, use this command to end your turn." & vbCrLf &
                       "$k11$ccolour $k10<colour>$o    Chooses a colour for your Wild card." & vbCrLf &
                       "If you need to leave before the game ends, use $k11$cuquit$o." & vbCrLf &
                       "If you lose track of the game: $k11$cupcard  $cturn  $chand  $ccount" & vbCrLf &
                       "Also, if you're familiar with Marky's UNO bot, most of those commands work here too."
            Case "uno-cards"
                Return "Some of the cards in UNO have special effects when they are played." & vbCrLf &
                       "$bReverse$b: Reverses the turn order." & vbCrLf &
                       "$bSkip$b: The next player is 'skipped' and loses a turn." & vbCrLf &
                       "$bDraw Two$b: The next player must draw two cards and lose a turn." & vbCrLf &
                       "$bWild$b: You can play this on top of any card. This lets you choose what colour you want the Wild card to be. The next player must play that colour or another Wild card." & vbCrLf &
                       "$bWild Draw Four$b: This is the best card to have. Not only is it a wild card, it also forces the player after you to draw $bfour$b cards and lose a turn, a powerful blow." & vbCrLf &
                       "    There's a catch, though: you can't play this if you have a card of the same colour as the last card played. It also can't show up as the initial up-card. See $k11$chelp UNO-DrawFour$o for more info." & vbCrLf &
                       "Be sure not to let someone else win while you have some of these cards, as they're worth a lot of points."
            Case "uno-scoring"
                Return "The round ends when someone 'goes out' by playing their last card. That player wins points from the cards everyone else is holding:" & vbCrLf &
                       "$bAny number card (0-9)$b is worth that number of points." & vbCrLf &
                       "$bReverse, skip and draw two cards$b are worth $b20$b points." & vbCrLf &
                       "$bWild and Wild Draw Four cards$b are worth $b50$b points." & vbCrLf &
                       "In short, if the other players have a lot of cards left, especially if they're action cards, you win a lot of points."
            Case "uno-wilddrawfour", "uno-wilddraw", "uno-wilddraw4", "uno-drawfour", "uno-draw4", "uno-wd", "uno-wd4", "uno-wdf"
                Select Case WildDrawFour
                    Case 0
                        Return "It's against the rules to play a Wild Draw Four if you have another card of a matching colour." & vbCrLf &
                               "Bluffing is not enabled here."
                    Case 1
                        Return "It's against the rules to play a Wild Draw Four if you have another card of a matching colour." & vbCrLf &
                               "However, you can 'bluff' and play one anyway." & vbCrLf &
                               "If you think a Wild Draw Four has been played on you illegally, you may challenge it by entering $k11$cchallenge$o." & vbCrLf &
                               "If your challenge is correct, the person who played the Draw Four must take the four cards instead of you." & vbCrLf &
                               "But if you're wrong, you have to take two extra cards on top of the four."
                    Case 2
                        Return "This game is set so that you can play a Wild Draw Four, regardless of what else you hold."
                End Select
            Case Else
                Return "The $bUNO card game$b is hosted in this channel." & vbCrLf &
                       "Say $k11$cujoin$o to start a game, or to join a game that someone else starts." & vbCrLf &
                       "For more information about the game, say $k11$chelp UNO$o."
        End Select

    End Function

    <Command({"uhelp", "unohelp", "unocmds"}, 0, 0,
    "uhelp",
    "Shows basic information on how to play UNO.")>
    Public Sub CommandHelp(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        For Each Line In Help("UNO", True).Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries)
            Threading.Thread.Sleep(700)
            Reply(Connection, Channel, Sender, Line)
        Next
    End Sub

    Public Sub New(ByVal Key As String)
        LoadSettings(Key)
        LoadStats(Key)
    End Sub

    Public Overrides Sub OnUnload()
        For Each Game In Games
            Game.Value.GameTimer.Stop()
        Next
        Games.Clear()

        MyBase.OnUnload()
    End Sub

    Public Overrides Sub OnChannelMessage(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Message As String)
        If Players.ContainsKey(Sender.Split("!"c)(0)) Then
            If Players(Sender.Split("!"c)(0)).Highlight = 1 Then
                Reply(Connection, Channel, Sender, ChrW(2) & Sender.Split("!"c)(0) & "$b, your UNO game alerts were disabled because you stopped playing.")
                Reply(Connection, Channel, Sender, "You can enable them permanently with $k11$cuset highlight permanent$o.")
                Players(Sender.Split("!"c)(0)).Highlight = 0
            ElseIf Players(Sender.Split("!"c)(0)).Highlight = 2 Then
                Reply(Connection, Channel, Sender, ChrW(2) & Sender.Split("!"c)(0) & "$b, your UNO game alerts were disabled because you left the channel.")
                Reply(Connection, Channel, Sender, "You can enable them permanently with $k11$cuset highlight permanent$o.")
                Players(Sender.Split("!"c)(0)).Highlight = 0
            End If
        End If

        MyBase.OnChannelMessage(Connection, Sender, Channel, Message)
    End Sub

#Region "Filing"
    Public Sub LoadSettings(ByVal Key As String)
        If My.Computer.FileSystem.FileExists("Config\" & Key & ".ini") Then
            Dim Reader = My.Computer.FileSystem.OpenTextFileReader("Config\" & Key & ".ini"), s As String, Section As String = "", Settings As PlayerSettings
            Do Until Reader.EndOfStream
                s = Reader.ReadLine

                Dim Match As System.Text.RegularExpressions.Match
                Match = System.Text.RegularExpressions.Regex.Match(s, "^\s*\[(?<Section>.*?)\]?\s*$")
                If Match.Success Then
                    Section = Match.Groups("Section").Value
                    If Section.ToLower.StartsWith("player:") Then
                        Settings = New PlayerSettings
                        Players.Add(Section.Substring(7), Settings)
                    ElseIf Section.ToLower <> "game" And Section.ToLower <> "rules" And Section.ToLower <> "scoring" Then
                        Settings = New PlayerSettings
                        Players.Add(Section, Settings)
                    End If
                    Continue Do
                End If

                Match = System.Text.RegularExpressions.Regex.Match(s, "^\s*(?<Property>(?>[^=]*))=(?<Value>.*)$")
                If Match.Success Then
                    Dim Identifier = Match.Groups("Property").Value
                    Dim Value = Match.Groups("Value").Value

                    If Section.ToLower = "game" Then
                        Select Case Identifier.ToLower
                            Case "ai"
                                Select Case Value.ToLower
                                    Case "true", "on", "yes", "enabled"
                                        AIEnabled = True
                                    Case "false", "off", "no", "disabled"
                                        AIEnabled = False
                                    Case Else
                                        Throw New ArgumentException("The AI setting is not a valid Boolean value (on or off).")
                                End Select
                            Case "entrytime"
                                Dim nValue As Integer
                                If Integer.TryParse(Value, nValue) Then
                                    If nValue <= 0 Then
                                        Throw New ArgumentException("The EntryTime setting is non-positive, which is not valid.")
                                    Else
                                        EntryPeriod = nValue
                                    End If
                                Else
                                    Throw New ArgumentException("The EntryTime setting is not a valid integer.")
                                End If
                            Case "turntime"
                                Dim nValue As Integer
                                If Integer.TryParse(Value, nValue) Then
                                    If nValue < 0 Then
                                        Throw New ArgumentException("The TurnTime setting is negative, which is not valid.")
                                    Else
                                        TurnTimeLimit = nValue
                                    End If
                                Else
                                    Throw New ArgumentException("The TurnTime setting is not a valid integer.")
                                End If
                        End Select
                    ElseIf Section.ToLower = "scoring" Then
                        Select Case Identifier.ToLower
                            Case "victorybonus", "winbonus", "scorevictory", "scorewin", "victorypoints", "winpoints", "pointsforwin"
                                Select Case Value.ToLower
                                    Case "true", "on", "yes", "enabled"
                                        VictoryBonus = True
                                    Case "false", "off", "no", "disabled"
                                        VictoryBonus = False
                                    Case Else
                                        Throw New ArgumentException("The VictoryBonus setting is not a valid Boolean value (on or off).")
                                End Select
                            Case "victorybonuslastplace", "winbonuslastplace", "victorybonuslast", "winbonuslast"
                                Select Case Value.ToLower
                                    Case "true", "on", "yes", "enabled"
                                        VictoryBonusLastPlace = True
                                    Case "false", "off", "no", "disabled"
                                        VictoryBonusLastPlace = False
                                    Case Else
                                        Throw New ArgumentException("The VictoryBonusLastPlace setting is not a valid Boolean value (on or off).")
                                End Select
                            Case "victorybonusrepeat", "winbonusrepeat"
                                Select Case Value.ToLower
                                    Case "true", "on", "yes", "enabled"
                                        VictoryBonusRepeat = True
                                    Case "false", "off", "no", "disabled"
                                        VictoryBonusRepeat = False
                                    Case Else
                                        Throw New ArgumentException("The VictoryBonusRepeat setting is not a valid Boolean value (on or off).")
                                End Select
                            Case "handbonus", "cardbonus", "scorehand", "scorecards", "cardpoints", "handpoints", "pointsforhand", "pointsforcards"
                                Select Case Value.ToLower
                                    Case "true", "on", "yes", "enabled"
                                        HandBonus = True
                                    Case "false", "off", "no", "disabled"
                                        HandBonus = False
                                    Case Else
                                        Throw New ArgumentException("The VictoryBonus setting is not a valid Boolean value (on or off).")
                                End Select
                            Case "participationbonus", "playbonus", "scoreparticipation", "scoreplay", "participationpoints", "playpoints", "pointsforparticipation", "pointsforplay"
                                If Not Integer.TryParse(Value, ParticipationBonus) Then Throw New ArgumentException("The ParticipationBonus setting is not a valid integer.")
                            Case "quitpenalty"
                                If Not Integer.TryParse(Value, QuitPenalty) Then Throw New ArgumentException("The QuitPenalty setting is not a valid integer.")
                            Case "victorybonusvalue", "victorybonuspoints", "winbonusvalue", "winbonuspoints"
                                Dim nValue As New List(Of Integer)
                                For Each s In Value.Split({","c, ";"c, " "c}, StringSplitOptions.RemoveEmptyEntries)
                                    Dim nValue2 As Integer
                                    If Integer.TryParse(s, nValue2) Then
                                        nValue.Add(nValue2)
                                    Else
                                        Throw New ArgumentException("The VictoryBonusValue setting contains invalid integers.")
                                    End If
                                Next
                                If nValue.Count = 0 Then Throw New ArgumentException("The VictoryBonusValue setting cannot be empty.")
                                VictoryBonusValue = nValue.ToArray()
                        End Select
                    ElseIf Section.ToLower = "rules" Then
                        Select Case Identifier.ToLower
                            Case "allout"
                                Select Case Value.ToLower
                                    Case "true", "on", "yes", "enabled"
                                        AllOut = True
                                    Case "false", "off", "no", "disabled"
                                        AllOut = False
                                    Case Else
                                        Throw New ArgumentException("The AllOut setting is not a valid Boolean value (on or off).")
                                End Select
                            Case "wilddrawfour", "drawfour", "wilddraw", "wilddraw4", "draw4", "wd", "wdf", "wd4"
                                Select Case Value.ToLower
                                    Case "bluffoff", "bluffingoff", "bluffdisabled", "bluffingdisabled", "nobluff", "nobluffing", "original", "strict"
                                        WildDrawFour = 0
                                    Case "bluffon", "bluffingon", "bluffenabled", "bluffingenabled", "bluff", "bluffing", "normal"
                                        WildDrawFour = 1
                                    Case "free", "alwaysallow", "alwaysallowed", "alwayslegal", "alwayson"
                                        WildDrawFour = 2
                                    Case Else
                                        Throw New ArgumentException("The WildDrawFour setting is not valid (BluffOff, BluffOn or Free).")
                                End Select
                            Case "showhandonchallenge"
                                Select Case Value.ToLower
                                    Case "true", "on", "yes", "enabled"
                                        ShowHandOnChallenge = True
                                    Case "false", "off", "no", "disabled"
                                        ShowHandOnChallenge = False
                                    Case Else
                                        Throw New ArgumentException("The ShowHandOnChallenge setting is not a valid Boolean value (on or off).")
                                End Select
                        End Select
                    Else
                        Select Case Identifier.ToLower
                            Case "highlight", "ping", "alert", "highlights", "pings", "alerts", "gamealert", "gamealerts"
                                Select Case Value.ToLower
                                    Case "0", "off", "disable", "disabled", "no", "false"
                                        Settings.Highlight = 0
                                    Case "1", "4", "on", "temporary", "temp", "enable", "enabled", "yes", "true"
                                        Settings.Highlight = 4
                                    Case "6", "always", "permanent", "perm"
                                        Settings.Highlight = 6
                                    Case Else
                                        Throw New ArgumentException(Section & "'s Highlight setting is not a valid value (on, off, always).")
                                End Select
                            Case "autosort", "sort", "arrange", "autoarrange", "sorthand", "arrangehand"
                                Select Case Value.ToLower
                                    Case "0", "off", "disable", "disabled", "no", "false"
                                        Settings.AutoSort = 0
                                    Case "1", "on", "enable", "enabled", "yes", "true", "colour", "color", "bycolour", "bycolor"
                                        Settings.AutoSort = 1
                                    Case "2", "rank", "number", "byrank", "bynumber"
                                        Settings.AutoSort = 2
                                    Case Else
                                        Throw New ArgumentException(Section & "'s AutoSort setting is not a valid Boolean value (off, colour, rank).")
                                End Select
                        End Select
                    End If
                    Continue Do
                End If
            Loop
            Reader.Close()
        End If
    End Sub

    Public Sub LoadStats(ByVal Key As String)
        If My.Computer.FileSystem.FileExists(Key & "-stats.dat") Then
            Dim Reader As New System.IO.BinaryReader(System.IO.File.Open(Key & "-stats.dat", IO.FileMode.Open, IO.FileAccess.Read))
            Try
                Dim Version = Reader.ReadUInt16

                If Version = 2 Then LoadStats2(Reader) ' 1.3 format
                If Version = 3 Then LoadStats3(Reader) ' 1.4 format

            Catch ex As Exception
                LogError("Loading stats", ex)
            Finally
                Reader.Close()
            End Try
        ElseIf My.Computer.FileSystem.FileExists(Key & "-scores.dat") Then
            LoadOldScores(Key)
        Else
            CurrentStats = New Dictionary(Of String, PlayerStats)(StringComparer.OrdinalIgnoreCase)
            LastStats = New Dictionary(Of String, PlayerStats)(StringComparer.OrdinalIgnoreCase)
            AllTimeStats = New Dictionary(Of String, PlayerStats)(StringComparer.OrdinalIgnoreCase)
            CurrentStreak = New Dictionary(Of String, Short)(StringComparer.OrdinalIgnoreCase)
            BestStreak = New Dictionary(Of String, Short)(StringComparer.OrdinalIgnoreCase)
        End If

        If Not ScoreResetTimer.Enabled And ScoresPeriodEnd <> Nothing Then
            ScoreResetTimer.Enabled = True
            ScoresResetTimer_Elapsed(Nothing, Nothing)
        End If
    End Sub

    Public Sub LoadStats2(ByVal Reader As System.IO.BinaryReader)
        Dim count As UShort

        ' Read current period statistics
        CurrentStats = New Dictionary(Of String, PlayerStats)(StringComparer.OrdinalIgnoreCase)
        count = Reader.ReadUInt16

        For i = 1 To count
            Dim Player As New PlayerStats
            Player.Name = Reader.ReadString
            Player.Points = Reader.ReadUInt64
            Player.Plays = Reader.ReadUInt32
            Player.Wins = Reader.ReadUInt32
            Player.Losses = Reader.ReadUInt32
            Player.RecordPoints = Reader.ReadUInt32
            Player.TurnsPlayed = Reader.ReadUInt32
            Player.TotalTime = Reader.ReadUInt64
            Player.ChallengePoints = Reader.ReadInt64
            CurrentStats.Add(Player.Name, Player)
        Next

        ' Read last period statistics
        LastStats = New Dictionary(Of String, PlayerStats)(StringComparer.OrdinalIgnoreCase)
        count = Reader.ReadUInt16

        For i = 1 To count
            Dim Player As New PlayerStats
            Player.Name = Reader.ReadString
            Player.Points = Reader.ReadUInt64
            Player.Plays = Reader.ReadUInt32
            Player.Wins = Reader.ReadUInt32
            Player.Losses = Reader.ReadUInt32
            Player.RecordPoints = Reader.ReadUInt32
            Player.TurnsPlayed = Reader.ReadUInt32
            Player.TotalTime = Reader.ReadUInt64
            Player.ChallengePoints = Reader.ReadInt64
            LastStats.Add(Player.Name, Player)
        Next

        ' Read all time statistics
        AllTimeStats = New Dictionary(Of String, PlayerStats)(StringComparer.OrdinalIgnoreCase)
        count = Reader.ReadUInt16

        For i = 1 To count
            Dim Player As New PlayerStats
            Player.Name = Reader.ReadString
            Player.Points = Reader.ReadUInt64
            Player.Plays = Reader.ReadUInt32
            Player.Wins = Reader.ReadUInt32
            Player.Losses = Reader.ReadUInt32
            Player.RecordPoints = Reader.ReadUInt32
            Player.TurnsPlayed = Reader.ReadUInt32
            Player.TotalTime = Reader.ReadUInt64
            Player.ChallengePoints = Reader.ReadInt64
            AllTimeStats.Add(Player.Name, Player)
        Next

        ' Read period time.
        If CurrentStats.Count > 0 Then
            ScoresPeriodEnd = Date.FromBinary(Reader.ReadInt64)
            ScoreResetTimer.Interval = 60000
            ScoreResetTimer.Enabled = True
        Else
            Reader.ReadInt64()
        End If

        CurrentStreak = New Dictionary(Of String, Short)(StringComparer.OrdinalIgnoreCase)
        BestStreak = New Dictionary(Of String, Short)(StringComparer.OrdinalIgnoreCase)
    End Sub
    Public Sub LoadStats3(ByVal Reader As System.IO.BinaryReader)
        Dim count As UShort
        LoadStats2(Reader)

        ' Read the streak data.
        count = Reader.ReadUInt16
        For i = 1 To count
            Dim Nickname = Reader.ReadString
            Dim Value = Reader.ReadInt16
            If Value <> 0 Then CurrentStreak.Add(Nickname, Value)
        Next

        count = Reader.ReadUInt16
        For i = 1 To count
            Dim Nickname = Reader.ReadString
            Dim Value = Reader.ReadInt16
            If Value <> 0 Then BestStreak.Add(Nickname, Value)
        Next

    End Sub

    Public Sub LoadOldScores(ByVal Key As String)
        If My.Computer.FileSystem.FileExists(Key & "-scores.dat") Then
            Dim Reader As New System.IO.BinaryReader(System.IO.File.Open(Key & "-scores.dat", IO.FileMode.Open, IO.FileAccess.Read))
            Try
                Dim NumberOfEntries As Short = Reader.ReadInt16
                CurrentStats = New Dictionary(Of String, PlayerStats)(StringComparer.OrdinalIgnoreCase)
                For i = 1 To NumberOfEntries
                    Dim Name As String, Score As Integer
                    Name = Reader.ReadString
                    Score = Reader.ReadInt32

                    CurrentStats.Add(Name, New PlayerStats With {.Name = Name, .Points = Score})
                Next

                NumberOfEntries = Reader.ReadInt16
                LastStats = New Dictionary(Of String, PlayerStats)(StringComparer.OrdinalIgnoreCase)
                For i = 1 To NumberOfEntries
                    Dim Name As String, Score As Integer
                    Name = Reader.ReadString
                    Score = Reader.ReadInt32

                    LastStats.Add(Name, New PlayerStats With {.Name = Name, .Points = Score})
                Next

                AllTimeStats = New Dictionary(Of String, PlayerStats)(StringComparer.OrdinalIgnoreCase)

                If CurrentStats.Count > 0 Then
                    ScoresPeriodEnd = Date.FromBinary(Reader.ReadInt64)
                    ScoreResetTimer.Interval = 60000
                    ScoreResetTimer.Enabled = True
                End If
            Catch ex As Exception
                LogError("Loading scores", ex)
            Finally
                Reader.Close()
            End Try
        End If

        If Not ScoreResetTimer.Enabled And ScoresPeriodEnd <> Nothing Then
            ScoreResetTimer.Enabled = True
            ScoresResetTimer_Elapsed(Nothing, Nothing)
        End If
    End Sub

    Public Sub SaveSettings()
        If Not My.Computer.FileSystem.DirectoryExists("Config") Then My.Computer.FileSystem.CreateDirectory("Config")
        Dim writer = My.Computer.FileSystem.OpenTextFileWriter("Config\" & MyKey & ".ini", False)
        writer.WriteLine("[Game]")
        writer.WriteLine("AI=" & If(AIEnabled, "on", "off"))
        writer.WriteLine("EntryTime=" & EntryPeriod)
        writer.WriteLine("TurnTime=" & TurnTimeLimit)
        writer.WriteLine()
        writer.WriteLine("[Rules]")
        writer.WriteLine("AllOut=" & If(AllOut, "on", "off"))
        writer.WriteLine("WildDrawFour=" & {"BluffOff", "BluffOn", "Free"}(WildDrawFour))
        writer.WriteLine("ShowHandOnChallenge=" & If(ShowHandOnChallenge, "yes", "no"))
        writer.WriteLine()
        writer.WriteLine("[Scoring]")
        writer.WriteLine("VictoryBonus=" & If(VictoryBonus, "on", "off"))
        writer.WriteLine("VictoryBonusValue=" & String.Join(",", VictoryBonusValue))
        writer.WriteLine("VictoryBonusLastPlace=" & If(VictoryBonusLastPlace, "on", "off"))
        writer.WriteLine("VictoryBonusRepeat=" & If(VictoryBonusRepeat, "on", "off"))
        writer.WriteLine("HandBonus=" & If(HandBonus, "on", "off"))
        writer.WriteLine("ParticipationBonus=" & ParticipationBonus)
        writer.WriteLine("QuitPenalty=" & QuitPenalty)

        For Each Player In Players
            If Player.Value.Highlight = 0 And Player.Value.AutoSort = 1 Then Continue For
            writer.WriteLine()
            writer.WriteLine("[Player:" & Player.Key & "]")
            Select Case Player.Value.Highlight
                Case 0 To 2 : writer.WriteLine("Highlight=No")
                Case 4 To 5 : writer.WriteLine("Highlight=Yes")
                Case 6 : writer.WriteLine("Highlight=Always")
            End Select
            Select Case Player.Value.AutoSort
                Case 0 : writer.WriteLine("AutoSort=No")
                Case 1 : writer.WriteLine("AutoSort=Colour")
                Case 2 : writer.WriteLine("AutoSort=Rank")
            End Select
        Next
        writer.Close()
    End Sub

    Public Sub SaveStats()
        Try
            Dim sWriter As New System.IO.BinaryWriter(System.IO.File.Open(MyKey & "-stats.dat", IO.FileMode.Create, IO.FileAccess.Write))
            ' Write the version number.
            sWriter.Write(CUShort(3))

            ' Write current period statistics.
            If CurrentStats Is Nothing Then
                sWriter.Write(CUShort(0))
            Else
                sWriter.Write(CUShort(CurrentStats.Count))
                For Each Player In CurrentStats
                    sWriter.Write(Player.Value.Name)
                    sWriter.Write(Player.Value.Points)
                    sWriter.Write(Player.Value.Plays)
                    sWriter.Write(Player.Value.Wins)
                    sWriter.Write(Player.Value.Losses)
                    sWriter.Write(Player.Value.RecordPoints)
                    sWriter.Write(Player.Value.TurnsPlayed)
                    sWriter.Write(Player.Value.TotalTime)
                    sWriter.Write(Player.Value.ChallengePoints)
                Next
            End If

            ' Write last period statistics.
            If LastStats Is Nothing Then
                sWriter.Write(CUShort(0))
            Else
                sWriter.Write(CUShort(LastStats.Count))
                For Each Player In LastStats
                    sWriter.Write(Player.Value.Name)
                    sWriter.Write(Player.Value.Points)
                    sWriter.Write(Player.Value.Plays)
                    sWriter.Write(Player.Value.Wins)
                    sWriter.Write(Player.Value.Losses)
                    sWriter.Write(Player.Value.RecordPoints)
                    sWriter.Write(Player.Value.TurnsPlayed)
                    sWriter.Write(Player.Value.TotalTime)
                    sWriter.Write(Player.Value.ChallengePoints)
                Next
            End If

            ' Write all-time statistics.
            If AllTimeStats Is Nothing Then
                sWriter.Write(CUShort(0))
            Else
                sWriter.Write(CUShort(AllTimeStats.Count))
                For Each Player In AllTimeStats
                    sWriter.Write(Player.Value.Name)
                    sWriter.Write(Player.Value.Points)
                    sWriter.Write(Player.Value.Plays)
                    sWriter.Write(Player.Value.Wins)
                    sWriter.Write(Player.Value.Losses)
                    sWriter.Write(Player.Value.RecordPoints)
                    sWriter.Write(Player.Value.TurnsPlayed)
                    sWriter.Write(Player.Value.TotalTime)
                    sWriter.Write(Player.Value.ChallengePoints)
                Next
            End If

            If ScoresPeriodEnd = Nothing Then sWriter.Write(0L) Else sWriter.Write(ScoresPeriodEnd.ToBinary)

            sWriter.Write(CUShort(CurrentStreak.Count))
            For Each Player In CurrentStreak
                sWriter.Write(Player.Key)
                sWriter.Write(Player.Value)
            Next

            sWriter.Write(CUShort(BestStreak.Count))
            For Each Player In BestStreak
                sWriter.Write(Player.Key)
                sWriter.Write(Player.Value)
            Next

            sWriter.Close()
        Catch ex As Exception
            LogError("Saving statistics", ex)
        End Try
    End Sub

#End Region

    Public Overrides Sub OnSave()
        SaveSettings()
        SaveStats()
    End Sub

    <Command({"uset", "uconfig", "uproperty"}, 1, 2,
    "set <property> <value>",
    "Changes plugin settings." & vbCrLf &
    "You can set the following properties: $k11aienable$o, $k11outlimit$o, $k11entryperiod$o, $11turntimelimit$o." & vbCrLf &
    "Alternatively, you can omit the $k11value$o parameter to just check a property's value.",
     Nothing, CommandAttribute.CommandScope.Channel)>
    Public Sub CommandSet(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim lProperty = args(0)
        Dim lValue = args.ElementAtOrDefault(1)

        Select Case lProperty.ToLower.Replace("_", "").Replace("-", "")
            Case "aienable", "ai", "comenable", "com"
                If Not UserHasPermission(Connection, Channel, Sender, MyKey & ".set") Then : Reply(Connection, Channel, Sender, "You don't have access to that setting.") : Return : End If
                If lValue = Nothing Then
                    If AIEnabled Then
                        Say(Connection, Channel, Choose("I $k9will$o join UNO games.", "The $bAI$b is $k9enabled$o."))
                    Else
                        Say(Connection, Channel, Choose("I $k4will not$o join UNO games.", "The $bAI$b is $k4dibabled$o."))
                    End If
                Else
                    If {"1", "-1", "on", "yes", "true", "+", "enable", "enabled", "activate", "activated", "active"}.Contains(lValue.ToLower) Then
                        AIEnabled = True
                        Say(Connection, Channel, Choose("I $k9will$o join UNO games.", "The $bAI$b is $k9enabled$o."))
                    ElseIf {"0", "off", "no", "false", "-", "disable", "disabled", "deactivate", "deactivated", "inactive"}.Contains(lValue.ToLower) Then
                        AIEnabled = False
                        Say(Connection, Channel, Choose("I will $k4no longer$o join UNO games.", "The $bAI$b is now $k4disabled$o."))
                    Else
                        Reply(Connection, Channel, Sender, VBot.Choose("", "Sorry " & Sender.Split("!"c)(0) & ", ") & "I don't recognise " & VBot.Choose(IRCColours.Red & lValue & "$o", "that") & " as a Boolean value. Please enter $k11on$o or $k11off$o.")
                    End If
                End If
            Case "outlimit", "endgame", "gameend", "allout", "everyout", "everyoneout", "everybodyout"
                If Not UserHasPermission(Connection, Channel, Sender, MyKey & ".set") Then : Reply(Connection, Channel, Sender, "You don't have access to that setting.") : Return : End If
                If lValue = Nothing Then
                    If AllOut Then Say(Connection, Channel, Choose("The $ball-out rule$b is $k9enabled$o.", "The game will end when $k9only one player$o remains.")) _
                        Else Say(Connection, Channel, Choose("The $ball-out rule$b is $k4disabled$o.", "The game will end when $k4one player$o goes out."))
                Else
                    Select Case lValue.ToLower
                        Case "one", "oneout", "no", "false", "disable", "disabled", "off"
                            AllOut = False
                            Say(Connection, Channel, Choose("The $ball-out rule$b is now $k4disabled$o.", "The game will now end when $k4one player$o goes out."))
                        Case "all", "allout", "yes", "true", "enable", "enabled", "on"
                            AllOut = True
                            Say(Connection, Channel, Choose("The $ball-out rule$b is now $k9enabled$o.", "The game will now end when $k9only one player$o remains."))
                        Case Else
                            Reply(Connection, Channel, Sender, Choose("That isn't a valid setting. Please use $k11one$o or $k11all$o."))
                    End Select
                End If
            Case "wilddrawfour", "drawfour", "wilddraw", "wilddraw4", "draw4", "wd", "wdf", "wd4"
                If Not UserHasPermission(Connection, Channel, Sender, MyKey & ".set") Then : Reply(Connection, Channel, Sender, "You don't have access to that setting.") : Return : End If
                If lValue = Nothing Then
                    Select Case WildDrawFour
                        Case 0
                            Say(Connection, Channel, "$bWild Draw Four bluffing$b is $k4disabled$o.")
                        Case 1
                            Say(Connection, Channel, "$bWild Draw Four bluffing$b is $k9enabled$o.")
                        Case 2
                            Say(Connection, Channel, "$bWild Draw Four$b is $k12freely playable$o.")
                    End Select
                Else
                    Select Case lValue.ToLower.Replace(" ", "")
                        Case "0", "bluffoff", "bluffingoff", "bluffdisabled", "bluffingdisabled", "nobluff", "nobluffing", "original", "strict"
                            WildDrawFour = 0
                            Say(Connection, Channel, "$bWild Draw Four bluffing$b is now $k4disabled$o.")
                        Case "1", "bluffon", "bluffingon", "bluffenabled", "bluffingenabled", "bluff", "bluffing", "normal"
                            WildDrawFour = 1
                            Say(Connection, Channel, "$bWild Draw Four bluffing$b is now $k9enabled$o.")
                        Case "2", "free", "alwaysallow", "alwaysallowed", "alwayslegal", "alwayson"
                            WildDrawFour = 2
                            Say(Connection, Channel, "$bWild Draw Four$b is now $k12freely playable$o.")
                        Case Else
                            Reply(Connection, Channel, Sender, Choose("That isn't a valid setting. Please use $k11bluff off$o, $k11bluff on$o or $k11free$o."))
                    End Select
                End If
            Case "showhandonchallenge", "showcardsonchallenge"
                If Not UserHasPermission(Connection, Channel, Sender, MyKey & ".set") Then : Reply(Connection, Channel, Sender, "You don't have access to that setting.") : Return : End If
                If lValue = Nothing Then
                    If ShowHandOnChallenge Then
                        Say(Connection, Channel, "Cards $k9must$o be shown for a Wild Draw Four challenge.")
                    Else
                        Say(Connection, Channel, "Cards $k4need not$o be shown for a Wild Draw Four challenge.")
                    End If
                Else
                    If {"1", "-1", "on", "yes", "true", "+", "enable", "enabled", "activate", "activated", "active"}.Contains(lValue.ToLower) Then
                        ShowHandOnChallenge = True
                        Say(Connection, Channel, "Cards now $k9must$o be shown for a Wild Draw Four challenge.")
                    ElseIf {"0", "off", "no", "false", "-", "disable", "disabled", "deactivate", "deactivated", "inactive"}.Contains(lValue.ToLower) Then
                        ShowHandOnChallenge = False
                        Say(Connection, Channel, "Cards now $k4need not$o be shown for a Wild Draw Four challenge.")
                    Else
                        Reply(Connection, Channel, Sender, VBot.Choose("", "Sorry " & Sender.Split("!"c)(0) & ", ") & "I don't recognise " & VBot.Choose(IRCColours.Red & lValue & "$o", "that") & " as a Boolean value. Please enter $k11on$o or $k11off$o.")
                    End If
                End If
            Case "entryperiod", "gamestarttimer", "starttimer", "entrytime", "timetoenter", "openperiod", "opentime", "joinperiod", "jointime", "joiningperiod", "joiningtime"
                If Not UserHasPermission(Connection, Channel, Sender, MyKey & ".set") Then : Reply(Connection, Channel, Sender, "You don't have access to that setting.") : Return : End If
                If lValue = Nothing Then
                    Say(Connection, Channel, "Players will have $k09" & EntryPeriod & "$o seconds to join the game.")
                Else
                    Dim nValue As Integer
                    If Integer.TryParse(lValue, nValue) Then
                        If nValue <= 0 Then
                            Reply(Connection, Channel, Sender, "$k4The value must be positive.")
                        Else
                            EntryPeriod = nValue
                            Say(Connection, Channel, Choose("The game entry period has been set to $k09" & nValue & "$o seconds.", "Players will now have $k09" & nValue & "$o seconds to join the game."))
                        End If
                    Else
                        Reply(Connection, Channel, Sender, "$k4That isn't a valid integer.")
                    End If
                End If
            Case "turntimelimit", "turntime", "turntimer", "idletime", "idletimer", "timeforidle", "timetoidle"
                If Not UserHasPermission(Connection, Channel, Sender, MyKey & ".set") Then : Reply(Connection, Channel, Sender, "You don't have access to that setting.") : Return : End If
                If lValue = Nothing Then
                    If TurnTimeLimit = 0 Then Say(Connection, Channel, "The turn time limit is turned off.") _
                        Else Say(Connection, Channel, "Players will have $k12" & TurnTimeLimit & "$o seconds to take their turn.")
                Else
                    Dim nValue As Integer
                    If Integer.TryParse(lValue, nValue) Then
                        If nValue < 0 Then
                            Reply(Connection, Channel, Sender, "$k4The value cannot be negative.")
                        Else
                            TurnTimeLimit = nValue
                            If nValue = 0 Then SayToAllChannels(MinorLabel & "The turn time limit was turned off.") _
                                Else SayToAllChannels(MinorLabel & Choose("The turn time limit has been set to $k09" & nValue & "$o seconds.", "Players will now have $k09" & TurnTimeLimit & "$o seconds to take their turn."))

                            ' Reset the existing turn timers.
                            For Each Game In Games
                                Game.Value.GameTimer.Interval = If(TurnTimeLimit <= 0, 60000, TurnTimeLimit * 1000) * If(Game.Value.LongTimer, 2, 1)
                            Next
                        End If
                    Else
                        Reply(Connection, Channel, Sender, "$k4That isn't a valid integer.")
                    End If
                End If
            Case "victorybonus", "winbonus", "scorevictory", "scorewin", "victorypoints", "winpoints", "pointsforwin"
                If lValue Is Nothing Then
                    Say(Connection, Channel, "Victory bonuses are " & If(VictoryBonus, "$k9enabled$o.", "$k4disabled$o."))
                Else
                    Select Case lValue.ToLower
                        Case "true", "on", "yes", "enabled"
                            VictoryBonus = True
                            Say(Connection, Channel, "Victory bonuses are now $k9enabled$o.")
                        Case "false", "off", "no", "disabled"
                            VictoryBonus = False
                            Say(Connection, Channel, "Victory bonuses are now $k4disabled$o.")
                        Case Else
                            Reply(Connection, Channel, Sender, VBot.Choose("", "Sorry " & Sender.Split("!"c)(0) & ", ") & "I don't recognise " & VBot.Choose(IRCColours.Red & lValue & "$o", "that") & " as a Boolean value. Please enter $k11on$o or $k11off$o.")
                    End Select
                End If
            Case "victorybonuslastplace", "winbonuslastplace", "victorybonuslast", "winbonuslast"
                If lValue Is Nothing Then
                    Say(Connection, Channel, "A victory bonus " & If(VictoryBonusLastPlace, "$k9will$o", "$k4will not$o") & " be awarded to last place.")
                Else
                    Select Case lValue.ToLower
                        Case "true", "on", "yes", "enabled"
                            VictoryBonusLastPlace = True
                            Say(Connection, Channel, "A victory bonus $k9will$o be awarded to last place.")
                        Case "false", "off", "no", "disabled"
                            VictoryBonusLastPlace = False
                            Say(Connection, Channel, "A victory bonus $k4will not$o be awarded to last place.")
                        Case Else
                            Reply(Connection, Channel, Sender, VBot.Choose("", "Sorry " & Sender.Split("!"c)(0) & ", ") & "I don't recognise " & VBot.Choose(IRCColours.Red & lValue & "$o", "that") & " as a Boolean value. Please enter $k11on$o or $k11off$o.")
                    End Select
                End If
            Case "victorybonusrepeat", "winbonusrepeat"
                If lValue Is Nothing Then
                    Say(Connection, Channel, "A victory bonus " & If(VictoryBonusRepeat, "$k9will$o", "$k4may not$o") & " be awarded to all above last place.")
                Else
                    Select Case lValue.ToLower
                        Case "true", "on", "yes", "enabled"
                            VictoryBonusRepeat = True
                            Say(Connection, Channel, "A victory bonus $k9will$o be awarded to all above last place.")
                        Case "false", "off", "no", "disabled"
                            VictoryBonusRepeat = False
                            Say(Connection, Channel, "A victory bonus $k4may not$o be awarded to all above last place.")
                        Case Else
                            Reply(Connection, Channel, Sender, VBot.Choose("", "Sorry " & Sender.Split("!"c)(0) & ", ") & "I don't recognise " & VBot.Choose(IRCColours.Red & lValue & "$o", "that") & " as a Boolean value. Please enter $k11on$o or $k11off$o.")
                    End Select
                End If
            Case "handbonus", "cardbonus", "scorehand", "scorecards", "cardpoints", "handpoints", "pointsforhand", "pointsforcards"
                If lValue Is Nothing Then
                    Say(Connection, Channel, "Points " & If(HandBonus, "$k9will$o", "$k4may not$o") & " be added for cards taken.")
                Else
                    Select Case lValue.ToLower
                        Case "true", "on", "yes", "enabled"
                            HandBonus = True
                            Say(Connection, Channel, "Points now $k9will$o be added for cards taken.")
                        Case "false", "off", "no", "disabled"
                            HandBonus = False
                            Say(Connection, Channel, "Points now $k4may not$o be added for cards taken.")
                        Case Else
                            Reply(Connection, Channel, Sender, VBot.Choose("", "Sorry " & Sender.Split("!"c)(0) & ", ") & "I don't recognise " & VBot.Choose(IRCColours.Red & lValue & "$o", "that") & " as a Boolean value. Please enter $k11on$o or $k11off$o.")
                    End Select
                End If
            Case "participationbonus", "playbonus", "scoreparticipation", "scoreplay", "participationpoints", "playpoints", "pointsforparticipation", "pointsforplay"
                If lValue Is Nothing Then
                    If ParticipationBonus = 0 Then
                        Say(Connection, Channel, "The participation bonus is $k4disabled$o.")
                    Else
                        Say(Connection, Channel, "The participation bonus is $k9$b" & ParticipationBonus & "$b points$o.")
                    End If
                Else
                    If Integer.TryParse(lValue, ParticipationBonus) Then
                        If ParticipationBonus = 0 Then
                            Say(Connection, Channel, "The participation bonus is now $k4disabled$o.")
                        Else
                            Say(Connection, Channel, "The participation bonus is now $k9$b" & ParticipationBonus & "$b points$o.")
                        End If
                    Else
                        Reply(Connection, Channel, Sender, "$k4That isn't a valid integer.")
                    End If
                End If
            Case "quitpenalty"
                If lValue Is Nothing Then
                    If QuitPenalty = 0 Then
                        Say(Connection, Channel, "The quit penalty is $k4disabled$o.")
                    Else
                        Say(Connection, Channel, "The quit penalty is $k9$b" & QuitPenalty & "$b points$o.")
                    End If
                Else
                    If Integer.TryParse(lValue, QuitPenalty) Then
                        If QuitPenalty = 0 Then
                            Say(Connection, Channel, "The quit penalty is now $k4disabled$o.")
                        Else
                            Say(Connection, Channel, "The quit penalty is now $k9$b" & QuitPenalty & "$b points$o.")
                        End If
                    Else
                        Reply(Connection, Channel, Sender, "$k4That isn't a valid integer.")
                    End If
                End If
            Case "victorybonusvalue", "victorybonuspoints", "winbonusvalue", "winbonuspoints"
                If lValue Is Nothing Then
                    Say(Connection, Channel, "The victory bonuses are $k09" & String.Join("$o, $k09", VictoryBonusValue) & "$o.")
                Else
                    Dim nValue As New List(Of Integer)
                    For Each s In lValue.Split({","c, ";"c, " "c}, StringSplitOptions.RemoveEmptyEntries)
                        Dim nValue2 As Integer
                        If Integer.TryParse(s, nValue2) Then
                            nValue.Add(nValue2)
                        Else
                            Reply(Connection, Channel, Sender, "$k04" & s & "$o isn't a valid integer.")
                        End If
                    Next
                    VictoryBonusValue = nValue.ToArray()
                    Say(Connection, Channel, "The victory bonuses are now $k09" & String.Join("$o, $k09", VictoryBonusValue) & "$o.")
                End If
            Case "highlight", "ping", "alert", "highlights", "pings", "alerts", "gamealert", "gamealerts"
                If lValue = Nothing Then
                    Dim Settings As PlayerSettings
                    If Not Players.TryGetValue(Sender.Split("!"c)(0), Settings) Then : Say(Connection, Channel, ChrW(2) & Sender.Split("!"c)(0) & "$b, your game alerts are $k4disabled$o.")
                    ElseIf Settings.Highlight = 0 Then : Say(Connection, Channel, ChrW(2) & Sender.Split("!"c)(0) & "$b, your game alerts are $k4disabled$o.")
                    ElseIf Settings.Highlight = 1 Then : Say(Connection, Channel, ChrW(2) & Sender.Split("!"c)(0) & "$b, your game alerts were $k7recently disabled$o.")
                        Reply(Connection, Channel, Sender, ChrW(2) & Sender.Split("!"c)(0) & "$b, your game alerts were disabled because you stopped playing.")
                        Reply(Connection, Channel, Sender, "You can enable them permanently with $k11$cuset highlight permenent$o.")
                        Settings.Highlight = 0
                    ElseIf Settings.Highlight = 2 Then : Say(Connection, Channel, ChrW(2) & Sender.Split("!"c)(0) & "$b, your game alerts were $k7recently disabled$o.")
                        Reply(Connection, Channel, Sender, ChrW(2) & Sender.Split("!"c)(0) & "$b, your game alerts were disabled because you left the channel.")
                        Reply(Connection, Channel, Sender, "You can enable them permanently with $k11$cuset highlight permenent$o.")
                        Settings.Highlight = 0
                    ElseIf Settings.Highlight = 4 Then : Say(Connection, Channel, ChrW(2) & Sender.Split("!"c)(0) & "$b, your game alerts are $k12enabled$o.")
                    ElseIf Settings.Highlight = 5 Then : Say(Connection, Channel, ChrW(2) & Sender.Split("!"c)(0) & "$b, your game alerts are $k12enabled$o.")
                    ElseIf Settings.Highlight = 6 Then : Say(Connection, Channel, ChrW(2) & Sender.Split("!"c)(0) & "$b, your game alerts are $k9permanently enabled$o.")
                    End If
                Else
                    Dim Settings As PlayerSettings
                    If Not Players.TryGetValue(Sender.Split("!"c)(0), Settings) Then
                        Settings = New PlayerSettings
                        Players.Add(Sender.Split("!"c)(0), Settings)
                    End If
                    Select Case lValue.ToLower
                        Case "0", "off", "disable", "disabled", "no", "false"
                            Settings.Highlight = 0
                            Say(Connection, Channel, ChrW(2) & Sender.Split("!"c)(0) & "$b, your game alerts are now $k4disabled$o.")
                        Case "1", "4", "on", "temporary", "temp", "enable", "enabled", "yes", "true"
                            Settings.Highlight = 4
                            Say(Connection, Channel, ChrW(2) & Sender.Split("!"c)(0) & "$b, your game alerts are now $k12enabled$o.")
                        Case "6", "always", "permanent", "perm"
                            Settings.Highlight = 6
                            Say(Connection, Channel, ChrW(2) & Sender.Split("!"c)(0) & "$b, your game alerts are now $k9permanently enabled$o.")
                    End Select
                End If
            Case "autosort", "sort", "arrange", "autoarrange", "sorthand", "arrangehand"
                If lValue = Nothing Then
                    Dim Settings As PlayerSettings
                    If Not Players.TryGetValue(Sender.Split("!"c)(0), Settings) Then : Say(Connection, Channel, ChrW(2) & Sender.Split("!"c)(0) & "$b, your cards will be sorted $k9by colour$o.")
                    ElseIf Settings.AutoSort = 0 Then : Say(Connection, Channel, ChrW(2) & Sender.Split("!"c)(0) & "$b, your cards will $k4not be sorted$o.")
                    ElseIf Settings.AutoSort = 1 Then : Say(Connection, Channel, ChrW(2) & Sender.Split("!"c)(0) & "$b, your cards will be sorted $k9by colour$o.")
                    ElseIf Settings.AutoSort = 2 Then : Say(Connection, Channel, ChrW(2) & Sender.Split("!"c)(0) & "$b, your cards will be sorted $k12by rank$o.")
                    End If
                Else
                    Dim Settings As PlayerSettings
                    If Not Players.TryGetValue(Sender.Split("!"c)(0), Settings) Then
                        Settings = New PlayerSettings
                        Players.Add(Sender.Split("!"c)(0), Settings)
                    End If
                    Select Case lValue.ToLower
                        Case "0", "off", "disable", "disabled", "no", "false"
                            Settings.AutoSort = 0
                            Say(Connection, Channel, ChrW(2) & Sender.Split("!"c)(0) & "$b, your cards will $k4no longer be sorted$o.")
                        Case "1", "on", "enable", "enabled", "yes", "true", "colour", "color", "bycolour", "bycolor"
                            Settings.AutoSort = 1
                            Say(Connection, Channel, ChrW(2) & Sender.Split("!"c)(0) & "$b, your cards will now be sorted $k9by colour$o.")
                        Case "2", "rank", "number", "byrank", "bynumber"
                            Settings.AutoSort = 2
                            Say(Connection, Channel, ChrW(2) & Sender.Split("!"c)(0) & "$b, your cards will now be sorted $k12by rank$o.")
                    End Select
                End If
            Case Else
                Say(Connection, Channel, "I don't manage a property named $k04" & lProperty & "$o.")
        End Select
    End Sub

#Region "Joining/Quitting"
    <Regex({"\x01ACTION enters( the game( of UNO)?)?\.?\x01"})>
    Public Sub RegexJoin(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As System.Text.RegularExpressions.Match)
        CommandJoin(Connection, Sender, Channel, {})
    End Sub
    <Regex({"^jo$"})>
    Public Sub RegexJoin2(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As System.Text.RegularExpressions.Match)
        If Games.ContainsKey(If(Connection IsNot Nothing, Connection.Address & "/", "") & Channel) Then CommandJoin(Connection, Sender, Channel, {})
    End Sub
    <Command({"ujoin", "uno"}, 0, 0,
    "ujoin",
    "Enters you into a game of UNO. You may use this command to open a game, even if someone else hasn't started one.")>
    Public Sub CommandJoin(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Threading.Thread.Sleep(600)
        Dim CurrentGame As ChannelGame = Nothing
        If Not Games.TryGetValue(If(Connection IsNot Nothing, Connection.Address & "/", "") & Channel, CurrentGame) Then
            ' If no game has started, start one now.
            CurrentGame = New ChannelGame With {.Connection = Connection, .Channel = Channel, .IsOpen = True, .GameTimer = New Timers.Timer(EntryPeriod * 1000) With {.AutoReset = False, .Enabled = True}, .GameStartTime = Now + TimeSpan.FromSeconds(EntryPeriod)}

            If CallEvent(MyKey, "GameOpen", {{"connection", Connection}, {"channel", Channel}, {"user", Sender}, {"game", CurrentGame}}) Then
                CurrentGame.GameTimer.Dispose()
                Return
            End If

            Games.Add(If(Connection IsNot Nothing, Connection.Address & "/", "") & Channel, CurrentGame)
            CurrentGame.Players.Add(Sender.Split("!"c)(0), New Player)
            CurrentGame.PlayerCount += 1
            Say(Connection, Channel, "$k13$b" & Sender.Split("!")(0) & "$b is starting a game of UNO!")

            ' Alert players.
            Dim message As String = ""
            For Each Player In Players
                If Connection.Channels(Channel).Users.ContainsKey(Player.Key) And (Player.Value.Highlight And 4) = 4 And Not CurrentGame.Players.ContainsKey(Player.Key) Then message &= " " & Player.Key
            Next
            If message <> "" Then Say(Connection, Channel, "$aACTION alerts:" & message & "$a")

            Say(Connection, Channel, "$k12Starting in $b" & EntryPeriod & "$b seconds; $k12say $k11$cujoin$k12 if you wish to join the game.")
            'AddHandler CurrentGame.GameTimer.Elapsed, AddressOf PrepareAITimer
            AddHandler CurrentGame.GameTimer.Elapsed, AddressOf GameClose
        ElseIf Not CurrentGame.IsOpen Then
            ' The game has already started...
            Reply(Connection, Channel, Sender.Split("!"c)(0), "Sorry " & Sender.Split("!")(0) & ", but this game has already started. Feel free to join the next game, though.")
        ElseIf CurrentGame.Players.ContainsKey(Sender.Split("!"c)(0)) Then
            ' This player has already entered the game.
            Reply(Connection, Channel, Sender.Split("!"c)(0), "You've already entered the game, " & Sender.Split("!")(0) & ".")
        Else
            ' A player is joining.
            If CallEvent(MyKey, "GameJoin", {{"connection", Connection}, {"channel", Channel}, {"user", Sender}, {"game", CurrentGame}}) Then Return

            'CurrentGame.GameStartTime += TimeSpan.FromSeconds(5)
            'CurrentGame.GameTimer.Interval = (CurrentGame.GameStartTime - Now).TotalMilliseconds
            CurrentGame.Players.Add(Sender.Split("!"c)(0), New Player)
            CurrentGame.PlayerCount += 1
            Say(Connection, Channel, "$k13$b" & Sender.Split("!")(0) & "$b has entered the game.")
        End If
    End Sub

    <Command({"uquit", "uleave", "upart"}, 0, 0,
"uquit",
"Removes you from the game of UNO.")>
    Public Sub CommandQuit(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim CurrentGame As ChannelGame = Nothing
        If Not Games.TryGetValue(If(Connection IsNot Nothing, Connection.Address & "/", "") & Channel, CurrentGame) Then
            Return
        ElseIf Not CurrentGame.Players.ContainsKey(Sender.Split("!"c)(0)) Then
        Else
            If CallEvent(MyKey, "GameQuit", {{"connection", Connection}, {"channel", Channel}, {"user", Sender}, {"game", CurrentGame}}) Then Return
            Say(Connection, Channel, "$k15$b" & Sender.Split("!"c)(0) & "$b$k12 left the game.")
            RemovePlayer(CurrentGame, Sender.Split("!"c)(0))
        End If
    End Sub

    Public Overrides Sub OnChannelExit(ByVal Connection As VBot.IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Reason As String)
        MyBase.OnChannelExit(Connection, Sender, Channel, Reason)

        If Players.ContainsKey(Sender.Split("!"c)(0)) Then
            If (Players(Sender.Split("!"c)(0)).Highlight And 4) = 4 And Players(Sender.Split("!"c)(0)).Highlight <> 6 Then Players(Sender.Split("!"c)(0)).Highlight = 2
        End If

        ' Reset the turn timer if someone leaves the channel. Even if the time limit is turned off, this will give them
        '   60 seconds to reconnect before they are dropped.
        Dim CurrentGame As ChannelGame = Nothing
        If Games.TryGetValue(If(Connection IsNot Nothing, Connection.Address & "/", "") & Channel, CurrentGame) Then
            If Not CurrentGame.Players.ContainsKey(Sender.Split("!"c)(0)) Then
            ElseIf CurrentGame.IsOpen Then
                Say(Connection, Channel, "$k15$b" & Sender.Split("!"c)(0) & "$b$k12 left the game.")
                RemovePlayer(CurrentGame, Sender.Split("!"c)(0))
            Else
                CurrentGame.Players(Sender.Split("!"c)(0)).LeftAt = Now
                If CurrentGame.Players.Keys(CurrentGame.CurrentTurn) <> Sender.Split("!"c)(0) Then
                ElseIf Not CurrentGame.GameTimer.Enabled Then
                    CurrentGame.GameTimer.Start()
                End If
            End If
        End If
    End Sub

    Public Sub RemovePlayer(ByVal Game As ChannelGame, ByVal Nickname As String)
        SyncLock Game
            ' Find this player's index.
            Dim PlayerIndex = Array.IndexOf(Of String)(Game.Players.Keys.ToArray, Nickname)
            If Not Game.IsOpen Then
                ' If the game has started, add this player to the list of quit players.
                Game.QuitPlayers.Add(Nickname, Game.Players(Nickname))
            End If

            If Not Game.IsOpen And Game.CurrentTurn = PlayerIndex Then
                With Game
                    If .DrawFourChallenger = Nickname Then
                        DealCards(Game, .CurrentTurn, 4, False)
                        Say(.Connection, .Channel, "$k12" & .Players.Keys(.CurrentTurn) & " takes four cards.")
                        .DrawFourChallenger = Nothing
                        .DrawFourUser = Nothing
                    ElseIf .DrawnCard <> 255 Then
                        .DrawnCard = 255
                    ElseIf (.WildColour And Colour.Pending) Then
                        .WildColour = .WildColour Xor Colour.Pending
                    End If
                End With
            End If

            Game.Players.Remove(Nickname)
            Game.PlayerCount -= 1

            If Game.Players.Count = 2 And Game.Players.ContainsKey(Game.Connection.Nickname) Then
                Game.LongTimer = True
                Game.GameTimer.Interval *= 2
            End If

            ' If there's only one player left, declare them the winner.
            If Not Game.IsOpen And Game.PlayerCount <= 1 Then
                Game.GameTimer.Stop()
                Game.CurrentTurn = Game.NextPlayer
                Game.Players.Values(Game.CurrentTurn).Position = Game.PlayersOut + 1
                If HandBonus Then CountHandPoints(Game, Game.CurrentTurn)
                EndGame(Game)
            ElseIf Game.IsOpen And Game.PlayerCount = 0 Then
                Game.GameTimer.Stop()
                Games.Remove(If(Game.Connection IsNot Nothing, Game.Connection.Address & "/", "") & Game.Channel)
            ElseIf Not Game.IsOpen And Game.CurrentTurn = PlayerIndex Then
                ' It was the leaving player's turn, so do some housekeeping and skip them.
                With Game
                    If .DrawnCard <> 255 Then
                        .DrawnCard = 255
                    ElseIf (.WildColour And Colour.Pending) Then
                        .WildColour = .WildColour Xor Colour.Pending
                    End If
                End With

                If Game.IsReversed Then
                    If Game.CurrentTurn = 0 Then Game.CurrentTurn = Game.Players.Count - 1 Else Game.CurrentTurn -= 1
                Else
                    If Game.CurrentTurn >= Game.Players.Count Then Game.CurrentTurn = 0
                End If

                Game.GameTimer.Stop()

                Game.DrawnCard = 255

                Say(Game.Connection, Game.Channel, "$b" & Game.Players.Keys(Game.CurrentTurn) & "$b, it's now your turn.")
                If Not Game.IsOpen And Game.IdleTurn > PlayerIndex Then Game.IdleTurn -= 1
                If TurnTimeLimit > 0 Then Game.GameTimer.Start() ' Restart the timer.
                AICheck(Game)
            Else
                If Not Game.IsOpen And Game.CurrentTurn > PlayerIndex Then Game.CurrentTurn -= 1
IdleTurnCheck:
                If Not Game.IsOpen And Game.IdleTurn = PlayerIndex Then
                    If Game.IsReversed Then
                        If Game.IdleTurn = 0 Then Game.IdleTurn = Game.Players.Count - 1 Else Game.IdleTurn -= 1
                    Else
                        If Game.IdleTurn >= Game.Players.Count Then Game.IdleTurn = 0
                    End If
                    If Game.CurrentTurn = Game.IdleTurn Then
                        Games.Remove(If(Game.Connection IsNot Nothing, Game.Connection.Address & "/", "") & Game.Channel)
                        Say(Game.Connection, Game.Channel, "$k13The game has been cancelled.")
                    Else
                        Game.Players.Values(Game.IdleTurn).CanMove = True
                        Say(Game.Connection, Game.Channel, "$k12$b" & Game.Players.Keys(Game.IdleTurn) & Choose("$b ", "$b, you ") & "may play now.")
                        AICheck(Game)
                    End If
                ElseIf Not Game.IsOpen And Game.IdleTurn > PlayerIndex Then
                    Game.IdleTurn -= 1
                End If
            End If
        End SyncLock
    End Sub

    <Command({"aichallenge", "aisummon", "aijoin", "summonai", "challengeai", "botchallenge", "botsummon", "botjoin", "summonbot", "challengebot"}, 0, 0,
"aichallenge",
"Calls me into the game, even if there are already two or more players.",
Nothing, CommandAttribute.CommandScope.Channel)>
    Public Sub CommandAIChallenge(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Threading.Thread.Sleep(600)
        Dim CurrentGame As ChannelGame = Nothing
        If Not Games.TryGetValue(If(Connection IsNot Nothing, Connection.Address & "/", "") & Channel, CurrentGame) Then
            Say(Connection, Channel, "$k4There's no game going on right now. Type $k11$cujoin$k4 to start one.")
            Return
        ElseIf Not CurrentGame.IsOpen Then
            ' The game has already started...
            Reply(Connection, Channel, Sender.Split("!"c)(0), "This game has already started.")
        ElseIf CurrentGame.Players.ContainsKey(Connection.Nickname) Then
            ' This player has already entered the game.
            Reply(Connection, Channel, Sender.Split("!"c)(0), "I've already entered the game.")
        ElseIf Not AIEnabled Then
            Say(Connection, Channel, "$k4The AI player is turned off.")
        Else
            ' A player is joining.
            CurrentGame.Players.Add(Connection.Nickname, New Player)
            CurrentGame.PlayerCount += 1
            Say(Connection, Channel, "$k13$b" & Connection.Nickname & "$b has entered the game.")
        End If
    End Sub

    Private Sub GameClose(ByVal sender As Object, ByVal e As Timers.ElapsedEventArgs)
        ' Find out what game, what connection and what channel this is for.
        Dim CurrentGame As ChannelGame = Nothing
        For Each Game In Games
            If Game.Value.GameTimer Is sender Then
                CurrentGame = Game.Value
                Exit For
            End If
        Next
        If IsNothing(CurrentGame) Then Return

        RemoveHandler CurrentGame.GameTimer.Elapsed, AddressOf GameClose

        SyncLock CurrentGame
            For Each Player In Players
                If CurrentGame.Players.ContainsKey(Player.Key) Then
                    If Player.Value.Highlight = 5 Then Player.Value.Highlight = 4
                Else
                    If Player.Value.Highlight = 4 Then : Player.Value.Highlight = 5
                    ElseIf Player.Value.Highlight = 5 Then : Player.Value.Highlight = 1
                    End If
                End If
            Next

            With CurrentGame
                ' Start the game.
                If .PlayerCount < 2 And AIEnabled And Not .Players.ContainsKey(.Connection.Nickname) Then
                    CurrentGame.Players.Add(Nickname(.Connection), New Player)
                    CurrentGame.PlayerCount += 1
                    Say(.Connection, .Channel, "$k13$b" & .Connection.Nickname & "$b has entered the game.")
                    Threading.Thread.Sleep(600)
                End If
                If .PlayerCount < 2 Then
                    Say(.Connection, .Channel, "$k12Not enough players joined. Please enter $k11$cujoin$k12 when you're ready for a game.")
                    Games.Remove(If(.Connection IsNot Nothing, .Connection.Address & "/", "") & .Channel)
                    Return
                End If

                .IsOpen = False

                For i = 0 To .Players.Count - 1
                    GetStats(CurrentStats, .Connection, .Channel, .Players.Keys(i), True).Plays += 1
                    GetStats(AllTimeStats, .Connection, .Channel, .Players.Keys(i), True).Plays += 1
                    DealCards(CurrentGame, i, 7, True)
                    Threading.Thread.Sleep(600)
                Next
                Say(.Connection, .Channel, "$k13The game of $bUNO$b has started!")
                Threading.Thread.Sleep(600)
                .GameTimer.Interval = If(TurnTimeLimit <= 0, 60000, TurnTimeLimit * 1000)  ' Set the turn timer.
                ' Set the long timer if it's a duel with the bot.
                If .Players.Count = 2 And .Players.ContainsKey(.Connection.Nickname) Then
                    .LongTimer = True
                    .GameTimer.Interval *= 2
                End If

DrawUpCard:
                ' Draw the first card.
                .Discards.Push(DrawCard(CurrentGame, 1)(0))

                Select Case .Discards.Peek
                    ' If it's a Draw Four, put it back.
                    Case 65
                        Say(.Connection, .Channel, "$k12The first up-card is: " & ShowCard(65) & "$k12 Let's pick a different card.")
                        .Deck.Add(.Discards.Pop)
                        GoTo DrawUpCard
                    Case 10, 26, 42, 58  ' A reverse card.
                        .CurrentTurn = .Players.Count - 1
                        .IsReversed = True
                        Say(.Connection, .Channel, "$k12The first up-card is: " & ShowCard(.Discards.Peek) & "$k12 Play will begin with $b" & .Players.Keys(.CurrentTurn) & "$b.")
                    Case 11, 27, 43, 59  ' A skip card.
                        .CurrentTurn = Math.Min(.Players.Count - 1, 1)
                        Say(.Connection, .Channel, "$k12The first up-card is: " & ShowCard(.Discards.Peek) & "$k12 $b" & .Players.Keys(0) & "$b is skipped; play will begin with $b" & .Players.Keys(.CurrentTurn) & "$b.")
                    Case 12, 28, 44, 60  ' A draw two card.
                        .CurrentTurn = Math.Min(.Players.Count - 1, 1)
                        Say(.Connection, .Channel, "$k12The first up-card is: " & ShowCard(.Discards.Peek) & "$k12 $b" & .Players.Keys(0) & "$b draws two cards; play will begin with $b" & .Players.Keys(.CurrentTurn) & "$b.")
                        DealCards(CurrentGame, 0, 2, False)
                    Case 64  ' A wild card.
                        .CurrentTurn = .Players.Count - 1
                        .WildColour = 192
                        Say(.Connection, .Channel, "$k12The first up-card is: " & ShowCard(.Discards.Peek) & "$k12 $b" & .Players.Keys(.CurrentTurn) & "$b, please choose a colour to begin play.")
                    Case Else
                        .CurrentTurn = 0
                        Say(.Connection, .Channel, "$k12The first up-card is: " & ShowCard(.Discards.Peek) & "$k12 Play will begin with $b" & .Players.Keys(.CurrentTurn) & "$b.")
                End Select

                ShowHand(CurrentGame, .Players.Keys(.CurrentTurn))
                AddHandler .GameTimer.Elapsed, AddressOf IdleCheck
                .GameTimer.Start()
            End With
        End SyncLock
        AICheck(CurrentGame)
    End Sub

    <Command({"ustop", "uclose"}, 0, 0,
"ustop",
"Stops the game of UNO. Use only in emergencies.",
".stop")>
    Public Sub CommandStop(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Threading.Thread.Sleep(600)
        Dim CurrentGame As ChannelGame = Nothing
        If Not Games.TryGetValue(If(Connection IsNot Nothing, Connection.Address & "/", "") & Channel, CurrentGame) Then
            Say(Connection, Channel, "$k4There's no game going on right now.")
        Else
            CurrentGame.GameTimer.Stop()
            Games.Remove(If(Connection IsNot Nothing, Connection.Address & "/", "") & Channel)
            Say(Connection, Channel, "$k13The game has been cancelled.")
        End If
    End Sub

#End Region

#Region "Gameplay"
    Friend Function DealCards(ByVal Game As ChannelGame, ByVal Player As Integer, ByVal Number As Short, ByVal IsInitialDraw As Boolean)
        If IsInitialDraw Then CallEvent(MyKey, "DealCards", {{"game", Game}, {"player", Player}}) _
        Else CallEvent(MyKey, "DrawCards", {{"game", Game}, {"player", Player}, {"number", Number}})

        Dim DealtCards = DrawCard(Game, Number)

        Dim Message As String = ""
        For Each Card In DealtCards
            Message &= " " & ShowCard(Card)
        Next

        If Game.Players.Keys(Player) <> Game.Connection.Nickname Then
            If IsInitialDraw Then
                Say(Game.Connection, Game.Players.Keys(Player), "You were dealt:" & Message)
            Else
                Say(Game.Connection, Game.Players.Keys(Player), "You draw:" & Message)
            End If
        End If
        Game.Players.Values(Player).Hand.AddRange(DealtCards)

        Return DealtCards
    End Function

    Friend Function ShowCard(ByVal Number As Byte) As String
        ' This is where we define how the IRC users will 'see' each card.
        If (Number And 64) Then
            ' It's a wild card.
            If Number = 64 Then
                Return Chr(3) & "0,14" & IRCColours.Bold & " Wild " & IRCColours.ClearFormat
            ElseIf Number = 65 Then
                Return Chr(3) & "0,14" & " Wild $b$k4D$k8r$k9a$k12w $k4F$k8o$k9u$k12r " & IRCColours.ClearFormat
            End If
            Return Chr(3) & "4,14" & IRCColours.Bold & " ??? " & IRCColours.ClearFormat
        Else
            Dim Colour1 As String = "", Colour2 As String = "", Colour As String = "", Value As String = ""
            Select Case (Number And 48)
                Case 0
                    ' A red card.
                    Colour1 = Chr(3) & "4"
                    Colour2 = Chr(3) & "0,4"
                    Colour = "Red"
                Case 16
                    ' A yellow card.
                    Colour1 = Chr(3) & "8"
                    Colour2 = Chr(3) & "1,8"
                    Colour = "Yellow"
                Case 32
                    ' A green card.
                    Colour1 = Chr(3) & "9"
                    Colour2 = Chr(3) & "1,9"
                    Colour = "Green"
                Case 48
                    ' A blue card.
                    Colour1 = Chr(3) & "12"
                    Colour2 = Chr(3) & "0,12"
                    Colour = "Blue"
            End Select
            Value = {"0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "Reverse", "Skip", "Draw Two"}(Number And 15)
            Return Colour2 & " " & Colour & Chr(2) & " " & Value & " " & Chr(15)
        End If
    End Function

    Friend Sub ShowHand(ByVal Game As ChannelGame, ByVal PlayerName As String)
        Dim Message As String = ""

        If Not Players.ContainsKey(PlayerName) OrElse Players(PlayerName).AutoSort = 1 Then
            Game.Players(PlayerName).SortHandByColour()
        ElseIf Players(PlayerName).AutoSort = 2 Then
            Game.Players(PlayerName).SortHandByRank()
        End If

        For Each Card In Game.Players(PlayerName).Hand
            Message &= " " & ShowCard(Card)
        Next

        If PlayerName <> Game.Connection.Nickname Then Say(Game.Connection, PlayerName, "You hold:" & Message)
    End Sub

    Friend Function DrawCard(ByVal Game As ChannelGame, ByVal Number As Short) As Byte()
        Dim CardsDrawn(Number - 1) As Byte
        For i = 1 To Number
            ' First make sure that there is a card left in the deck.
            If Game.Deck.Count < 1 Then
                ' There are no cards left in the deck. Let's reshuffle the discards minus the top one into the deck.
                Say(Game.Connection, Game.Channel, "$k12There are no cards left in the deck! We'll shuffle the discards back into the deck...")
                Dim UpCard = Game.Discards.Pop() ' Hold the up-card while we reshuffle.
                Game.Deck.AddRange(Game.Discards.ToArray())
                Game.Discards.Clear()
                Game.Discards.Push(UpCard)
            End If
            If Game.Deck.Count < 1 Then
                ' There are no cards left to draw!
                Say(Game.Connection, Game.Channel, "$k12There are $bstill$b no cards left in the deck!")
                For j = i To Number
                    CardsDrawn(i - 1) = 128
                Next
                Return CardsDrawn
            Else
                ' Pick a card at random from the deck.
                Randomize()
                Dim RandomIndex = Int(Rnd() * Game.Deck.Count)
                Dim CardDrawn = Game.Deck(RandomIndex)
                Game.Deck.RemoveAt(RandomIndex)
                CardsDrawn(i - 1) = CardDrawn
            End If
        Next
        Return CardsDrawn
    End Function

    <Regex("^pl (?<Card>(r(ed)?|y(ellow)?|g(reen)?|b(lue)?) ?(\d|zero|one|two|three|four|five|six|seven|eight|nine|r(everse)?|s(kip)?|d((raw ?(t(wo)?|2)|2)))|w(ild)? ?(d(raw)? ?(f(our)?|4)?)?|d(raw)? ?(f(our)?|4)|(?<Something>[A-Za-z 0-9])*)")>
    Public Sub RegexPlay(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As System.Text.RegularExpressions.Match)
        CommandPlay(Connection, Sender, Channel, {Match.Groups("Card").Value, Match.Groups("Something").Success})
    End Sub
    <Regex("^\x01ACTION (plays|discards) (an? |his |her |their |my |its |the )?(?<Card>(r(ed)?|y(ellow)?|g(reen)?|b(lue)?) ?(\d|zero|one|two|three|four|five|six|seven|eight|nine|r(everse)?|s(kip)?|d((raw ?(t(wo)?|2)|2)))|w(ild)? ?(d(raw)? ?(f(our)?|4)?)?|d(raw)? ?(f(our)?|4)|(?<Something>[A-Za-z 0-9])*)(\.|!)?\x01$")>
    Public Sub RegexPlay2(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As System.Text.RegularExpressions.Match)
        If Not Games.ContainsKey(If(Connection IsNot Nothing, Connection.Address & "/", "") & Channel) Then Return
        CommandPlay(Connection, Sender, Channel, {Match.Groups("Card").Value, Match.Groups("Something").Success})
    End Sub
    <Command({"play"}, 1, 1,
 "play <card>",
 "Allows you to play a card when it's your turn.")>
    Public Sub CommandPlay(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim PlayerCanMove As Boolean, PlayerIndex As Integer
        Threading.Thread.Sleep(600)
        Dim CurrentGame As ChannelGame = Nothing
        If Not Games.TryGetValue(If(Connection IsNot Nothing, Connection.Address & "/", "") & Channel, CurrentGame) Then
            If args.ElementAtOrDefault(1) <> "True" Then Say(Connection, Channel, "$k4There's no game going on right now. Type $k11$cujoin$k4 to start one.")
        ElseIf CurrentGame.IsOpen Then
            If args.ElementAtOrDefault(1) <> "True" Then Say(Connection, Channel, "$k4This game hasn't started yet!")
        ElseIf Not CurrentGame.Players.ContainsKey(Sender.Split("!"c)(0)) Then
            If args.ElementAtOrDefault(1) <> "True" Then Say(Connection, Channel, "$k4" & Sender.Split("!")(0) & ", you're not in this game.")
        ElseIf CurrentGame.Players(Sender.Split("!"c)(0)).CanMove Then
            PlayerCanMove = True
            PlayerIndex = Array.IndexOf(CurrentGame.Players.Keys.ToArray, Sender.Split("!")(0))
            GoTo OK
        ElseIf CurrentGame.Players.Keys(CurrentGame.CurrentTurn) <> Sender.Split("!"c)(0) Then
            If args.ElementAtOrDefault(1) <> "True" Then Say(Connection, Channel, "$k4" & Sender.Split("!")(0) & ", it's not your turn.")
        ElseIf (CurrentGame.WildColour And Colour.Pending) AndAlso (CurrentGame.Discards.Peek <> 65 Or CurrentGame.PlayerCount > 2 Or WildDrawFour = 1) Then
            ' In a two-player game, you can play another card straight on top of your own Wild Draw Four.
            Reply(Connection, Channel, Sender.Split("!")(0), "Please choose a colour for your wild card, " & Sender.Split("!")(0) & ". Enter $k11$ccolour$o followed by the colour you want.")
        ElseIf CurrentGame.DrawFourChallenger <> Nothing Then
            Reply(Connection, Channel, Sender.Split("!")(0), "That's a Wild Draw Four, " & Sender.Split("!")(0) & ". You must either $k11$cchallenge$o or enter $k11$cdraw$o to take the four cards. Enter $k11!help UNO-DrawFour$o for more info.")
        Else
            PlayerIndex = CurrentGame.CurrentTurn
OK:
            SyncLock CurrentGame
                args(0) = args(0).ToLower.Replace(" ", "")
                Dim Card As Byte = 255

                If args(0) = "wild" Or args(0) = "w" Then
                    Card = 64
                ElseIf System.Text.RegularExpressions.Regex.IsMatch(args(0), "^(w(ild)? ?d(raw)?|(w(ild)?)? ?d(raw)? ?(f(our)?|4))$") Then
                    Card = 65
                Else
                    Dim Match = System.Text.RegularExpressions.Regex.Match(args(0), "^(?<Colour>r(ed)?|y(ellow)?|g(reen)?|b(lue)?)(?<Value>[0-9]|zero|one|two|three|four|five|six|seven|eight|nine|r(everse)?|s(kip)?|d((raw)?(t(wo)?|2))?)$")
                    If Not Match.Success Then
                        Reply(Connection, Channel, Sender.Split("!")(0), "Oops! That isn't a valid identifier. Enter $k11$chelp UNO-commands$o if you're stuck.")
                        ' Obsolete.
                        'Reply(Connection, Channel, Sender.Split("!")(0), "That isn't a valid identifier. Please specify a colour followed by a value.")
                        'Threading.Thread.Sleep(600)
                        'Reply(Connection, Channel, Sender.Split("!")(0), "For example, $k10$cplay $k12b$k155$o for a blue 5.")
                        'Threading.Thread.Sleep(600)
                        'Reply(Connection, Channel, Sender.Split("!")(0), "You may use, in place of a number, r (for reverse), s (for skip), d (for draw two).")
                        'Threading.Thread.Sleep(600)
                        'Reply(Connection, Channel, Sender.Split("!")(0), "You may also use w or wd for the wild cards.")
                        'Threading.Thread.Sleep(600)
                        'Reply(Connection, Channel, Sender.Split("!")(0), "Or type the card name in full if you like.")
                        Return
                    Else
                        Select Case Match.Groups("Colour").Value
                            Case "red", "r"
                                Card = 0
                            Case "yellow", "y"
                                Card = 16
                            Case "green", "g"
                                Card = 32
                            Case "blue", "b"
                                Card = 48
                        End Select
                        Select Case Match.Groups("Value").Value
                            Case "0", "1", "2", "3", "4", "5", "6", "7", "8", "9"
                                Card += Val(Match.Groups("Value").Value)
                            Case "zero" : Card += 0
                            Case "one" : Card += 1
                            Case "two" : Card += 2
                            Case "three" : Card += 3
                            Case "four" : Card += 4
                            Case "five" : Card += 5
                            Case "six" : Card += 6
                            Case "seven" : Card += 7
                            Case "eight" : Card += 8
                            Case "nine" : Card += 9
                            Case "reverse", "r" : Card += 10
                            Case "skip", "s" : Card += 11
                            Case "drawtwo", "d", "draw2", "d2", "dt" : Card += 12
                        End Select
                    End If
                End If

                With CurrentGame
                    ' After drawing a card, you can't play a different card.
                    If Not PlayerCanMove And CurrentGame.DrawnCard <> 255 And CurrentGame.DrawnCard <> Card Then
                        Reply(Connection, Channel, Sender.Split("!")(0), "You've already drawn a card this turn, " & Sender.Split("!")(0) & ". You must play that card or pass.")
                        Return
                    End If

                    ' Make sure that card is legal.
                    If Card = 65 Then
                        If WildDrawFour = 0 Then
                            ' You can't play a Wild Draw Four if you have a card in your hand of the same colour as the up-card.
                            For Each lCard In .Players.Values(PlayerIndex).Hand
                                If (lCard And 112) = If(.Discards.Peek And 64, .WildColour << 4, .Discards.Peek And 48) Then
                                    Reply(Connection, Channel, Sender.Split("!")(0), "You can't play a Wild Draw Four, because you have a matching colour card.")
                                    Return
                                End If
                            Next
                        End If
                    ElseIf (Card And 64) = 0 And Not (.WildColour And UNOPlugin.Colour.None) Then
                        ' If it's not a Wild card, the card you play must be the same colour or the same symbol as the up-card.
                        ' Wild cards are always valid except in the case checked above.
                        If (.Discards.Peek And 64 And .WildColour = UNOPlugin.Colour.None) Then
                        ElseIf (Card And 48) <> If(.Discards.Peek And 64, .WildColour << 4, .Discards.Peek And 48) And
                                                If(.Discards.Peek And 64, True, (Card And 15) <> (.Discards.Peek And 15)) Then
                            Reply(Connection, Channel, Sender.Split("!")(0), "You can't play that card right now. Please choose a different card, or enter $k11$cdraw$o to draw from the deck.")
                            Return
                        End If
                    End If


                    ' Check if the player actually has the card they're trying to play.
                    If Not .Players.Values(PlayerIndex).Hand.Contains(Card) Then
                        Reply(Connection, Channel, Sender.Split("!")(0), "You don't have that card. Please choose a different card, or enter $k11$cdraw$o to draw from the deck.")
                        Return
                    End If

                    If Card = 65 And WildDrawFour = 1 Then
                        If (.Discards.Peek And 64) Then .DrawFourBadColour = If(.WildColour And UNOPlugin.Colour.None, UNOPlugin.Colour.None, .WildColour << 4) _
                            Else .DrawFourBadColour = .Discards.Peek And 48
                    End If

                    .GameTimer.Stop()
                    .WildColour = UNOPlugin.Colour.None

                    ' Skip players if necessary.
                    IdleSkip(CurrentGame, Array.IndexOf(.Players.Keys.ToArray, Sender.Split("!")(0)))

                    ' Take the card from their hand and put it on top of the discard pile.
                    .Players.Values(.CurrentTurn).Hand.Remove(Card)
                    .Discards.Push(Card)

                    .Players.Values(.CurrentTurn).IdleCount = 0
                    CallEvent(MyKey, "PlayerPlay", {{"game", CurrentGame}, {"player", .CurrentTurn}, {"card", Card}})

                    ' Did they go out?
                    Dim GoneOut As Boolean = .Players.Values(.CurrentTurn).Hand.Count = 0
                    Dim HasUNO As Boolean = .Players.Values(.CurrentTurn).Hand.Count = 1

                    Dim EndOfGame As Boolean
                    If GoneOut Then
                        .Players.Values(.CurrentTurn).Position = .PlayersOut + 1
                        .PlayersOut += 1
                        .PlayerCount -= 1
                        If AllOut Then
                            EndOfGame = (.PlayerCount <= 1)
                        Else
                            EndOfGame = True
                        End If
                    End If

                    If EndOfGame Then
                        ' The game is ending.
                        Say(.Connection, .Channel, "$k12$b" & Sender.Split("!")(0) & "$b plays " & ShowCard(Card) & "$k12 and $bgoes out$b!")
                        CallEvent(MyKey, "PlayerOut", {{"game", CurrentGame}, {"player", .CurrentTurn}})

                        ' If it's a Draw card, deal the cards.
                        If Card = 65 Then
                            Dim PlayerToSkip = .NextPlayer
                            Say(.Connection, .Channel, "$k12$b" & .Players.Keys(PlayerToSkip) & "$b draws four cards.")
                            DealCards(CurrentGame, PlayerToSkip, 4, False)
                        ElseIf (Card And 15) = 12 Then
                            Dim PlayerToSkip = .NextPlayer
                            Say(.Connection, .Channel, "$k12$b" & .Players.Keys(PlayerToSkip) & "$b draws two cards.")
                            DealCards(CurrentGame, PlayerToSkip, 2, False)
                        End If
                    ElseIf Card = 64 Then
                        ' If they played a Wild card, wait to pick a colour.
                        .WildColour = UNOPlugin.Colour.None + UNOPlugin.Colour.Pending
                        If GoneOut Then
                            Say(.Connection, .Channel, "$k12$b" & Sender.Split("!")(0) & "$b plays " & ShowCard(Card) & "$k12 and $bgoes out$b! Choose a colour, " & Sender.Split("!")(0) & ".")
                            CallEvent(MyKey, "PlayerOut", {{"game", CurrentGame}, {"player", .CurrentTurn}})
                        Else
                            Say(.Connection, .Channel, "$k12$b" & Sender.Split("!")(0) & "$b plays " & ShowCard(Card) & "$k12 Choose a colour for the wild card, " & Sender.Split("!")(0) & ".")
                        End If
                    ElseIf Card = 65 Then
                        ' Wild Draw Four card.
                        Dim PlayerToSkip = .NextPlayer

                        .WildColour = UNOPlugin.Colour.None + UNOPlugin.Colour.Pending
                        If GoneOut Then
                            Say(.Connection, .Channel, "$k12$b" & Sender.Split("!")(0) & "$b plays " & ShowCard(Card) & "$k12 and $bgoes out$b!")
                            CallEvent(MyKey, "PlayerOut", {{"game", CurrentGame}, {"player", .CurrentTurn}})
                            Threading.Thread.Sleep(600)
                            Say(.Connection, .Channel, "$k12$b" & .Players.Keys(PlayerToSkip) & " draws four cards. $b" & Sender.Split("!")(0) & "$b, please choose a colour.")
                            DealCards(CurrentGame, PlayerToSkip, 4, False)
                        Else
                            If WildDrawFour = 1 And (.DrawFourBadColour And 128) = 0 Then
                                Say(.Connection, .Channel, "$k12$b" & Sender.Split("!")(0) & "$b plays " & ShowCard(Card) & "$k12 to $b" & .Players.Keys(PlayerToSkip) & "$b. $b" & Sender.Split("!")(0) & "$b, please choose a colour.")
                                .DrawFourUser = Sender.Split("!")(0)
                                .DrawFourChallenger = .Players.Keys(.NextPlayer)
                            Else
                                ' Deal the four-card punishment!
                                DealCards(CurrentGame, PlayerToSkip, 4, False)
                                Say(.Connection, .Channel, "$k12$b" & Sender.Split("!")(0) & "$b plays " & ShowCard(Card) & "$k12 " & .Players.Keys(PlayerToSkip) & " draws four cards. $b" & Sender.Split("!")(0) & "$b, please choose a colour.")
                            End If
                        End If
                        Threading.Thread.Sleep(600)

                        ' Now, we wait for the current player to pick a colour.
                    ElseIf (Card And 15) = 12 Then
                        ' Draw Two card.
                        Dim PlayerToSkip = .NextPlayer
                        Dim PlayerToPlay = .NextPlayer(Times:=2)

                        If GoneOut Then
                            Say(.Connection, .Channel, "$k12$b" & Sender.Split("!")(0) & "$b plays " & ShowCard(Card) & "$k12 and $bgoes out$b!")
                            CallEvent(MyKey, "PlayerOut", {{"game", CurrentGame}, {"player", .CurrentTurn}})
                            Threading.Thread.Sleep(600)
                            Say(.Connection, .Channel, "$k12$b" & .Players.Keys(PlayerToSkip) & " draws two cards; play continues with $b" & .Players.Keys(PlayerToPlay) & "$b.")
                        Else
                            Say(.Connection, .Channel, "$k12$b" & Sender.Split("!")(0) & "$b plays " & ShowCard(Card) & "$k12 $b" & .Players.Keys(PlayerToSkip) & "$b draws two cards; play continues with $b" & .Players.Keys(PlayerToPlay) & "$b.")
                        End If
                        Threading.Thread.Sleep(600)

                        ' Lay on the two cards.
                        .Advance()
                        DealCards(CurrentGame, PlayerToSkip, 2, False)
                        Threading.Thread.Sleep(600)
                        .Advance()

                    ElseIf (Card And 15) = 10 And .PlayerCount > 2 Then
                        ' Reverse card with more than two players.
                        .IsReversed = Not .IsReversed
                        .Advance()

                        If GoneOut Then
                            Say(.Connection, .Channel, "$k12$b" & Sender.Split("!")(0) & "$b plays " & ShowCard(Card) & "$k12 and $bgoes out$b!")
                            CallEvent(MyKey, "PlayerOut", {{"game", CurrentGame}, {"player", .CurrentTurn}})
                            Threading.Thread.Sleep(600)
                            Say(.Connection, .Channel, "$k12Play continues with $b" & .Players.Keys(.CurrentTurn) & "$b.")
                        Else
                            Say(.Connection, .Channel, "$k12$b" & Sender.Split("!")(0) & "$b plays " & ShowCard(Card) & "$k12 Play continues with $b" & .Players.Keys(.CurrentTurn) & "$b.")
                        End If
                        Threading.Thread.Sleep(600)
                    ElseIf (Card And 14) = 10 Then
                        ' Skip card, or Reverse card with two players.
                        Dim PlayerToSkip = .NextPlayer
                        Dim PlayerToPlay = .NextPlayer(Times:=2)

                        If GoneOut Then
                            Say(.Connection, .Channel, "$k12$b" & Sender.Split("!")(0) & "$b plays " & ShowCard(Card) & "$k12 and $bgoes out$b!")
                            CallEvent(MyKey, "PlayerOut", {{"game", CurrentGame}, {"player", .CurrentTurn}})
                            Threading.Thread.Sleep(600)
                            Say(.Connection, .Channel, "$k12$b" & .Players.Keys(PlayerToSkip) & " is skipped; play continues with $b" & .Players.Keys(PlayerToPlay) & "$b.")
                        Else
                            Say(.Connection, .Channel, "$k12$b" & Sender.Split("!")(0) & "$b plays " & ShowCard(Card) & "$k12 $b" & .Players.Keys(PlayerToSkip) & "$b is skipped; play continues with $b" & .Players.Keys(PlayerToPlay) & "$b.")
                        End If
                        Threading.Thread.Sleep(600)

                        ' Advance the game so that the player will be skipped.
                        .Advance(Times:=2)
                    Else
                        ' A number card.
                        .Advance()

                        If GoneOut Then
                            Say(.Connection, .Channel, "$k12$b" & Sender.Split("!")(0) & "$b plays " & ShowCard(Card) & "$k12 and $bgoes out$b!")
                            CallEvent(MyKey, "PlayerOut", {{"game", CurrentGame}, {"player", .CurrentTurn}})
                            Threading.Thread.Sleep(600)
                            Say(.Connection, .Channel, "$k12Play continues with $b" & .Players.Keys(.CurrentTurn) & "$b.")
                        Else
                            Say(.Connection, .Channel, "$k12$b" & Sender.Split("!")(0) & "$b plays " & ShowCard(Card) & "$k12 to $b" & .Players.Keys(.CurrentTurn) & "$b.")
                        End If
                        Threading.Thread.Sleep(600)
                    End If

                    .DrawnCard = 255

                    If GoneOut Then
                        If HandBonus Then CountHandPoints(CurrentGame, CurrentGame.CurrentTurn)
                    End If
                    If EndOfGame Then
                        .GameEnded = True
                        EndGame(CurrentGame)
                    Else
                        If HasUNO Then
                            Threading.Thread.Sleep(600)
                            If Not CallEvent(MyKey, "PlayerUNO", {{"game", CurrentGame}, {"player", .CurrentTurn}}) Then _
                                Say(Connection, Channel, "$k13$b" & Sender.Split("!")(0) & "$b has $bUNO$b!")
                        End If
                        If (.WildColour And UNOPlugin.Colour.Pending) = 0 Then ShowHand(CurrentGame, .Players.Keys(.CurrentTurn))
                        .GameTimer.Start() ' Restart the timer.
                        AICheck(CurrentGame)
                    End If
                End With
            End SyncLock
        End If
    End Sub

    <Regex("^dr($|\s)")>
    Public Sub RegexDraw(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As System.Text.RegularExpressions.Match)
        CommandDraw(Connection, Sender, Channel, {Match.Length > 2})
    End Sub
    <Regex("^\x01ACTION draws( a card( from the (stock|deck|pack))?)?(\.|!)?\x01$")>
    Public Sub RegexDraw2(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As System.Text.RegularExpressions.Match)
        If Not Games.ContainsKey(If(Connection IsNot Nothing, Connection.Address & "/", "") & Channel) Then Return
        CommandDraw(Connection, Sender, Channel, {Not Match.Groups(1).Success})
    End Sub
    <Command({"draw"}, 0, 0,
"draw",
"Allows you to draw a card from the deck." & vbCrLf &
"If there are no cards left in the deck, I'll reshuffle the discards to make a new deck.")>
    Public Sub CommandDraw(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim PlayerCanMove As Boolean, PlayerIndex As Integer
        Threading.Thread.Sleep(600)
        Dim CurrentGame As ChannelGame
        Games.TryGetValue(If(Connection IsNot Nothing, Connection.Address & "/", "") & Channel, CurrentGame)
        With CurrentGame
            If CurrentGame Is Nothing Then
                If args.ElementAtOrDefault(0) <> "True" Then Say(Connection, Channel, "$k4There's no game going on right now. Type $k11$cujoin$k4 to start one.") : Return
            ElseIf .IsOpen Then
                If args.ElementAtOrDefault(0) <> "True" Then Say(Connection, Channel, "$k4This game hasn't started yet!") : Return
            ElseIf Not .Players.ContainsKey(Sender.Split("!"c)(0)) Then
                If args.ElementAtOrDefault(0) <> "True" Then Say(Connection, Channel, "$k4" & Sender.Split("!")(0) & ", you're not in this game.") : Return
            ElseIf CurrentGame.Players(Sender.Split("!"c)(0)).CanMove Then
                PlayerCanMove = True
                PlayerIndex = Array.IndexOf(CurrentGame.Players.Keys.ToArray, Sender.Split("!")(0))
                GoTo OK
            ElseIf .Players.Keys(.CurrentTurn) <> Sender.Split("!"c)(0) Then
                If args.ElementAtOrDefault(0) <> "True" Then Say(Connection, Channel, "$k4" & Sender.Split("!")(0) & ", it's not your turn.") : Return
            Else
                PlayerIndex = CurrentGame.CurrentTurn
            End If
            If (.WildColour And UNOPlugin.Colour.Pending) Then
                Say(Connection, Channel, "Please choose a colour for your wild card, " & Sender.Split("!")(0) & ". Enter $k11!colour$o followed by the colour you want.")
                Return
            ElseIf .DrawnCard <> 255 Then
                Say(Connection, Channel, "You've already drawn a card this turn, " & Sender.Split("!")(0) & ". Use $k11!pass$o to end your turn.")
                Return
            End If
OK:
            IdleSkip(CurrentGame, Array.IndexOf(.Players.Keys.ToArray, Sender.Split("!")(0)))

            If .DrawFourChallenger = Sender.Split("!"c)(0) Then
                ' The victim of a Wild Draw Four may enter the draw command if they don't want to challenge it.
                ' We deal the four cards here.
                CurrentGame.GameTimer.Stop()

                DealCards(CurrentGame, Array.IndexOf(.Players.Keys.ToArray, .DrawFourChallenger), 4, False)
                CurrentGame.Advance()
                Say(.Connection, .Channel, "$k12$b" & Sender.Split("!"c)(0) & "$b draws four cards; play continues with $b" & .Players.Keys(.CurrentTurn) & "$b.")
                Threading.Thread.Sleep(600)

                CurrentGame.DrawFourChallenger = Nothing
                CurrentGame.DrawFourUser = Nothing
                ShowHand(CurrentGame, CurrentGame.Players.Keys(CurrentGame.CurrentTurn))

                If TurnTimeLimit > 0 Then CurrentGame.GameTimer.Start()
                AICheck(CurrentGame)
            Else
                If Not CallEvent(MyKey, "PlayerDraw", {{"game", CurrentGame}, {"player", .CurrentTurn}}) Then _
                    Say(Connection, Channel, "$k12$b" & Sender.Split("!")(0) & "$b draws a card.")
                Threading.Thread.Sleep(600)
                .DrawnCard = DealCards(CurrentGame, .CurrentTurn, 1, False)(0)
                .Players.Values(.CurrentTurn).IdleCount = 0
                AICheck(CurrentGame)
            End If
        End With
    End Sub

    <Regex("^pa($|\s)")>
    Public Sub RegexPass(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As System.Text.RegularExpressions.Match)
        CommandPass(Connection, Sender, Channel, {Match.Length > 2})
    End Sub
    <Regex("^\x01ACTION (passes|ends (his |her |their |its |my |the |an? )?turn)(\.|!)?\x01$")>
    Public Sub RegexPass2(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As System.Text.RegularExpressions.Match)
        If Not Games.ContainsKey(If(Connection IsNot Nothing, Connection.Address & "/", "") & Channel) Then Return
        CommandPass(Connection, Sender, Channel, {True})
    End Sub
    <Command({"pass"}, 0, 0,
  "pass",
  "Use this command after you draw a card to end your turn.")>
    Public Sub CommandPass(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Threading.Thread.Sleep(600)
        Dim CurrentGame As ChannelGame = Nothing
        If Not Games.TryGetValue(If(Connection IsNot Nothing, Connection.Address & "/", "") & Channel, CurrentGame) Then
            If args.ElementAtOrDefault(0) <> "True" Then Say(Connection, Channel, "$k4There's no game going on right now. Type $k11$cujoin$k4 to start one.")
        ElseIf CurrentGame.IsOpen Then
            If args.ElementAtOrDefault(0) <> "True" Then Say(Connection, Channel, "$k4This game hasn't started yet!")
        ElseIf Not CurrentGame.Players.ContainsKey(Sender.Split("!"c)(0)) Then
            If args.ElementAtOrDefault(0) <> "True" Then Say(Connection, Channel, "$k4" & Sender.Split("!")(0) & ", you're not in this game.")
        ElseIf CurrentGame.Players.Keys(CurrentGame.CurrentTurn) <> Sender.Split("!"c)(0) Then
            If args.ElementAtOrDefault(0) <> "True" Then Say(Connection, Channel, "$k4" & Sender.Split("!")(0) & ", it's not your turn.")
        ElseIf CurrentGame.DrawnCard = 255 And (CurrentGame.Deck.Count > 0 Or CurrentGame.Discards.Count > 1) Then
            Say(Connection, Channel, "There are still cards in the deck, " & Sender.Split("!")(0) & ". You must draw one before passing.")
        Else
            CurrentGame.GameTimer.Stop()
            CurrentGame.Advance()
            If Not CallEvent(MyKey, "PlayerDraw", {{"game", CurrentGame}, {"player", CurrentGame.CurrentTurn}}) Then _
                Say(CurrentGame.Connection, CurrentGame.Channel, "$k12$b" & Sender.Split("!")(0) & "$b passes to $b" & CurrentGame.Players.Keys(CurrentGame.CurrentTurn) & "$b.")
            Threading.Thread.Sleep(600)
            ShowHand(CurrentGame, CurrentGame.Players.Keys(CurrentGame.CurrentTurn))
            If TurnTimeLimit > 0 Then CurrentGame.GameTimer.Start()
            AICheck(CurrentGame)
        End If
    End Sub
    <Regex("^co (?<Colour>r|y|g|b)")>
    Public Sub RegexColour(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As System.Text.RegularExpressions.Match)
        CommandColour(Connection, Sender, Channel, {Match.Groups("Colour").Value, True})
    End Sub
    <Regex("^\x01ACTION (chooses|makes (his |her |their |its |my |the |an? )?(wild( draw four)? |draw four )?card) (?<Colour>.*)(\.|!)?\x01")>
    Public Sub RegexColour2(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As System.Text.RegularExpressions.Match)
        If Not Games.ContainsKey(If(Connection IsNot Nothing, Connection.Address & "/", "") & Channel) Then Return
        CommandColour(Connection, Sender, Channel, {Match.Groups("Colour").Value, True})
    End Sub
    <Regex("^(?<Colour>red|yellow|green|blue)(\.|!)?$")>
    Public Sub RegexColourNatural(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As System.Text.RegularExpressions.Match)
        Dim CurrentGame As ChannelGame = Nothing
        If Not Games.TryGetValue(If(Connection IsNot Nothing, Connection.Address & "/", "") & Channel, CurrentGame) Then
        ElseIf CurrentGame.IsOpen Then
        ElseIf Not CurrentGame.Players.ContainsKey(Sender.Split("!"c)(0)) Then
        ElseIf CurrentGame.Players.Keys(CurrentGame.CurrentTurn) <> Sender.Split("!"c)(0) Then
        ElseIf (CurrentGame.WildColour And UNOPlugin.Colour.Pending) = 0 Then
        Else
            CommandColour(Connection, Sender, Channel, {Match.Groups("Colour").Value})
        End If
    End Sub
    <Command({"colour", "color"}, 1, 1,
"colour red|yellow|green|blue",
"Changes the colour in play when you play a wild card.")>
    Public Sub CommandColour(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Threading.Thread.Sleep(600)
        Dim CurrentGame As ChannelGame = Nothing
        If Not Games.TryGetValue(If(Connection IsNot Nothing, Connection.Address & "/", "") & Channel, CurrentGame) Then
            If args.ElementAtOrDefault(0) <> "True" Then Say(Connection, Channel, "$k4There's no game going on right now. Type $k11$cujoin$k4 to start one.")
            Return
        ElseIf CurrentGame.IsOpen Then
            If args.ElementAtOrDefault(0) <> "True" Then Say(Connection, Channel, "$k4This game hasn't started yet!")
            Return
        ElseIf Not CurrentGame.Players.ContainsKey(Sender.Split("!"c)(0)) Then
            If Not args.ElementAtOrDefault(0) Or (CurrentGame.WildColour And UNOPlugin.Colour.Pending) > 0 Then Say(Connection, Channel, "$k4" & Sender.Split("!")(0) & ", you're not in this game.")
        ElseIf CurrentGame.Players.Keys(CurrentGame.CurrentTurn) <> Sender.Split("!"c)(0) Then
            If Not args.ElementAtOrDefault(0) Or (CurrentGame.WildColour And UNOPlugin.Colour.Pending) > 0 Then Say(Connection, Channel, "$k4" & Sender.Split("!")(0) & ", it's not your turn.")
        ElseIf (CurrentGame.WildColour And UNOPlugin.Colour.Pending) = 0 Then
            Say(Connection, Channel, "$k4Use that command after you play a wild card, " & Sender.Split("!")(0) & ".")
        Else
            CurrentGame.GameTimer.Stop()
            Select Case args(0).ToLower
                Case "r", "red"
                    CurrentGame.WildColour = 0
                Case "y", "yellow"
                    CurrentGame.WildColour = 1
                Case "g", "green"
                    CurrentGame.WildColour = 2
                Case "b", "blue"
                    CurrentGame.WildColour = 3
                Case Else
                    Return
            End Select

            If CurrentGame.Discards.Peek = 65 And CurrentGame.DrawFourChallenger = Nothing Then CurrentGame.Advance()
            CurrentGame.Advance()

            If CurrentGame.DrawFourChallenger <> Nothing Then
                If Not CallEvent(MyKey, "PlayerDraw", {{"game", CurrentGame}, {"player", CurrentGame.CurrentTurn}, {"colour", {"red", "yellow", "green", "$blue"}(CurrentGame.WildColour)}, {"drawfour", True}}) Then _
                    Say(Connection, Channel, "$k12$b" & Sender.Split("!"c)(0) & "$b chooses " & {"$k4red", "$k8yellow", "$k9green", "$k12blue"}(CurrentGame.WildColour) & "$k12. Now waiting on $b" & CurrentGame.DrawFourChallenger & "$b's response.")
            Else
                If Not CallEvent(MyKey, "PlayerDraw", {{"game", CurrentGame}, {"player", CurrentGame.CurrentTurn}, {"colour", {"red", "yellow", "green", "$blue"}(CurrentGame.WildColour)}, {"drawfour", False}}) Then _
                    Say(Connection, Channel, "$k12$b" & Sender.Split("!"c)(0) & "$b chooses " & {"$k4red", "$k8yellow", "$k9green", "$k12blue"}(CurrentGame.WildColour) & "$k12. Play continues with $b" & CurrentGame.Players.Keys(CurrentGame.CurrentTurn) & "$b.")
            End If

            Threading.Thread.Sleep(600)
            If CurrentGame.DrawFourChallenger = Nothing Then ShowHand(CurrentGame, CurrentGame.Players.Keys(CurrentGame.CurrentTurn))
            If TurnTimeLimit > 0 Then CurrentGame.GameTimer.Start()
            AICheck(CurrentGame)
        End If
    End Sub

    <Command({"challenge"}, 0, 0,
"challenge",
"Allows you to challenge a Wild Draw Four played on you.")>
    Public Sub CommandChallenge(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Threading.Thread.Sleep(600)
        Dim CurrentGame As ChannelGame = Nothing
        If Not Games.TryGetValue(If(Connection IsNot Nothing, Connection.Address & "/", "") & Channel, CurrentGame) Then
            Say(Connection, Channel, "$k4There's no game going on right now. Type $k11$cujoin$k4 to start one.")
            Return
        ElseIf CurrentGame.IsOpen Then
            Say(Connection, Channel, "$k4This game hasn't started yet!")
            Return
        ElseIf Not CurrentGame.Players.ContainsKey(Sender.Split("!"c)(0)) Then
            Say(Connection, Channel, "$k4" & Sender.Split("!")(0) & ", you're not in this game.")
        ElseIf CurrentGame.Players.Keys(CurrentGame.CurrentTurn) <> Sender.Split("!"c)(0) Then
            Say(Connection, Channel, "$k4" & Sender.Split("!")(0) & ", it's not your turn.")
        ElseIf CurrentGame.DrawFourChallenger = Sender.Split("!")(0) Then
            CurrentGame.GameTimer.Stop()
            ' Challenging a Wild Draw Four.
            ' Show the user's hand.
            Dim Message As String = ""
            Dim PlayerHadQuit As Boolean, Hand As List(Of Byte)
            PlayerHadQuit = Not CurrentGame.Players.ContainsKey(CurrentGame.DrawFourUser)
            Hand = If(PlayerHadQuit, CurrentGame.QuitPlayers, CurrentGame.Players)(CurrentGame.DrawFourUser).Hand
            For Each Card In Hand
                Message &= " " & ShowCard(Card)
            Next

            If ShowHandOnChallenge And CurrentGame.DrawFourChallenger <> CurrentGame.Connection.Nickname Then Say(CurrentGame.Connection, Sender.Split("!"c)(0), "$b" & CurrentGame.DrawFourUser & "$b holds:" & Message)
            Threading.Thread.Sleep(1500)

            ' Check the user's hand.
            For Each lCard In Hand
                If (lCard And 112) = CurrentGame.DrawFourBadColour Then
                    ' An invalid card.
                    Say(Connection, Channel, "$k13The challenge succeeds.")
                    Threading.Thread.Sleep(600)

                    If Not PlayerHadQuit Then
                        DealCards(CurrentGame, Array.IndexOf(CurrentGame.Players.Keys.ToArray, CurrentGame.DrawFourUser), 4, False)
                        Say(CurrentGame.Connection, CurrentGame.Channel, "$k12$b" & CurrentGame.DrawFourUser & "$b draws four cards.")
                    End If

                    CurrentGame.DrawFourChallenger = Nothing
                    CurrentGame.DrawFourUser = Nothing

                    AICheck(CurrentGame)
                    Return
                End If
            Next

            Say(Connection, Channel, "$k13The challenge fails.")
            Threading.Thread.Sleep(600)

            DealCards(CurrentGame, CurrentGame.CurrentTurn, 6, False)
            CurrentGame.Advance()
            Say(CurrentGame.Connection, CurrentGame.Channel, "$k12$b" & Sender.Split("!"c)(0) & "$b draws six cards; play continues with $b" & CurrentGame.Players.Keys(CurrentGame.CurrentTurn) & "$b.")

            Threading.Thread.Sleep(600)

            CurrentGame.DrawFourChallenger = Nothing
            CurrentGame.DrawFourUser = Nothing
            ShowHand(CurrentGame, CurrentGame.Players.Keys(CurrentGame.CurrentTurn))

            If TurnTimeLimit > 0 Then CurrentGame.GameTimer.Start()
            AICheck(CurrentGame)
        Else
            Say(Connection, Channel, "$k4There's nothing to challenge.")
        End If
    End Sub

    <Command({"ainudge"}, 0, 0,
"ainudge",
"Reminds me to play. For debugging.",
".debug", CommandAttribute.CommandScope.Channel)>
    Public Sub CommandAINudge(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Threading.Thread.Sleep(600)
        Dim CurrentGame As ChannelGame = Nothing
        If Not Games.TryGetValue(If(Connection IsNot Nothing, Connection.Address & "/", "") & Channel, CurrentGame) Then
            Say(Connection, Channel, "$k4There's no game going on right now. Type $k11$cujoin$k4 to start one.")
            Return
        ElseIf CurrentGame.IsOpen Then
            Say(Connection, Channel, "$k4This game hasn't started yet!")
            Return
        Else
            AICheck(CurrentGame)
        End If
    End Sub
    Friend Sub AICheck(ByVal Game As ChannelGame)
        Dim PlayerIndex As Integer, PlayerCanMove As Boolean
        If Game.Players.Keys(Game.CurrentTurn) = Game.Connection.Nickname Then
            PlayerIndex = Game.CurrentTurn
Play:
            ' It is the bot's turn.
            If (Game.WildColour And UNOPlugin.Colour.Pending) Then
                ' We need to choose a colour for a wild card.
                ' We'll choose the colour that we have the most cards of.
                Dim ColourCount() As Integer = {0, 0, 0, 0}
                For Each card In Game.Players.Values(PlayerIndex).Hand
                    If (card And 64) = 0 Then ColourCount((card And 48) >> 4) += 1
                Next

                'Threading.Thread.Sleep(600)
                If ColourCount.SequenceEqual({0, 0, 0, 0}) Then
                    CommandColour(Game.Connection, Game.Connection.Nickname & "!*@*", Game.Channel, {Choose("red", "yellow", "green", "blue")})
                Else
                    Dim DominantColour As Short = 0
                    For i = 1 To 3
                        If ColourCount(i) > ColourCount(0) Then DominantColour = i
                    Next
                    CommandColour(Game.Connection, Game.Connection.Nickname & "!*@*", Game.Channel, {{"red", "yellow", "green", "blue"}(DominantColour)})
                End If
            ElseIf Game.DrawFourChallenger = Game.Connection.Nickname Then
                ' Someone has played a Draw Four on the bot.
                Dim cRating As Short = 0D
                cRating += (5 - Game.Players.Values(PlayerIndex).Hand.Count) * 2
                cRating += Game.Players(Game.DrawFourUser).Hand.Count - 3
                If Game.WildColour = Game.DrawFourBadColour Then cRating += 8

                If cRating >= 6 And Rnd() < (-0.2 + cRating * 0.08) Then
                    Say(Game.Connection, Game.Channel, "$k13$bI$b challenge the Wild Draw Four.")
                    CommandChallenge(Game.Connection, Game.Connection.Nickname & "!*@*", Game.Channel, {})
                Else
                    CommandDraw(Game.Connection, Game.Connection.Nickname & "!*@*", Game.Channel, {})
                End If
            ElseIf Game.DrawnCard <> 255 Then
                ' We've already drawn a card this turn.
                ' Make sure that card is valid.
                If Game.DrawnCard = 65 Then
                    ' You can't play a Wild Draw Four if you have a card in your hand of the same colour as the up-card.
                    If WildDrawFour = 0 Or (WildDrawFour = 1 And Rnd() < 0.35) Then
                        For Each lCard In Game.Players.Values(PlayerIndex).Hand
                            If (lCard And 112) = If(Game.Discards.Peek And 64, Game.WildColour << 4, Game.Discards.Peek And 48) Then
                                'Threading.Thread.Sleep(600)
                                CommandPass(Game.Connection, Game.Connection.Nickname & "!*@*", Game.Channel, {})
                                Return
                            End If
                        Next
                    End If
                ElseIf (Game.DrawnCard And 64) = 0 Then
                    ' If it's not a Wild card, the card you play must be the same colour or the same symbol as the up-card.
                    ' Wild cards are always valid except in the case checked above.
                    If (Game.DrawnCard And 48) <> If(Game.Discards.Peek And 64, Game.WildColour << 4, Game.Discards.Peek And 48) And
                        If(Game.Discards.Peek And 64, True, (Game.DrawnCard And 15) <> (Game.Discards.Peek And 15)) Then
                        'Threading.Thread.Sleep(1000)
                        CommandPass(Game.Connection, Game.Connection.Nickname & "!*@*", Game.Channel, {})
                        Return
                    End If
                End If

                'Threading.Thread.Sleep(1000)
                If (Game.DrawnCard And 64) <> 0 Then
                    CommandPlay(Game.Connection, Game.Connection.Nickname & "!*@*", Game.Channel, {"wild" & If(Game.DrawnCard = 65, " draw four", "")})
                Else
                    CommandPlay(Game.Connection, Game.Connection.Nickname & "!*@*", Game.Channel, {{"red", "yellow", "green", "blue"}((Game.DrawnCard And 48) >> 4) & {"0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "Reverse", "Skip", "Draw Two"}(Game.DrawnCard And 15)})
                End If
            Else
                ' We need to play a card or draw.
                Dim PlayableCards As New List(Of Byte)

                For Each card In Game.Players.Values(PlayerIndex).Hand
                    ' Make sure that card is valid.
                    If card = 65 AndAlso (WildDrawFour = 0 Or (WildDrawFour = 1 And Rnd() < 0.1)) Then
                        ' You can't play a Wild Draw Four if you have a card in your hand of the same colour as the up-card.
                        If Rnd() < 0.1 Then
                            For Each lCard In Game.Players.Values(PlayerIndex).Hand
                                If (lCard And 112) = If(Game.Discards.Peek And 64, Game.WildColour << 4, Game.Discards.Peek And 48) Then
                                    GoTo NextCard
                                End If
                            Next
                        End If
                    ElseIf (card And 64) = 0 Then
                        ' If it's not a Wild card, the card you play must be the same colour or the same symbol as the up-card.
                        ' Wild cards are always valid except in the case checked above.
                        If (Game.Discards.Peek And 64 And Game.WildColour = UNOPlugin.Colour.None) Then
                        ElseIf (card And 48) <> If(Game.Discards.Peek And 64, Game.WildColour << 4, Game.Discards.Peek And 48) And
                                                        If(Game.Discards.Peek And 64, True, (card And 15) <> (Game.Discards.Peek And 15)) Then
                            Continue For
                        End If
                    End If

                    PlayableCards.Add(card)
NextCard:
                Next

                If PlayableCards.Count = 0 Then
                    ' No playable cards; we have to draw.
                    'Threading.Thread.Sleep(1000)
                    CommandDraw(Game.Connection, Game.Connection.Nickname & "!*@*", Game.Channel, {})
                Else
                    ' Play a random playable card.
                    Randomize()
                    Dim SelectedCard = PlayableCards(Int(Rnd() * PlayableCards.Count))
                    'Threading.Thread.Sleep(1000)
                    If (SelectedCard And 64) <> 0 Then
                        CommandPlay(Game.Connection, Game.Connection.Nickname & "!*@*", Game.Channel, {"wild" & If(SelectedCard = 65, " draw four", "")})
                    Else
                        CommandPlay(Game.Connection, Game.Connection.Nickname & "!*@*", Game.Channel, {{"red", "yellow", "green", "blue"}((SelectedCard And 48) >> 4) & {"0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "Reverse", "Skip", "Draw Two"}(SelectedCard And 15)})
                    End If
                End If
            End If
            Return
        End If
        If Game.Players.ContainsKey(Game.Connection.Nickname) AndAlso Game.Players(Game.Connection.Nickname).CanMove Then
            PlayerCanMove = True
            PlayerIndex = Array.IndexOf(Game.Players.Keys.ToArray, Game.Connection.Nickname)
            GoTo Play
        End If
    End Sub

    <Regex("^tu($|\s)")>
    Public Sub RegexTurn(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As System.Text.RegularExpressions.Match)
        CommandTurn(Connection, Sender, Channel, {Match.Length > 2})
    End Sub
    <Command({"turn"}, 0, 0,
 "turn",
 "Reminds you whose turn it is.")>
    Public Sub CommandTurn(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Threading.Thread.Sleep(600)
        Dim CurrentGame As ChannelGame = Nothing
        If Not Games.TryGetValue(If(Connection IsNot Nothing, Connection.Address & "/", "") & Channel, CurrentGame) Then
            If args.ElementAtOrDefault(0) <> "True" Then Say(Connection, Channel, "$k4There's no game going on right now.")
        ElseIf CurrentGame.IsOpen Then
            If args.ElementAtOrDefault(0) <> "True" Then Say(Connection, Channel, "$k4This game hasn't started yet!")
        ElseIf CurrentGame.Players.Keys(CurrentGame.CurrentTurn) = Sender.Split("!"c)(0) Then
            Say(Connection, Channel, Choose(String.Format(Choose("{0}, {1}.", "{1}, {0}"), Choose("it is ", "it's ") & "your turn", Chr(2) & Sender.Split("!"c)(0) & Chr(2)), Choose("It's ", "It is ") & Choose("", "currently ") & Chr(2) & CurrentGame.Players.Keys(CurrentGame.CurrentTurn) & "$b's turn."), SayOptions.Capitalise)
        Else
            Say(Connection, Channel, Choose("It's ", "It is ") & Choose("", "currently ") & Chr(2) & CurrentGame.Players.Keys(CurrentGame.CurrentTurn) & "$b's turn.")
        End If
    End Sub

    <Regex("^cd($|\s)")>
    Public Sub RegexUpCard(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As System.Text.RegularExpressions.Match)
        CommandUpCard(Connection, Sender, Channel, {Match.Length > 2})
    End Sub
    <Command({"upcard", "up-card", "topcard", "discard", "card"}, 0, 0,
 "upcard",
 "Shows you the current up-card - that is, the most recent discard.")>
    Public Sub CommandUpCard(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Threading.Thread.Sleep(600)
        Dim CurrentGame As ChannelGame = Nothing
        If Not Games.TryGetValue(If(Connection IsNot Nothing, Connection.Address & "/", "") & Channel, CurrentGame) Then
            If args.ElementAtOrDefault(0) <> "True" Then Say(Connection, Channel, "$k4There's no game going on right now.")
        ElseIf CurrentGame.IsOpen Then
            If args.ElementAtOrDefault(0) <> "True" Then Say(Connection, Channel, "$k4This game hasn't started yet!")
        Else
            Say(Connection, Channel, Choose("The current up-card is: ", "The last discard was: ") & ShowCard(CurrentGame.Discards.Peek))
            If (CurrentGame.Discards.Peek And 64) Then
                If (CurrentGame.WildColour And UNOPlugin.Colour.Pending) Then
                    Say(Connection, Channel, Choose("A colour hasn't been chosen yet."))
                ElseIf (CurrentGame.WildColour And UNOPlugin.Colour.None) Then
                    Say(Connection, Channel, Choose("No colour was chosen. You may play any card."))
                Else
                    Say(Connection, Channel, Choose("The colour chosen was " & {"$k4red", "$k8yellow", "$k9green", "$k12blue"}(CurrentGame.WildColour And 3) & "$o."))
                End If
            End If
        End If
    End Sub

    <Regex("^ca($|\s)")>
    Public Sub RegexHand(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As System.Text.RegularExpressions.Match)
        CommandHand(Connection, Sender, Channel, {Match.Length > 2})
    End Sub
    <Command({"hand", "cards"}, 0, 0,
"hand",
"Shows you your hand.")>
    Public Sub CommandHand(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Threading.Thread.Sleep(600)
        Dim CurrentGame As ChannelGame = Nothing
        If Not Games.TryGetValue(If(Connection IsNot Nothing, Connection.Address & "/", "") & Channel, CurrentGame) Then
            If args.ElementAtOrDefault(0) <> "True" Then Say(Connection, Channel, "$k4There's no game going on right now. Type $k11$cujoin$k4 to start one.")
        ElseIf CurrentGame.IsOpen Then
            If args.ElementAtOrDefault(0) <> "True" Then Say(Connection, Channel, "$k4This game hasn't started yet!")
        ElseIf Not CurrentGame.Players.ContainsKey(Sender.Split("!"c)(0)) Then
            If args.ElementAtOrDefault(0) <> "True" Then Say(Connection, Channel, "$k4" & Sender.Split("!")(0) & ", you're not in this game.")
        Else
            ShowHand(CurrentGame, Sender.Split("!"c)(0))
        End If
    End Sub

    <Regex("^ct($|\s)")>
    Public Sub RegexCount(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As System.Text.RegularExpressions.Match)
        CommandCount(Connection, Sender, Channel, {Match.Length > 2})
    End Sub
    <Command({"count", "cardscount", "handcount"}, 0, 0,
 "count",
 "Shows you the number of cards in each player's hand.")>
    Public Sub CommandCount(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Threading.Thread.Sleep(600)
        Dim CurrentGame As ChannelGame = Nothing
        If Not Games.TryGetValue(If(Connection IsNot Nothing, Connection.Address & "/", "") & Channel, CurrentGame) Then
            If args.ElementAtOrDefault(0) <> "True" Then Say(Connection, Channel, "$k4There's no game going on right now.")
        ElseIf CurrentGame.IsOpen Then
            If args.ElementAtOrDefault(0) <> "True" Then Say(Connection, Channel, "$k4This game hasn't started yet!")
        Else
            Dim Message As String = ""
            For i = 0 To CurrentGame.Players.Count - 1
                Message &= If(Message = "", "$b", " $k15| $b") & NicknameColour(CurrentGame.Players.Keys(i)) & CurrentGame.Players.Keys(i) & "$o "
                Select Case CurrentGame.Players.Values(i).Hand.Count
                    Case 0
                        Message &= "$k15is out"
                    Case 1
                        Message &= "$k4has UNO"
                    Case Else
                        Message &= "holds $b" & CurrentGame.Players.Values(i).Hand.Count & "$b cards"
                End Select
                If i Mod 4 = 3 Then
                    Say(Connection, Channel, Message)
                    Message = ""
                End If
            Next
            Say(Connection, Channel, Message)
        End If
    End Sub

    Public Sub IdleCheck(ByVal sender As Object, ByVal e As Timers.ElapsedEventArgs)
        ' Find out what game, what connection and what channel this is for.
        Dim CurrentGame As ChannelGame = Nothing
        For Each Game In Games
            If Game.Value.GameTimer Is sender Then
                CurrentGame = Game.Value
                Exit For
            End If
        Next
        If IsNothing(CurrentGame) Then Return

        With CurrentGame
            If .IdleTurn = -1 Then .IdleTurn = .CurrentTurn
            ' Remove the player if they left the channel. 
            If .Connection IsNot Nothing AndAlso Not .Connection.Channels(.Channel).Users.ContainsKey(.Players.Keys(.IdleTurn)) AndAlso Now - .Players.Values(.IdleTurn).LeftAt >= TimeSpan.FromMinutes(1) Then
                Say(.Connection, .Channel, "$k15$b" & .Players.Keys(.IdleTurn) & "$b$k12 left the channel, and was removed from the game.")
                RemovePlayer(CurrentGame, .Players.Keys(.IdleTurn))
            ElseIf TurnTimeLimit > 0 Then
                .Players.Values(.IdleTurn).IdleCount += 1
                If .Players.Values(.IdleTurn).IdleCount >= 2 Then
                    Say(.Connection, .Channel, Chr(2) & .Players.Keys(.IdleTurn) & "$b has timed out twice in a row, and is removed from the game. :-(")
                    RemovePlayer(CurrentGame, .Players.Keys(.IdleTurn))
                Else
                    .GameTimer.Stop()

                    Say(.Connection, .Channel, Chr(2) & .Players.Keys(.IdleTurn) & "$b " & Choose("has been idle for too long", "is taking too long") & ".")
                    If .LongTimer Then
                        RemovePlayer(CurrentGame, .Players.Keys(.IdleTurn))
                        Return
                    End If

                    Dim NextPlayer = .IdleTurn

                    Do
                        If .IsReversed Then
                            If NextPlayer <= 0 Then NextPlayer = .Players.Count - 1 Else NextPlayer -= 1
                        Else
                            If NextPlayer >= .Players.Count - 1 Then NextPlayer = 0 Else NextPlayer += 1
                        End If
                        ' Have we looped through everyone?
                        If NextPlayer = .CurrentTurn Then
                            ' Cancel the game.
                            Games.Remove(If(.Connection IsNot Nothing, .Connection.Address & "/", "") & .Channel)
                            Say(.Connection, .Channel, "$k13The game has been cancelled.")
                            Return
                        End If

                        If .Players.Values(NextPlayer).Hand.Count = 0 Or .QuitPlayers.ContainsKey(.Players.Keys(NextPlayer)) Then Continue Do
                        If .Players.Values(NextPlayer).CanMove Then Continue Do
                        .Players.Values(NextPlayer).CanMove = True
                        .IdleTurn = NextPlayer
                        Exit Do
                    Loop

                    Say(.Connection, .Channel, "$k12$b" & .Players.Keys(NextPlayer) & Choose("$b ", "$b, you ") & "may play now.")
                    .GameTimer.Start()
                    AICheck(CurrentGame)
                End If
            End If
        End With
    End Sub

    Public Sub IdleSkip(ByVal Game As ChannelGame, ByVal SkipTo As Integer)
        With Game
            ' Skip everyone up to the target player.
            Do
                If .CurrentTurn = SkipTo Then Exit Do

                If .DrawFourChallenger = .Players.Keys(.CurrentTurn).Split("!"c)(0) Then
                    Say(.Connection, .Channel, "$k12" & .Players.Keys(.CurrentTurn) & " takes four cards.")
                    DealCards(Game, .CurrentTurn, 4, False)
                    .DrawFourChallenger = Nothing
                    .DrawFourUser = Nothing
                ElseIf .DrawnCard <> 255 Then
                    .DrawnCard = 255
                ElseIf (.WildColour And Colour.Pending) Then
                    .WildColour = .WildColour Xor Colour.Pending
                Else
                    Say(.Connection, .Channel, "$k12" & .Players.Keys(.CurrentTurn) & " takes one card.")
                    DealCards(Game, .CurrentTurn, 1, False)
                End If

                .Players.Values(.CurrentTurn).CanMove = False
                .Advance()
            Loop
        End With
    End Sub

    ''' <summary>
    ''' Awards points to a player based on the other players' hands.
    ''' </summary>
    ''' <param name="Game">The game to work with.</param>
    ''' <param name="iPlayer">The index of the player to award points to.</param>
    ''' <remarks></remarks>
    Public Sub CountHandPoints(ByVal Game As ChannelGame, ByVal iPlayer As Integer)
        Dim NumberOfCards As Integer
        For Each Player In Game.Players.Concat(Game.QuitPlayers)
            If Player.Value Is Game.Players.Values(iPlayer) Then Continue For
            Game.Players.Values(iPlayer).HandPoints += GetHandTotal(Player.Value.Hand)
            NumberOfCards += Player.Value.Hand.Count
        Next
        Game.Players.Values(iPlayer).MultipleCards = Game.Players.Values(iPlayer).MultipleCards Or NumberOfCards > 1
    End Sub

    Public Function GetHandTotal(ByVal Hand As List(Of Byte)) As Integer
        GetHandTotal = 0
        For Each Card In Hand
            If (Card And 64) Then
                GetHandTotal += 50
            ElseIf (Card And 15) >= 10 Then
                GetHandTotal += 20
            Else
                GetHandTotal += (Card And 15)
            End If
        Next
    End Function

    ''' <summary>
    ''' Handles the end of a game. This involves the scoring, and other procedures.
    ''' </summary>
    ''' <param name="Game"></param>
    ''' <remarks></remarks>
    Public Sub EndGame(ByVal Game As ChannelGame)
        With Game
            Dim AllPlayers = .Players.Concat(.QuitPlayers)

            For Each Player In .Players
                If Player.Value.Position = 0 Then Player.Value.Position = AllPlayers.Count
            Next

            Threading.Thread.Sleep(2000)

            ' End the game here.
            Say(.Connection, .Channel, "$k13This game is finished.")
            Threading.Thread.Sleep(2000)

            ' Show the other players' hands.
            For Each Player In AllPlayers
                If Player.Value.Hand.Count = 0 Then Continue For

                Dim Message As String = ""
                For Each Card In Player.Value.Hand
                    Message &= " " & ShowCard(Card)
                Next

                Dim HandTotal = GetHandTotal(Player.Value.Hand)
                Say(.Connection, .Channel, "$b" & Player.Key & "$b still held:" & Message & If(HandBonus, String.Format("$o : $b{0}$b " & If(HandTotal = 1, "point", "points"), HandTotal), ""))

                ' Take points from their Challenge score.
                If Game.GameEnded And HandBonus Then Player.Value.HandPoints -= HandTotal

                Threading.Thread.Sleep(600)
            Next

            ' Award points.
            For Each Player In AllPlayers
                ' Victory bonus
                If VictoryBonus Then
                    If Player.Value.Position > 0 Then
                        If Player.Value.Position >= AllPlayers.Count And Not VictoryBonusLastPlace Then
                        ElseIf Player.Value.Position <= VictoryBonusValue.Length Then
                            Player.Value.BasePoints += VictoryBonusValue(Player.Value.Position - 1)
                        ElseIf Player.Value.Position < AllPlayers.Count And VictoryBonusRepeat Then
                            Player.Value.BasePoints += VictoryBonusValue(UBound(VictoryBonusValue))
                        End If
                    End If
                End If

                ' Participation bonus
                Player.Value.BasePoints += ParticipationBonus

                ' Leaving penalty
                If Player.Value.Position = 0 Then Player.Value.BasePoints -= QuitPenalty

                ' Total points
                Dim TotalPoints = Player.Value.BasePoints + Math.Max(Player.Value.HandPoints, 0)
                Dim ChallengePoints = Player.Value.BasePoints + Player.Value.HandPoints

                Dim stats = GetStats(CurrentStats, .Connection, .Channel, Player.Key, True)
                stats.Points += TotalPoints
                stats.ChallengePoints += ChallengePoints
                If Player.Value.Position = 1 Then stats.Wins += 1
                If AllOut And (Player.Value.Position = Players.Count Or Player.Value.Position = 0) Then stats.Losses += 1
                If Not AllOut And Player.Value.Position <> 1 Then stats.Losses += 1

                Dim astats = GetStats(AllTimeStats, .Connection, .Channel, Player.Key, True)
                astats.Points += TotalPoints
                astats.ChallengePoints += ChallengePoints
                If Player.Value.Position = 1 Then astats.Wins += 1
                If AllOut And (Player.Value.Position = Players.Count Or Player.Value.Position = 0) Then astats.Losses += 1
                If Not AllOut And Player.Value.Position <> 1 Then astats.Losses += 1

                If TotalPoints > 0 Then
                    If Player.Value.BasePoints = 0 Then
                        ' Hand bonus only.
                        Say(Game.Connection, Game.Channel, String.Format("$k12$b{0}$b takes{2} $b{1}$b {3}.", Player.Key, TotalPoints, If(Player.Value.MultipleCards, " a total of", ""), If(TotalPoints > 1, "points", "point")))
                    ElseIf Player.Value.HandPoints = 0 Then
                        ' Position bonus only.
                        Say(Game.Connection, Game.Channel, String.Format("$k12$b{0}$b takes $b{1}$b {2}.", Player.Key, TotalPoints, If(TotalPoints > 1, "points", "point")))
                    Else
                        Say(Game.Connection, Game.Channel, String.Format("$k12$b{0}$b takes a total of $b{1}$b {2}.", Player.Key, TotalPoints, If(TotalPoints > 1, "points", "point")))
                    End If
                ElseIf TotalPoints < 0 Then
                    Say(Game.Connection, Game.Channel, String.Format("$k12$b{0}$b must lose $b{1}$b {2}...", Player.Key, -TotalPoints, If(TotalPoints < -1, "points", "point")))
                ElseIf Player.Value.Position < AllPlayers.Count And Player.Value.Position <> 0 And Player.Value.HandPoints = 0 Then
                    Say(Game.Connection, Game.Channel, "$k12Aww... $b" & Player.Key & "$b didn't take any points.")
                End If
            Next

            ' Check the single-round record.
            For Each Player In AllPlayers
                Dim TotalPoints = Player.Value.BasePoints + Math.Max(Player.Value.HandPoints, 0)
                Dim stats = GetStats(CurrentStats, .Connection, .Channel, Player.Key, True)
                Dim astats = GetStats(AllTimeStats, .Connection, .Channel, Player.Key, True)

                If TotalPoints > stats.RecordPoints Then
                    stats.RecordPoints = TotalPoints
                End If
                If TotalPoints > astats.RecordPoints Then
                    astats.RecordPoints = TotalPoints
                    Threading.Thread.Sleep(600)
                    Say(Game.Connection, Game.Channel, "$k13That's a new record for $b" & Player.Key & "$b!")
                End If

                Threading.Thread.Sleep(600)
            Next

            ' TODO: Let them know if their rank changes.

            ' Check the streak.
            For Each Player In AllPlayers
                If Player.Value.Position = 1 Then
                    StreakWin(Game, Player.Key)
                ElseIf Player.Value.Position = 0 Or Player.Value.Position = AllPlayers.Count Then
                    StreakLoss(Game, Player.Key)
                End If
                If Player.Value.StreakMessage <> Nothing Then Say(Game.Connection, Game.Channel, Player.Value.StreakMessage)
            Next

            ' Remove the game.
            Games.Remove(If(.Connection IsNot Nothing, .Connection.Address & "/", "") & .Channel)
            ' Autosave the scores, in case the bot dies.
            OnSave()
            StartResetTimer()
        End With
    End Sub

#If Debug = True Then
    <Command({"ud4c", "ugimme"}, 0, 1,
"ud4c",
"Gives you a Wild Draw Four. If you're not a developer, you shouldn't be seeing this...")>
    Public Sub CommandDrawFourCheat(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim CurrentGame As ChannelGame = Nothing
        If Not Games.TryGetValue(If(Connection IsNot Nothing, Connection.Address & "/", "") & Channel, CurrentGame) Then
            Reply(Connection, Channel, Sender, "$bThwarted!$b There's no game going on at the moment.")
            Return
        ElseIf CurrentGame.IsOpen Then
            Reply(Connection, Channel, Sender, "$bThwarted!$b The game hasn't started yet.")
            Return
        ElseIf Not CurrentGame.Players.ContainsKey(Sender.Split("!"c)(0)) Then
            Reply(Connection, Channel, Sender, "$bThwarted!$b You're not in this game.")
        Else
            CurrentGame.Players(Sender.Split("!"c)(0)).Hand.Add(If(args.ElementAtOrDefault(0), 65))
        End If
    End Sub

    <Command({"ud4cl", "uclear"}, 0, 1,
"ud4cl",
"Removes all your cards. If you're not a developer, you shouldn't be seeing this...")>
    Public Sub CommandClear(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim CurrentGame As ChannelGame = Nothing
        If Not Games.TryGetValue(If(Connection IsNot Nothing, Connection.Address & "/", "") & Channel, CurrentGame) Then
            Reply(Connection, Channel, Sender, "$bThwarted!$b There's no game going on at the moment.")
            Return
        ElseIf CurrentGame.IsOpen Then
            Reply(Connection, Channel, Sender, "$bThwarted!$b The game hasn't started yet.")
            Return
        ElseIf Not CurrentGame.Players.ContainsKey(Sender.Split("!"c)(0)) Then
            Reply(Connection, Channel, Sender, "$bThwarted!$b You're not in this game.")
        Else
            CurrentGame.Players(Sender.Split("!"c)(0)).Hand.Clear()
        End If
    End Sub
#End If
#End Region

#Region "Statistics"
    'Public Sub AddScore(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal IrcNickname As String, ByVal Score As Integer)
    '    Dim Username As String, NewScore As Integer

    '    ' Check if this player is logged in. If they are, associate the score with their account.
    '    'Username = "~" & IrcNickname
    '    'If IrcNickname = Connection.Nickname Then
    '    Username = IrcNickname
    '    'ElseIf Connection Is Nothing Then
    '    'If Identifications.ContainsKey(Channel.Split({"/"c}, 3)(0) & "/" & IrcNickname) Then
    '    '    Username = Identifications(Channel.Split({"/"c}, 3)(0) & "/" & IrcNickname).AccountName
    '    'End If
    '    'Else
    '    'If Identifications.ContainsKey(Connection.Address & "/" & IrcNickname) Then
    '    '    Username = Identifications(Connection.Address & "/" & IrcNickname).AccountName
    '    'End If
    '    'End If

    '    If Scores.ContainsKey(Username) Then
    '        NewScore = Scores(Username) + Score
    '        Scores.Remove(Username)
    '    Else
    '        NewScore = Score
    '    End If

    '    Scores.Add(Username, NewScore)

    '    If ScoresPeriodEnd = Nothing Then
    '        ScoresPeriodEnd = Date.UtcNow.Date.Add(New TimeSpan(14, 8, 0, 0))
    '        'ScoresPeriodEnd = Date.UtcNow.Add(New TimeSpan(0, 2, 0, 0))
    '        ScoreResetTimer.Interval = 3600000
    '    End If
    '    If Not ScoreResetTimer.Enabled Then ScoreResetTimer.Enabled = True
    '    ScoresResetTimer_Elapsed(Nothing, Nothing)
    'End Sub

    Public Sub StreakWin(ByVal Game As ChannelGame, ByVal Nickname As String)
        Dim Streak As Short = 0
        If Not CurrentStreak.TryGetValue(Nickname, Streak) Then CurrentStreak.Add(Nickname, 0)

        If Streak < 0 Then
            ' A losing streak has been broken.
            If Streak <= -2 Then Game.Players(Nickname).StreakMessage = String.Format("$k13$b{0}$b has broken their $b{1}$b-loss streak.", Nickname, -Streak)
            Streak = 1
        ElseIf Streak < Short.MaxValue Then
            Streak += 1

            Select Case Streak
                Case 3
                    Game.Players(Nickname).StreakMessage = String.Format("$k13$b{0}$b has won $b{1}$b games in a row!", Nickname, Streak)
                Case 6
                    Game.Players(Nickname).StreakMessage = String.Format("$k13$b{0}$b has won $b{1}$b games in a row!", Nickname, Streak)
                Case 10
                    Game.Players(Nickname).StreakMessage = String.Format("$k13$b{0}$b has won $b{1}$b games in a row!", Nickname, Streak)
            End Select
        End If
        CurrentStreak(Nickname) = Streak
    End Sub

    Public Sub StreakLoss(ByVal Game As ChannelGame, ByVal Nickname As String)
        Dim Streak As Short = 0, lBestStreak As Short = 0
        If Not CurrentStreak.TryGetValue(Nickname, Streak) Then CurrentStreak.Add(Nickname, 0)

        If Streak > 0 Then
            ' A losing streak has been broken.
            If Streak >= 2 Then
                If Game.Players.ContainsKey(Nickname) Then Game.Players(Nickname).StreakMessage = String.Format("$k13$b{0}$b's $b{1}$b-win streak has ended.", Nickname, Streak)
                If Game.QuitPlayers.ContainsKey(Nickname) Then Game.QuitPlayers(Nickname).StreakMessage = String.Format("$k13$b{0}$b's $b{1}$b-win streak has ended.", Nickname, Streak)
            End If
            If Not BestStreak.TryGetValue(Nickname, lBestStreak) Then BestStreak.Add(Nickname, 0)
            If Streak > lBestStreak Then
                BestStreak(Nickname) = Streak
            End If
            Streak = -1
        ElseIf Streak > Short.MinValue Then
            Streak -= 1
        End If
        CurrentStreak(Nickname) = Streak
    End Sub

    ''' <summary>
    ''' Retrieves a player's entry in the statistics list.
    ''' </summary>
    ''' <param name="Stats">Which list to search.</param>
    ''' <param name="Connection">The IRC connection the player is on.</param>
    ''' <param name="Channel">The channel the game is in.</param>
    ''' <param name="IrcNickname">The player's IRC nickname.</param>
    ''' <param name="Add">If set to True, a new entry may be created in the list. If set to False, the function will return Nothing if the player has no record.</param>
    ''' <returns>The player's record, or Nothing if they don't have one.</returns>
    Public Function GetStats(ByVal Stats As Dictionary(Of String, PlayerStats), ByVal Connection As IRCConnection, ByVal Channel As String, ByVal IrcNickname As String, Optional ByVal Add As Boolean = False) As PlayerStats
        Dim Username As String

        Username = IrcNickname

        If Stats.ContainsKey(Username) Then
            Return Stats(Username)
        ElseIf Add Then
            Dim newRecord = New PlayerStats With {.Name = Username}
            Stats.Add(Username, newRecord)
            Return newRecord
        Else
            Return Nothing
        End If
    End Function

    ''' <summary>
    ''' Starts the timer to reset the stats.
    ''' If the timer isn't already set, it will be set to 8:00 AM UTC on the day fourteen days after this procedure is called.
    ''' </summary>
    Public Sub StartResetTimer()
        If ScoresPeriodEnd = Nothing Then
            ScoresPeriodEnd = Date.UtcNow.Date.Add(New TimeSpan(14, 8, 0, 0))
            'ScoresPeriodEnd = Date.UtcNow.Add(New TimeSpan(0, 2, 0, 0))
            ScoreResetTimer.Interval = 3600000
        End If
        If Not ScoreResetTimer.Enabled Then ScoreResetTimer.Enabled = True
        ScoresResetTimer_Elapsed(Nothing, Nothing)
    End Sub

    <Command({"uscore", "urank"}, 0, 1,
"uscore [name]",
"Shows you a player's (default your own) total score.")>
    Public Sub CommandScore(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim TargetNickname As String = If(args.ElementAtOrDefault(0), Sender.Split("!"c)(0))
        Dim stats = GetStats(CurrentStats, Nothing, Nothing, TargetNickname)

        If stats Is Nothing Then
            Say(Connection, Channel, "$b" & TargetNickname & "$b hasn't played a game yet.")
        Else
            Dim Result = GetRank(stats.Name, CurrentStats)
            Say(Connection, Channel, String.Format("$b{0}$b " & If(Result(2), "is currently " & Choose("tying ", "tied ") & "for {1} " & Choose("place", "position"), Choose("is ranked {1}", "is in {1} " & Choose("place", "position"))) & ", with $b{2}$b points.", stats.Name, Result(1), stats.Points.ToString("N0")))
        End If
    End Sub
    <Command({"uscorelast", "uranklast", "uscoreprev", "urankprev", "ulastscore", "ulastrank", "uprevscore", "uprevrank"}, 0, 1,
"uscorelast [name]",
"Shows you a player's (default your own) total score last period.")>
    Public Sub CommandScoreLast(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim TargetNickname As String = If(args.ElementAtOrDefault(0), Sender.Split("!"c)(0))
        Dim stats = GetStats(LastStats, Nothing, Nothing, TargetNickname)

        If LastStats.Count = 0 Then
            Say(Connection, Channel, "We haven't had a complete scoring period yet.")
            Return
        End If

        If stats Is Nothing Then
            Say(Connection, Channel, "$b" & TargetNickname & "$b did not win any points in the last period.")
        Else
            Dim Result = GetRank(stats.Name, LastStats)
            Say(Connection, Channel, String.Format("$b{0}$b " & If(Result(2), "tied for {1} " & Choose("place", "position"), Choose("was ranked {1}", "was in {1} " & Choose("place", "position"))) & " last period, with $b{2}$b points.", stats.Name, Result(1), stats.Points.ToString("N0")))
        End If
    End Sub

    Private Function GetRank(ByVal PlayerName As String, ByVal Scores As Dictionary(Of String, PlayerStats)) As Object()
        If Not Scores.ContainsKey(PlayerName) Then Return Nothing

        Dim Rank As Integer = 1, RankString As String, IsTied As Boolean, Score As Integer = Scores(PlayerName).Points

        For Each Player In Scores.Values
            If Player.Name <> PlayerName Then
                If Player.Points > Score Then
                    Rank += 1
                ElseIf Player.Points = Score Then
                    IsTied = True
                End If
            End If
        Next

        RankString = GetRankString(Rank)

        Return {Rank, RankString, IsTied}
    End Function

    Private Function GetRankString(ByVal Rank As Integer) As String
        GetRankString = Rank
        If (Rank Mod 100) \ 10 = 1 Then
            GetRankString &= "th"
        ElseIf Rank Mod 10 = 1 Then
            GetRankString &= "st"
        ElseIf Rank Mod 10 = 2 Then
            GetRankString &= "nd"
        ElseIf Rank Mod 10 = 3 Then
            GetRankString &= "rd"
        Else
            GetRankString &= "th"
        End If

    End Function

    <Command({"utop", "utop10", "unotop10", "uleader", "uleaderboard", "utops", "utopscores", "uleaders", "uranking", "urankings", "utoprankings"}, 0, 32767,
"utop [top|nearme] [current|last|alltime] [total|challenge|wins|plays]",
"Shows you the top 10 total scores. If you specify 'near me', you'll see entries near yourself if you haven't quite made the top 10.")>
    Public Sub CommandTop(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim TargetNickname As String = Sender.Split("!"c)(0)
        Dim Username = TargetNickname

        Dim List As Dictionary(Of String, PlayerStats) = CurrentStats
        Dim Rivals As Boolean = False, Stat As String = "total", MessageTitle = "Top scores", MessagePeriod = ""

        For Each s In args
            Select Case s.ToLower
                Case "top"
                    Rivals = False
                Case "near", "rival", "rivals", "nearme", "nearby", "close"
                    Rivals = True
                Case "current"
                    List = CurrentStats
                    MessagePeriod = ""
                Case "last"
                    List = LastStats
                    MessagePeriod = " last period"
                Case "alltime"
                    List = AllTimeStats
                    MessagePeriod = " all time"
                Case "total"
                    Stat = "total"
                    MessageTitle = "Top scores"
                Case "challenge"
                    Stat = "challenge"
                    MessageTitle = "Top challenge scores"
                Case "wins"
                    Stat = "wins"
                    MessageTitle = "Most victories"
                Case "plays"
                    Stat = "plays"
                    MessageTitle = "Top participants"
            End Select
        Next

        If List.Count = 0 Then
            Say(Connection, Channel, "No one has scored yet.")
            Return
        End If

        Dim Top10 As New List(Of Object())
        For Each Player In List.Values
            Dim i As Integer = 0, Value As Long
            Select Case Stat
                Case "total"
                    Value = Player.Points
                Case "challenge"
                    Value = Player.ChallengePoints
                Case "wins"
                    Value = Player.Wins
                Case "plays"
                    Value = Player.Plays
                Case Else
                    Value = Player.Points
            End Select

            For i = 0 To Top10.Count - 1
                'If i >= 10 Then Exit For
                If Value > Top10(i)(1) Then Exit For
            Next
            'If i < 10 Then
            'If Top10.Count >= 10 Then Top10.RemoveAt(9)
            Top10.Insert(i, {Player.Name, Value})
            'End If
        Next

        Dim Message As String = ""
        Dim LastRank As Short = 0, LastScore As Integer = Integer.MaxValue
        Dim MinRank As Integer
        If Rivals Then
            Dim Rank As Integer
            For i = 0 To Top10.Count - 1
                If Top10(i)(0) = Username Then
                    Rank = i
                    Exit For
                End If
            Next
            MinRank = Math.Max(Rank - 5, 0)
        Else
            MinRank = 0
        End If

        For i = MinRank To MinRank + 9
            If i >= Top10.Count Then Exit For
            If Top10(i)(1) < LastScore Then
                LastRank = i
                LastScore = Top10(i)(1)
            End If

            Message &= "  $k14|  "
            If LastRank < 10 Then
                Message &= {"$k12$b1st$b", "$k4$b2nd$b", "$k09$b3rd$b", "$k10$b4th$b", "$k10$b5th$b", "$k10$b6th$b", "$k10$b7th$b", "$k10$b8th$b", "$k10$b9th$b", "$k10$b10th$b"}(LastRank)
            Else
                Message &= "$k6$b" & GetRankString(LastRank + 1) & "$b"
            End If
            Message &= "  "
            If Top10(i)(0) = Username Then _
 Message &= "$k9$b" & Username.TrimStart("~"c) & "$b $k03" & CLng(Top10(i)(1)).ToString("N0") _
 Else Message &= "$k12" & Top10(i)(0).TrimStart("~"c) & " $k02" & CLng(Top10(i)(1)).ToString("N0")
        Next

        Say(Connection, Channel, "$k12$b" & MessageTitle & MessagePeriod & "$b" & Message)
        Message = ""
        Threading.Thread.Sleep(600)

        If ScoresPeriodEnd = Nothing Then Return
        If List IsNot CurrentStats Then Return
        ' Tell the users how much time remains until the period ends.
        Dim TimeRemaining As TimeSpan = TimeSpan.FromMinutes(Math.Ceiling((ScoresPeriodEnd - Date.UtcNow).TotalMinutes)) ' Rounds it up to the next minute.

        If TimeRemaining.TotalMinutes < 1 Then
            Message &= "  $bless than 1$b minute"
        Else
            If TimeRemaining.TotalDays >= 1 Then _
               Message &= ", $b" & TimeRemaining.Days & "$b " & If(TimeRemaining.Days = 1, "day", "days")
            If TimeRemaining.TotalHours >= 1 Then _
                Message &= ", $b" & TimeRemaining.Hours & "$b " & If(TimeRemaining.Hours = 1, "hour", "hours")
            Message &= ", $b" & TimeRemaining.Minutes & "$b " & If(TimeRemaining.Minutes = 1, "minute", "minutes")
        End If

        Say(Connection, Channel, "This scoreboard resets in $k12" & Message.Substring(2) & "$o.")
    End Sub

    <Command({"ustats", "ustatistics"}, 0, 2,
"ustats [name] [current|last|alltime]",
"Shows you a player's (default your own) extended statistics.")>
    Public Sub CommandStats(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim TargetNickname As String = "?"
        If args.Count >= 2 Then TargetNickname = args(0)
        If args.Count = 1 Then TargetNickname = Sender.Split("!"c)(0)
        Dim List As Dictionary(Of String, PlayerStats), Period As String

        If args.Count = 0 Then
            TargetNickname = Sender.Split("!"c)(0)
            List = CurrentStats
            Period = " this period"
        Else
            Select Case args(UBound(args)).ToLower.Replace(" ", "")
                Case "current", "thisperiod", "now", "recent", "recently", "this", "currentperiod", "lately"
                    List = CurrentStats
                    If Period <> "streak" Then Period = " this period"
                Case "last", "lastperiod", "previous", "previousperiod"
                    List = LastStats
                    If Period <> "streak" Then Period = " last period"
                Case "all", "alltime", "overall", "ever", "totalever", "forever"
                    List = AllTimeStats
                    If Period <> "streak" Then Period = ""
                Case "streak"
                    Period = "streak"
                Case Else
                    If args.Count = 2 Then
                        Reply(Connection, Channel, Sender.Split("!")(0), "Please use $k11current$o, $k11last$o or $k11alltime$o for the period.")
                        Return
                    Else
                        TargetNickname = args(0)
                        List = CurrentStats
                        Period = " this period"
                    End If
            End Select
        End If

        If Period = "streak" Then
            CommandStreak(Connection, Sender, Channel, {TargetNickname})
            Return
        End If

        Dim stats = GetStats(List, Nothing, Nothing, TargetNickname)

        If stats Is Nothing Then
            If List Is LastStats Then
                Say(Connection, Channel, "$b" & TargetNickname & "$b didn't play during the last period.")
            Else
                Say(Connection, Channel, "$b" & TargetNickname & "$b doesn't have a record yet.")
            End If
        Else
            Say(Connection, Channel, String.Format("$k13$b{0}$b's stats{7} $k15|$k4 Total points$k12 {1} $k15|$k4 Games entered$k12 {2} $k15|$k4 Wins$k12 {3} $k15|$k4 Losses$k12 {4} $k15|$k4 Single-round record$k12 {5} $k15|$k4 Challenge score$k12 {6}",
                stats.Name, stats.Points.ToString("N0"), stats.Plays.ToString("N0"), stats.Wins.ToString("N0"), stats.Losses.ToString("N0"), stats.RecordPoints.ToString("N0"), stats.ChallengePoints.ToString("N0"), Period))
        End If
    End Sub

    <Command({"ustreak"}, 0, 1,
"ustreak [name]",
"Shows you a player's (default your own) streak.")>
    Public Sub CommandStreak(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim Nickname = If(args.ElementAtOrDefault(0), Sender.Split("!"c)(0))
        Dim lStreak = 0
        CurrentStreak.TryGetValue(Nickname, lStreak)
        Dim lBestStreak = 0
        BestStreak.TryGetValue(Nickname, lBestStreak)

        If lStreak = 0 Then
            Say(Connection, Channel, "$b" & Nickname & "$b hasn't played yet.")
        Else
            Say(Connection, Channel, "$b" & Nickname & "$b is currently on a streak of $b" & Math.Abs(lStreak) & " " & If(lStreak > 0, If(lStreak = 1, "win", "wins"), If(lStreak = -1, "loss", "losses")) & "$b.")
            If lBestStreak >= lStreak Then
                Say(Connection, Channel, "$b" & Nickname & "$b's best winning streak is $b" & lBestStreak & "$b wins.")
            ElseIf lBestStreak > 0 Then
                Say(Connection, Channel, "$b" & Nickname & "$b's former best was $b" & lBestStreak & "$b wins.")
            End If
        End If
    End Sub

    Friend Sub ScoresResetTimer_Elapsed(ByVal sender As Object, ByVal e As Timers.ElapsedEventArgs) Handles ScoreResetTimer.Elapsed
        If ScoresPeriodEnd = Nothing Then
            ScoreResetTimer.Enabled = False
            Return
        End If
        Select Case ScoresPeriodEnd - Date.UtcNow
            Case Is <= TimeSpan.Zero
                ResetScores()
                ScoreResetTimer.Enabled = False
            Case Is <= TimeSpan.FromHours(1)
                ScoreResetTimer.Interval = 60000
            Case Else
                ScoreResetTimer.Interval = 3600000
        End Select
    End Sub

    Public Sub ResetScores()
        If Games.Count = 0 Then
            Dim Top3 As New List(Of Object())
            For Each Player In CurrentStats.Values
                Dim i As Integer = 0
                For i = 0 To Top3.Count - 1
                    If Player.Points > Top3(i)(1) Then Exit For
                Next
                If i <= 2 Then
                    If Top3.Count >= 3 Then Top3.RemoveAt(2)
                    Top3.Insert(i, {Player.Name, Player.Points})
                End If
            Next

            ' Show a message depending on how many players scored.
            Select Case Top3.Count
                Case 1
                    SayToAllChannels(String.Format("The UNO top scores have been $breset$b! Only $b{0}$b (with {1}) scored any points this time.", Top3(0)(0), Top3(0)(1)))
                Case 2
                    SayToAllChannels(String.Format("The UNO top scores have been $breset$b! The top player was $b{0}$b ({1}), followed by $b{2}$b ({3}).", Top3(0)(0), Top3(0)(1), Top3(1)(0), Top3(1)(1)))
                Case Is >= 3
                    SayToAllChannels(String.Format("The UNO top scores have been $breset$b! The top player was $b{0}$b ({1}), followed by $b{2}$b ({3}), then $b{4}$b ({5}).", Top3(0)(0), Top3(0)(1), Top3(1)(0), Top3(1)(1), Top3(2)(0), Top3(2)(1)))
            End Select

            LastStats = CurrentStats
            CurrentStats = New Dictionary(Of String, PlayerStats)(StringComparer.OrdinalIgnoreCase)

            ScoresPeriodEnd = Nothing
            ScoreResetTimer.Interval = 3600000

            OnSave()
        End If
    End Sub
#End Region

End Class