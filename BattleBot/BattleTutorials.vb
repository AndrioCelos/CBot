Imports VBot
Imports System.Text.RegularExpressions

Partial Public Class BattleBot

    Public CurrentTutorial As String, CurrentTutorialTarget As String

    <Command({"tutorial", "tut"}, 0, 2,
"tutorial [which tutorial] [target player]",
"Starts one of my tutorials.",
".debug", CommandAttribute.CommandScope.Channel)>
    Public Sub CommandTutorial(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        If CurrentTutorial <> Nothing Then
            Say(Connection, Channel, "There's already a tutorial in progress!")
            Return
        End If

        Dim TutorialThread As New Threading.Thread(AddressOf TutorialAttacking)
        TutorialThread.Start(Sender.Split("!"c)(0))
    End Sub

    Public Sub TutorialAttacking(ByVal Target As String)
        ' Don't run the tutorial if there's a battle going on.
        If IsBattleStarted = True Then
            DoBattle("Wait for this battle to finish first.")
            Return
        End If

        CurrentTutorial = "Attacking"
        CurrentTutorialTarget = Target

        If IsBattleOpen And Not IsOwner Then
            DoBattle("$k12As you can see, the portal is currently open. Monsters are now preparing to arrive to the battlefield! Players will now assemble to defeat the monsters.")
        Else
            If IsBattleOpen Then
                DoBattlePrivate("!end battle")
                Threading.Thread.Sleep(5000)
            End If

            DoBattle("$k12Keep an eye on that portal. When it opens...")
2:          DoBattlePrivate("!start battle")
            For i = 1 To 120
                If IsBattleOpen Then Exit For
                Threading.Thread.Sleep(1000)
            Next
            DoBattle("$k12...monsters are preparing to arrive to the battlefield! Players will now assemble to battle the monsters.")
        End If
        DoBattle("$k13$b" & Target & "$b: Use $k11!enter$k13 to join the battle!")
        Do
            If BattleList.ContainsKey(Target) Then GoTo 1
            If Not IsBattleOpen Then GoTo 2
        Loop

1:      DoBattle("!enter")

        ' Wait for the battle to start.
        Do
            If IsBattleStarted Then GoTo 3
            'If Not BattleOpen Then GoTo 2
        Loop

3:      Dim Stage = 0S, CurrentTurn As String = Me.CurrentTurn
        Dim DamageExplanation = False
        Do
            If CurrentTurn <> Me.CurrentTurn Then
                CurrentTurn = Me.CurrentTurn

                If CurrentTurn = LoggedIn Then
                    If Stage = 0 Then
                        DoBattle("$k12The flow of battle is turn-based. On your turn, you have a number of options.")
                        DoBattle("$k12To start with, let's perform a $bstandard attack$b with our weapons.")
                        Threading.Thread.Sleep(3000)

                        Stage = 1
                        WriteMessage(1, 12, "Attacking " & currentMonsters(0))
                        DoBattle("$aACTION attacks " & currentMonsters(0) & "$a")
                    ElseIf Stage = 2 Then
                        DoBattle("$k12Your attack did some damage to the monster, reducing its health.")
                        DoBattle("$k12When its health reaches zero, it is defeated and you win the battle.")
                        Stage = 3
                        WriteMessage(1, 12, "Attacking " & currentMonsters(0))
                        DoBattle("$aACTION attacks " & currentMonsters(0) & "$a")
                    ElseIf Rnd() < 0.5 Then
                        WriteMessage(1, 12, "Attacking " & currentMonsters(0))
                        DoBattle("$aACTION attacks " & currentMonsters(0) & "$a")
                    Else
                        WriteMessage(1, 12, "Taunting " & currentMonsters(0))
                        DoBattle("$aACTION taunts " & currentMonsters(0) & "$a")
                    End If
                ElseIf CurrentTurn = Target Then
                    If Stage = 1 Then
                        DoBattle("$k12It's your turn.")
                        DoBattle("$k134$b" & Target & "$b: Use $k11/me attacks " & currentMonsters(0) & "$k13 to do a standard attack!")

                        Do
                            Dim message = WaitForMessage(ArenaConnection, ArenaChannel, Target, 5)
                            If Me.CurrentTurn <> CurrentTurn Then Exit Do
                            If message = Nothing Then Continue Do
                            If Regex.IsMatch(message, "^(\x01ACTION attacks +|!attack +)" & Regex.Escape(currentMonsters(0)) & "( .*)?\x01?$", RegexOptions.IgnoreCase) Then

                                For i = 1 To 30
                                    If Me.CurrentTurn <> CurrentTurn Then Exit Do
                                    Threading.Thread.Sleep(1000)
                                Next
                                DoBattle("$k12Good one, " & Target & ".")

                                Stage = 2
                            End If
                        Loop
                    ElseIf Stage = 3 Then
                        DoBattle("$k12Another thing you can do for your turn is taunt.")
                        DoBattle("$k12This will add to your style rating, so you'll get more red orbs after the battle.")
                        DoBattle("$k134$b" & Target & "$b: Use $k11/me taunts " & currentMonsters(0) & "$k13 and give it your all!")

                        Do
                            Dim message = WaitForMessage(ArenaConnection, ArenaChannel, Target, 5)
                            If Me.CurrentTurn <> CurrentTurn Then Exit Do
                            If Regex.IsMatch(message, "^\x01ACTION taunt[^ ]* +" & Regex.Escape(currentMonsters(0)) & "( .*)?\x01?$", RegexOptions.IgnoreCase) Then

                                For i = 1 To 30
                                    If Me.CurrentTurn <> CurrentTurn Then Exit Do
                                    Threading.Thread.Sleep(1000)
                                Next
                                DoBattle("$k12Nice, " & Target & ". Now, keep attacking to defeat the monster.")

                                Stage = 4
                            End If
                        Loop
                    End If
                End If
            End If
        Loop

        CurrentTutorial = Nothing
        CurrentTutorialTarget = Nothing
    End Sub

End Class
