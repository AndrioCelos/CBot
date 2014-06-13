Imports VBot
Imports System.Text.RegularExpressions

Partial Public Class BattleBot

    ''' <summary>
    ''' Returns True if it is the turn of a character whom the bot should control, or False otherwise.
    ''' </summary>
    Public Function ShouldAct() As Boolean
        If CurrentTurn Is Nothing Then Return False
        If CurrentTurn = LoggedIn Then
            Return EnableParticipation
        ElseIf CurrentTurn = LoggedIn & "_clone" And If(Characters(LoggedIn).CurrentStyle, "").ToLower = "doppelganger" Then
            Return EnableParticipation
        ElseIf CurrentTurn.EndsWith("_clone") AndAlso (Controlling.Contains(CurrentTurn.Substring(0, CurrentTurn.Length - 6)) And If(Characters(CurrentTurn.Substring(0, CurrentTurn.Length - 6)).CurrentStyle, "").ToLower = "doppelganger") Then
            Return True
        Else
            Return Controlling.Contains(CurrentTurn)
        End If
    End Function

    ''' <summary>
    ''' Returns True if it is the bot's turn or its clone's turn, and False otherwise. We should not switch other characters' weapons.
    ''' </summary>
    Public Function CanSwitchWeapon() As Boolean
        If Not (CurrentTurn = LoggedIn Or CurrentTurn = LoggedIn & "_clone") Then Return False
        If BattleList(CurrentTurn).Status.Contains("weapon locked") Then Return False
        Return True
    End Function

    ''' <summary>
    ''' Performs an action for the bot when it's its turn.
    ''' </summary>
    ''' <param name="Health">The health of this player, from the turn message.</param>
    ''' <param name="Status">Any status effects on this player, from the turn message.</param>
    <Obsolete("Deprecated – use AITurn() instead.")>
    Private Sub AISelf(ByVal Health As String, ByVal Status As String)
        ' Check that there are actually monsters in the battle.
        If currentMonsters.Count = 0 Then
            'If WaitingForRegistration Is Nothing And UnmatchedFullNames.Count = 0 Then
            DoBattle("No monsters were found in the battle...")
            'Else
            'WaitingForRegistration = {LoggedIn, Health, Status}
            'DoBattle("!bat info")
            'End If
            Return
        End If
        If CurrentTurn <> LoggedIn Then Return
        WaitingForRegistration = Nothing
        Randomize()

        If AI = 0 Then AI0()
        If AI = 1 Then AI1()
    End Sub

    ''' <summary>
    ''' Performs an action for someone else when it's their turn.
    ''' </summary>
    ''' <param name="Player">The short name of the player who must act.</param>
    ''' <param name="Health">The health of this player, from the turn message.</param>
    ''' <param name="Status">Any status effects on this player, from the turn message.</param>
    ''' <remarks>This action requires the bot to have admin status in the Arena.</remarks>
    <Obsolete("Deprecated – use AITurn() instead.")>
    Private Sub AIOther(ByVal Player As String, ByVal Health As String, ByVal Status As String)
        With Characters(Player)
            ' Check that there are actually monsters in the battle.
            If currentMonsters.Count = 0 And UnmatchedFullNames.Count = 0 Then
                If WaitingForRegistration Is Nothing Then
                    DoBattle("There are no monsters in the battle!")
                Else
                    WaitingForRegistration = {Player, Health, Status}
                    DoBattle("!bat info")
                End If
                Return
            End If
            WaitingForRegistration = Nothing


            Randomize()

            ' For now the AI will be very, very basic and random.  Later on I'll try to make it more complicated.

            '' They can change their weapon.
            'If .Weapons IsNot Nothing AndAlso .Weapons.Count > 0 AndAlso Rnd() < 0.2 Then
            '    Dim SelectedWeapon As String = .Weapons.Keys(Rnd() * .Weapons.Count)
            '    If SelectedWeapon <> EquippedWeapon Then
            '        WriteMessage(1, 12, "[" & Player & "] Switching weapon to " & SelectedWeapon)
            '        Say(Connection, Channel, Player & " equips " & SelectedWeapon)
            '        .EquippedWeaponTechs = New List(Of String)
            '        For Each Technique In Weapons(SelectedWeapon).Techniques
            '            If .Techniques.ContainsKey(Technique) Then .EquippedWeaponTechs.Add(Technique)
            '        Next
            '    End If
            'End If

            ' We need to choose a random action.

            If Rnd() < 0.6 And Not If(BattleList(Player).Status, {}).Contains("cursed") And CurrentEvent <> BattleEvents.MeleeLock Then
                Dim TechniqueToUse As String = Choose(.EquippedWeaponTechs.ToArray), Target As String
                If BattleList(CurrentTurn).TP >= Techniques(TechniqueToUse).TP Then

                    If Techniques(TechniqueToUse).Type.ToLower.Contains("heal") Then
                        Target = Choose(currentPlayers.ToArray)
                    Else
                        Target = Choose(currentMonsters.ToArray)
                    End If

                    WriteMessage(1, 12, "[" & Player & "] Using technique " & TechniqueToUse & " on " & Target)
                    CurrentAbility = TechniqueToUse
                    Say(ArenaConnection, ArenaChannel, Player & " uses " & .GenderWord.tolower & " " & TechniqueToUse & " on " & Target)
                    Return
                End If
            End If
            If Rnd() < 0.25 Then
                WriteMessage(1, 12, "[" & Player & "] Taunting " & currentMonsters(0))
                Say(ArenaConnection, ArenaChannel, Player & " taunts " & currentMonsters(0))
            Else
                WriteMessage(1, 12, "[" & Player & "] Attacking " & currentMonsters(0))
                Say(ArenaConnection, ArenaChannel, Player & " attacks " & currentMonsters(0))
            End If

        End With
    End Sub

    Public Sub AITurn()
        Dim AIThread = New Threading.Thread(AddressOf AITurnSub)
        AIThread.Start()
    End Sub
    Public Sub AITurnSub()
        Dim lPlayers = currentPlayers, lMonsters = currentMonsters, lAllies = currentAllies
        Try
            If IsPVPBattle Then
                currentPlayers = New List(Of String)
                currentPlayers.Add(CurrentTurn)
                currentMonsters = New List(Of String)

                For Each Combatant In BattleList
                    If Combatant.Key <> CurrentTurn Then currentMonsters.Add(Combatant.Key)
                Next
            End If
            Threading.Thread.Sleep(5000)

            ' Check again check that there are monsters in the battle. This time if there's someone we don't know, we'll try to attack them.
            If Not IsPVPBattle Then
                If NoMonsterFix And currentMonsters.Count = 0 Then
                    For Each Entry In BattleList
                        If Entry.Value.Category = Nothing Then
                            AIAction("Attack", Nothing, Entry.Key)
                            Return
                        End If
                    Next
                End If
            End If

            ' Check that there are actually monsters in the battle.
            If currentMonsters.Count = 0 Then
                'If WaitingForRegistration Is Nothing Then Threading.Thread.Sleep(10000)
                If WaitingForRegistration Is Nothing And UnmatchedFullNames.Count = 0 And UnmatchedShortNames.Count = 0 Then
                    DoBattle("There are no monsters in the battle!")
                Else
                    WaitingForRegistration = {CurrentTurn, BattleList(CurrentTurn).Health, String.Join(", ", BattleList(CurrentTurn).Status)}
                    DoBattle("!bat info")
                End If
                Return
            End If
            If Not ShouldAct() Then Return
            WaitingForRegistration = Nothing
            Randomize()

            If AI = 0 Then AI0()
            If AI = 1 Then AI1()
        Catch ex As Threading.ThreadAbortException
        Finally
            If IsPVPBattle Then
                currentPlayers = lPlayers
                currentMonsters = lMonsters
                currentAllies = lAllies
            End If
        End Try
    End Sub

    ''' <summary>
    ''' Actually performs a battle action for the character whose turn it is.
    ''' </summary>
    ''' <param name="Type">Attack, Taunt, Technique, Skill, Item or Equip.</param>
    ''' <param name="Ability">The technique or skill to use. For skills, the name must be in CamelCase.</param>
    ''' <param name="Target">The target to attack, taunt or use a technique/skill/item on, or the weapon to equip.</param>
    ''' <exception cref="System.ArgumentException">The character whose turn it is is a clone or summon, and Type equals "Item".</exception>
    Public Sub AIAction(ByVal Type As String, Ability As String, Target As String)
        If Type = "Attack" Then
            CurrentAbility = "Attack"
            CurrentTarget = Target
        ElseIf Type = "Taunt" Then
            CurrentAbility = "Taunt"
            CurrentTarget = Target
        ElseIf Type = "Technique" Then
            CurrentAbility = Ability
            CurrentTarget = Target
        ElseIf Type = "Skill" Then
            If Ability = "ShadowCopy" Then BattleList(CurrentTurn).HasUsedShadowCopy = True
        End If

        If CurrentTurn = LoggedIn Then
            ' The bot must act.
            If Type = "Attack" Then
                WriteMessage(1, 12, "Attacking " & Target)
                DoBattle(Chr(1) & "ACTION attacks " & Target & Chr(1))
            ElseIf Type = "Taunt" Then
                WriteMessage(1, 12, "Taunting " & Target)
                DoBattle(Chr(1) & "ACTION taunts " & Target & Chr(1))
            ElseIf Type = "Technique" Then
                If Target = CurrentTurn And (Techniques(Ability).Type = "Boost" Or Techniques(Ability).Type = "Buff") Then
                    WriteMessage(1, 12, "Activating technique " & Ability)
                    DoBattle(Chr(1) & "ACTION goes " & Target & Chr(1))
                Else
                    WriteMessage(1, 12, "Using technique " & Ability & " on " & Target)
                    DoBattle(Chr(1) & "ACTION uses " & Characters(CurrentTurn).GenderWord.tolower & " " & Ability & " on " & Target & Chr(1))
                End If
            ElseIf Type = "Skill" Then
                WriteMessage(1, 12, "Using skill " & Ability & If(Target = Nothing, "", " on " & Target))
                Select Case Ability
                    Case "Speed" : DoBattle("!Speed")
                    Case "ElementalSeal" : DoBattle("!Elemental Seal")
                    Case "MightyStrike" : DoBattle("!Mighty Strike")
                    Case "ManaWall" : DoBattle("!Mana Wall")
                    Case "RoyalGuard" : DoBattle("!Royal Guard")
                    Case "Sugitekai" : DoBattle("!Sugitekai")
                    Case "Meditate" : DoBattle("!Meditate")
                    Case "ConserveTP" : DoBattle("!Conserve TP")
                    Case "BloodBoost" : DoBattle("!Blood Boost")
                    Case "DrainSamba" : DoBattle("!Drain Samba")
                    Case "Regen" : DoBattle("!Regen")
                    Case "Kikouheni" : DoBattle("!Kikouheni " & Target)
                    Case "ShadowCopy" : DoBattle("!Shadow Copy")
                    Case "Utsusemi" : DoBattle("!Utsusemi")
                    Case "Steal" : DoBattle("!Steal " & Target)
                    Case "Analysis" : DoBattle("!Analyze " & Target)
                    Case "Cover" : DoBattle("!Cover " & Target)
                    Case "Aggressor" : DoBattle("!Aggressor")
                    Case "Defender" : DoBattle("!Defender")
                    Case "HolyAura" : DoBattle("!Holy Aura")
                    Case "Provoke" : DoBattle("!Provoke " & Target)
                    Case "Disarm" : DoBattle("!Disarm " & Target)
                    Case "WeaponLock" : DoBattle("!WeaponLock " & Target)
                    Case "Konzen-Ittai" : DoBattle("!Konzen-Ittai")
                    Case "SealBreak" : DoBattle("!Seal Break")
                    Case "MagicMirror" : DoBattle("!MagicMirror")
                    Case "Gamble" : DoBattle("!Gamble")
                    Case "ThirdEye" : DoBattle("!Third Eye")
                    Case "Scavenge" : DoBattle("!Scavenge")
                    Case "JustRelease" : DoBattle("!JustRelease " & Target)
                End Select
            ElseIf Type = "Item" Then
                ' TODO: Ignore the parameter for keys, portal items and summon items.
                WriteMessage(1, 12, "Using item " & Ability & " on " & Target)
                DoBattle("!use " & Ability & " on " & Target)
            ElseIf Type = "Equip" Then
                WriteMessage(1, 12, "Switching weapon to " & Target)
                DoBattle("!equip " & Target)
            ElseIf Type = "Style change" Then
                WriteMessage(1, 12, "Switching style to " & Target)
                DoBattle("!style change " & Target)
            End If
        ElseIf CurrentTurn = LoggedIn & "_clone" And Characters(LoggedIn).CurrentStyle.ToLower = "doppelganger" Then
            ' The bot's clone must act.
            If Type = "Attack" Then
                WriteMessage(1, 12, "[" & Characters(CurrentTurn).Name & "] Attacking " & Target)
                DoBattle("!shadow attack " & Target)
            ElseIf Type = "Taunt" Then
                WriteMessage(1, 12, "[" & Characters(CurrentTurn).Name & "] Taunting " & Target)
                DoBattle("!shadow taunt " & Target)
            ElseIf Type = "Technique" Then
                WriteMessage(1, 12, "[" & Characters(CurrentTurn).Name & "] Using technique " & Ability & " on " & Target)
                DoBattle("!shadow tech " & Ability & " " & Target)
            ElseIf Type = "Skill" Then
                WriteMessage(1, 12, "[" & Characters(CurrentTurn).Name & "] Using skill " & Ability & If(Target = Nothing, "", " on " & Target))
                DoBattle("!shadow skill " & Ability & If(Target = Nothing, "", " " & Target))
            ElseIf Type = "Item" Then
                Throw New ArgumentException("Shadow clones can't use items.")
            ElseIf Type = "Equip" Then
                WriteMessage(1, 12, "[" & Characters(CurrentTurn).Name & "] Switching weapon to " & Target)
                DoBattle("!equip " & Target)
            ElseIf Type = "Style change" Then
                Throw New ArgumentException("Shadow clones can't switch styles.")
            End If
        Else
            ' Someone else must act.

            If Type = "Attack" Then
                WriteMessage(1, 12, "[" & Characters(CurrentTurn).Name & "] Attacking " & Target)
                DoBattle(CurrentTurn & " attacks " & Target)
            ElseIf Type = "Taunt" Then
                WriteMessage(1, 12, "[" & Characters(CurrentTurn).Name & "] Taunting " & Target)
                DoBattle(CurrentTurn & " taunts " & Target)
            ElseIf Type = "Technique" Then
                If Target = CurrentTurn And (Techniques(Ability).Type = "Boost" Or Techniques(Ability).Type = "Buff") Then
                    WriteMessage(1, 12, "[" & Characters(CurrentTurn).Name & "] Activating technique " & Ability)
                    DoBattle(CurrentTurn & " goes " & Target)
                Else
                    WriteMessage(1, 12, "[" & Characters(CurrentTurn).Name & "] Using technique " & Ability & " on " & Target)
                    DoBattle(CurrentTurn & " uses " & Characters(CurrentTurn).GenderWord.tolower & " " & Ability & " on " & Target)
                End If
            ElseIf Type = "Skill" Then
                WriteMessage(1, 12, "[" & Characters(CurrentTurn).Name & "] Using skill " & Ability & If(Target = Nothing, "", " on " & Target))
                DoBattle(CurrentTurn & " does " & Ability & If(Target = Nothing, "", " " & Target))
            ElseIf Type = "Item" Then
                ' TODO: Ignore the parameter for keys, portal items and summon items.
                WriteMessage(1, 12, "[" & Characters(CurrentTurn).Name & "] Using item " & Ability & " on " & Target)
                DoBattle(CurrentTurn & " uses item " & Ability & " on " & Target)
            ElseIf Type = "Equip" Then
                WriteMessage(1, 12, "[" & Characters(CurrentTurn).Name & "] Switching weapon to " & Target)
                DoBattle(CurrentTurn & " equips " & Target)
            ElseIf Type = "Style change" Then
                WriteMessage(1, 12, "[" & Characters(CurrentTurn).Name & "] Switching style to " & Target)
                DoBattle(CurrentTurn & " style change to " & Target)
            End If
        End If
    End Sub

#Region "Algorithm 0"
    Public Sub AI0()
        If AI0_HealCheck() Then Return

        If AI0_SkillsCheckSelf() Then Return

        ' I can change my weapon. (Don't change other people's weapons.)
        If CanSwitchWeapon Then
            If Rnd() < 0.2 And Not BattleList(CurrentTurn).Status.Contains("weapon locked") Then
                Dim SelectedWeapon As String = Characters(CurrentTurn).Weapons.Keys(Int(Rnd() * Characters(CurrentTurn).Weapons.Count))
                If SelectedWeapon <> Characters(CurrentTurn).EquippedWeapon Then
                    AIAction("Equip", Nothing, SelectedWeapon)
                    Characters(CurrentTurn).EquippedWeapon = SelectedWeapon
                    Characters(CurrentTurn).EquippedWeaponTechs = New List(Of String)
                    For Each Technique In Weapons(Characters(CurrentTurn).EquippedWeapon).Techniques
                        If Characters(CurrentTurn).Techniques.ContainsKey(Technique) Then Characters(CurrentTurn).EquippedWeaponTechs.Add(Technique)
                    Next
                End If
            End If
        End If

        ' We need to choose a random action.

        If Rnd() < 0.6 And CurrentEvent <> BattleEvents.MeleeLock And Characters(CurrentTurn).EquippedWeaponTechs IsNot Nothing Then
            Dim TechniqueToUse As String = Choose(Characters(CurrentTurn).EquippedWeaponTechs.ToArray), Target As String
            If BattleList(CurrentTurn).TP >= Techniques(TechniqueToUse).TP Then

                If Techniques(TechniqueToUse).Type.ToLower.Contains("heal") Then
                    Target = Choose(currentPlayers.ToArray)
                Else
                    Target = Choose(currentMonsters.ToArray)
                End If

                AIAction("Technique", TechniqueToUse, Target)
                Return
            End If
        End If
        If Rnd() < 0.25 Then
            AIAction("Taunt", Nothing, currentMonsters(0))
        Else
            AIAction("Attack", Nothing, currentMonsters(0))
        End If
    End Sub

    Private Function AI0_HealCheck() As Boolean
        ' Check if there's an ally who needs healing.
        Dim Target As String
        For Each Character In BattleList
            If Character.Value.Category <> CharacterCategory.Player And Character.Value.Category <> CharacterCategory.Ally Then Continue For
            If Character.Value.Status.Contains("zombie") Then Continue For

            If Character.Value.Health = "Injured Badly" Then
                If Target = Nothing OrElse BattleList(Target).Health = "Injured Badly" Then _
                    Target = Character.Key
            ElseIf Character.Value.Health = "Critical" Then
                If Target = Nothing OrElse (BattleList(Target).Health = "Injured Badly" Or BattleList(Target).Health = "Critical") Then _
                    Target = Character.Key
            ElseIf Character.Value.Health = "Alive by a hair's bredth" Or Character.Value.Health = "Alive by a hair's breadth" Then
                If Target = Nothing OrElse (BattleList(Target).Health = "Injured Badly" Or BattleList(Target).Health = "Critical" Or BattleList(Target).Health = "Alive by a hair's bredth" Or BattleList(Target).Health = "Alive by a hair's breadth") Then _
                    Target = Character.Key
            End If
        Next

        If Target = Nothing Then Return False

        ' Find a healing technique to use.
        Dim HealingTechnique As String, HealingWeapon As String, HealingTechniquePower As Integer
        For Each Technique In If(CanSwitchWeapon, Characters(CurrentTurn).Techniques.Keys, Characters(CurrentTurn).EquippedWeaponTechs)
            If (Techniques(Technique).Type = "Heal" Or Techniques(Technique).Type = "AoE Heal") And BattleList(CurrentTurn).TP >= Techniques(Technique.Key).TP Then
                For Each Weapon In Characters(CurrentTurn).Weapons
                    If Weapons(Weapon.Key).Techniques.Contains(Technique.Key, System.StringComparer.OrdinalIgnoreCase) Then
                        Dim TempPower = Weapons(Weapon.Key).Power + Techniques(Technique).Power
                        If Techniques(Technique).Type = "AoE Heal" Then TempPower *= (currentPlayers.Count + currentAllies.Count) / 1.5

                        If TempPower > HealingTechniquePower Then
                            HealingWeapon = Weapon.Key
                            HealingTechnique = Technique
                            HealingTechniquePower = TempPower
                        End If
                    End If
                Next
            End If
        Next

        If HealingTechnique = Nothing Then Return False

        If HealingWeapon <> Characters(CurrentTurn).EquippedWeapon Then
            WriteMessage(1, 12, "Switching weapon to " & HealingWeapon)
            AIAction("Equip", Nothing, HealingWeapon)
            Characters(CurrentTurn).EquippedWeapon = HealingWeapon
            Characters(CurrentTurn).EquippedWeaponTechs = New List(Of String)
            For Each Technique In Weapons(Characters(CurrentTurn).EquippedWeapon).Techniques
                If Characters(CurrentTurn).Techniques.ContainsKey(Technique) Then Characters(CurrentTurn).EquippedWeaponTechs.Add(Technique)
            Next
        End If


        If HealingTechniquePower > 25 + 4 * BattleLevel Then
            WriteMessage(1, 12, "Healing " & Target & " with " & HealingTechnique)
            AIAction("Technique", HealingTechnique, Target)
            Return True
        End If

        Return False
    End Function

    Private Function AI0_SkillsCheckSelf() As Boolean
        Dim Ic = Characters(CurrentTurn), Ib = BattleList(CurrentTurn)
        AI0_SkillsCheckSelf = False

        ' Mighty Strike
        If Ic.HasSkill("MightyStrike") Then

        End If

        ' Elemental Seal
        If Ic.HasSkill("ElementalSeal") Then

        End If

        ' Shadow Copy
        If Ic.HasSkill("ShadowCopy") And Not Ib.HasUsedShadowCopy Then
            If Ib.Health <> "Decent" And Ib.Health <> "Good" And Ib.Health <> "Great" And Ib.Health <> "Perfect" Then
                For Each Monster In currentMonsters
                    If BattleList(Monster).Health = "Scratched" Or BattleList(Monster).Health = "Good" Or BattleList(Monster).Health = "Decent" Then
                        AIAction("Skill", "ShadowCopy", Nothing)
                        Ib.HasUsedShadowCopy = True
                        Return True
                    End If
                Next
            End If
        End If

        ' Cover
        If Ic.HasSkill("Cover") Then

        End If

        ' Analysis
        If Ic.HasSkill("Analysis") And CurrentTurn = CurrentTurn Then
            If currentMonsters.Count = 1 And TurnNumber <= 1 And Not Characters(currentMonsters(0)).IsWellKnown And (Ib.Health = "Decent" Or Ib.Health = "Good" Or Ib.Health = "Great" Or Ib.Health = "Perfect") Then
                AIAction("Skill", "Analysis", currentMonsters(0))
                Return True
            End If
        End If

        ' Steal
        If Ic.HasSkill("Steal") And Not CurrentTurn.EndsWith("_clone") And Not CurrentTurn.EndsWith("_summon") Then
            If currentMonsters.Count = 1 And TurnNumber <= 1 AndAlso currentMonsters(0).ToLower = "orb_fountain" Then
                AIAction("Skill", "Steal", currentMonsters(0))
                Return True
            ElseIf currentMonsters.Count = 1 And TurnNumber <= 1 And BattleLevel <= If(IsBossBattle, 8, 15) And (Ib.Health = "Great" Or Ib.Health = "Perfect") Then
                AIAction("Skill", "Steal", currentMonsters(0))
                Return True
            End If
        End If


        ' Drain Samba
        If Ic.HasSkill("DrainSamba") Then

        End If

        ' Regeneration
        If Ic.HasSkill("Regen") Then

        End If

        ' Provoke
        If Ic.HasSkill("Provoke") Then

        End If

        ' Disarm
        If Ic.HasSkill("Disarm") Then

        End If

        ' Weapon Lock
        If Ic.HasSkill("WeaponLock") Then

        End If

        ' Konzen-Ittai
        If Ic.HasSkill("Konzen-Ittai") Then

        End If
    End Function
#End Region

#Region "Algorithm 1"
    Dim Ratings As List(Of Object())

    Public Sub AI1()
        AI1_ActionCheck()

        Dim tRatings = New List(Of Object())(Ratings)
        For i = 1 To 10
            Dim TopTechnique As String = "", lTopTarget As String = "", lTopRating As Decimal = 0, lTopIndex As Integer = -1
            ' Find the top technique.
            Select Case tRatings.Count
                Case 0
                    Exit For
                Case 1
                    TopTechnique = tRatings(0)(1)
                    lTopTarget = tRatings(0)(2)
                    lTopRating = tRatings(0)(3)
                    tRatings.RemoveAt(0)
                Case Else
                    For j = 0 To tRatings.Count - 1
                        If lTopIndex < 0 OrElse tRatings(j)(3) > lTopRating Then
                            TopTechnique = tRatings(j)(1)
                            lTopTarget = tRatings(j)(2)
                            lTopRating = tRatings(j)(3)
                            lTopIndex = j
                        End If
                    Next
                    tRatings.RemoveAt(lTopIndex)
            End Select
            WriteMessage(3, 2, "Top action #" & CStr(i).PadLeft(2) & ": $o" & TopTechnique.PadRight(16) & "$k15 on $o" & If(lTopTarget, "").PadRight(16) & "$k15: $o" & lTopRating.ToString("0.00").PadRight(6))
        Next

        Dim TopAction As String = "", TopParameter As String = "", TopTarget As String = "", TopRating As Decimal = 0, TopIndex As Integer = -1
        ' Find the top technique.
        If Ratings.Count = 0 Then
            WriteMessage(1, 12, "Taunting " & currentMonsters(0))
            DoBattle(Chr(1) & "ACTION taunts " & currentMonsters(0) & Chr(1))
        Else
            For j = 0 To Ratings.Count - 1
                Ratings(j)(3) *= Rnd(0.1) + 0.95  ' Randomise it a little bit.
                If TopIndex < 0 OrElse Ratings(j)(3) > TopRating Then
                    TopAction = Ratings(j)(0)
                    TopParameter = Ratings(j)(1)
                    TopTarget = Ratings(j)(2)
                    TopRating = Ratings(j)(3)
                    TopIndex = j
                End If
            Next

            Select Case TopAction
                Case "Attack"
                    ' Attack with a weapon.
                    If Characters(CurrentTurn).EquippedWeapon <> TopParameter Then
                        AIAction("Equip", Nothing, TopParameter)
                        Threading.Thread.Sleep(600)
                    End If
                Case "Technique"
                    ' Find a weapon that has the technique
                    Dim TopWeapon As String, TopWeaponPower As Decimal = 0
                    For Each wList In Weapons
                        If Not Weapons(wList.Key).Techniques.Contains(TopParameter, System.StringComparer.OrdinalIgnoreCase) Then Continue For

                        Dim WeaponPower As Decimal = Weapons(wList.Key).Power + Characters(CurrentTurn).Weapons(wList.Key) * 1.5 * {1, 1, 1, 1, 1, 1, 1, 1, 1}(Weapons(wList.Key).Hits)
                        If WeaponPower > TopWeaponPower Then
                            TopWeapon = wList.Key
                            TopWeaponPower = WeaponPower
                        End If
                    Next

                    If Characters(CurrentTurn).EquippedWeapon <> TopWeapon Then
                        WriteMessage(1, 12, "Switching weapon to " & TopWeapon)
                        AIAction("Equip", Nothing, TopWeapon)
                        Threading.Thread.Sleep(600)
                    End If
            End Select
        End If

        AIAction(TopAction, TopParameter, TopTarget)
    End Sub

    Private Sub AI1_ActionCheck(Optional ByVal Whose As String = Nothing)
        If Whose = Nothing Then Whose = CurrentTurn
        Dim Ib = BattleList(Whose), Ic = Characters(Whose)

        Ratings = New List(Of Object())

        ' Check techniques.
        If CurrentEvent <> BattleEvents.MeleeLock And Ic.Techniques IsNot Nothing Then _
            AI1_TechniquesCheck(Ib, Ic)

        ' Check standard attacks.
        AI1_AttacksCheck(Ib, Ic)

        ' Check skills.
        AI1_SkillsCheck(Ib, Ic)
    End Sub

    Private Sub AI1_AttacksCheck(ByVal Ib As Combatant, ByVal Ic As CharacterData)
        For Each wList In If(CanSwitchWeapon, Ic.Weapons.Keys, {Ic.EquippedWeapon})
            Dim Weapon = Weapons(wList)
            Dim aRating As Decimal, aeRating As Decimal

            ' Check for weapon lock.
            If wList <> Ic.EquippedWeapon And Ib.Status.Contains("weapon locked") Then Continue For

            ' Check the weapon power.
            aRating = Weapon.Power + Ic.Weapons(wList) * 1.5
            aRating += Ic.bSTR / If(Ib.Status.Contains("strength down"), 4, 1)
            If Weapon.Category.ToLower = "handtohand" Then aRating += Ic.Weapons("Fists")

            ' Check for a mastery skill.
            Select Case Weapon.Category.ToLower
                Case "handtohand", "nunchuku"
                    If Ic.HasSkill("MartialArts") Then aRating += Ic.SkillLevel("MartialArts")
                Case "katana", "sword", "greatsword"
                    If Ic.HasSkill("Swordmaster") Then aRating += Ic.SkillLevel("Swordmaster")
                Case "gun", "rifle"
                    If Ic.HasSkill("Gunslinger") Then aRating += Ic.SkillLevel("Gunslinger")
                Case "wand", "stave", "glyph"
                    If Ic.HasSkill("Wizardry") Then aRating += Ic.SkillLevel("Wizardry")
                Case "spear"
                    If Ic.HasSkill("Polemaster") Then aRating += Ic.SkillLevel("Polemaster")
                Case "bow"
                    If Ic.HasSkill("Archery") Then aRating += Ic.SkillLevel("Archery")
            End Select

            ' Check for Desperate Blows
            If Ic.HasSkill("DesperateBlows") Then
                If Ib.Health = "Injured Badly" Then aRating *= 1.5
                If Ib.Health = "Critical" Then aRating *= 2
                If Ib.Health = "Alive by a hair's bredth" Or Ib.Health = "Alive by a hair's breadth" Then aRating *= 2.5
            End If

            ' If it's the same weapon as the last attack, disfavour it.
            If wList = Ib.LastAction Then aRating /= 2.5

            ' Check the targets.
            For Each mList In currentMonsters
                Dim mb = BattleList(mList), mc = Characters(mList)
                aeRating = aRating

                AI1_AttacksCheck_WeaponTargetCheck(Ib, mList, aRating, aeRating, Weapon)

                If aeRating > 0 Then
                    Ratings.Add({"Attack", Weapon.Name, mList, aeRating})
                End If
            Next
        Next
    End Sub

    Private Sub AI1_AttacksCheck_WeaponTargetCheck(Ib As Combatant, ByRef mList As String, ByRef aRating As Decimal, ByRef aeRating As Decimal, ByRef Weapon As WeaponData)
        aeRating = aRating
        Dim bMonster = BattleList(mList), cMonster = Characters(mList)

        If cMonster.AttacksAllies Then aeRating /= 1.5
        If bMonster.Status.Contains("ethereal") Then
            aeRating = 0
            Return
        End If
        If cMonster.IsElemental Then aeRating *= 0.7

        ' Check the current monster level.
        Dim cRatio As Single, LevelDifference As Integer
        cRatio = aeRating / Math.Max(BattleLevel * 4.3875, 1)
        LevelDifference = (Ib.STR + Ib.DEF + Ib.INT + Ib.SPD) / 20 - BattleLevel * If(IsBossBattle, 1.05, 1)
        If LevelDifference > 50 Then LevelDifference = 50
        If LevelDifference < -50 Then LevelDifference = -50
        cRatio += 0.05 * LevelDifference
        aeRating *= Math.Min(cRatio, 2) * 0.65 + 0.7
        If aeRating < 5 Then
            ' We're below the cap.
            aeRating = Weapon.Power + Ib.STR / 2
        End If

        ' Check for elemental strengths and weaknesses.
        If cMonster.ElementalResistances IsNot Nothing AndAlso cMonster.ElementalResistances.Contains(Weapon.Element) Then : aeRating *= 0.5 : aeRating -= 5 : End If
        If cMonster.ElementalWeaknesses IsNot Nothing AndAlso cMonster.ElementalWeaknesses.Contains(Weapon.Element) Then : aeRating *= 1.5 : aeRating += 5 : End If
        If cMonster.ElementalImmunities IsNot Nothing AndAlso cMonster.ElementalImmunities.Contains(Weapon.Element) Then aeRating = 0
        If cMonster.ElementalAbsorbs IsNot Nothing AndAlso cMonster.ElementalAbsorbs.Contains(Weapon.Element) Then aeRating *= -1

        ' Check the monster's health and favour wounded targets.
        Select Case bMonster.Health
            Case "Perfect" : aeRating *= 1.0
            Case "Great" : aeRating *= 1.01
            Case "Good" : aeRating *= 1.02
            Case "Decent" : aeRating *= 1.05
            Case "Scratched" : aeRating *= 1.1
            Case "Bruised" : aeRating *= 1.15
            Case "Hurt" : aeRating *= 1.2
            Case "Injured" : aeRating *= 1.25
            Case "Injured Badly" : aeRating *= 1.35
            Case "Critical" : aeRating *= 1.5
            Case "Alive by a hair's bredth", "Alive by a hair's breadth" : aeRating *= 1.75
            Case "Dead" : aeRating = -10000
        End Select

        ' Check for status effects.
        For Each Effect In If(Weapon.Status, "").Split({"."c})
            Select Case Effect.ToLower
                Case "stop"
                    If Not bMonster.Status.Contains("frozen in time") Then aeRating += 20 / currentMonsters.Count
                Case "poison"
                    If Not bMonster.Status.Contains("poisoned heavily") Then aeRating += 10 + 15 / currentMonsters.Count
                Case "blind"
                    If Not bMonster.Status.Contains("blind") Then aeRating += 20 / currentMonsters.Count
                Case "virus"
                    If IsBossBattle And Not bMonster.Status.Contains("inflicted with a virus") Then aeRating += 10
                Case "amnesia"
                    If Not bMonster.Status.Contains("under amnesia") And (cMonster.bINT / cMonster.bSTR > 1.5) Then aeRating += 25
                Case "paralysis"
                    If Not bMonster.Status.Contains("paralyzed") Then aeRating += 10 + 20 / currentMonsters.Count
                Case "zombie"
                    If Not bMonster.Status.Contains("a zombie") Then aeRating += 5 + 1 * currentMonsters.Count
                Case "slow"
                Case "stun"
                    If Not bMonster.Status.Contains("stunned") Then aeRating += 20 / currentMonsters.Count
                Case "curse"
                    If Not bMonster.Status.Contains("cursed") And (cMonster.bINT / cMonster.bSTR > 1.5) Then aeRating += 25
                Case "charm"
                    If Not bMonster.Status.Contains("charmed") Then aeRating += 25 + If(currentMonsters.Count = 2, 5, 0)
                Case "intimidate"
                    If Not bMonster.Status.Contains("intimidated") Then aeRating += 20 / currentMonsters.Count
                Case "defensedown"
                Case "strengthdown"
                Case "intdown"
                Case "petrify"
                    If Not bMonster.Status.Contains("petrified") Then aeRating += 20 / currentMonsters.Count
                Case "bored"
                    If Not bMonster.Status.Contains("bored") Then aeRating += 15 + 15 / currentMonsters.Count
                Case "confuse"
                    If Not bMonster.Status.Contains("confused") Then aeRating += 20
                Case "random"
                    aeRating += 15
            End Select
        Next

        If bMonster.Status.Contains("ethereal") Then aeRating = 0
        If bMonster.Status.Contains("evolving") Then aeRating = 0
    End Sub

    Private Sub AI1_TechniquesCheck(ByVal Ib As Combatant, ByVal Ic As CharacterData)
        For Each tList In If(CanSwitchWeapon, Ic.Techniques.Keys, Ic.EquippedWeaponTechs)
            Dim Technique = Techniques(tList)
            Dim tRating As Decimal, teRating As Decimal

            ' Do we have enough TP?
            If Not Ib.Status.Contains("conserving TP") And Ib.TP < Technique.TP Then Continue For

            ' Is it a Final Getsuga technique?
            If Technique.Type = "Final Getsuga" Then
                'tRating = -10000
                Continue For
            End If

            ' Initialise the rating to the technique's power.
            tRating = (Technique.Power + Ic.Techniques(tList) * 1.6 + If(Technique.IsMagic, Ib.INT, Ib.STR)) * AttackMultiplier(Short.Parse(If(Technique.Hits, 0)))

            ' If the technique is magic, favour it if it matches the weather.
            If Technique.IsMagic Then
                Select Case Technique.Element.ToUpper
                    Case "FIRE" : If Weather.ToUpper = "HOT" Then tRating *= 1.25
                    Case "ICE" : If Weather.ToUpper = "SNOWY" Then tRating *= 1.25
                    Case "LIGHTNING" : If Weather.ToUpper = "STORMY" Then tRating *= 1.25
                    Case "WATER" : If Weather.ToUpper = "RAINY" Then tRating *= 1.25
                    Case "WIND" : If Weather.ToUpper = "WINDY" Then tRating *= 1.25
                    Case "EARTH" : If Weather.ToUpper = "DRY" Then tRating *= 1.25
                    Case "LIGHT" : If Weather.ToUpper = "BRIGHT" Then tRating *= 1.25
                    Case "DARK" : If Weather.ToUpper = "GLOOMY" Then tRating *= 1.25
                        'Case Else : tRating /= 1.25
                End Select
            End If

            ' If the technique is the same as the last one used, disfavour it.
            If tList = Ib.LastAction Then tRating /= 2.5

            ' Check the TP cost to disfavour inefficient techniques, but not if we're conserving TP.
            If Not Ib.Status.Contains("conserving TP") And Technique.TP < Ic.SkillLevel("Zen") * 5 + 5 Then
                tRating /= Math.Max(CDec(Technique.TP) / (CDec(If(Technique.Power = 0, 1, Technique.Power)) * AttackMultiplier(Technique.Hits)), 1)
            End If

            Select Case Technique.Type
                Case "Attack"
                    For Each mList In currentMonsters
                        AI1_TechniquesCheck_TargetCheck(Ib, mList, tRating, teRating, Technique)
                        Ratings.Add({"Technique", Technique.Name, mList, teRating})
                    Next
                Case "AoE Attack"
                    Dim taRating As Decimal
                    For Each mList In currentMonsters
                        AI1_TechniquesCheck_TargetCheck(Ib, mList, tRating, teRating, Technique)
                        taRating += teRating * 0.6
                    Next
                    Ratings.Add({"Technique", Technique.Name, currentMonsters(0), taRating})
                Case "Heal"
                    For Each pList In currentPlayers
                        ' Healing techniques on zombified players will not be considered.
                        If Not (BattleList(pList).Status.Contains("zombie") Or Characters(pList).IsUndead) Then
                            AI1_TechniquesCheck_TargetCheckHeal(pList, tRating, teRating, Technique)
                            If teRating <> -10000 Then Ratings.Add({"Technique", Technique.Name, pList, teRating})
                        End If
                    Next
                    For Each mList In currentMonsters
                        If BattleList(mList).Status.Contains("zombie") Or Characters(mList).IsUndead Then
                            AI1_TechniquesCheck_TargetCheck(Ib, mList, tRating, teRating, Technique)
                            Ratings.Add({"Technique", Technique.Name, mList, teRating * 2})  ' We multiply by 2 because it bypasses the monster's defense.
                        End If
                    Next
                Case "AoE Heal"
                    Dim taRating As Decimal
                    For Each pList In currentPlayers
                        AI1_TechniquesCheck_TargetCheckHeal(pList, tRating, teRating, Technique)
                        If BattleList(pList).Status.Contains("zombie") Or Characters(pList).IsUndead Then
                            If teRating <> -10000 Then taRating += teRating * 0.7
                        Else
                            If teRating <> -10000 Then taRating += teRating * -3 - 100
                        End If
                    Next
                    Ratings.Add({"Technique", Technique.Name, currentPlayers(0), taRating})
                Case "Suicide"
                    ' Favour suicides if and only if we're badly injured.
                    Select Case Ib.Health
                        Case "Perfect" : tRating *= 0.01
                        Case "Great" : tRating *= 0.04
                        Case "Good" : tRating *= 0.08
                        Case "Decent" : tRating *= 0.13
                        Case "Scratched" : tRating *= 0.18
                        Case "Bruised" : tRating *= 0.25
                        Case "Hurt" : tRating *= 0.33
                        Case "Injured" : tRating *= 0.48
                        Case "Injured Badly" : tRating *= 0.64
                        Case "Critical" : tRating *= 0.8
                        Case "Alive by a hair's bredth", "Alive by a hair's breadth" : teRating *= 1.0
                        Case "Dead" : tRating *= 2.0
                    End Select

                    For Each mList In currentMonsters
                        AI1_TechniquesCheck_TargetCheck(Ib, mList, tRating, teRating, Technique)
                        Ratings.Add({"Technique", Technique.Name, mList, teRating})
                    Next
                Case "AoE Suicide"
                    Select Case Ib.Health
                        Case "Perfect" : tRating *= 0.01
                        Case "Great" : tRating *= 0.04
                        Case "Good" : tRating *= 0.08
                        Case "Decent" : tRating *= 0.13
                        Case "Scratched" : tRating *= 0.18
                        Case "Bruised" : tRating *= 0.25
                        Case "Hurt" : tRating *= 0.33
                        Case "Injured" : tRating *= 0.48
                        Case "Injured Badly" : tRating *= 0.64
                        Case "Critical" : tRating *= 0.8
                        Case "Alive by a hair's bredth", "Alive by a hair's breadth" : teRating *= 1.0
                        Case "Dead" : tRating *= 2.0
                    End Select

                    Dim taRating As Decimal
                    For Each mList In currentMonsters
                        AI1_TechniquesCheck_TargetCheck(Ib, mList, tRating, teRating, Technique)
                        taRating += teRating * 0.65
                    Next
                    Ratings.Add({"Technique", Technique.Name, currentMonsters(0), taRating})
                Case "Clear Status Negative"

            End Select
        Next
    End Sub

    Private Sub AI1_TechniquesCheck_TargetCheck(Ib As Combatant, ByRef mList As String, ByRef tRating As Decimal, ByRef teRating As Decimal, ByRef Technique As TechniqueData)
        teRating = tRating
        Dim bMonster = BattleList(mList), cMonster = Characters(mList)
        ' Check for elemental strengths and weaknesses.
        If cMonster.ElementalResistances IsNot Nothing AndAlso cMonster.ElementalResistances.Contains(Technique.Element) Then teRating = teRating * 0.5 - 10 ' Subtract 10 for the slight strength boost.
        If cMonster.ElementalWeaknesses IsNot Nothing AndAlso cMonster.ElementalWeaknesses.Contains(Technique.Element) Then teRating = teRating * 1.5 + 10 ' Add 10 for the slight defense penalty.
        If cMonster.ElementalImmunities IsNot Nothing AndAlso cMonster.ElementalImmunities.Contains(Technique.Element) Then teRating = 0
        If cMonster.ElementalAbsorbs IsNot Nothing AndAlso cMonster.ElementalAbsorbs.Contains(Technique.Element) Then teRating *= -1

        If cMonster.IsElemental And Technique.IsMagic Then teRating *= 1.3

        ' Check the current monster level.
        If Not (Technique.Type.Contains("Heal") Or mList.ToLower = "orb_fountain" Or mList.ToLower = "demon_portal") Then
            Dim cRatio As Single, LevelDifference As Integer
            cRatio = teRating / Math.Max(BattleLevel * 4.3875, 1)
            LevelDifference = (Ib.STR + Ib.DEF + Ib.INT + Ib.SPD) / 20 - BattleLevel * If(IsBossBattle, 1.05, 1)
            If LevelDifference > 50 Then LevelDifference = 50
            If LevelDifference < -50 Then LevelDifference = -50
            cRatio += 0.05 * LevelDifference
            teRating *= Math.Min(cRatio, 2) * 0.65 + 0.7
            If teRating < 5 Then
                ' We're below the cap.
                teRating = If(Technique.IsMagic, Ib.INT, Ib.STR) / 20
            End If
        End If

        ' Check for status effects.
        For Each Effect In Technique.Status.Split({"."c})
            Select Case Effect.ToLower
                Case "stop"
                    If Not bMonster.Status.Contains("frozen in time") Then teRating += 20 / currentMonsters.Count
                Case "poison"
                    If Not bMonster.Status.Contains("poisoned heavily") Then teRating += 10 + 15 / currentMonsters.Count
                Case "blind"
                    If Not bMonster.Status.Contains("blind") Then teRating += 20 / currentMonsters.Count
                Case "virus"
                    If IsBossBattle And Not bMonster.Status.Contains("inflicted with a virus") Then teRating += 10
                Case "amnesia"
                    If Not bMonster.Status.Contains("under amnesia") And (cMonster.bINT / cMonster.bSTR > 1.5) Then teRating += 25
                Case "paralysis"
                    If Not bMonster.Status.Contains("paralyzed") Then teRating += 10 + 20 / currentMonsters.Count
                Case "zombie"
                    If Not bMonster.Status.Contains("a zombie") Then teRating += 5 + 1 * currentMonsters.Count
                Case "slow"
                    ' Slightly favour slowing techniques because it can cause the monster to effectively lose a turn as the turn order changes.
                    If Not bMonster.Status.Contains("slowed") Then teRating += 8 - 2 * currentMonsters.Count
                Case "stun"
                    If Not bMonster.Status.Contains("stunned") Then teRating += 20 / currentMonsters.Count
                Case "curse"
                    If Not bMonster.Status.Contains("cursed") And (cMonster.bINT / cMonster.bSTR > 1.5) Then teRating += 25
                Case "charm"
                    If Not bMonster.Status.Contains("charmed") Then teRating += 25 + If(currentMonsters.Count = 2, 5, 0)
                Case "intimidate"
                    If Not bMonster.Status.Contains("intimidated") Then teRating += 20 / currentMonsters.Count
                Case "defensedown"
                Case "strengthdown"
                Case "intdown"
                Case "petrify"
                    If Not bMonster.Status.Contains("petrified") Then teRating += 20 / currentMonsters.Count
                Case "bored"
                    If Not bMonster.Status.Contains("bored") Then teRating += 15 + 15 / currentMonsters.Count
                Case "confuse"
                    If Not bMonster.Status.Contains("confused") Then teRating += 20
                Case "random"
                    teRating += 15
            End Select
        Next

        ' Check the monster's health and favour wounded targets.
        Select Case bMonster.Health
            Case "Enhanced" : teRating *= 1.0
            Case "Perfect" : teRating *= 1.0
            Case "Great" : teRating *= 1.01
            Case "Good" : teRating *= 1.02
            Case "Decent" : teRating *= 1.05
            Case "Scratched" : teRating *= 1.1
            Case "Bruised" : teRating *= 1.15
            Case "Hurt" : teRating *= 1.2
            Case "Injured" : teRating *= 1.25
            Case "Injured Badly" : teRating *= 1.35
            Case "Critical" : teRating *= 1.5
            Case "Alive by a hair's bredth", "Alive by a hair's breadth" : teRating *= 1.75
            Case "Dead" : teRating = -10000
        End Select

        If bMonster.Status.Contains("evolving") Then teRating = 0
        If bMonster.Status.Contains("ethereal") And Not Technique.IsMagic Then teRating = 0
    End Sub

    Private Sub AI1_TechniquesCheck_TargetCheckHeal(ByRef pList As String, ByRef tRating As Decimal, ByRef teRating As Decimal, ByRef Technique As TechniqueData)
        teRating = tRating

        If Characters(pList).IsElemental And Technique.IsMagic Then teRating *= 1.3

        If Not (BattleList(pList).Status.Contains("zombie") Or Characters(pList).IsUndead) Then
            ' The player will be healed.
            Select Case BattleList(pList).Health
                Case "Enhanced" : teRating *= 0.3
                Case "Perfect" : teRating *= 0
                Case "Great" : teRating *= 0.05
                Case "Good" : teRating *= 0.1
                Case "Decent" : teRating *= 0.15
                Case "Scratched" : teRating *= 0.25
                Case "Bruised" : teRating *= 0.45
                Case "Hurt" : teRating *= 0.7
                Case "Injured" : teRating *= 1
                Case "Injured Badly" : teRating *= 1.4
                Case "Critical" : teRating *= 1.9
                Case "Alive by a hair's bredth", "Alive by a hair's breadth" : teRating *= 2.5
                Case "Dead" : teRating = -10000
            End Select

            If pList = "AlliedForces_President" Then teRating *= 2
        End If
    End Sub

    Private Sub AI1_SkillsCheck(ByVal Ib As Combatant, ByVal Ic As CharacterData)
        Dim TopRating As Decimal
        If Ratings.Count = 0 Then
            TopRating = 0
        Else
            For j = 0 To Ratings.Count - 1
                TopRating = Math.Max(TopRating, Ratings(j)(3))
            Next
        End If


        ' Mighty Strike
        If Ic.HasSkill("MightyStrike") Then

        End If

        ' Elemental Seal
        If Ic.HasSkill("ElementalSeal") Then

        End If

        ' Shadow Copy
        If Ic.HasSkill("ShadowCopy") And Not Ib.HasUsedShadowCopy Then
            Dim sRating As Decimal = TopRating + 10
            Dim sFactor As Decimal = 0.7 - currentMonsters.Count * 0.1

            Select Case Ib.Health
                Case "Enhanced" : sFactor += 0.09
                Case "Perfect" : sFactor += 0.03
                Case "Great" : sFactor += 0.05
                Case "Good" : sFactor += 0.07
                Case "Decent" : sFactor += 0.09
                Case "Scratched" : sFactor += 0.12
                Case "Bruised" : sFactor += 0.15
                Case "Hurt" : sFactor += 0.12
                Case "Injured" : sFactor += 0.08
                Case "Injured Badly" : sFactor += 0.05
                Case "Critical" : sFactor += 0.03
                Case "Alive by a hair's bredth", "Alive by a hair's breadth" : sFactor += 0.01
                Case "Dead" : sFactor += 0
            End Select

            For Each Monster In currentMonsters
                Select Case BattleList(Monster).Health
                    Case "Perfect" : sFactor += 0.03
                    Case "Great" : sFactor += 0.05
                    Case "Good" : sFactor += 0.07
                    Case "Decent" : sFactor += 0.1
                    Case "Scratched" : sFactor += 0.12
                    Case "Bruised" : sFactor += 0.15
                    Case "Hurt" : sFactor += 0.1
                    Case "Injured" : sFactor += 0.08
                    Case "Injured Badly" : sFactor += 0.06
                    Case "Critical" : sFactor += 0.05
                    Case "Alive by a hair's bredth", "Alive by a hair's breadth" : sFactor += 0.02
                    Case "Dead" : sFactor += 0.1
                End Select

            Next
            Ratings.Add({"Skill", "ShadowCopy", Nothing, sRating * sFactor})
        End If

        ' Cover
        If Ic.HasSkill("Cover") Then

        End If

        ' Analysis
        If Ic.SkillLevel("Analysis") >= 4 Then
            Dim sRating As Decimal = (TopRating + 30) / currentMonsters.Count

            Select Case Ib.Health
                Case "Perfect" : sRating *= 1
                Case "Great" : sRating *= 1
                Case "Good" : sRating *= 1
                Case "Decent" : sRating *= 0.9
                Case "Scratched" : sRating *= 0.8
                Case "Bruised" : sRating *= 0.7
                Case "Hurt" : sRating *= 0.6
                Case "Injured" : sRating *= 0.5
                Case "Injured Badly" : sRating *= 0.4
                Case "Critical" : sRating *= 0.3
                Case "Alive by a hair's bredth", "Alive by a hair's breadth" : sRating *= 0.2
                Case "Dead" : sRating *= 0.1
            End Select

            For Each Monster In currentMonsters
                If BattleList(Monster).Category <> CharacterCategory.Monster Then Continue For
                Dim seRating As Decimal = sRating
                If Characters(Monster).IsWellKnown Then Continue For
                Ratings.Add({"Skill", "Analysis", Monster, seRating})
            Next
        End If

        ' Steal
        If Ic.HasSkill("Steal") Then
            Dim sRating As Decimal
            Select Case Ib.Health
                Case "Enhanced" : sRating = 80
                Case "Perfect" : sRating = 60
                Case "Great" : sRating = 57
                Case "Good" : sRating = 53
                Case "Decent" : sRating = 49
                Case "Scratched" : sRating = 44
                Case "Bruised" : sRating = 39
                Case "Hurt" : sRating = 33
                Case "Injured" : sRating = 25
                Case "Injured Badly" : sRating = 18
                Case "Critical" : sRating = 11
                Case "Alive by a hair's bredth", "Alive by a hair's breadth" : sRating = 3
                Case "Dead" : sRating = 0
            End Select

            sRating /= currentMonsters.Count
            sRating /= Math.Max(BattleLevel, 1) / If(IsBossBattle, 1, 2)
            If TurnNumber > 0 Then sRating /= TurnNumber + 2 ' TODO: Actually keep track of the skill cooldown.

            For Each Monster In currentMonsters
                If BattleList(Monster).Category <> CharacterCategory.Monster Then Continue For
                Dim seRating As Decimal = sRating
                If Monster.ToLower = "orb_fountain" And TurnNumber <= 0 Then seRating = TopRating * 1.25 + 25
                Ratings.Add({"Skill", "Steal", Monster, seRating})
            Next
        End If


        ' Drain Samba
        If Ic.HasSkill("DrainSamba") Then

        End If

        ' Regeneration
        If Ic.HasSkill("Regen") Then

        End If

        ' Provoke
        If Ic.HasSkill("Provoke") Then

        End If

        ' Disarm
        If Ic.HasSkill("Disarm") Then

        End If

        ' Weapon Lock
        If Ic.HasSkill("WeaponLock") Then

        End If

        ' Konzen-Ittai
        If Ic.HasSkill("Konzen-Ittai") Then

        End If
    End Sub
#End Region

#Region "AI commands"

    <Regex("^attack (?<Target>[^.!]*)[.!]?", Nothing, RegexAttribute.CommandScope.Channel, True)>
    Public Sub CommandAIAttack(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Not IsBattleStarted Then
            Say(Connection, Channel, "There's no battle going on at the moment" & Choose(".", ", " & Sender.Split("!"c)(0) & "."))
            Return
        ElseIf CurrentTurn = Nothing Or CurrentTurn <> LoggedIn Then
            Say(Connection, Channel, "It isn't my turn" & Choose(".", ", " & Sender.Split("!"c)(0) & "."))
            Return
        End If

        Dim Target As String = Nothing, Tb As Combatant = Nothing
        ' Find the target.
        If BattleList.TryGetValue(Match.Groups("Target").Value, Tb) Then
            Target = Match.Groups("Target").Value
        Else
            For Each Character In BattleList
                If Character.Value.Name.ToLower = Match.Groups("Target").Value.ToLower Then
                    If Target = Nothing Then Target = Character.Key Else Target = "?"
                    If Target = "?" Then Exit For
                End If
            Next

            If Target = Nothing Then
                Say(Connection, Channel, "$k04" & Match.Groups("Target").Value & "$o isn't in this battle" & Choose(".", ", " & Sender.Split("!"c)(0) & "."))
                Return
            ElseIf Target = "?" Then
                Say(Connection, Channel, "There are multiple $k04" & Match.Groups("Target").Value & "$o in this battle" & Choose(".", ", " & Sender.Split("!"c)(0) & "."))
                Return
            End If
        End If

        AIAction("Attack", Nothing, Target)
    End Sub

    <Regex("^taunt (?<Target>[^.!]*)[.!]?", Nothing, RegexAttribute.CommandScope.Channel, True)>
    Public Sub CommandAITaunt(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Not IsBattleStarted Then
            Say(Connection, Channel, "There's no battle going on at the moment" & Choose(".", ", " & Sender.Split("!"c)(0) & "."))
            Return
        ElseIf CurrentTurn = Nothing Or CurrentTurn <> LoggedIn Then
            Say(Connection, Channel, "It isn't my turn" & Choose(".", ", " & Sender.Split("!"c)(0) & "."))
            Return
        End If

        Dim Target As String = Nothing, Tb As Combatant = Nothing
        ' Find the target.
        If BattleList.TryGetValue(Match.Groups("Target").Value, Tb) Then
            Target = Match.Groups("Target").Value
        Else
            For Each Character In BattleList
                If Character.Value.Name.ToLower = Match.Groups("Target").Value.ToLower Then
                    If Target = Nothing Then Target = Character.Key Else Target = "?"
                    If Target = "?" Then Exit For
                End If
            Next

            If Target = Nothing Then
                Say(Connection, Channel, "$k04" & Match.Groups("Target").Value & "$o isn't in this battle" & Choose(".", ", " & Sender.Split("!"c)(0) & "."))
                Return
            ElseIf Target = "?" Then
                Say(Connection, Channel, "There are multiple $k04" & Match.Groups("Target").Value & "$o in this battle" & Choose(".", ", " & Sender.Split("!"c)(0) & "."))
                Return
            End If
        End If

        AIAction("Taunt", Nothing, Target)
    End Sub

    <Regex("^use your (?<Technique>[^ ]*)( on (?<Target>[^.!]*))?[.!]?", Nothing, RegexAttribute.CommandScope.Channel, True)>
    Public Sub CommandAITechnique(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Not IsBattleStarted Then
            Say(Connection, Channel, "There's no battle going on at the moment" & Choose(".", ", " & Sender.Split("!"c)(0) & "."))
            Return
        ElseIf CurrentTurn = Nothing Or CurrentTurn <> LoggedIn Then
            Say(Connection, Channel, "It isn't my turn" & Choose(".", ", " & Sender.Split("!"c)(0) & "."))
            Return
        End If

        Dim Tt As TechniqueData
        If Not Characters(LoggedIn).Techniques.ContainsKey(Match.Groups("Technique").Value) Then
            Say(Connection, Channel, "I " & Choose(Choose("do not ", "don't ") & "have ", Choose("do not ", "don't ") & "know ", Choose("cannot ", "can't ") & Choose("use ", "perform ")) & "the $k04" & Match.Groups("Technique").Value & "$o technique" & Choose(".", ", " & Sender.Split("!"c)(0) & "."))
            Return
        Else
            Tt = Techniques(Match.Groups("Technique").Value)
        End If

        ' Find a weapon that has the technique
        Dim TopWeapon As String, TopWeaponPower As Decimal = 0
        For Each wList In Weapons
            If Not Weapons(wList.Key).Techniques.Contains(Match.Groups("Technique").Value, System.StringComparer.OrdinalIgnoreCase) Then Continue For

            Dim WeaponPower As Decimal = Weapons(wList.Key).Power + Characters(LoggedIn).Weapons(wList.Key) * 1.5 * {1, 1, 1, 1, 1, 1, 1, 1, 1}(Weapons(wList.Key).Hits)
            If WeaponPower > TopWeaponPower Then
                TopWeapon = wList.Key
                TopWeaponPower = WeaponPower
            End If
        Next

        If TopWeapon = Nothing Then
            Say(Connection, Channel, Choose("I " & Choose("don't ", "do not ") & "have any weapon " & Choose("with that technique", "to " & Choose("use ", "perform ") & "that technique"), "None of my weapons have that technique") & Choose(".", ", " & Sender.Split("!"c)(0) & "."))
            Return
        End If

        If Characters(LoggedIn).EquippedWeapon <> TopWeapon Then
            AIAction("Equip", Nothing, TopWeapon)
            Threading.Thread.Sleep(600)
        End If


        If Tt.Type = "Boost" Or Tt.Type = "Final Getsuga" Then
            AIAction("Technique", Match.Groups("Technique").Value, Nothing)
            Return
        End If

        Dim Target As String = Nothing, Tb As Combatant = Nothing
        ' Find the target.
        If BattleList.TryGetValue(Match.Groups("Target").Value, Tb) Then
            Target = Match.Groups("Target").Value
        Else
            For Each Character In BattleList
                If Character.Value.Name.ToLower = Match.Groups("Target").Value.ToLower Then
                    If Target = Nothing Then Target = Character.Key Else Target = "?"
                    If Target = "?" Then Exit For
                End If
            Next

            If Target = Nothing Then
                If Tt.Type = "AoE Attack" Or Tt.Type = "AoE Suicide" Then
                    CurrentAbility = Match.Groups("Technique").Value
                    DoBattle(ChrW(1) & "ACTION uses " & Characters(LoggedIn).GenderWord.tolower & " " & Match.Groups("Technique").Value & " on " & currentMonsters(0) & ChrW(1))
                    Return
                End If
                If Tt.Type = "AoE Heal" Then
                    CurrentAbility = Match.Groups("Technique").Value
                    DoBattle(ChrW(1) & "ACTION uses " & Characters(LoggedIn).GenderWord.tolower & " " & Match.Groups("Technique").Value & " on " & currentPlayers(0) & ChrW(1))
                    Return
                End If
                Say(Connection, Channel, "$k04" & Match.Groups("Target").Value & "$o isn't in this battle" & Choose(".", ", " & Sender.Split("!"c)(0) & "."))
                Return
            ElseIf Target = "?" Then
                Say(Connection, Channel, "There are multiple $k04" & Match.Groups("Target").Value & "$o in this battle" & Choose(".", ", " & Sender.Split("!"c)(0) & "."))
                Return
            End If
        End If

        AIAction("Technique", Match.Groups("Technique").Value, Target)
    End Sub
#End Region

    Public Sub DoBattle(ByVal Message As String)
        If dccSocket Is Nothing OrElse Not dccSocket.Connected Then
            Say(ArenaConnection, ArenaChannel, Message, SayOptions.NoticeNever)
        Else
            DCCSend(Message, Nothing)
        End If
    End Sub

    Public Sub DoBattlePrivate(ByVal Message As String)
        If dccSocket Is Nothing OrElse Not dccSocket.Connected Then
            Say(ArenaConnection, ArenaNickname, Message, SayOptions.NoticeNever)
        Else
            DCCSend(Message, Nothing)
        End If
    End Sub
End Class