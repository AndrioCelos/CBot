' General to-do list:
'   TODO: Add an option for players to specify how long they must be idle before the bot will control them.
'   TODO: Improve the AI with regards to discovering elemental weaknesses, and give it the ability to use buffs and more skills.
'   TODO: Finish the tutorials.
'   TODO: Add functionality to automatically start Gauntlet battles at regular and/or random times.
'   TODO: Add functionality to automatically start PvP battles on request from players.

Imports VBot
Imports System.Text.RegularExpressions
Imports System.Text

''' <summary>The main class of the Battle Bot plugin.</summary>
Public Class BattleBot
    Inherits Plugin

    Public Overrides ReadOnly Property Name As String
        Get
            Return "Battlebot"
        End Get
    End Property

    Public Overrides ReadOnly Property UseGlobalKeyCommand As Boolean
        Get
            Return True
        End Get
    End Property

#Region "Variables"

    ''' <summary>The short name of the character we are logged in as. Will conatin Nothing if we're not logged in.</summary>
    Public LoggedIn As String
    Public Version As ArenaVersion
    Public VersionPreCTCP As Boolean
    Public BattleLevel As Integer
    Public TurnNumber As Integer

    Public ViewingWeaponsCharacter As String = ""
    Public ViewingStatsCharacter As String

    Public ViewingInfoWeapon As String = ""
    Public ViewingInfoTechnique As String = ""
    Public ViewingInfoItem As String = ""
    Public ViewingInfoSkill As String = ""
    Public RepeatCommand As Short

    Public TempSkills As String
    Public WaitingForOwnTechniques As Boolean
    Public WaitingForOwnSkills As Boolean

    '''''''' Arena data ''''''''
    ' TODO: Have the bot find this itself using WHO.
    Public ArenaConnection As IRCConnection
    Public ArenaChannel As String
    Public ArenaNickname As String = "BattleArena"

    Public ArenaDirectory As String = Nothing

    Public NoMonsterFix As Boolean = False

    Public OwnCharacters As New Dictionary(Of String, OwnCharacterData)(StringComparer.OrdinalIgnoreCase)
    Public Characters As New Dictionary(Of String, CharacterData)(StringComparer.OrdinalIgnoreCase)
    Public Techniques As New Dictionary(Of String, TechniqueData)(StringComparer.OrdinalIgnoreCase)
    Public Weapons As New Dictionary(Of String, WeaponData)(StringComparer.OrdinalIgnoreCase)
    Public WithEvents ListRequestTimer As Timers.Timer

    Public Entering As Boolean

    Public EntryTime As TimeSpan
    Public ListCheckTimer As Timers.Timer
    Public EntryStopwatch As Stopwatch

    '''''''' Arena status ''''''''
    Public IsBattleOpen As Boolean
    Public IsBattleStarted As Boolean

    Public BattleWaitTime As Date

    Public IsOwner As Boolean = False
    Public IsOwnerChecked As Date = Nothing
    Public WithEvents IsOwnerCheckTimer As New Timers.Timer
    Public IsOwnerChecking As Boolean = False

    Public BattleList As New Dictionary(Of String, Combatant)(StringComparer.OrdinalIgnoreCase)
    Public currentPlayers As New List(Of String)
    Public currentMonsters As New List(Of String)
    Public currentAllies As New List(Of String)
    Public SlainCharacters As New List(Of String)
    Public MissingPlayers As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
    Public UnmatchedFullNames As New List(Of UnmatchedName)
    Public UnmatchedShortNames As New List(Of UnmatchedName)
    Public LastName As String
    ' These are characters that have tried to enter the battle using a command but haven't been registered yet.
    Public PendingPlayers As New Dictionary(Of String, Date)(StringComparer.OrdinalIgnoreCase)
    Public PendingMonsters As New Dictionary(Of String, Date)(StringComparer.OrdinalIgnoreCase)
    Public PendingAllies As New Dictionary(Of String, Date)(StringComparer.OrdinalIgnoreCase)

    Public number_of_monsters_needed As Integer  ' This is used to deduce the darkness timer.
    Public Weather As String
    Public IsBossBattle As Boolean               ' Whether it's a boss battle.
    Public IsPVPBattle As Boolean
    Public IsNPCBattle As Boolean
    Public BattleStartTime As Date               ' The time when the battle started.
    Public FiveMinuteWarning As Date
    Public DarknessHasRisen As Boolean           ' Whether darkness has overcome the battlefield already.
    Public TurnsToDarkness As Short = -1
    Public HolyAuraUser As String
    Public HolyAuraEnd As Date
    Public HolyAuraTurns As Short = -1

    ' In Battle Arena there are many events that can change battles.
    Public CurrentEvent As BattleEvents

    Public CurrentTurn As String
    Public CurrentAction As String
    Public CurrentAbility As String
    Public CurrentTarget As String
    Public CurrentAoEHit As Boolean
    Public Counterer As String

    Public BetAmount As Integer
    Public TotalBetAmount As Integer
    Public BetOnAlly As Boolean

    Public WaitingForRegistration() As String = Nothing
    Public WaitingForBattleList As Boolean = False

    ''' <summary>Determines whether the bot will participate in battles.</summary>
    Public EnableParticipation As Boolean = False
    ''' <summary>The minimum number of players who must enter the battle before the bot will.</summary>
    Public MinimumPlayersToEnter As Integer = 1
    ''' <summary>Determines whether the bot will analyze the Arena combatants.</summary>
    Public EnableAnalysis As Boolean = True
    ''' <summary>Determines whether the bot will purchase upgrades from the shop.</summary>
    Public EnableUpgrades As Boolean = False
    ''' <summary>Determines whether the bot will purchase items from the shop.</summary>
    Public EnablePurchases As Boolean = False
    ''' <summary>Determines whether the bot will purchase items from the shop.</summary>
    Public EnableGambling As Boolean = True
    Public AI As Short = 1

    ''' <summary>A list of players the bot is controlling.</summary>
    Public Controlling As New List(Of String)

#End Region

#Region "Subclasses"
    Public Structure ArenaVersion
        Public Major As Integer
        Public Minor As Integer
        Public Revision As Integer
        Public BetaDate As Date

        Public Sub New(Version As String)
            Dim m = Regex.Match(Version, "(\d+)\.(\d+)(\.(\d+))?(beta_(\d\d)(\d\d)(\d\d))?")
            If Not m.Success Then Throw New FormatException
            Major = m.Groups(1).Value
            Minor = m.Groups(2).Value
            Revision = If(m.Groups(3).Success, m.Groups(4).Value, 0)
            If m.Groups(5).Success Then BetaDate = New Date(m.Groups(8).Value, m.Groups(6).Value, m.Groups(7).Value)
        End Sub

        Public Sub New(Major As Integer, Minor As Integer)
            MyClass.New(Major, Minor, 0, Nothing)
        End Sub
        Public Sub New(Major As Integer, Minor As Integer, BetaDate As Date)
            MyClass.New(Major, Minor, 0, BetaDate)
        End Sub
        Public Sub New(Major As Integer, Minor As Integer, Revision As Integer)
            MyClass.New(Major, Minor, Revision, Nothing)
        End Sub
        Public Sub New(Major As Integer, Minor As Integer, Revision As Integer, BetaDate As Date)
            Me.Major = Major
            Me.Minor = Minor
            Me.Revision = Revision
            Me.BetaDate = BetaDate
        End Sub

        Public Shared Function Empty() As ArenaVersion
            Return New ArenaVersion(0, 0, 0, Nothing)
        End Function

        Public Function IsEmpty() As Boolean
            Return Me = ArenaVersion.Empty
        End Function

        ''' <summary>Returns true if the two version numbers are equal.</summary>
        Public Shared Operator =(Operand1 As ArenaVersion, Operand2 As ArenaVersion)
            Return Operand1.Major = Operand2.Major AndAlso Operand1.Minor = Operand2.Minor AndAlso Operand1.BetaDate = Operand2.BetaDate
        End Operator

        ''' <summary>Returns true if the two version numbers are unequal.</summary>
        Public Shared Operator <>(Operand1 As ArenaVersion, Operand2 As ArenaVersion)
            Return Operand1.Major <> Operand2.Major OrElse Operand1.Minor <> Operand2.Minor OrElse Operand1.BetaDate <> Operand2.BetaDate
        End Operator

        ''' <summary>Returns true if the left hand side version number is older than the right hand side.</summary>
        Public Shared Operator <(Operand1 As ArenaVersion, Operand2 As ArenaVersion)
            If Operand1.Major < Operand2.Major Then Return True
            If Operand1.Major > Operand2.Major Then Return False
            If Operand1.Minor < Operand2.Minor Then Return True
            If Operand1.Minor > Operand2.Minor Then Return False
            If Operand1.BetaDate = Nothing Then Return False
            If Operand2.BetaDate = Nothing And Operand1.BetaDate <> Nothing Then Return True
            Return Operand1.BetaDate < Operand2.BetaDate
        End Operator

        ''' <summary>Returns true if the left hand side version number is later than the right hand side.</summary>
        Public Shared Operator >(Operand1 As ArenaVersion, Operand2 As ArenaVersion)
            If Operand1.Major > Operand2.Major Then Return True
            If Operand1.Major < Operand2.Major Then Return False
            If Operand1.Minor > Operand2.Minor Then Return True
            If Operand1.Minor < Operand2.Minor Then Return False
            If Operand2.BetaDate = Nothing Then Return False
            If Operand1.BetaDate = Nothing And Operand2.BetaDate <> Nothing Then Return True
            Return Operand1.BetaDate > Operand2.BetaDate
        End Operator

        ''' <summary>Returns true if the left hand side version number is older than or equal to the right hand side.</summary>
        Public Shared Operator <=(Operand1 As ArenaVersion, Operand2 As ArenaVersion)
            If Operand1.Major < Operand2.Major Then Return True
            If Operand1.Major > Operand2.Major Then Return False
            If Operand1.Minor < Operand2.Minor Then Return True
            If Operand1.Minor > Operand2.Minor Then Return False
            If Operand1.BetaDate = Nothing And Operand2.BetaDate <> Nothing Then Return False
            If Operand2.BetaDate = Nothing Then Return True
            Return Operand1.BetaDate <= Operand2.BetaDate
        End Operator

        ''' <summary>Returns true if the left hand side version number is later than or equal to the right hand side.</summary>
        Public Shared Operator >=(Operand1 As ArenaVersion, Operand2 As ArenaVersion)
            If Operand1.Major > Operand2.Major Then Return True
            If Operand1.Major < Operand2.Major Then Return False
            If Operand1.Minor > Operand2.Minor Then Return True
            If Operand1.Minor < Operand2.Minor Then Return False
            If Operand2.BetaDate = Nothing And Operand1.BetaDate <> Nothing Then Return False
            If Operand1.BetaDate = Nothing Then Return True
            Return Operand1.BetaDate >= Operand2.BetaDate
        End Operator

        Public Overrides Function ToString() As String
            Return Major.ToString() & "." & Minor.ToString() & If(Revision <> 0, "." & Revision.ToString(), "") & If(BetaDate = Nothing, "", "beta" & BetaDate.ToString("MMddyy"))
        End Function
    End Structure

    Public Class UnmatchedName
        Public Name As String
        Public Category As CharacterCategory
        Public Description As String
    End Class

    Public Enum CharacterCategory As Short
        Player = 1
        Ally = 2
        Monster = 4
    End Enum

    Public Enum BattleEvents
        None
        CurseNight  ' A curse falls on the battlefield, draining everyone's TP.
        BloodMoon   ' The Blood Moon rises. Stealing is affected, and healing becomes less effective while draining becomes more effective.
        MeleeLock   ' A symbol appears on the ground, indicating that techniques cannot be used this battle.
    End Enum

    Enum Pings As Short
        BotVersion
        Skills
    End Enum

    Enum Size As Short
        Small = 1
        Medium = 0
        Large = 2
        Other = -1
    End Enum

    Enum Gender As Short
        Male = 0
        Female = 1
        Other = 2
        Unknown = -1
    End Enum

    ''' <summary>Contains data relating to a character that the bot owns.</summary>
    Public Class OwnCharacterData
        Public FullName As String
        Public Password As String
    End Class

    ''' <summary>Contains data relating to a character that is in the current battle.</summary>
    Public Class Combatant
        ''' <summary>The character's alias, used in commands.</summary>
        Public ShortName As String
        ''' <summary>The character's full name.</summary>
        Public Name As String
        ''' <summary>Player, Ally or Monster.</summary>
        Public Category As CharacterCategory = 7

        ''' <summary>Health: how much damage this character can sustain before being killed.</summary>
        Public HP As Integer
        Public Health As String = "Perfect"
        ''' <summary>Damage: how much damage this character has already taken.</summary>
        Public Damage As Integer
        Public DamagePercent As Decimal
        ''' <summary>TP</summary>
        Public TP As Integer
        ''' <summary>Strength</summary>
        Public STR As Integer
        ''' <summary>Defense</summary>
        Public DEF As Integer
        ''' <summary>Intelligence</summary>
        Public INT As Integer
        ''' <summary>Speed</summary>
        Public SPD As Integer

        Public Status As String() = {}

        Public TurnNumber As Integer

        Public IsRoyalGuarded As Boolean
        Public IsManaWalled As Boolean
        Public UtsusemiShadows As Short
        Public IsUsingMightyStrike As Boolean
        Public IsUsingElementalSeal As Boolean
        Public IsUsingThirdEye As Boolean
        Public HasUsedShadowCopy As Boolean
        Public HasUsedBloodPact As Boolean
        Public HasUsedScavenge As Boolean
        Public HasUsedMagicShift As Boolean

        Public LastAction As String

        Public DamageGiven As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
        Public DamageTaken As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
        Public HealingGiven As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
        Public HealingTaken As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)

        Public Odds As Decimal = 1

        Public Sub New()
        End Sub
        Public Sub New(Character As CharacterData)
            Category = Character.Category
            HP = Character.bHP
            Name = Character.Name
            ShortName = Character.ShortName
            TP = Character.bTP
            STR = Character.bSTR
            DEF = Character.bDEF
            INT = Character.bINT
            SPD = Character.bSPD
            IsRoyalGuarded = Character.StartsWithRoyalGuard
            IsManaWalled = Character.StartsWithManaWall
            IsUsingMightyStrike = Character.StartsWithMightyStrike
            IsUsingElementalSeal = Character.StartsWithElementalSeal
            UtsusemiShadows = Character.StartsWithUtsusemi
        End Sub

        Public Function IsUsingUtsusemi() As Boolean
            Return UtsusemiShadows > 0
        End Function
    End Class

    ''' <summary>Contains data relating to a character, either a monster or an ally.</summary>
    Public Class CharacterData
        ''' <summary>The character's full name.</summary>
        Public Name As String
        ''' <summary>Player, Ally or Monster.</summary>
        Public Category As CharacterCategory
        ''' <summary>The character's alias, used in commands.</summary>
        Public ShortName As String
        Public Gender As String
        Public Description As String

        Public RedOrbs As Integer, BlackOrbs As Integer, AlliedNotes As Integer, DoubleDollars As Integer

        ''' <summary>Base Health: this character's HP capacity.</summary>
        Public bHP As Integer
        ''' <summary>Base TP: this character's TP capacity.</summary>
        Public bTP As Integer
        ''' <summary>Ignition Capacity</summary>
        Public bIG As Integer
        ''' <summary>Base Strength</summary>
        Public bSTR As Integer
        ''' <summary>Base Defense</summary>
        Public bDEF As Integer
        ''' <summary>Base Intelligence</summary>
        Public bINT As Integer
        ''' <summary>Base Speed</summary>
        Public bSPD As Integer

        Public Power As Integer

        Public IgnitionCharge As Integer
        Public RoyalGuardCharge As Long

        Public Status As New List(Of String)

        Public EquippedWeapon As String
        Public EquippedAccessory As String

        ''' <summary>The character's elemental resistances.</summary>
        Public ElementalResistances As List(Of String)
        ''' <summary>The character's elemental weaknesses.</summary>
        Public ElementalWeaknesses As List(Of String)
        ''' <summary>The character's weapon weaknesses.</summary>
        Public WeaponResistances As List(Of String)
        ''' <summary>The character's weapon weaknesses.</summary>
        Public WeaponWeaknesses As List(Of String)
        ''' <summary>The elements this character absorbs to heal.</summary>
        Public ElementalAbsorbs As List(Of String)
        ''' <summary>The elements this character is immune to.</summary>
        Public ElementalImmunities As List(Of String)
        ''' <summary>The effects this character is immune to.</summary>
        Public StatusImmunities As List(Of String)

        Public IsWellKnown As Boolean

        ''' <summary>True if this character is undead, and will be damaged by healing effects.</summary>
        Public IsUndead As Boolean = False
        ''' <summary>True if this character is an elemental, and weak against magic.</summary>
        Public IsElemental As Boolean = False
        ''' <summary>True if this character is summoned.</summary>
        Public IsSummon As Boolean = False
        ''' <summary>True if this character is ethereal, and can't be harmed by standard attacks.</summary>
        Public IsEthereal As Boolean = False

        ''' <summary>True if this character attacks its allies.</summary>
        Public AttacksAllies As Boolean = False

        Public NumberOfTechniques As Integer
        Public HasIgnition As Boolean
        Public HasMech As Boolean
        Public Rating As Integer
        Public NPCBattlesFought As Integer

        ''' <summary>The weapons this character owns, as [name, level].</summary>
        Public Weapons As Dictionary(Of String, Integer)
        ''' <summary>The techniques this character knows, as [name, level].</summary>
        Public Techniques As Dictionary(Of String, Integer)
        ''' <summary>The skills this character has, as [name, level].</summary>
        Public Skills As Dictionary(Of String, Integer)

        Public Items As Dictionary(Of String, Integer)

        Public Styles As Dictionary(Of String, Short)
        Public StyleExperience As Dictionary(Of String, Integer)

        Public CurrentStyle As String

        Public Ignitions As List(Of String)

        Public StartsWithRoyalGuard As Boolean
        Public StartsWithManaWall As Boolean
        Public StartsWithUtsusemi As Integer
        Public StartsWithElementalSeal As Boolean
        Public StartsWithMightyStrike As Boolean
        Public ShadowCopyName As String

        ''' <summary>The techniques that this character can use with their currently equipped weapon.</summary>
        Public EquippedWeaponTechs As List(Of String)
        ''' <summary>How well the bot can control this character. (I don't intend it to be scary. :-) )</summary>
        Public IsReadyToControl As Boolean

        Public Function HasWeapon(ByVal WeaponName As String) As Boolean
            If Weapons Is Nothing Then Return False
            Return Weapons.ContainsKey(WeaponName)
        End Function
        Public Function WeaponLevel(ByVal WeaponName As String) As Integer
            If Not HasWeapon(WeaponName) Then Return 0
            Return Weapons(WeaponName)
        End Function

        Public Function HasTechnique(ByVal TechniqueName As String) As Boolean
            If Techniques Is Nothing Then Return False
            Return Techniques.ContainsKey(TechniqueName)
        End Function
        Public Function TechniqueLevel(ByVal TechniqueName As String) As Integer
            If Not HasTechnique(TechniqueName) Then Return 0
            Return Techniques(TechniqueName)
        End Function

        Public Function HasSkill(ByVal SkillName As String) As Boolean
            If Skills Is Nothing Then Return False
            Return Skills.ContainsKey(SkillName)
        End Function
        Public Function SkillLevel(ByVal SkillName As String) As Integer
            If Not HasSkill(SkillName) Then Return 0
            Return Skills(SkillName)
        End Function

        Public Function Level() As Single
            Return (bSTR + bDEF + bINT + bSPD) / 20
        End Function

        Public ReadOnly Property GenderPronoun
            Get
                Select Case If(Gender, "").ToLower
                    Case "male"
                        Return "His"
                    Case "female"
                        Return "Her"
                    Case "neither", "none"
                        Return "Its"
                    Case Else
                        Return "Their"
                End Select
            End Get
        End Property
        Public ReadOnly Property GenderPronoun2
            Get
                Select Case Gender.ToLower
                    Case "male"
                        Return "He"
                    Case "female"
                        Return "She"
                    Case "neither", "none"
                        Return "It"
                    Case Else
                        Return "They"
                End Select
            End Get
        End Property
    End Class

    ''' <summary>Contains data relating to a (player) weapon.</summary>
    Public Class WeaponData
        ''' <summary>The weapon's name</summary>
        Public Name As String

        Public IsWellKnown As Boolean

        ''' <summary>The weapon category: HandToHand, Sword, Wand etc.</summary>
        Public Category As String
        ''' <summary>The weapon size: short, medium or large.</summary>
        Public Size As Size = Size.Medium
        ''' <summary>The cost to buy this weapon in black orbs.</summary>
        Public Cost As Integer
        ''' <summary>The cost to upgrade this weapon in red orbs.</summary>
        Public UpgradeCost As Integer

        Public Hits As String
        Public HitsMin As Short
        Public HitsMax As Short

        ''' <summary>Base power</summary>
        Public Power As Integer
        ''' <summary>Status effect inflicted on the target/s, if any.</summary>
        Public Status As String
        ''' <summary>The element, if any, that this weapon is aligned with.</summary>
        Public Element As String

        ''' <summary>Techniques that can be performed using this weapon.</summary>
        Public Techniques As List(Of String)
    End Class

    ''' <summary>Contains data relating to a (player) technique.</summary>
    Public Class TechniqueData
        ''' <summary>The technique name</summary>
        Public Name As String
        ''' <summary>The technique description</summary>
        Public Description As String
        ''' <summary>The technique type: Single, AoE, Heal, AoE-Heal, Status or AoE-Status.</summary>
        Public Type As String
        Public Hits As String
        Public IsWellKnown As Boolean

        ''' <summary>Base power</summary>
        Public Power As Integer
        ''' <summary>Status effect inflicted on the target/s, if any.</summary>
        Public Status As String
        ''' <summary>The amount of TP required to use this technique.</summary>
        Public TP As Integer
        ''' <summary>The base price in red orbs.</summary>
        Public Cost As Integer
        ''' <summary>Whether this technique affects an entire side of the field.</summary>
        Public IsAoE As Boolean
        ''' <summary>Whether this technique is a magical spell.</summary>
        Public IsMagic As Boolean
        ''' <summary>The element channeled with this technique, if any.</summary>
        Public Element As String
    End Class

#End Region

    Public Sub New(ByVal Key As String)
        LoadSettings(Key)
        LoadData("BattleArena-" & Key & ".ini")
    End Sub

    Public Overrides Sub OnSave()
        SaveSettings()
        SaveData()
    End Sub

    Public Overrides Sub OnChannelJoinSelf(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String)
        MyBase.OnChannelJoinSelf(Connection, Sender, Channel)

        If ArenaChannel Is Nothing OrElse Not Connection.Channels.ContainsKey(ArenaChannel) Then
            ArenaConnection = Connection
            ArenaChannel = Channel

            Say(Connection, ArenaNickname, ChrW(1) & "BOTVERSION" & ChrW(1), SayOptions.NoticeNever)
            Say(Connection, ArenaNickname, ChrW(1) & "PING " & Pings.BotVersion & ChrW(1), SayOptions.NoticeNever)
            If OwnCharacters.ContainsKey(Nick(Connection)) Then
                Say(Connection, ArenaNickname, "!id " & OwnCharacters(Nick(Connection)).Password, SayOptions.NoticeNever)
                'ElseIf EnableParticipation Then
                '    Say(Connection, ArenaNickname, "!new char", SayOptions.NoticeNever)
            End If
        End If
    End Sub

    Public Overrides Sub OnPrivateNotice(Connection As IRCConnection, Sender As String, Message As String)
        MyBase.OnPrivateNotice(Connection, Sender, Message)

        If Message.StartsWith(ChrW(1)) Then
            If Message.StartsWith(ChrW(1) & "BOTVERSION ") Then
                Version = New ArenaVersion(Message.Substring(12).TrimEnd(ChrW(1)))
                WriteMessage(1, 12, "The Arena is running version " & Version.ToString & ".")
            ElseIf Message.StartsWith(ChrW(1) & "PING ") Then
                Select Case Message.TrimEnd(ChrW(1)).Split({" "c})(1)
                    Case Pings.Skills
                        TempSkills = Nothing
                        WaitingForOwnSkills = False
                    Case Pings.BotVersion
                        If Version.IsEmpty() Then VersionPreCTCP = True
                End Select
            End If
        End If
    End Sub

    Public Overrides Sub OnNicknameChangeSelf(ByVal Connection As VBot.IRCConnection, ByVal Sender As VBot.IRCConnection.IRCUser, ByVal NewNick As String)
        MyBase.OnNicknameChangeSelf(Connection, Sender, NewNick)

        If Connection Is ArenaConnection Then
            LoggedIn = Nothing

            If OwnCharacters.ContainsKey(Nick(Connection)) Then
                Say(Connection, ArenaNickname, "!id " & OwnCharacters(Nick(Connection)).Password, SayOptions.NoticeNever)
                'ElseIf EnableParticipation Then
                '    Say(Connection, ArenaNickname, "!new char", SayOptions.NoticeNever)
            End If
        End If
    End Sub

    Friend Function Nick(ByVal Connection As IRCConnection)
        If Connection Is Nothing Then Return DCCNickname Else Return Connection.Nickname
    End Function

    <System.AttributeUsage(System.AttributeTargets.Method)>
    Public Class ArenaRegexAttribute
        Inherits System.Attribute

        Public Expressions() As String

        Sub New(ByVal Expression As String)
            MyClass.New({Expression})
        End Sub
        Sub New(ByVal Expressions() As String)
            Me.Expressions = Expressions
        End Sub
    End Class

    ''' <summary>Checks for a regular expression in a message received by the client, and runs a command if there is.</summary>
    ''' <param name="Connection">The IRC connection the message was received on, or Nothing to redirect to a custom method.</param>
    ''' <param name="Sender">The hostmask of the user sending the message.</param>
    ''' <param name="Channel">The channel or custom method to send responses on.</param>
    ''' <param name="Message">The message that was received.</param>
    ''' <returns>True if a command was found; False otherwise.</returns>
    Public Function RunArenaRegex(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Message As String) As Boolean
        Dim Expressions() As String
        Dim Method As Reflection.MethodInfo, attr As Object, Match As System.Text.RegularExpressions.Match

        RunArenaRegex = False

        For Each Method In Me.GetType.GetMethods
            For Each attr In Method.GetCustomAttributes(False)
                If TypeOf attr Is RegexAttribute Then
                    Expressions = CType(attr, RegexAttribute).Expressions

                    For Each Expression In Expressions
                        Match = System.Text.RegularExpressions.Regex.Match(Message, Expression, RegularExpressions.RegexOptions.IgnoreCase)
                        If Match.Success Then
                            Dim Args = Method.GetParameters
                            If Args.Count = 4 Then
                                Try
                                    Method.Invoke(Me, {Connection, Sender, Channel, Match})
                                Catch ex As Exception
                                    LogError(Method.Name, ex)
                                End Try
                                Return True
                            ElseIf Args.Count = 5 Then
                                Dim Handled As Boolean
                                Try
                                    Method.Invoke(Me, {Connection, Sender, Channel, Match, Handled})
                                Catch ex As Exception
                                    LogError(Method.Name, ex)
                                End Try
                                If Handled Then Return True
                            End If
                            Return True
                        End If
                    Next
                End If
            Next
        Next
    End Function

#Region "Filing"

    ''' <summary>
    ''' Loads plugin settings from the plugin's configuration file.
    ''' </summary>
    ''' <param name="Key">This plugin's key. Pass the return value of MyKey outside of the New procedure.</param>
    Private Sub LoadSettings(ByVal Key As String)
        If My.Computer.FileSystem.FileExists("Config\" & Key & ".ini") Then
            Dim Reader = My.Computer.FileSystem.OpenTextFileReader("Config\" & Key & ".ini"), s As String, Section As String = ""
            Do Until Reader.EndOfStream
                s = Reader.ReadLine
                ' Check for a comment.
                If s.TrimStart.StartsWith(";") Then Continue Do

                Dim Match As System.Text.RegularExpressions.Match
                Match = System.Text.RegularExpressions.Regex.Match(s, "^\s*\[(?<Section>.*?)\]?\s*$")
                If Match.Success Then
                    Section = Match.Groups("Section").Value
                    Continue Do
                End If

                Match = System.Text.RegularExpressions.Regex.Match(s, "^\s*(?<Property>(?>[^=]*))=(?<Value>.*)$")
                If Match.Success Then
                    Dim Identifier = Match.Groups("Property").Value
                    Dim Value = Match.Groups("Value").Value

                    If Section.ToLower = "enable" Then
                        Select Case Identifier.ToLower
                            Case "participation"
                                EnableParticipation = Value
                            Case "analysis"
                                EnableAnalysis = Value
                            Case "upgrades"
                                EnableUpgrades = Value
                            Case "purchases"
                                EnablePurchases = Value
                            Case "gambling", "betting", "bets"
                                EnableGambling = Value
                            Case "ai"
                                If Value = 0 Or Value = 1 Then AI = Value
                            Case "minplayers", "minimumplayers", "minplayerstoenter", "minimumplayerstoenter"
                                MinimumPlayersToEnter = Value
                        End Select
                    ElseIf Section.ToLower = "arena" Then
                        If Identifier.ToLower = "botnickname" Then
                            ArenaNickname = Value
                        ElseIf Identifier.ToLower = "nomonsterfix" Then
                            NoMonsterFix = {"TRUE", "YES", "ON", "ENABLED"}.Contains(Value.ToUpper)
                        End If
                    End If
                    Continue Do
                End If
            Loop
            Reader.Close()
        End If
    End Sub

    ''' <summary>
    ''' Saves plugin settings to the plugin's configuration file.
    ''' </summary>
    Private Sub SaveSettings()
        If Not My.Computer.FileSystem.DirectoryExists("Config") Then My.Computer.FileSystem.CreateDirectory("Config")
        Dim writer = My.Computer.FileSystem.OpenTextFileWriter("Config\" & MyKey & ".ini", False)
        writer.WriteLine("[Enable]")
        writer.WriteLine("Analysis=" & EnableAnalysis)
        writer.WriteLine("Participation=" & EnableParticipation)
        writer.WriteLine("Upgrades=" & EnableUpgrades)
        writer.WriteLine("Purchases=" & EnablePurchases)
        writer.WriteLine("Gambling=" & EnableGambling)
        writer.WriteLine("AI=" & AI)
        writer.WriteLine("MinPlayers=" & MinimumPlayersToEnter)
        writer.WriteLine()
        writer.WriteLine("[Arena]")
        writer.WriteLine("BotNickname=" & ArenaNickname)
        writer.WriteLine("NoMonsterFix=" & If(NoMonsterFix, "On", "Off"))
        writer.Close()
    End Sub

    ''' <summary>Loads Arena data from the file.</summary>
    Private Sub LoadData()
        LoadData("BattleArena-" & MyKey & ".ini")
    End Sub
    ''' <summary>Loads Arena data from the file.</summary>
    ''' <param name="Filename">The file to load data from.</param>
    Private Sub LoadData(ByVal Filename As String)
        If My.Computer.FileSystem.FileExists(Filename) Then
            Dim Reader = My.Computer.FileSystem.OpenTextFileReader(Filename), s As String
            Dim Section As String, Field As String, Value As String
            Dim OwnCharacter As OwnCharacterData, Character As CharacterData, Weapon As WeaponData, Technique As TechniqueData

            Do Until Reader.EndOfStream
                s = Reader.ReadLine
                ' Check for a comment.
                If s.TrimStart.StartsWith(";") Then Continue Do

                Dim Match As System.Text.RegularExpressions.Match
                Match = System.Text.RegularExpressions.Regex.Match(s, "^\s*\[(?<Section>.*?)\]?\s*$")
                If Match.Success Then
                    Section = Match.Groups("Section").Value

                    If Section Is Nothing OrElse Not Section.Contains(":") Then Continue Do

                    Select Case Section.Split({":"c}, 2)(0).ToLower
                        Case "me"
                            OwnCharacter = New OwnCharacterData
                            OwnCharacters.Add(Section.Split({":"c}, 2)(1), OwnCharacter)
                        Case "character", "char", "player", "npc", "ally", "monster"
                            Character = New CharacterData With {.ShortName = Section.Split({":"c}, 2)(1)}
                            Characters.Add(Section.Split({":"c}, 2)(1), Character)
                        Case "weapon"
                            Weapon = New WeaponData With {.Name = Section.Split({":"c}, 2)(1)}
                            Weapons.Add(Section.Split({":"c}, 2)(1), Weapon)
                        Case "technique", "tech", "ability"
                            Technique = New TechniqueData With {.Name = Section.Split({":"c}, 2)(1)}
                            Techniques.Add(Section.Split({":"c}, 2)(1), Technique)
                    End Select

                    Continue Do
                End If

                Match = System.Text.RegularExpressions.Regex.Match(s, "^\s*(?<Property>(?>[^=]*))=(?<Value>.*)$")
                If Match.Success Then
                    Field = Match.Groups("Property").Value
                    Value = Match.Groups("Value").Value
                Else
                    Field = Nothing
                    Value = s
                End If

                If Section Is Nothing Then Continue Do
                'If Field Is Nothing Then Continue Do
                If Not Section.Contains(":") Then Continue Do
                Select Case Section.Split({":"c}, 2)(0).ToLower
                    Case "me"
                        Select Case If(Field Is Nothing, Nothing, Field.ToLower)
                            Case "name", "fullname", "longname"
                                OwnCharacter.FullName = Value
                            Case "password", "pass"
                                OwnCharacter.Password = Value
                        End Select
                    Case "character", "char", "player", "monster", "npc", "ally"
                        Select Case If(Field Is Nothing, Nothing, Field.ToLower)
                            Case "name", "fullname", "longname"
                                Character.Name = Value
                            Case "gender", "sex"
                                Character.Gender = Value
                            Case "category", "type"
                                Select Case Value.ToUpper
                                    Case "PLAYER", "1"
                                        Character.Category = CharacterCategory.Player
                                    Case "ALLY", "2"
                                        Character.Category = CharacterCategory.Ally
                                    Case "MONSTER", "4"
                                        Character.Category = CharacterCategory.Monster
                                    Case "3", "5", "6", "7"
                                        Character.Category = Value
                                    Case Else
                                        If Not Short.TryParse(Value, Character.Category) Then
                                            Console.WriteLine("Category of character " & Character.ShortName & " is not valid.")
                                        End If
                                End Select
                            Case "description", "desc"
                                Character.Description = Value
                            Case "str", "strength", "atk", "attack"
                                Character.bSTR = Value
                            Case "def", "defense", "res", "resilience", "armour", "armor", "guard"
                                Character.bDEF = Value
                            Case "int", "intelligence", "mmi", "mp", "matk", "mstr", "magicalmight", "magicalpower", "magic"
                                Character.bINT = Value
                            Case "equippedweapon", "eweapon", "weapon", "currentweapon"
                                Character.EquippedWeapon = Value
                            Case "equippedaccessory", "eaccessory", "accessory", "currentaccessory"
                                Character.EquippedAccessory = Value
                            Case "currentstyle", "style", "equippedstyle"
                                Character.CurrentStyle = Value
                            Case "elementalweaknesses", "elementweaknesses", "eweaknesses"
                                Character.ElementalWeaknesses = New List(Of String)(Value.Split({"."c}, StringSplitOptions.RemoveEmptyEntries))
                            Case "weaponweaknesses", "wweaknesses"
                                Character.WeaponWeaknesses = New List(Of String)(Value.Split({"."c}, StringSplitOptions.RemoveEmptyEntries))
                            Case "elementalresistances", "elementalresist", "elementalresists", "elementalstrengths", "elementresistances", "elementresist", "elementresists", "elementstrengths", "eresistances", "eresist", "eresists", "estrengths"
                                Character.ElementalResistances = New List(Of String)(Value.Split({"."c}, StringSplitOptions.RemoveEmptyEntries))
                            Case "weaponresistances", "weaponresist", "weaponresists", "weaponstrengths", "wresistances", "wresist", "wresists", "wstrengths"
                                Character.WeaponResistances = New List(Of String)(Value.Split({"."c}, StringSplitOptions.RemoveEmptyEntries))
                            Case "absorbs", "absorb", "heals", "heal"
                                Character.ElementalAbsorbs = New List(Of String)(Value.Split({"."c}, StringSplitOptions.RemoveEmptyEntries))
                            Case "immunities", "immune"
                                Character.ElementalImmunities = New List(Of String)(Value.Split({"."c}, StringSplitOptions.RemoveEmptyEntries))
                            Case "undead", "zombie", "isundead", "iszombie"
                                Character.IsUndead = {"yes", "true"}.Contains(Value.ToLower)
                            Case "elemental"
                                Character.IsElemental = {"yes", "true"}.Contains(Value.ToLower)
                            Case "ethereal", "isethereal", "ghost", "isghost"
                                Character.IsEthereal = {"yes", "true"}.Contains(Value.ToLower)
                            Case "weapons"
                                If Character.Weapons Is Nothing Then Character.Weapons = New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
                                For Each lWeapon In Value.Split({"."c}, StringSplitOptions.RemoveEmptyEntries)
                                    Character.Weapons.Add(lWeapon.Split({"|"c})(0), lWeapon.Split({"|"c})(1))
                                Next
                            Case "techniques", "abilities", "techs"
                                If Character.Techniques Is Nothing Then Character.Techniques = New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
                                For Each lTechnique In Value.Split({"."c}, StringSplitOptions.RemoveEmptyEntries)
                                    Character.Techniques.Add(lTechnique.Split({"|"c})(0), lTechnique.Split({"|"c})(1))
                                Next
                            Case "skills"
                                If Character.Skills Is Nothing Then Character.Skills = New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
                                For Each lSkill In Value.Split({"."c}, StringSplitOptions.RemoveEmptyEntries)
                                    Character.Skills.Add(lSkill.Split({"|"c})(0), lSkill.Split({"|"c})(1))
                                Next
                            Case "styles"
                                If Character.Styles Is Nothing Then Character.Styles = New Dictionary(Of String, Short)(StringComparer.OrdinalIgnoreCase)
                                For Each lStyle In Value.Split({"."c}, StringSplitOptions.RemoveEmptyEntries)
                                    Character.Styles.Add(lStyle.Split({"|"c})(0), lStyle.Split({"|"c})(1))
                                Next
                            Case "stylexp", "styleexp", "styleexperience", "styleprogress"
                                If Character.StyleExperience Is Nothing Then Character.StyleExperience = New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
                                For Each lStyle In Value.Split({"."c}, StringSplitOptions.RemoveEmptyEntries)
                                    Character.StyleExperience.Add(lStyle.Split({"|"c})(0), lStyle.Split({"|"c})(1))
                                Next
                            Case "ignitions", "ignition", "ignitionboosts", "igboosts"
                                Character.Ignitions = New List(Of String)(Value.Split({"."c}, StringSplitOptions.RemoveEmptyEntries))
                            Case "items"
                                If Character.Items Is Nothing Then Character.Items = New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
                                For Each lItem In Value.Split({"."c}, StringSplitOptions.RemoveEmptyEntries)
                                    Character.Items.Add(lItem.Split({"|"c})(0), lItem.Split({"|"c})(1))
                                Next
                            Case "redorbs", "rorbs", "orbs"
                                Character.RedOrbs = Value
                            Case "blackorbs", "borbs", "bossorbs"
                                Character.BlackOrbs = Value
                            Case "alliednotes", "notes"
                                Character.AlliedNotes = Value
                            Case "doubledollars", "dollars", "$$", "$"
                                Character.DoubleDollars = Value
                            Case "rating"
                                Character.Rating = Value
                            Case "npcbattles", "npcbattlesfought", "npcbattlecount", "aibattles", "aibattlesfought", "aibattlecount"
                                Character.NPCBattlesFought = Value
                            Case "iswellknown", "wellknown"
                                Character.IsWellKnown = {"yes", "true"}.Contains(Value.ToLower)
                            Case Nothing
                                Select Case Value.ToLower()
                                    Case "undead", "zombie", "isundead", "iszombie"
                                        Character.IsUndead = True
                                    Case "elemental"
                                        Character.IsElemental = True
                                    Case "ethereal", "isethereal", "ghost", "isghost"
                                        Character.IsEthereal = True
                                    Case "iswellknown", "wellknown"
                                        Character.IsWellKnown = True
                                End Select
                        End Select
                    Case "weapon"
                        Select Case If(Field Is Nothing, Nothing, Field.ToLower)
                            Case "category", "type"
                                Weapon.Category = Value
                            Case "cost", "price"
                                Weapon.Cost = Value
                            Case "upgradecost", "upgradeprice", "costtoupgrade"
                                Weapon.UpgradeCost = Value
                            Case "power", "strength"
                                Weapon.Power = Value
                            Case "element"
                                Weapon.Element = Value
                            Case "techniques", "techs", "abilities"
                                Weapon.Techniques = New List(Of String)(Value.Split({"."c}, StringSplitOptions.RemoveEmptyEntries))
                            Case "iswellknown", "wellknown"
                                Weapon.IsWellKnown = {"yes", "true"}.Contains(Value.ToLower)
                            Case Nothing
                                Select Case Value.ToLower()
                                    Case "iswellknown", "wellknown"
                                        Weapon.IsWellKnown = True
                                End Select
                        End Select
                    Case "technique", "tech", "ability"
                        Select Case If(Field Is Nothing, Nothing, Field.ToLower)
                            Case "description", "desc"
                                Technique.Description = Value
                            Case "type", "category"
                                Technique.Type = Value
                            Case "power"
                                Technique.Power = Value
                            Case "status", "statuses", "statuseffects", "effects", "sideeffects"
                                Technique.Status = Value
                            Case "tp", "tpcost", "mp", "mpcost", "mana", "manacost"
                                Technique.TP = Value
                            Case "cost", "price", "upgradecost", "upgradeprice"
                                Technique.Cost = Value
                            Case "aoe", "isaoe", "areaofeffect", "isareaofeffect"
                                Technique.IsAoE = {"yes", "true"}.Contains(Value.ToLower)
                            Case "magic", "ismagic", "spell", "isspell"
                                Technique.IsMagic = {"yes", "true"}.Contains(Value.ToLower)
                            Case "element"
                                Technique.Element = Value
                            Case "iswellknown", "wellknown"
                                Technique.IsWellKnown = {"yes", "true"}.Contains(Value.ToLower)
                            Case Nothing
                                Select Case Value.ToLower()
                                    Case "aoe", "isaoe", "areaofeffect", "isareaofeffect"
                                        Technique.IsAoE = True
                                    Case "magic", "ismagic", "spell", "isspell"
                                        Technique.IsMagic = True
                                    Case "iswellknown", "wellknown"
                                        Technique.IsWellKnown = True
                                End Select
                        End Select
                End Select
            Loop
            Reader.Close()
        End If

        For Each Character In Characters
            If Character.Value.EquippedWeapon = Nothing Or Character.Value.Techniques Is Nothing Then Continue For
            If Weapons.ContainsKey(Character.Value.EquippedWeapon) Then
                Character.Value.EquippedWeaponTechs = New List(Of String)
                For Each Technique In Weapons(Character.Value.EquippedWeapon).Techniques
                    If Character.Value.Techniques.ContainsKey(Technique) Then Character.Value.EquippedWeaponTechs.Add(Technique)
                Next
            End If
        Next
    End Sub

    ''' <summary>Saves Arena data to the file.</summary>
    Private Sub SaveData()
        SaveData("BattleArena-" & MyKey & ".ini")
    End Sub
    ''' <summary>Saves Arena data to the file.</summary>
    ''' <param name="Filename">The file to save data to.</param> 
    Private Sub SaveData(ByVal Filename As String)
        Dim writer = My.Computer.FileSystem.OpenTextFileWriter(Filename, False)
        For Each Character In OwnCharacters
            writer.WriteLine("[Me:" & Character.Key & "]")
            If Character.Value.FullName IsNot Nothing Then writer.WriteLine("Name=" & Character.Value.FullName)
            writer.WriteLine("Password=" & Character.Value.Password)
            writer.WriteLine()
        Next

        Dim FirstEntry As Boolean
        For Each Character In Characters
            writer.WriteLine("[Character:" & Character.Key & "]")
            writer.WriteLine("Name=" & Character.Value.Name)
            Select Case Character.Value.Category
                Case CharacterCategory.Player
                    writer.WriteLine("Category=Player")
                Case CharacterCategory.Ally
                    writer.WriteLine("Category=Ally")
                Case CharacterCategory.Monster
                    writer.WriteLine("Category=Monster")
                Case 7, -1
                Case Else
                    writer.WriteLine("Category=" & Character.Value.Category)
            End Select
            If Character.Value.Gender IsNot Nothing Then writer.WriteLine("Gender=" & Character.Value.Gender)
            If Character.Value.Description IsNot Nothing Then writer.WriteLine("Description=" & Character.Value.Description)
            If Character.Value.EquippedWeapon IsNot Nothing Then writer.WriteLine("EquippedWeapon=" & Character.Value.EquippedWeapon)
            If Character.Value.EquippedAccessory IsNot Nothing Then writer.WriteLine("EquippedAccessory=" & Character.Value.EquippedAccessory)
            If Character.Value.CurrentStyle IsNot Nothing Then writer.WriteLine("CurrentStyle=" & Character.Value.CurrentStyle)
            If Character.Value.ElementalWeaknesses IsNot Nothing Then writer.WriteLine("ElementalWeaknesses=" & String.Join(".", Character.Value.ElementalWeaknesses))
            If Character.Value.ElementalResistances IsNot Nothing Then writer.WriteLine("ElementalResistances=" & String.Join(".", Character.Value.ElementalResistances))
            If Character.Value.WeaponWeaknesses IsNot Nothing Then writer.WriteLine("WeaponWeaknesses=" & String.Join(".", Character.Value.ElementalWeaknesses))
            If Character.Value.WeaponResistances IsNot Nothing Then writer.WriteLine("WeaponResistances=" & String.Join(".", Character.Value.ElementalResistances))
            If Character.Value.ElementalAbsorbs IsNot Nothing Then writer.WriteLine("Absorbs=" & String.Join(".", Character.Value.ElementalAbsorbs))
            If Character.Value.ElementalImmunities IsNot Nothing Then writer.WriteLine("Immune=" & String.Join(".", Character.Value.ElementalImmunities))
            If Character.Value.IsUndead Then writer.WriteLine("Undead=Yes")
            If Character.Value.IsElemental Then writer.WriteLine("Elemental=Yes")
            If Character.Value.IsEthereal Then writer.WriteLine("Ethereal=Yes")
            If Character.Value.Weapons IsNot Nothing Then
                writer.Write("Weapons=")
                FirstEntry = True
                For Each Weapon In Character.Value.Weapons
                    writer.Write(If(FirstEntry, "", ".") & Weapon.Key & "|" & Weapon.Value)
                    FirstEntry = False
                Next
                writer.WriteLine()
            End If
            If Character.Value.Techniques IsNot Nothing Then
                writer.Write("Techniques=")
                FirstEntry = True
                For Each Technique In Character.Value.Techniques
                    writer.Write(If(FirstEntry, "", ".") & Technique.Key & "|" & Technique.Value)
                    FirstEntry = False
                Next
                writer.WriteLine()
            End If
            If Character.Value.Skills IsNot Nothing Then
                writer.Write("Skills=")
                FirstEntry = True
                For Each Skill In Character.Value.Skills
                    writer.Write(If(FirstEntry, "", ".") & Skill.Key & "|" & Skill.Value)
                    FirstEntry = False
                Next
                writer.WriteLine()
            End If
            If Character.Value.Styles IsNot Nothing Then
                writer.Write("Styles=")
                FirstEntry = True
                For Each Style In Character.Value.Styles
                    writer.Write(If(FirstEntry, "", ".") & Style.Key & "|" & Style.Value)
                    FirstEntry = False
                Next
                writer.WriteLine()
            End If
            If Character.Value.StyleExperience IsNot Nothing Then
                writer.Write("StyleEXP=")
                FirstEntry = True
                For Each Style In Character.Value.StyleExperience
                    writer.Write(If(FirstEntry, "", ".") & Style.Key & "|" & Style.Value)
                    FirstEntry = False
                Next
                writer.WriteLine()
            End If
            If Character.Value.Ignitions IsNot Nothing Then writer.WriteLine("Ignitions=" & String.Join(".", Character.Value.Ignitions))
            If Character.Value.Items IsNot Nothing Then
                writer.Write("Items=")
                FirstEntry = True
                For Each Item In Character.Value.Items
                    writer.Write(If(FirstEntry, "", ".") & Item.Key & "|" & Item.Value)
                    FirstEntry = False
                Next
                writer.WriteLine()
            End If
            If Character.Value.RedOrbs <> 0 Then writer.WriteLine("RedOrbs=" & Character.Value.RedOrbs)
            If Character.Value.BlackOrbs <> 0 Then writer.WriteLine("BlackOrbs=" & Character.Value.BlackOrbs)
            If Character.Value.AlliedNotes <> 0 Then writer.WriteLine("AlliedNotes=" & Character.Value.AlliedNotes)
            If Character.Value.DoubleDollars <> 0 Then writer.WriteLine("DoubleDollars=" & Character.Value.DoubleDollars)
            If Character.Value.Rating <> 0 Then writer.WriteLine("Rating=" & Character.Value.Rating)
            If Character.Value.NPCBattlesFought <> 0 Then writer.WriteLine("NPCBattles=" & Character.Value.NPCBattlesFought)
            If Character.Value.IsWellKnown Then writer.WriteLine("WellKnown=Yes")
            writer.WriteLine()
        Next

        For Each Weapon In Weapons
            writer.WriteLine("[Weapon:" & Weapon.Key & "]")
            If Weapon.Value.Category IsNot Nothing Then writer.WriteLine("Category=" & Weapon.Value.Category)
            If Weapon.Value.Cost <> 0 Then writer.WriteLine("Cost=" & Weapon.Value.Cost)
            If Weapon.Value.UpgradeCost <> 0 Then writer.WriteLine("UpgradeCost=" & Weapon.Value.UpgradeCost)
            If Weapon.Value.Power <> 0 Then writer.WriteLine("Power=" & Weapon.Value.Power)
            If Weapon.Value.Element IsNot Nothing Then writer.WriteLine("Element=" & Weapon.Value.Element)
            If Weapon.Value.Techniques IsNot Nothing Then writer.WriteLine("Techniques=" & String.Join(".", Weapon.Value.Techniques))
            If Weapon.Value.IsWellKnown Then writer.WriteLine("WellKnown=Yes")
            writer.WriteLine()
        Next

        For Each Technique In Techniques
            writer.WriteLine("[Technique:" & Technique.Key & "]")
            If Technique.Value.Description IsNot Nothing Then writer.WriteLine("Description=" & Technique.Value.Description)
            If Technique.Value.Type IsNot Nothing Then writer.WriteLine("Type=" & Technique.Value.Type)
            If Technique.Value.Power <> 0 Then writer.WriteLine("Power=" & Technique.Value.Power)
            If Technique.Value.Status IsNot Nothing Then writer.WriteLine("Status=" & Technique.Value.Status)
            If Technique.Value.TP <> 0 Then writer.WriteLine("TP=" & Technique.Value.TP)
            If Technique.Value.TP <> 0 Then writer.WriteLine("Cost=" & Technique.Value.TP)
            If Technique.Value.IsAoE Then writer.WriteLine("AoE=Yes")
            If Technique.Value.IsMagic Then writer.WriteLine("Magic=Yes")
            If Technique.Value.Element IsNot Nothing Then writer.WriteLine("Element=" & Technique.Value.Element)
            If Technique.Value.IsWellKnown Then writer.WriteLine("WellKnown=Yes")
            writer.WriteLine()
        Next
        writer.Close()
    End Sub

#End Region

#Region "Commands"

    <Command({"set", "config", "property"}, 1, 2,
    "set <property> <value>",
    "Changes settings for this plugin." & vbCrLf &
    "You can set the following properties: $k11analysis$o, $k11participation$o, $k11arenanickname$o, $k11timerfix$o." & vbCrLf &
    "Alternatively, you can omit the $k11value$o parameter to just check a property's value.",
     ".set", CommandAttribute.CommandScope.Channel)>
    Public Sub CommandSet(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim lProperty = args(0)
        Dim lValue = args.ElementAtOrDefault(1)

        Select Case lProperty.ToLower.Replace("_", "")
            Case "enableparticipation", "participation"
                If lValue = Nothing Then
                    Say(Connection, Channel, Choose("$bParticipation$b is currently " & If(EnableParticipation, "$k9enabled", "$k4disabled") & "$o.", "I " & If(EnableParticipation, "$k9will$o ", "$k4will not$o ") & "participate in battles."))
                Else
                    If {"1", "-1", "on", "yes", "true", "+", "enable", "enabled", "activate", "activated", "active"}.Contains(lValue.ToLower) Then
                        EnableParticipation = True
                        Say(Connection, Channel, Choose("I will now $k9participate$o in battles.", "$bParticipation$b is now $k9enabled$o."))
                        Dim GetAbilitiesThread = New Threading.Thread(AddressOf GetAbilities)
                        If LoggedIn <> Nothing Then GetAbilitiesThread.Start()
                    ElseIf {"0", "off", "no", "false", "-", "disable", "disabled", "deactivate", "deactivated", "inactive"}.Contains(lValue.ToLower) Then
                        EnableParticipation = False
                        Say(Connection, Channel, Choose("I will $k4no longer$o participate in battles.", "$bParticipation$b is now $k4disabled$o."))
                        'IsReady = False
                    Else
                        Reply(Connection, Channel, Sender, VBot.Choose("", "Sorry " & Sender.Split("!"c)(0) & ", ") & "I don't recognise " & VBot.Choose(IRCColours.Red & lValue & "$o", "that") & " as a Boolean value. Please enter $k11on$o or $k11off$o.")
                    End If
                End If
            Case "ai"
                Select Case lValue
                    Case Nothing
                        Say(Connection, Channel, "I am currently using the $k12" & {"old", "new"}(AI) & "$o version of the AI.")
                    Case "0", "old"
                        AI = 0
                        Say(Connection, Channel, "I will now use the $k09old$o version of the AI.")
                    Case "1", "new"
                        AI = 1
                        Say(Connection, Channel, "I will now use the $k09new$o version of the AI.")
                    Case Else
                        Reply(Connection, Channel, Sender, VBot.Choose("", "Sorry " & Sender.Split("!"c)(0) & ", ") & "that isn't a valid setting. Please enter $k11olds$o or $k11new$o.", SayOptions.Capitalise)
                End Select
            Case "enableanalysis", "analysis"
                If lValue = Nothing Then
                    Say(Connection, Channel, Choose("$bAnalysis$b is currently " & If(EnableAnalysis, "$k9enabled", "$k4disabled") & "$o.", "I " & If(EnableAnalysis, "$k9will$o ", "$k4will not$o ") & "analyse the Arena combatants."))
                Else
                    If {"1", "-1", "on", "yes", "true", "+", "enable", "enabled", "activate", "activated", "active"}.Contains(lValue.ToLower) Then
                        EnableAnalysis = True
                        Say(Connection, Channel, Choose("I will now $k9analyse$o the Arena combatants.", "$bAnalysis$b is now $k9enabled$o."))
                    ElseIf {"0", "off", "no", "false", "-", "disable", "disabled", "deactivate", "deactivated", "inactive"}.Contains(lValue.ToLower) Then
                        EnableAnalysis = False
                        Say(Connection, Channel, Choose("I will $k4no longer$o analyse the Arena conbatants.", "$bAnalysis$b is now $k4disabled$o."))
                    Else
                        Reply(Connection, Channel, Sender, VBot.Choose("", "Sorry " & Sender.Split("!"c)(0) & ", ") & "I don't recognise " & VBot.Choose(IRCColours.Red & lValue & "$o", "that") & " as a Boolean value. Please enter $k11on$o or $k11off$o.")
                    End If
                End If
            Case "enablegambling", "gambling", "enablebetting", "betting", "enablebets", "bets"
                If lValue = Nothing Then
                    Say(Connection, Channel, Choose("$bGambling$b is currently " & If(EnableGambling, "$k9enabled", "$k4disabled") & "$o.", "I " & If(EnableAnalysis, "$k9will$o ", "$k4will not$o ") & "bet on NPC battles."))
                Else
                    If {"1", "-1", "on", "yes", "true", "+", "enable", "enabled", "activate", "activated", "active"}.Contains(lValue.ToLower) Then
                        EnableGambling = True
                        Say(Connection, Channel, Choose("I will now $k9bet$o on NPC battles.", "$bGambling$b is now $k9enabled$o."))
                    ElseIf {"0", "off", "no", "false", "-", "disable", "disabled", "deactivate", "deactivated", "inactive"}.Contains(lValue.ToLower) Then
                        EnableGambling = False
                        Say(Connection, Channel, Choose("I will $k4no longer$o bet on NPC battles.", "$bGambling$b is now $k4disabled$o."))
                    Else
                        Reply(Connection, Channel, Sender, VBot.Choose("", "Sorry " & Sender.Split("!"c)(0) & ", ") & "I don't recognise " & VBot.Choose(IRCColours.Red & lValue & "$o", "that") & " as a Boolean value. Please enter $k11on$o or $k11off$o.")
                    End If
                End If
            Case "arenanickname", "arenanick", "botnickname", "botnick"
                If lValue = Nothing Then
                    Say(Connection, Channel, Choose("The Arena bot's nickname is assumed to be$k12 " & ArenaNickname & "$o."))
                Else
                    ArenaNickname = lValue
                    Say(Connection, Channel, Choose("The Arena bot's nickname is now assumed to be$k12 " & ArenaNickname & "$o."))
                End If
            Case "nomonsterfix", "nomonfix", "emptybattlefix"
                If lValue = Nothing Then
                    Say(Connection, Channel, Choose("The $bno monster fix$b is currently " & If(NoMonsterFix, "$k9enabled", "$k4disabled") & "$o.", "I " & Choose(NoMonsterFix, "$k9will$o ", "$k4will not$o ") & "stop empty battles."))
                Else
                    If {"1", "-1", "on", "yes", "true", "+", "enable", "enabled", "activate", "activated", "active"}.Contains(lValue.ToLower) Then
                        NoMonsterFix = True
                        Say(Connection, Channel, Choose("I will now $k9stop$o empty battles.", "The $bno monster fix$b is now $k9enabled$o."))
                    ElseIf {"0", "off", "no", "false", "-", "disable", "disabled", "deactivate", "deactivated", "inactive"}.Contains(lValue.ToLower) Then
                        NoMonsterFix = False
                        Say(Connection, Channel, Choose("I will $k4no longer$o stop empty battles.", "The $bno monster fix$b is now $k4disabled$o."))
                    Else
                        Reply(Connection, Channel, Sender, VBot.Choose("", "Sorry " & Sender.Split("!"c)(0) & ", ") & "I don't recognise " & VBot.Choose(IRCColours.Red & lValue & "$o", "that") & " as a Boolean value. Please enter $k11on$o or $k11off$o.")
                    End If
                End If
            Case "isowner", "owner"
                If lValue = Nothing Then
                    Say(Connection, Channel, "I " & If(IsOwner, "$k9have$o ", "$k4do not$o have ") & "admin status here.")
                ElseIf {"check", "test", "checknow", "testnow"}.Contains(lValue.ToLower.Replace(" ", "").Replace("_", "")) Then
                    Reply(Connection, Channel, Sender.Split("!"c)(0), "Checking for admin status...")
                    IsOwnerCheckTimer_Elapsed(Me, Nothing)
                ElseIf {"1", "-1", "on", "yes", "true", "+", "enable", "enabled", "activate", "activated", "active"}.Contains(lValue.ToLower) Then
                    IsOwner = True
                    IsOwnerCheckTimer.Enabled = True
                    Say(Connection, Channel, Choose("I will now assume that I $k9have$o admin status here."))
                ElseIf {"0", "off", "no", "false", "-", "disable", "disabled", "deactivate", "deactivated", "inactive"}.Contains(lValue.ToLower) Then
                    IsOwner = True
                    IsOwnerCheckTimer.Enabled = True
                    Say(Connection, Channel, Choose("I will now assume that I $k4do not$o have admin status here."))
                Else
                    Reply(Connection, Channel, Sender, VBot.Choose("", "Sorry " & Sender.Split("!"c)(0) & ", ") & "I don't recognise " & VBot.Choose(IRCColours.Red & lValue & "$o", "that") & " as a Boolean value. Please enter $k11yes$o, $k11no$o or $k11check now$o.")
                End If
            Case "directory", "folder", "arenadirectory", "arenafolder", "dir", "arenadir", "botdirectory", "botfolder", "botdir"
                If lValue = Nothing Then
                    If ArenaDirectory Is Nothing Then
                        Say(Connection, Channel, "I don't have access to the Arena bot's main folder.")
                    Else
                        Say(Connection, Channel, "The Arena bot's main folder is found at $k12" & ArenaDirectory & "$o.")
                    End If
                ElseIf {"nothing", "none", "off", "cancel"}.Contains(lValue.ToLower) Then
                    ArenaDirectory = Nothing
                    Say(Connection, Channel, "The Arena bot's main folder was disassociated.")
                ElseIf Not IO.Directory.Exists(lValue) Then
                    Reply(Connection, Channel, Sender.Split("!")(0), "That folder " & Choose("does not ", "doesn't ") & "exist.", SayOptions.Capitalise)
                Else
                    ArenaDirectory = lValue
                    Say(Connection, Channel, "The Arena bot's main folder is now found at $k9" & ArenaDirectory & "$o.")
                End If
            Case "minplayers", "minimumplayers", "minplayerstoenter", "minimumplayerstoenter"
                Dim liValue As Integer
                If lValue = Nothing Then
                    Say(Connection, Channel, "I will enter with at least $k12" & MinimumPlayersToEnter & "$o other " & If(MinimumPlayersToEnter = 1, "player.", "players."))
                ElseIf Integer.TryParse(lValue, liValue) Then
                    If liValue < 0 Then
                        Say(Connection, Channel, "A negative number is not valid.")
                    Else
                        MinimumPlayersToEnter = liValue
                        Say(Connection, Channel, "I will now enter with at least $k09" & MinimumPlayersToEnter & "$o other " & If(MinimumPlayersToEnter = 1, "player.", "players."))
                    End If
                Else
                    Say(Connection, Channel, "That isn't a valid integer.")
                End If
            Case Else
                Say(Connection, Channel, "I don't manage a property named $k04" & lProperty & "$o here.")
        End Select
    End Sub

    <Command({"time", "timeleft", "timeremain", "timeremaining", "remaintime", "remainingtime", "timetodarkness", "timetorage", "timetilldarkness", "timetillrage", "ragetime", "ragetimer", "timer"}, 0, 0,
"time",
"Tells you how long you have left to defeat the enemy force.",
 Nothing, CommandAttribute.CommandScope.Channel)>
    Public Sub CommandTime(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim TimeRemaining As TimeSpan, HolyAuraTimeRemaining As TimeSpan, HolyAuraMessage As String
        Dim TimeMessage As String, TimeColour = IRCColours.Blue, DemonWallMessage As String

        If Not IsBattleStarted Then
            Say(Connection, Channel, "There's no battle going on at the moment" & Choose(".", ", " & Sender.Split("!"c)(0) & "."))
            Return
        ElseIf DarknessHasRisen Then
            Say(Connection, Channel, Choose("...", "Darkness has already arisen in this battle" & Choose(".", ", " & Sender.Split("!"c)(0) & ".")))
            Return
        ElseIf TurnsToDarkness <> -1 Then
            If TurnsToDarkness = 0 Then
                DemonWallMessage = "200%"
            Else
                TimeMessage = TurnsToDarkness.ToString() & If(TurnsToDarkness = 1, " turn", " turns")

                If TurnsToDarkness < 3 Then
                    TimeColour = IRCColours.Red
                ElseIf TurnsToDarkness < 5 Then
                    TimeColour = IRCColours.Orange
                ElseIf TurnsToDarkness < 10 Then
                    TimeColour = IRCColours.Yellow
                Else
                    TimeColour = IRCColours.Green
                End If

                If HolyAuraTurns <> -1 Then
                    ' Show the time remaining for Holy Aura.
                    If HolyAuraTurns = 0 Then
                        HolyAuraMessage = "is about to " & Choose("expire.", "fade.", "run out.", "wear off.")
                    Else
                        HolyAuraMessage = String.Format(Choose("will hold for $k12{0}$o.", "will last for $k12{0}$o.", "has $k12{0}$o remaining."), HolyAuraTurns.ToString() & If(HolyAuraTurns = 1, " turn", " turns"))
                    End If
                End If

                If BattleList.ContainsKey("Demon_Wall") Then
                    Dim MaxTime = TurnNumber + TurnsToDarkness - 1
                    Dim BoostFactor As Single = (CSng(CurrentTurn) / CSng(MaxTime)) + 1.0F
                    DemonWallMessage = BoostFactor.ToString("P0")
                End If
            End If
        Else
            If FiveMinuteWarning <> Nothing Then
                TimeRemaining = (FiveMinuteWarning + TimeSpan.FromSeconds(300)) - Now
            ElseIf IsBossBattle Then
                TimeRemaining = (BattleStartTime + TimeSpan.FromSeconds(1500)) - Now
                'ElseIf number_of_monsters_needed <= 3 Then  ' There was a bug in Battle Arena that stops this case from occurring.
                '    TimeRemaining = (BattleStartTime + TimeSpan.FromSeconds(1200)) - Now
            Else
                TimeRemaining = (BattleStartTime + TimeSpan.FromSeconds(2100)) - Now
            End If

            If FiveMinuteWarning <> Nothing And TimeRemaining <= TimeSpan.Zero Then TimeRemaining = TimeSpan.FromMinutes(5)
            If TimeRemaining < TimeSpan.FromSeconds(5) Then
                DemonWallMessage = "500%"
                Return
            Else
                If TimeRemaining.Minutes = 1 Then
                    TimeMessage = "1 minute"
                ElseIf TimeRemaining.Minutes > 1 Then
                    TimeMessage = TimeRemaining.Minutes & " minutes"
                End If
                If TimeRemaining.Seconds = 1 Then
                    If TimeMessage IsNot Nothing Then TimeMessage &= Choose(", ", " and ")
                    TimeMessage &= "1 second"
                ElseIf TimeRemaining.Seconds > 1 Then
                    If TimeMessage IsNot Nothing Then TimeMessage &= Choose(", ", " and ")
                    TimeMessage &= TimeRemaining.Seconds & " seconds"
                End If

                If TimeRemaining < TimeSpan.FromMinutes(2) Then
                    TimeColour = IRCColours.Red
                ElseIf TimeRemaining < TimeSpan.FromMinutes(5) Then
                    TimeColour = IRCColours.Orange
                ElseIf TimeRemaining < TimeSpan.FromMinutes(10) Then
                    TimeColour = IRCColours.Yellow
                Else
                    TimeColour = IRCColours.Green
                End If


                If HolyAuraEnd <> Nothing Then
                    ' Show the time remaining for Holy Aura.
                    HolyAuraTimeRemaining = (FiveMinuteWarning + TimeSpan.FromSeconds(300)) - Now
                    If HolyAuraTimeRemaining <= TimeSpan.FromSeconds(5) Then
                        HolyAuraMessage = "is about to " & Choose("expire.", "fade.", "run out.", "wear off.")
                    Else
                        If HolyAuraTimeRemaining.Minutes = 1 Then
                            HolyAuraMessage = "1 minute"
                        ElseIf HolyAuraTimeRemaining.Minutes > 1 Then
                            HolyAuraMessage = HolyAuraTimeRemaining.Minutes & " minutes"
                        End If
                        If HolyAuraTimeRemaining.Seconds = 1 Then
                            If HolyAuraMessage IsNot Nothing Then HolyAuraMessage &= Choose(", ", " and ")
                            HolyAuraMessage &= "1 second"
                        ElseIf HolyAuraTimeRemaining.Seconds > 1 Then
                            If HolyAuraMessage IsNot Nothing Then HolyAuraMessage &= Choose(", ", " and ")
                            HolyAuraMessage &= HolyAuraTimeRemaining.Seconds & " seconds"
                        End If
                        HolyAuraMessage = String.Format(Choose("will hold for $k12{0}$o.", "will last for $k12{0}$o.", "has $k12{0}$o remaining."), HolyAuraMessage)
                    End If
                End If

                If BattleList.ContainsKey("Demon_Wall") Then
                    Dim BoostPercentage As String = "100%"
                    ' What is the Demon Wall's attack power?
                    Select Case TimeRemaining.TotalSeconds
                        Case Is >= 270 : BoostPercentage = "100%"
                        Case 240 To 270 : BoostPercentage = "150%"
                        Case 210 To 240 : BoostPercentage = "200%"
                        Case 180 To 210 : BoostPercentage = "250%"
                        Case 120 To 180 : BoostPercentage = "300%"
                        Case 60 To 120 : BoostPercentage = "350%"
                        Case 30 To 60 : BoostPercentage = "400%"
                        Case Is < 30 : BoostPercentage = "500%"
                    End Select
                    DemonWallMessage = BoostPercentage
                End If
            End If
        End If

        If TimeMessage Is Nothing Then
            Say(Connection, Channel, "Darkness should arise any second now.")
        Else
            Say(Connection, Channel, String.Format(Choose("You have {1}{0}{2} " & Choose("until ", "before ") & "darkness arises.", "Darkness " & Choose("will arise ", "arises ", "will overcome the battlefield ", "overcomes the battlefield ") & "in " & If(TurnsToDarkness = -1, Choose("around ", "about ", "approximately ", ""), "") & "{1}{0}{2}."), TimeMessage, TimeColour, ChrW(15)))
        End If

        If HolyAuraMessage IsNot Nothing Then
            Say(Connection, Channel, String.Format("$b{1}$b's holy aura {0}", HolyAuraMessage, HolyAuraUser))
        End If

        If DemonWallMessage IsNot Nothing Then
            Say(Connection, Channel, String.Format("The $bDemon Wall$b's attack power is at $k04{0}$o.", DemonWallMessage))
        End If
    End Sub

    <Command({"control", "posess"}, 1, 1,
"control <target's nickname>",
"Instructs me to control another character. I'll need bot owner status to do this.",
 ".control", CommandAttribute.CommandScope.Channel)>
    Public Sub CommandControl(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim Character As CharacterData
        ' We can't control someone we don't know about.
        If Not Characters.TryGetValue(args(0), Character) Then
            Say(Connection, Channel, "I'm not familiar enough with this character to control them. Have them enter a battle first.")
            ' Check if we're already controlling this person.
        ElseIf Controlling.Contains(args(0), StringComparer.OrdinalIgnoreCase) Then
            Say(Connection, Channel, "I'm already controlling " & Characters(args(0)).Name & ".")
            ' Make sure it is a player.
        ElseIf Character.Category <> CharacterCategory.Player And Not UserHasPermission(Connection, Channel, Sender, MyKey & ".control.nonplayer") Then
            Say(Connection, Channel, "You don't have permission to use that command on a non-player!")
        ElseIf Character.Category <> CharacterCategory.Player And Not IsOwner Then
            Say(Connection, Channel, Choose("I can't control non-players here, because I don't have bot owner status.", "I need to be a bot owner here to control a non-player."))
        ElseIf Character.IsReadyToControl Then
            Controlling.Add(args(0))
            Say(Connection, Channel, "OK, I will control " & Characters(args(0)).Name & ".")
        Else
            Say(Connection, Channel, ChrW(1) & "ACTION looks carefully at " & Characters(args(0)).Name & "..." & ChrW(1))
            Dim GetAbilitiesThread = New Threading.Thread(AddressOf GetAbilitiesOther)
            GetAbilitiesThread.Start(args(0))
        End If
    End Sub

    <Command({"controlme", "posessme"}, 0, 0,
"controlme",
"Instructs me to control your character. I'll need bot owner status to do this.",
 Nothing, CommandAttribute.CommandScope.Channel)>
    Public Sub CommandControlMe(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim Character As CharacterData
        If Not Characters.TryGetValue(Sender.Split("!"c)(0), Character) Then
            Say(Connection, Channel, "I'm not familiar enough with this character to control them. Have them enter a battle first.")
        ElseIf Controlling.Contains(Sender.Split("!"c)(0), StringComparer.OrdinalIgnoreCase) Then
            Say(Connection, Channel, "I'm already controlling you.")
        ElseIf Character.IsReadyToControl Then
            Controlling.Add(Sender.Split("!"c)(0))
            Say(Connection, Channel, "OK, I will control " & Characters(Sender.Split("!"c)(0)).Name & ".")
        Else
            Say(Connection, Channel, ChrW(1) & "ACTION looks carefully at " & Characters(Sender.Split("!"c)(0)).Name & "..." & ChrW(1))
            Dim GetAbilitiesThread = New Threading.Thread(AddressOf GetAbilitiesOther)
            GetAbilitiesThread.Start(Sender.Split("!"c)(0))
        End If
    End Sub

    <Command({"stopcontrol", "stopposess"}, 0, 1,
"stopcontrol [nickname]",
"Instructs me to stop controlling someone, default yourself.",
 Nothing, CommandAttribute.CommandScope.Channel)>
    Public Sub CommandControlStop(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim Character As CharacterData, Nickname As String = If(args.ElementAtOrDefault(0), Sender.Split("!"c)(0))
        If Nickname <> Sender.Split("!"c)(0) And Not UserHasPermission(Connection, Channel, Sender, MyKey & ".control") Then
            Say(Connection, Channel, "You may not use that command on others.")
            Return
        End If
        If Not Controlling.Contains(Nickname, StringComparer.OrdinalIgnoreCase) Then
            Say(Connection, Channel, "I'm not controlling " & Nickname & ".")
        Else
            Controlling.Remove(Nickname)
            Say(Connection, Channel, "OK, I will stop controlling " & Characters(Nickname).Name & ".")
        End If
    End Sub

    <Command({"match"}, 2, 2,
"match <short name> <full name>",
"Tests the function for name matching.",
 ".debug")>
    Public Sub CommandMatch(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim MatchScore As Integer
        MatchScore = NameMatch(args(0), args(1))

        Say(Connection, Channel, String.Format("The match score is $k12{0:0.00}$k2%$o. $k2($k12{1}$k2 / $k12{2}$k2)", MatchScore / (args(1).Length * 2) * 100, MatchScore, args(1).Length * 2))
    End Sub

    <Command({"arena-id", "arena-identify", "arena-login", "ba-id", "ba-identify", "ba-login"}, 0, 1,
"identify [existing]",
"Instructs me to identify myself to the Arena. Specify 'existing' if I should not create a new character.",
".identify", CommandAttribute.CommandScope.Channel)>
    Public Sub CommandIdentify(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        If OwnCharacters.ContainsKey(ArenaConnection.Nickname) Then
            Say(ArenaConnection, ArenaNickname, "!id " & OwnCharacters(ArenaConnection.Nickname).Password, SayOptions.NoticeNever)
        ElseIf args.Count = 0 Then
            Return
        ElseIf EnableParticipation Then
            Say(Connection, ArenaNickname, "!new char", SayOptions.NoticeNever)
        End If
    End Sub

    <Command({"rename"}, 2, 2,
"rename <player> <new name>",
"Renames a character file. If they are in battle, I'll keep their place.",
".admin")>
    Public Sub CommandRename(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        If ArenaDirectory = Nothing Then
            Say(Connection, Channel, "I don't have access to the Arena files.")
            Return
        Else
            ' Check for conflicts.
            If args(1).ToUpper.EndsWith("_CLONE") Or args(1).ToUpper.EndsWith("_SUMMON") Or args(1).ToUpper.StartsWith("EVIL_") Or args(1).Contains(" "c) Or args(1).Contains("."c) Then
                Reply(Connection, Channel, Sender, "$k4" & args(1) & "$o is not a legal player name.")
                Return
            Else
                For Each c In IO.Path.GetInvalidFileNameChars
                    If args(1).Contains(c) Then
                        Reply(Connection, Channel, Sender, "$k4" & args(1) & "$o is not a valid Windows file name.")
                        Return
                    End If
                Next
            End If
            If IO.File.Exists(IO.Path.Combine(ArenaDirectory, "characters", args(1) & ".char")) Then
                Reply(Connection, Channel, Sender, "A player named $k4" & args(1) & "$o is already present.")
                Return
            ElseIf IO.File.Exists(IO.Path.Combine(ArenaDirectory, "monsters", args(1) & ".char")) Then
                Reply(Connection, Channel, Sender, "A monster named $k4" & args(1) & "$o is already present.")
                Return
            ElseIf IO.File.Exists(IO.Path.Combine(ArenaDirectory, "bosses", args(1) & ".char")) Then
                Reply(Connection, Channel, Sender, "A boss named $k4" & args(1) & "$o is already present.")
                Return
            ElseIf IO.File.Exists(IO.Path.Combine(ArenaDirectory, "npcs", args(1) & ".char")) Then
                Reply(Connection, Channel, Sender, "An ally named $k4" & args(1) & "$o is already present.")
                Return
            End If

            ' Create the new character file.
            Dim sw = New IO.StreamWriter(IO.File.Create(IO.Path.Combine(ArenaDirectory, "characters", args(1) & ".char")), System.Text.Encoding.ASCII)
            Dim sr = New IO.StreamReader(IO.File.Open(IO.Path.Combine(ArenaDirectory, "characters", args(0) & ".char"), IO.FileMode.Open, IO.FileAccess.Read), System.Text.Encoding.UTF8)
            Dim BaseStatsSection As Boolean
            Do Until sr.EndOfStream
                Dim s = sr.ReadLine
                If s.ToUpper = "[BASESTATS]" Then
                    BaseStatsSection = True
                    sw.WriteLine(s)
                ElseIf BaseStatsSection AndAlso s.ToUpper.StartsWith("NAME=") Then
                    sw.WriteLine("Name=" & args(1))
                Else
                    sw.WriteLine(s)
                End If
            Loop
            sr.Close() : sw.Close()
            sr.Dispose()

            ' Delete the old character.
            IO.File.Delete(IO.Path.Combine(ArenaDirectory, "characters", args(0) & ".char"))
            ' If they're on the channel, devoice them.
            Dim lChannel As IRCConnection.IRCChannel, lUser As IRCConnection.IRCUser
            If ArenaConnection.Channels.TryGetValue(ArenaChannel, lChannel) Then
                If lChannel.Users.TryGetValue(args(0), lUser) Then
                    If lChannel.CanDeVoice Then lChannel.DeVoice(lUser.Nickname)
                End If
            End If

            ' Update battle.txt.
            Dim BattleTxt As New StringBuilder, BattleTxtPath As String
            If IO.File.Exists(IO.Path.Combine(ArenaDirectory, "txts", "battle.txt")) Then
                BattleTxtPath = IO.Path.Combine(ArenaDirectory, "txts", "battle.txt")
            ElseIf IO.File.Exists(IO.Path.Combine(ArenaDirectory, "battle.txt")) Then
                BattleTxtPath = IO.Path.Combine(ArenaDirectory, "battle.txt")
            End If
            If BattleTxtPath <> Nothing Then
                sr = New IO.StreamReader(IO.File.Open(BattleTxtPath, IO.FileMode.Open, IO.FileAccess.Read))
                Do Until sr.EndOfStream
                    Dim s = sr.ReadLine
                    If s.ToUpper = args(0).ToUpper Then BattleTxt.AppendLine(args(1)) Else BattleTxt.AppendLine(s)
                Loop
                sr.Close()
                IO.File.WriteAllText(BattleTxtPath, BattleTxt.ToString, Encoding.ASCII)

                ' Update battle2.txt.
                BattleTxt = New StringBuilder : BattleTxtPath = IO.Path.Combine(IO.Path.GetDirectoryName(BattleTxtPath), "battle2.txt")
                If Not IO.File.Exists(BattleTxtPath) Then BattleTxtPath = Nothing
                If BattleTxtPath <> Nothing Then
                    sr = New IO.StreamReader(IO.File.Open(BattleTxtPath, IO.FileMode.Open, IO.FileAccess.Read))
                    Dim Section As String
                    Do Until sr.EndOfStream
                        Dim s = sr.ReadLine
                        If s.ToUpper = "[BATTLE]" Then
                            Section = "Battle"
                        ElseIf s.ToUpper = "[STYLE]" Then
                            Section = "Style"
                        ElseIf s.StartsWith("[") Then
                            Section = Nothing
                        ElseIf Section = "Battle" AndAlso s.ToUpper.StartsWith("LIST=") Then
                            Dim List = s.Substring(5).Split("."c)
                            For i = 0 To UBound(List) : If List(i).ToUpper = args(0).ToUpper Then List(i) = args(1)
                            Next
                            s = "List=" & String.Join(".", List)
                        ElseIf s.ToUpper.StartsWith(args(0).ToUpper & "=") Then
                            s = args(1) & s.Substring(args(0).Length)
                        ElseIf s.ToUpper.StartsWith(args(0).ToUpper & ".LASTACTION=") Then
                            s = args(1) & s.Substring(args(0).Length)
                        End If
                        BattleTxt.AppendLine(s)
                    Loop
                    sr.Close()
                    IO.File.WriteAllText(BattleTxtPath, BattleTxt.ToString, Encoding.ASCII)
                End If

                If IsBattleStarted And IsOwner And CurrentTurn.ToUpper = args(0).ToUpper Then Say(ArenaConnection, ArenaNickname, "!next", SayOptions.NoticeNever)
            End If

            Say(Connection, Channel, "$k12" & args(0) & "$o is now known as $k12" & args(1) & "$o.")
        End If
    End Sub

    <Command({"restore"}, 1, 2,
"restore <player> [new name]",
"Restores a zapped character.",
".admin")>
    Public Sub CommandRestore(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim dName = If(args.ElementAtOrDefault(1), args(0)), sFile As String

        If ArenaDirectory = Nothing Then
            Say(Connection, Channel, "I don't have access to the Arena files.")
            Return
        Else
            ' Check for conflicts.
            If dName.ToUpper.EndsWith("_CLONE") Or dName.ToUpper.EndsWith("_SUMMON") Or dName.ToUpper.StartsWith("EVIL_") Or dName.Contains(" "c) Or dName.Contains("."c) Then
                Reply(Connection, Channel, Sender, "$k4" & dName & "$o is not a legal player name.")
                Return
            Else
                For Each c In IO.Path.GetInvalidFileNameChars
                    If dName.Contains(c) Then
                        Reply(Connection, Channel, Sender, "$k4" & dName & "$o is not a valid Windows file name.")
                        Return
                    End If
                Next
            End If
            If IO.File.Exists(IO.Path.Combine(ArenaDirectory, "characters", dName & ".char")) Then
                Reply(Connection, Channel, Sender, "A player named $k4" & dName & "$o is already present.")
                Return
            ElseIf IO.File.Exists(IO.Path.Combine(ArenaDirectory, "monsters", dName & ".char")) Then
                Reply(Connection, Channel, Sender, "A monster named $k4" & dName & "$o is already present.")
                Return
            ElseIf IO.File.Exists(IO.Path.Combine(ArenaDirectory, "bosses", dName & ".char")) Then
                Reply(Connection, Channel, Sender, "A boss named $k4" & dName & "$o is already present.")
                Return
            ElseIf IO.File.Exists(IO.Path.Combine(ArenaDirectory, "npcs", dName & ".char")) Then
                Reply(Connection, Channel, Sender, "An ally named $k4" & dName & "$o is already present.")
                Return
            End If

            ' Find the zapped character.
            Dim LatestFileDate As Date
            For Each File In IO.Directory.GetFiles(IO.Path.Combine(ArenaDirectory, "characters", "zapped"), args(0) & "_??????.char", IO.SearchOption.TopDirectoryOnly)
                If IO.File.GetLastWriteTime(File) > LatestFileDate Then sFile = File
            Next
            If sFile = Nothing Then
                Reply(Connection, Channel, Sender, "No record of $k4" & args(0) & "$o was found.")
                Return
            End If

            ' Restore the character.
            Dim sw = New IO.StreamWriter(IO.File.Create(IO.Path.Combine(ArenaDirectory, "characters", dName & ".char")), System.Text.Encoding.ASCII)
            Dim sr = New IO.StreamReader(IO.File.Open(sFile, IO.FileMode.Open, IO.FileAccess.Read), System.Text.Encoding.UTF8)
            Dim Section As String
            Do Until sr.EndOfStream
                Dim s = sr.ReadLine
                If s.ToUpper = "[INFO]" Then
                    Section = "Info"
                ElseIf s.ToUpper = "[BASESTATS]" Then
                    Section = "BaseStats"
                ElseIf s.StartsWith("[") Then
                    Section = Nothing
                ElseIf Section = "Info" AndAlso s.ToUpper.StartsWith("LASTSEEN=") Then
                    Dim LastSeenDate As Date
                    s = s.Substring(s.IndexOf("="c) + 1).ToUpper
                    Dim ldDay As Short, ldMonth As Short, ldYear As Short, ldHour As Short, ldMinute As Short, ldSecond As Short

                    ' Month
                    For ldMonth = 1 To 12
                        If s.Substring(4, 3) = {"JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC"}(ldMonth - 1) Then
                            GoTo DateFound
                        End If
                    Next
                    GoTo NoDateFound
DateFound:
                    ' Day
                    If Not Short.TryParse(s.Substring(8, 2), ldDay) Then GoTo NoDateFound
                    If Not Short.TryParse(s.Substring(11, 2), ldHour) Then GoTo NoDateFound
                    If Not Short.TryParse(s.Substring(14, 2), ldMinute) Then GoTo NoDateFound
                    If Not Short.TryParse(s.Substring(17, 2), ldSecond) Then GoTo NoDateFound
                    If Not Short.TryParse(s.Substring(20, 4), ldYear) Then GoTo NoDateFound
                    Try
                        LastSeenDate = New Date(ldYear, ldMonth, ldDay, ldHour, ldMinute, ldSecond)
                    Catch ex As Exception
                        GoTo NoDateFound
                    End Try

                    If (Now - LastSeenDate) > TimeSpan.FromDays(180) Then
                        ' The character is older than 180 days. Let's reset the last seen date.
                        s = "LastSeen=" & Now.ToString("ddd MMM dd HH:mm:ss yyyy")
                    End If
                ElseIf Section = "BaseStats" AndAlso s.ToUpper.StartsWith("NAME=") Then
                    s = "Name=" & dName
                End If
NoDateFound:
                sw.WriteLine(s)
            Loop
            sr.Close() : sw.Close()
            sr.Dispose()

            ' Delete the old character.
            IO.File.Delete(sFile)

            Say(Connection, Channel, "$k12" & args(0) & "$o has been restored" & If(dName = args(0), ".", " as $k12" & dName & "."))
        End If
    End Sub

    <Command({"lateentry"}, 1, 1,
"lateentry <player>",
"Enters a player into the battle after it begins.",
".lateentry")>
    Public Sub CommandLateEntry(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        If ArenaDirectory = Nothing Then
            Say(Connection, Channel, "I don't have access to the Arena files.")
            Return
        Else
            Dim BattleFile As String, Battle2File As String
            If IO.Directory.Exists(IO.Path.Combine(ArenaDirectory, "txts")) Then
                BattleFile = IO.Path.Combine(ArenaDirectory, "txts", "battle.txt")
                Battle2File = IO.Path.Combine(ArenaDirectory, "txts", "battle2.txt")
            Else
                BattleFile = IO.Path.Combine(ArenaDirectory, "battle.txt")
                Battle2File = IO.Path.Combine(ArenaDirectory, "battle2.txt")
            End If
            ' Make sure the files exist.
            If Not IO.File.Exists(BattleFile) Then
                Say(Connection, Channel, "$bbattle.txt$b is missing.")
                Return
            End If
            If Not IO.File.Exists(Battle2File) Then
                Say(Connection, Channel, "$bbattle2.txt$b is missing.")
                Return
            End If
            If Not IO.File.Exists(IO.Path.Combine(ArenaDirectory, "characters", args(0) & ".char")) Then
                Say(Connection, Channel, "No character named$b " & args(0) & " $bis known.")
                Return
            End If

            ' Open battle2.txt
            Dim Battle2Builder As New StringBuilder
            Dim Battle2 = New IO.StreamReader(IO.File.Open(Battle2File, IO.FileMode.Open, IO.FileAccess.Read))

            Dim Section As String
            Do Until Battle2.EndOfStream
                Dim s = Battle2.ReadLine

                If s.ToUpper = "[BATTLE]" Then
                    Section = "Battle"
                ElseIf s.StartsWith("[") Then
                    Section = Nothing
                ElseIf Section = "Battle" And s.ToUpper.StartsWith("LIST=") Then
                    Dim BattleList = s.Substring(s.IndexOf("="c) + 1).Split({"."c}, System.StringSplitOptions.RemoveEmptyEntries)
                    If BattleList.Contains(args(0), StringComparer.OrdinalIgnoreCase) Then
                        Say(Connection, Channel, "$b" & args(0) & " $bis already in the battle.")
                        Return
                    End If
                    ReDim Preserve BattleList(UBound(BattleList) + 1)
                    BattleList(UBound(BattleList)) = args(0)
                    s = "List=" & String.Join("."c, BattleList)
                End If
                Battle2Builder.AppendLine(s)
            Loop
            Battle2.Close()
            IO.File.WriteAllText(Battle2File, Battle2Builder.ToString)
            IO.File.AppendAllText(BattleFile, args(0) & ChrW(13) & ChrW(10), Encoding.ASCII)

            Say(Connection, Channel, "$k12" & args(0) & " $ohas been entered into the battle.")
        End If
    End Sub

#End Region

#Region "Character information"

    Private Sub GetAbilities()
        Dim I = Characters(LoggedIn)
        Threading.Thread.Sleep(600)

        If dccSocket IsNot Nothing AndAlso dccSocket.Connected Then
            ' Make sure DCC battle chat is enabled.
            If Not DCCBattleChatEnabled Then
                For j = 1 To 240  ' Give up after one minute.
                    If DCCBattleChatEnabled Then GoTo 1
                    Threading.Thread.Sleep(250)
                Next
                Return
            End If
        End If
1:
        ' Find my attributes.
        WriteMessage(2, 12, "Getting attributes.")
        If I.bHP = 0 Or I.bTP = 0 Or I.bSTR = 0 Or I.bDEF = 0 Or I.bINT = 0 Or I.bSPD = 0 Or I.EquippedWeapon = Nothing Then
            DoBattlePrivate("!stats")
            For j = 1 To 120
                If Not (I.bHP = 0 Or I.bTP = 0 Or I.bSTR = 0 Or I.bDEF = 0 Or I.bINT = 0 Or I.bSPD = 0 Or I.EquippedWeapon = Nothing) Then GoTo 2
                Threading.Thread.Sleep(250)
            Next
        End If
2:
        ' Find my skills.
        If I.Skills Is Nothing Then
            WriteMessage(2, 12, "Own skill list not known; retrieving.")
            DoBattlePrivate("!skills")
            For j = 1 To 120
                If I.Skills IsNot Nothing And TempSkills = Nothing Then Exit For
                Threading.Thread.Sleep(250)
            Next
        End If

        ' Find my weapons.
        If I.Weapons Is Nothing Then
            WriteMessage(2, 12, "Own weapon list not known; retrieving.")
9:
            RepeatCommand = 0
            DoBattlePrivate("!weapons")
            For j = 1 To 120
                If I.Weapons IsNot Nothing Then Exit For
                Threading.Thread.Sleep(250)
                If RepeatCommand = 1 Then GoTo 9
            Next
        End If

        For Each Weapon In I.Weapons
            If Not Weapons.ContainsKey(Weapon.Key) OrElse Not Weapons(Weapon.Key).IsWellKnown Then
                WriteMessage(2, 12, "Retrieving data for weapon " & Weapon.Key)
                If I.EquippedWeapon <> Weapon.Key Then
                    DoBattlePrivate("!equip " & Weapon.Key)
                    For j = 1 To 120
                        If I.EquippedWeapon = Weapon.Key And Weapons.ContainsKey(Weapon.Key) Then GoTo 3
                        Threading.Thread.Sleep(250)
                    Next
                    Return
                End If
3:
                ' !view-info the weapon.
                Threading.Thread.Sleep(1000)
10:
                RepeatCommand = 0
                DoBattlePrivate("!view-info weapon " & Weapon.Key)
                For j = 1 To 120
                    If Weapons.ContainsKey(Weapon.Key) AndAlso Weapons(Weapon.Key).IsWellKnown Then GoTo 4
                    Threading.Thread.Sleep(250)
                    If RepeatCommand = 1 Then GoTo 10
                Next
                Return
4:
                WaitingForOwnTechniques = True
                Threading.Thread.Sleep(1000)
                DoBattlePrivate("!techs")
                For j = 1 To 120
                    If Not WaitingForOwnTechniques Then GoTo 5
                    Threading.Thread.Sleep(250)
                Next
                Return
5:
            End If
        Next

        If I.Techniques IsNot Nothing Then
            For Each Technique In I.Techniques
                ' !view-info each technique.
                If Not (Techniques.ContainsKey(Technique.Key) AndAlso Techniques(Technique.Key).IsWellKnown) Then
                    WriteMessage(2, 12, "Retrieving data for technique " & Technique.Key)
                    'Threading.Thread.Sleep(1000)
11:
                    RepeatCommand = 0
                    DoBattlePrivate("!view-info tech " & Technique.Key)
                    For j = 1 To 120
                        If Techniques.ContainsKey(Technique.Key) AndAlso Techniques(Technique.Key).IsWellKnown Then GoTo 6
                        Threading.Thread.Sleep(250)
                        If RepeatCommand = 1 Then GoTo 11
                    Next
                    Return
6:
                End If
            Next
        End If

        If Not Weapons.ContainsKey(I.EquippedWeapon) Then
            WriteMessage(2, 4, "Equipped weapon is unknown!")
            DoBattlePrivate("!equip Fists")
            For j = 1 To 120
                If I.EquippedWeapon = "Fists" Then GoTo 7
                Threading.Thread.Sleep(250)
            Next
            Return
        End If
7:
        ' Find my style.
        If I.CurrentStyle = Nothing Then
            WriteMessage(2, 12, "Own style not known; retrieving.")
            DoBattlePrivate("!styles")
            For j = 1 To 120
                If I.CurrentStyle <> Nothing Then GoTo 8
                Threading.Thread.Sleep(250)
            Next
        End If
8:
        I.IsWellKnown = True
        I.IsReadyToControl = True
        WriteMessage(2, 12, "Finished setting up.")
    End Sub

    Private Sub GetAbilitiesOther(ByVal CharacterName As String)
        Dim InBattle = BattleList.ContainsKey(If(CharacterName, LoggedIn))
        Dim Ic = Characters(If(CharacterName, LoggedIn))
        Dim Ib As Combatant : If InBattle Then Ib = BattleList(If(CharacterName, LoggedIn))
        WriteMessage(2, 12, "Examining " & Ic.Name & ".")
        Threading.Thread.Sleep(600)

        ' Find their attributes.
        If Ic.bHP = 0 Or Ic.bTP = 0 Or Ic.bSTR = 0 Or Ic.bDEF = 0 Or Ic.bINT = 0 Or Ic.bSPD = 0 Or
           (InBattle AndAlso (Ib.HP = 0 Or Ib.TP = 0 Or Ib.STR = 0 Or Ib.DEF = 0 Or Ib.INT = 0 Or Ib.SPD = 0)) Or
           Ic.EquippedWeapon = Nothing Then
            WriteMessage(2, 12, "[" & Ic.Name & "] Attributes are not known; retrieving")
            DoBattlePrivate("!stats " & CharacterName)
            For j = 1 To 120
                If Not (Ic.bHP = 0 Or Ic.bTP = 0 Or Ic.bSTR = 0 Or Ic.bDEF = 0 Or Ic.bINT = 0 Or Ic.bSPD = 0 Or
                        (InBattle AndAlso (Ib.HP = 0 Or Ib.TP = 0 Or Ib.STR = 0 Or Ib.DEF = 0 Or Ib.INT = 0 Or Ib.SPD = 0)) Or
                        Ic.EquippedWeapon = Nothing) Then GoTo 2
                Threading.Thread.Sleep(250)
            Next
        End If
2:
        ' Find my skills.
        If Ic.Skills Is Nothing Then
            WriteMessage(2, 12, "[" & Ic.Name & "] Skill list is not known; retrieving")
            DoBattlePrivate("!skills " & CharacterName)
            For j = 1 To 120
                If Ic.Skills IsNot Nothing And TempSkills = Nothing Then Exit For
                Threading.Thread.Sleep(250)
            Next
        End If

        ' Find my weapons.
        If Ic.Weapons Is Nothing Then
            WriteMessage(2, 12, "[" & Ic.Name & "] Weapon list is not known; retrieving")
            DoBattlePrivate("!weapons " & CharacterName)
            For j = 1 To 120
                If Ic.Weapons IsNot Nothing Then Exit For
                Threading.Thread.Sleep(250)
            Next
        End If

        For Each Weapon In Ic.Weapons
            If Not Weapons.ContainsKey(Weapon.Key) OrElse Not Weapons(Weapon.Key).IsWellKnown Then
                ' !view-info the weapon.
                If Not Weapons(Weapon.Key).IsWellKnown Then
                    WriteMessage(2, 12, "Retrieving data for weapon " & Weapon.Key)
                    Threading.Thread.Sleep(1000)
                    DoBattlePrivate("!view-info weapon " & Weapon.Key)
                    For j = 1 To 120
                        If Weapons(Weapon.Key).IsWellKnown Then GoTo 4
                        Threading.Thread.Sleep(250)
                    Next
4:
                End If
            End If
        Next

        'If Ic.Techniques Is Nothing Then
        Ic.EquippedWeapon = Nothing
        WriteMessage(2, 12, "[" & Ic.Name & "] Retrieving technique list.")
        Threading.Thread.Sleep(1000)
        DoBattlePrivate("!techs " & CharacterName)
        For j = 1 To 120
            If Ic.EquippedWeapon IsNot Nothing Then GoTo 5
            Threading.Thread.Sleep(250)
        Next
        Return
        'End If
5:
        For Each Technique In Ic.Techniques
            ' !view-info each technique.
            If Not Techniques(Technique.Key).IsWellKnown Then
                WriteMessage(2, 12, "Retrieving data for technique " & Technique.Key)
                Threading.Thread.Sleep(1000)
                DoBattlePrivate("!view-info tech " & Technique.Key)
                For j = 1 To 120
                    If Techniques(Technique.Key).IsWellKnown Then GoTo 6
                    Threading.Thread.Sleep(250)
                Next
6:
            End If

        Next

        ' Find my style.
        If Ic.CurrentStyle = Nothing Then
            DoBattlePrivate("!xp " & CharacterName)
            For j = 1 To 120
                If Ic.CurrentStyle <> Nothing Then GoTo 8
                Threading.Thread.Sleep(250)
            Next
        End If
8:

        Ic.IsReadyToControl = True
        WriteMessage(2, 12, "[" & Ic.Name & "] Finished the examination.")

        Controlling.Add(CharacterName)
        DoBattle("OK. I'll control " & Ic.Name & ".")
    End Sub

    <ArenaRegex("(?:\x033\x02|\x02\x033)(?<Character>.*) \x02has the following weapons:( (?<Weapons>\x02[^(), ]+\(\d+\)\x02(, \x02[^(), ]+\(\d+\)\x02)*))?")>
    Public Sub OnWeapons(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        If Match.Groups("Weapons").Value = "" Then
            RepeatCommand += 1
            Return
        End If
        'If Match.Groups("Character").Value = OwnName Then
        '    Dim GetWeaponsThread = New Threading.Thread(AddressOf GetWeapons)
        '    GetWeaponsThread.Start({Connection, Channel, Match})
        'Else
        For Each Character In Characters
            If Character.Value.Name = Match.Groups("Character").Value Then
                Character.Value.Weapons = New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
                Dim Weapons As String() = Match.Groups("Weapons").Value.Split({", "}, StringSplitOptions.None)

                For Each Weapon In Weapons
                    Dim Key = Weapon.Split("("c)(0).Trim(Chr(2))
                    If Not Me.Weapons.ContainsKey(Key) Then _
                        Me.Weapons.Add(Key, New WeaponData With {.Name = Key, .Techniques = New List(Of String)})
                    Dim lMatch = Regex.Match(Weapon.Trim(Chr(2)), "(?<Name>.*)\((?<Level>\d+)\)")
                    Character.Value.Weapons.Add(lMatch.Groups("Name").Value, lMatch.Groups("Level").Value)
                Next
                WriteMessage(2, 7, "Registered " & Character.Value.Name & "'s weapons: " & String.Join(", ", Character.Value.Weapons.ToArray))

            End If
        Next

        'Dim GetWeaponsThread = New Threading.Thread(AddressOf GetWeapons)
        'GetWeaponsThread.Start({Connection, Channel, Match})
        'End If
    End Sub

    Private Sub GetWeapons(ByVal args As Object)
        Characters(LoggedIn).Weapons = New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)

        Dim Weapons As String() = args(2).Groups("Weapons").Value.Split({", "}, StringSplitOptions.None)

        For Each Weapon In Weapons
            If Not Me.Weapons.ContainsKey(Weapon.Split("("c)(0).Trim(Chr(2))) Then _
                Me.Weapons.Add(Weapon.Split("("c)(0).Trim(Chr(2)), New WeaponData With {.Name = Weapon.Split("("c)(0).Trim(Chr(2)), .Techniques = New List(Of String)})
            Dim Match = Regex.Match(Weapon.Trim(Chr(2)), "(?<Name>.*)\((?<Level>\d+)\)")
            Characters(LoggedIn).Weapons.Add(Match.Groups("Name").Value, Match.Groups("Level").Value)
        Next
        WriteMessage(2, 7, "Registered own weapons: " & String.Join(", ", Characters(LoggedIn).Weapons.ToArray))

        If Characters(LoggedIn).Techniques Is Nothing Then

            If Weapons.Count > 2 Then
                Say(args(0), args(1), Choose("I'm going to need to use $k11!techs$o a lot now; please excuse me...", "Prepare for a bit of spam... sorry."))
            End If

            For Each Weapon In Characters(LoggedIn).Weapons
                Say(args(0), ArenaNickname, "!view-info weapon " & Weapon.Key, SayOptions.NoticeNever)
                Threading.Thread.Sleep(1500)
                Say(args(0), ArenaNickname, "!equip " & Weapon.Key, SayOptions.NoticeNever)
                Threading.Thread.Sleep(1500)
                Say(args(0), ArenaNickname, "!techs", SayOptions.NoticeNever)
                Threading.Thread.Sleep(3000)
            Next

            Threading.Thread.Sleep(6000)

            For Each Technique In Characters(LoggedIn).Techniques
                Say(args(0), ArenaNickname, "!view-info tech " & Technique.Key, SayOptions.NoticeNever)
                Threading.Thread.Sleep(3000)
            Next

            If Weapons.Count > 2 Then
                Say(args(0), args(1), Choose("OK, that's all.", "OK, I'm good now.", "OK, I'm ready now."))
            End If

            Characters(LoggedIn).IsReadyToControl = True
        End If

    End Sub

    Private Function GetWeapons(ByVal Character As String)
        DoBattle("!weapons " & Character)

        Dim Reply As String, Weapons As String()
        Do
            Reply = WaitForMessage(ArenaConnection, ArenaChannel, ArenaNickname, 15)
            If Reply = Nothing Then Return False

            Dim m As Match = Regex.Match(Reply, "(?:\x033\x02|\x02\x033)(?<Character>.*) \x02has the following weapons:( (?<Weapons>\x02[^(), ]+\(\d+\)\x02(, \x02[^(), ]+\(\d+\)\x02)*))?")
            If m.Success Then
                If Not Characters.ContainsKey(Character) Then
                    ' GetWeapons was called on an unknown name. Assume that the bot's reponse was for my request.
                    Characters.Add(Character, New CharacterData With {.Name = m.Groups(Character).Value, .ShortName = Character})
                ElseIf m.Groups(Character).Value <> Characters(Character).Name Then
                    Continue Do
                End If
            End If

            Weapons = m.Groups("Weapons").Value.Split({", "}, StringSplitOptions.None)
            Exit Do
        Loop

        Characters(Character).Weapons = New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)

        For Each Weapon In Weapons
            Dim Match = Regex.Match(Weapon.Trim(Chr(2)), "(?<Name>.*)\((?<Level>\d+)\)")
            If Not Me.Weapons.ContainsKey(Match.Groups("Name").Value.Trim(Chr(2))) Then _
                Me.Weapons.Add(Match.Groups("Name").Value.Trim(Chr(2)), New WeaponData With {.Name = Match.Groups("Name").Value.Trim(Chr(2)), .Techniques = New List(Of String)})
            Characters(Character).Weapons.Add(Match.Groups("Name").Value, Match.Groups("Level").Value)
        Next
        WriteMessage(2, 3, "Registered " & Characters(Character).Name & "'s weapons: " & String.Join(", ", Characters(Character).Weapons.ToArray))
        Return True
    End Function

    Private Function GetTechniques(ByVal Character As String)
        DoBattle("!techs " & Character)

        Dim Reply As String, Techniques As String(), Weapon As String, Gender As String
        Do
            Reply = WaitForMessage(ArenaConnection, ArenaChannel, ArenaNickname, 15)
            If Reply = Nothing Then Return False

            Dim m As Match = Regex.Match(Reply, "(?:\x033\x02|\x02\x033)(?<Character>.*) \x02knows the following techniques for (?<Gender>his|her|its|their) (?<Weapon>[^ ]*):\x02 (?<Techniques>[^(), ]+\(\d+\)(, [^(), ]+\(\d+\))*)")
            If m.Success Then
                If Not Characters.ContainsKey(Character) Then
                    ' GetTechniques was called on an unknown name. Assume that the bot's reponse was for my request.
                    Characters.Add(Character, New CharacterData With {.Name = m.Groups(Character).Value, .ShortName = Character})
                ElseIf m.Groups(Character).Value <> Characters(Character).Name Then
                    Continue Do
                End If
            End If

            m = Regex.Match(Reply, "(?:\x033\x02|\x02\x033)(?<Character>.*) \x02does not know any techniques for (?<Gender>his|her|its|their) (?<Weapon>[^ ]*)\.")
            If m.Success Then
                If Not Characters.ContainsKey(Character) Then
                    ' GetTechniques was called on an unknown name. Assume that the bot's reponse was for my request.
                    Characters.Add(Character, New CharacterData With {.Name = m.Groups(Character).Value, .ShortName = Character, .Techniques = New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)})
                ElseIf m.Groups(Character).Value <> Characters(Character).Name Then
                    Continue Do
                End If

            End If

            Weapon = m.Groups("Weapon").Value
            Techniques = m.Groups("Techniques").Value.Split({", "}, StringSplitOptions.None)
            Gender = m.Groups("Gender").Value
            Exit Do
        Loop

        Characters(Character).EquippedWeapon = Weapon
        Characters(Character).EquippedWeaponTechs = New List(Of String)
        If Characters(Character).Techniques Is Nothing Then Characters(Character).Techniques = New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)

        ' Register the character's gender.
        Select Case Gender
            Case "his" : Characters(Character).Gender = "Male"
            Case "her" : Characters(Character).Gender = "Female"
            Case "its" : Characters(Character).Gender = "None"
        End Select

        If Weapons.ContainsKey(Weapon) Then
            ' Deregister techniques for that weapon.
            For Each Technique In Weapons(Weapon).Techniques
                Characters(Character).Techniques.Remove(Technique)
            Next
        End If

        For Each Technique In Techniques
            Dim Match = Regex.Match(Weapon.Trim(Chr(2)), "(?<Name>.*)\((?<Level>\d+)\)")
            If Not Me.Techniques.ContainsKey(Match.Groups("Name").Value.Trim(Chr(2))) Then _
                Me.Techniques.Add(Match.Groups("Name").Value.Trim(Chr(2)), New TechniqueData With {.Name = Match.Groups("Name").Value.Trim(Chr(2))})
            If Not Weapons(Weapon).Techniques.Contains(Match.Groups("Name").Value) Then Weapons(Weapon).Techniques.Add(Match.Groups("Name").Value)
            Characters(Character).Techniques.Add(Match.Groups("Name").Value, Match.Groups("Level").Value)
            Characters(Character).EquippedWeaponTechs.Add(Match.Groups("Name").Value)
        Next
        WriteMessage(2, 3, "Registered " & Characters(Character).Name & "'s techniques for " & Characters(Character).Gender & " " & Weapon & ": " & String.Join(", ", Characters(Character).Weapons.ToArray))
    End Function

    Private Function GetSkills(ByVal Character As String)
        DoBattle("!skills " & Character)

        Dim Reply As String, Skills As String(), Clear As Boolean = True
        Do
            Reply = WaitForMessage(ArenaConnection, ArenaChannel, ArenaNickname, 15)
            If Reply = Nothing Then Return False

            Dim m As Match

            m = Regex.Match(Reply, "(?:\x033\x02|\x02\x033)(?<Character>.*) \x02currently knows no skills.")
            If m.Success Then
                If Not Characters.ContainsKey(Character) Then
                    ' GetWeapons was called on an unknown name. Assume that the bot's reponse was for my request.
                    Characters.Add(Character, New CharacterData With {.Name = m.Groups(Character).Value, .ShortName = Character, .Skills = New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)})
                    Return True
                ElseIf m.Groups(Character).Value <> Characters(Character).Name Then
                    Continue Do
                Else
                    Characters(Character).Skills = New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
                    Return True
                End If
            End If

            m = Regex.Match(Reply, "(?:\x033\x02|\x02\x033)(?<Character>.*) \x02(knows|has) the following (?<Type>passive skills|active skills|resistances):\x02 (?<Skills>[^(), ]+\(\d+\)(, [^(), ]+\(\d+\))*)")
            If m.Success Then
                If Not Characters.ContainsKey(Character) Then
                    ' GetWeapons was called on an unknown name. Assume that the bot's reponse was for my request.
                    Characters.Add(Character, New CharacterData With {.Name = m.Groups(Character).Value, .ShortName = Character})
                ElseIf m.Groups(Character).Value <> Characters(Character).Name Then
                    Continue Do
                End If

                If Characters(Character).Skills Is Nothing Or Clear Then
                    Characters(Character).Skills = New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
                    Clear = False
                End If

                Skills = m.Groups("Skills").Value.Split({", "}, StringSplitOptions.None)
                For Each Skill In Skills
                    Dim Match = Regex.Match(Skill.Trim(Chr(2)), "(?<Name>.*)\((?<Level>\d+)\)")
                    Characters(Character).Skills.Add(Match.Groups("Name").Value, Match.Groups("Level").Value)
                Next

                WriteMessage(2, 3, "Registered " & Characters(Character).Name & "'s skills: " & String.Join(", ", Characters(Character).Skills.ToArray))

                If m.Groups("Type").Value = "resistances" Then Return True
            End If
        Loop
    End Function

    <ArenaRegex("^(?:\x033\x02|\x02\x033)(?<Character>.*) \x02(knows|has) the following (\x0312)?(?<Skills>passive(\x033)? skills|active(\x033)? skills|resistances(\x033)?|monster killer traits(\x033)?):\x02 (?<Skills>[^(), ]+\(\d+\)(, [^(), ]+\(\d+\))*)")>
    Public Sub OnSkills(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        For Each Character In Characters
            If Character.Value.Name = Match.Groups("Character").Value Then

                If TempSkills <> Character.Value.Name Then
                    Character.Value.Skills = New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
                    TempSkills = Character.Value.Name
                    Say(ArenaConnection, ArenaNickname, ChrW(1) & "PING " & Pings.Skills & ChrW(1), SayOptions.NoticeNever)
                End If

                Dim Skills = Match.Groups("Skills").Value.Split({", "}, StringSplitOptions.None)
                For Each Skill In Skills
                    Dim lMatch = Regex.Match(Skill, "(?<Name>.*)\((?<Level>\d+)\)")
                    Character.Value.Skills.Add(lMatch.Groups("Name").Value, lMatch.Groups("Level").Value)
                Next
                WriteMessage(2, 7, "Registered " & Character.Value.Name & "'s skills: " & String.Join(", ", Character.Value.Skills.ToArray))

                If Match.Groups("Type").Value = "monster killer traits" Then TempSkills = Nothing ' Resistances are shown last.
            End If
        Next
    End Sub

    <ArenaRegex("^(?:\x033\x02|\x02\x033)(?<Character>.*) \x02currently knows no skills\.")>
    Public Sub OnSkillsNone(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        For Each Character In Characters
            If Character.Value.Name = Match.Groups("Character").Value Then

                Character.Value.Skills = New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)

                WriteMessage(2, 7, "Registered " & Character.Value.Name & "'s skills: none")

                If Match.Groups("Type").Value = "resistances" Then TempSkills = Nothing ' Resistances are shown last.
            End If
        Next
    End Sub

    <ArenaRegex("^(?:\x033\x02|\x02\x033)(?<Character>.*) \x02knows the following techniques for (?<Gender>his|her|its|their) (?<Weapon>[^ ]*):\x02 (?<Techniques>[^(), ]+\(\d+\)(, [^(), ]+\(\d+\))*)")>
    Public Sub OnTechniques(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        For Each Character In Characters
            If Character.Value.Name = Match.Groups("Character").Value Then
                ' Register their gender.
                Select Case Match.Groups("Gender").Value
                    Case "his"
                        Character.Value.Gender = "Male"
                    Case "her"
                        Character.Value.Gender = "Female"
                    Case "its"
                        Character.Value.Gender = "None"
                    Case Else
                        Character.Value.Gender = Nothing
                End Select

                Character.Value.EquippedWeapon = Match.Groups("Weapon").Value

                If Character.Value.Techniques Is Nothing Then Character.Value.Techniques = New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
                Dim Techniques = Match.Groups("Techniques").Value.Split({", "}, StringSplitOptions.None)

                If Weapons.ContainsKey(Match.Groups("Weapon").Value) Then
                    For Each Technique In Weapons(Match.Groups("Weapon").Value).Techniques
                        Character.Value.Techniques.Remove(Technique)
                    Next
                Else
                    Weapons.Add(Match.Groups("Weapon").Value, New WeaponData With {.Name = Match.Groups("Weapon").Value, .Techniques = New List(Of String)})
                End If

                For Each Technique In Techniques
                    Dim lMatch = Regex.Match(Technique, "(?<Name>.*)\((?<Level>\d+)\)")

                    If Not Me.Techniques.ContainsKey(lMatch.Groups("Name").Value) Then _
                        Me.Techniques.Add(lMatch.Groups("Name").Value, New TechniqueData With {.Name = lMatch.Groups("Name").Value})

                    If Not Weapons(Match.Groups("Weapon").Value).Techniques.Contains(lMatch.Groups("Name").Value) Then _
                        Weapons(Match.Groups("Weapon").Value).Techniques.Add(lMatch.Groups("Name").Value)

                    Character.Value.Techniques.Add(lMatch.Groups("Name").Value, lMatch.Groups("Level").Value)
                    If Character.Value.EquippedWeaponTechs Is Nothing Then Character.Value.EquippedWeaponTechs = New List(Of String)
                    Character.Value.EquippedWeaponTechs.Add(lMatch.Groups("Name").Value)
                Next
                WriteMessage(2, 7, "Registered " & Character.Value.Name & "'s techniques for " & Character.Value.GenderPronoun.tolower & " " & Match.Groups("Weapon").Value & ": " & String.Join(", ", Techniques.ToArray))
                If Character.Key = LoggedIn Then WaitingForOwnTechniques = False
            End If
        Next
    End Sub

    <ArenaRegex("^(?:\x033\x02|\x02\x033)(?<Character>.*) \x02does not know any techniques for (?<Gender>his|her|its|their) (?<Weapon>[^ ]*)\.")>
    Public Sub OnTechniquesNone(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        For Each Character In Characters
            If Character.Value.Name = Match.Groups("Character").Value Then
                ' Register their gender.
                Select Case Match.Groups("Gender").Value
                    Case "his"
                        Character.Value.Gender = "Male"
                    Case "her"
                        Character.Value.Gender = "Female"
                    Case "its"
                        Character.Value.Gender = "None"
                    Case Else
                        Character.Value.Gender = Nothing
                End Select

                Character.Value.EquippedWeapon = Match.Groups("Weapon").Value

                If Character.Value.Techniques Is Nothing Then Return
                For Each Technique In Weapons(Match.Groups("Weapon").Value).Techniques
                    Character.Value.Techniques.Remove(Technique)
                Next

                WriteMessage(2, 7, "Registered " & Character.Value.Name & "'s techniques for " & Character.Value.GenderPronoun.tolower & " " & Match.Groups("Weapon").Value & ": none")
            End If
        Next
    End Sub

    <ArenaRegex("^\x033Here are your current stats:$")>
    Public Sub OnStatsOwn(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        ViewingStatsCharacter = LoggedIn
    End Sub

    <ArenaRegex("^\x033Here are the current stats for (?<Name>[^ ]*):$")>
    Public Sub OnStatsOther(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        ViewingStatsCharacter = Match.Groups("Name").Value
    End Sub

    <ArenaRegex("^\[\x034HP\x0312 (?<HP>\d*)\x031/\x0312(?<bHP>\d*)\x031\] \[\x034TP\x0312 (?<TP>\d*)\x031/\x0312(?<bTP>\d*)\x031\]( \[\x034Ignition Gauge\x0312 (?<IG>\d*)\x031/\x0312(?<bIG>\d*)\x031\])? \[\x034Status\x0312 \x033(?<Status>.*)\x031\]( \[\x034Royal Guard Meter\x0312 (?<RoyalGuard>\d*)\x031\])?$")>
    Public Sub OnStats1(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        Dim c As CharacterData = Nothing, b As Combatant = Nothing

        If Characters.TryGetValue(ViewingStatsCharacter, c) Then
            If BattleList.TryGetValue(ViewingStatsCharacter, b) Then
                b.HP = Match.Groups("HP").Value
                b.TP = Match.Groups("TP").Value
            End If

            c.bHP = Match.Groups("bHP").Value
            c.bTP = Match.Groups("bTP").Value
            c.IgnitionCharge = If(Match.Groups("IG").Success, Match.Groups("IG").Value, 0)
            c.bIG = If(Match.Groups("bIG").Success, Match.Groups("bIG").Value, 0)
            c.RoyalGuardCharge = If(Match.Groups("RoyalGuard").Success, Match.Groups("RoyalGuard").Value, 0)

            WriteMessage(2, 7, String.Format("Registered {0}'s attributes.  HP: {1}/{2}  TP: {3}/{4}  IG: {5}/{6}  RG: {7}", c.Name, If(b IsNot Nothing, b.HP, c.bHP), c.bHP, If(b IsNot Nothing, b.TP, c.bTP), c.bTP, c.IgnitionCharge, c.bIG, c.RoyalGuardCharge))
        End If
    End Sub

    <ArenaRegex("^(?:\x033\x02|\x02\x033)(?<Character>.*) \x02is currently using the\x02 (?<Style>[^ ]+) \x02style\. \[(XP: (?<EXP>\d+) / (?<EXPNeeded>\d+)|(?<Maxed>[^\]]*Max[^\]]*))\]$")>
    Public Sub OnStyle(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        For Each c In Characters.Values
            If c.Name = Match.Groups("Character").Value Then
                c.CurrentStyle = Match.Groups("Style").Value
                ' Register that this character has this style.
                If c.Styles Is Nothing Then c.Styles = New Dictionary(Of String, Short)(StringComparer.OrdinalIgnoreCase)

                If Not c.Styles.ContainsKey(c.CurrentStyle) Then c.Styles.Add(c.CurrentStyle, If(Match.Groups("Maxed").Success, 10, Match.Groups("EXPNeeded").Value / 500)) _
                        Else c.Styles(c.CurrentStyle) = If(Match.Groups("Maxed").Success, 10, Match.Groups("EXPNeeded").Value / 500)
                ' Register their experience in this style.
                If c.StyleExperience Is Nothing Then c.StyleExperience = New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
                If Not c.StyleExperience.ContainsKey(c.CurrentStyle) Then c.StyleExperience.Add(c.CurrentStyle, If(Match.Groups("Maxed").Success, 0, Match.Groups("EXP").Value)) _
                    Else c.StyleExperience(c.CurrentStyle) = If(Match.Groups("Maxed").Success, 0, Match.Groups("EXP").Value)

                WriteMessage(2, 7, String.Format("Registered {0}'s current style.  {1}  Level: {2}  Experience: {3}/{4}", c.Name, c.CurrentStyle, c.Styles(c.CurrentStyle), c.StyleExperience(c.CurrentStyle), If(Match.Groups("Maxed").Success, 0, c.Styles(c.CurrentStyle) * 500)))
                Return
            End If
        Next
    End Sub

    <ArenaRegex("^(?:\x033\x02|\x02\x033)(?<Character>.*) \x02knows the following styles: (?<Styles>(\x02[^ (]+\(\d+\)\x02(, \x02[^ (]+\(\d+\)\x02)*)?)$")>
    Public Sub OnStyles(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        For Each c In Characters.Values
            If c.Name = Match.Groups("Character").Value Then
                Dim Message As String = "", r As New Regex("\x02(?<Style>[^ (]+)\((?<Level>\d+)\)\x02")
                If c.Styles Is Nothing Then c.Styles = New Dictionary(Of String, Short)(StringComparer.OrdinalIgnoreCase)

                For Each Style In Match.Groups("Styles").Value.Split(", ")
                    Dim m = r.Match(Style)
                    If Not m.Success Then Continue For

                    ' Register that this character has this style.
                    If Not c.Styles.ContainsKey(m.Groups("Style").Value) Then c.Styles.Add(m.Groups("Style").Value, m.Groups("Level").Value) _
                        Else c.Styles(m.Groups("Style").Value) = m.Groups("Level").Value

                    Message &= If(Message = "", "", ", ") & m.Groups("Style").Value & " level " & m.Groups("Level").Value
                Next

                WriteMessage(2, 7, "Registered " & c.Name & "'s styles: " & Message)
                Return
            End If
        Next
    End Sub

    <ArenaRegex("^(?:\x033\x02|\x02\x033)(?<Character>.*)\x02's (?<Style>[^ ]+) style has leveled up! It is now\x02 level (?<Level>\d+)\x02!$")>
    Public Sub OnStyleLevelUp(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        For Each c In Characters.Values
            If c.Name = Match.Groups("Character").Value Then
                c.CurrentStyle = Match.Groups("Style").Value

                If c.Styles Is Nothing Then c.Styles = New Dictionary(Of String, Short)(StringComparer.OrdinalIgnoreCase)
                If Not c.Styles.ContainsKey(c.CurrentStyle) Then c.Styles.Add(c.CurrentStyle, Match.Groups("Level").Value) _
                    Else c.Styles(c.CurrentStyle) = Match.Groups("Level").Value

                WriteMessage(2, 7, String.Format("{0}'s {1} style level has increased to {2}!", c.Name, c.CurrentStyle, c.Styles(c.CurrentStyle)))
                Return
            End If
        Next
    End Sub

    <ArenaRegex("^(?:\x033\x02|\x02\x033)(?<Character>.*) \x02has switched to the\x02 (?<Style>[^ ]+) \x02style!$")>
    Public Sub OnStyleChange(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        For Each c In Characters.Values
            If c.Name = Match.Groups("Character").Value Then
                c.CurrentStyle = Match.Groups("Style").Value

                If c.Styles Is Nothing Then c.Styles = New Dictionary(Of String, Short)(StringComparer.OrdinalIgnoreCase)
                If Not c.Styles.ContainsKey(c.CurrentStyle) Then c.Styles.Add(c.CurrentStyle, 1)

                WriteMessage(2, 7, String.Format("{0} changes to the {1} style.", c.Name, c.CurrentStyle))
                Return
            End If
        Next
    End Sub

    <ArenaRegex("^\[\x034Strength\x0312 (?<STR>\d*)\x031\] \[\x034Defense\x0312 (?<DEF>\d*)\x031\] \[\x034Intelligence\x0312 (?<INT>\d*)\x031\] \[\x034Speed\x0312 (?<SPD>\d*)\x031\]$")>
    Public Sub OnStats2(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        Dim c As CharacterData = Nothing, b As Combatant = Nothing

        If Characters.TryGetValue(ViewingStatsCharacter, c) Then
            If BattleList.TryGetValue(ViewingStatsCharacter, b) Then
                b.STR = Match.Groups("STR").Value
                b.DEF = Match.Groups("DEF").Value
                b.INT = Match.Groups("INT").Value
                b.SPD = Match.Groups("SPD").Value
                '                OutputLine("\cGRAY[\cDKYELLOWBattle Arena\cGRAY] \cWHITE" & String.Format("Registered {0}'s current attributes.  STR: {1}  DEF: {2}  INT: {3}  SPD: {4}\r", c.Name, b.STR, b.DEF, b.INT, b.SPD))
            End If

            c.bSTR = Match.Groups("STR").Value - TurnNumber / 6
            c.bDEF = Match.Groups("DEF").Value + TurnNumber / 3
            c.bINT = Match.Groups("INT").Value
            c.bSPD = Match.Groups("SPD").Value
            WriteMessage(2, 7, String.Format("Registered {0}'s attributes.  STR: {1}  DEF: {2}  INT: {3}  SPD: {4}", c.Name, c.bSTR, c.bDEF, c.bINT, c.bSPD))
        End If
    End Sub

    <ArenaRegex("^\[\x034Current Weapon Equipped ?\x0312 ?(?<Weapon>[^ ]*)\x031\]( \[\x034Current Accessory( Equipped)? \x0312(?<Accessory>[^ ]*)\x031\]( \[\x034Current Head Armor \x0312(?<Head>[^ ]*)\x031\] \[\x034Current Body Armor \x0312(?<Body>[^ ]*)\x031\] \[\x034Current Leg Armor \x0312(?<Legs>[^ ]*)\x031\] \[\x034Current Feet Armor \x0312(?<Feet>[^ ]*)\x031\] \[\x034Current Hand Armor \x0312(?<Hands>[^ ]*)\x031\])?)?$")>
    Public Sub OnStats3(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        Dim c As CharacterData = Nothing

        If Characters.TryGetValue(ViewingStatsCharacter, c) Then
            c.EquippedWeapon = Match.Groups("Weapon").Value
            c.EquippedAccessory = If(Match.Groups("Accessory").Value = "nothing", Nothing, Match.Groups("Accessory").Value)

            Characters(LoggedIn).EquippedWeaponTechs = New List(Of String)


            If Weapons.ContainsKey(c.EquippedWeapon) Then
                If Characters(LoggedIn).Techniques IsNot Nothing Then
                    For Each Technique In Weapons(c.EquippedWeapon).Techniques
                        If c.Techniques.ContainsKey(Technique) Then c.EquippedWeaponTechs.Add(Technique)
                    Next
                End If
            Else
                Weapons.Add(c.EquippedWeapon, New WeaponData With {.Name = c.EquippedWeapon, .Techniques = New List(Of String)})
            End If

            WriteMessage(2, 7, String.Format("Registered {0}'s equipment.  Weapon: {1}  Accessory: {2}", c.Name, c.EquippedWeapon, If(c.EquippedAccessory, "nothing")))
        End If
    End Sub

    <ArenaRegex("^\x033\x02(?<Name>[^\x02]*) \x02has\x02 \$\$(?<Amount>\d*) \x02double dollars\.$")>
    Public Sub OnDoubleDollars(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        For Each c In Characters
            If c.Value.Name = Match.Groups("Name").Value Then
                c.Value.DoubleDollars = Match.Groups("Amount").Value
                WriteMessage(2, 7, String.Format("Registered that {0} has {1} double dollars.", c.Value.Name, c.Value.DoubleDollars))
                Return
            End If
        Next
    End Sub
#End Region

#Region "My character"

    <ArenaRegex("^\x032You enter the arena with a total of\x02 (?<Orbs>\d+) \x02Red Orbs to spend.$")>
    Public Sub OnNewCharOrbs(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        ' Register the new character.

        If Not OwnCharacters.ContainsKey(Nick(Connection)) Then
            OwnCharacters.Add(Nick(Connection), New OwnCharacterData With {.FullName = Match.Groups("Name").Value})
        End If

        Dim Character As New CharacterData With {
                                .Category = CharacterCategory.Player, .Name = Nick(Connection), .ShortName = Nick(Connection),
                                .ElementalResistances = New List(Of String), .ElementalWeaknesses = New List(Of String), .ElementalAbsorbs = New List(Of String), .ElementalImmunities = New List(Of String),
                                .WeaponResistances = New List(Of String), .WeaponWeaknesses = New List(Of String),
                                .bDEF = 5, .bHP = 100, .bIG = 0, .bINT = 5, .bSPD = 5, .bSTR = 5, .bTP = 20,
                                .EquippedAccessory = Nothing, .EquippedWeapon = "Fists", .EquippedWeaponTechs = New List(Of String) From {"DoublePunch"},
                                .Gender = "Male", .IsEthereal = False, .IsReadyToControl = True, .IsUndead = False, .IsWellKnown = True,
                                .Skills = New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase),
                                .Weapons = New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase) From {{"Fists", 1}},
                                .Techniques = New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase) From {{"DoublePunch", 1}},
                                .Items = New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase) From {{"Potion", 1}},
                                .Styles = New Dictionary(Of String, Short)(StringComparer.OrdinalIgnoreCase) From {{"Trickster", 1}},
                                .StyleExperience = New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase) From {{"Trickster", 0}},
                                .CurrentStyle = "Trickster",
                                .Ignitions = New List(Of String),
                                .RedOrbs = Match.Groups("Orbs").Value, .BlackOrbs = 1, .AlliedNotes = 0,
                                .StartsWithElementalSeal = False, .StartsWithManaWall = False, .StartsWithMightyStrike = False, .StartsWithRoyalGuard = False, .StartsWithUtsusemi = 0
                            }

        Characters.Add(Nick(Connection), Character)
    End Sub

    <ArenaRegex("^\x032Your password has been set to\x02 (?<DefaultPassword>battlearena\d\d\w) \x02and it is recommended you change it using the command\x02 !newpass battlearena\d\d\w newpasswordhere \x02in private or at least write the password down\. \x034?\x02Passwords cannot be recovered!$")>
    Public Sub OnNewCharReady(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        '' Set the password.
        'WriteMessage(1, 7, "Setting password (" & Password & ")")
        'Say(Connection, ArenaNickname, String.Format("!newpass {0} {1}", Match.Groups("DefaultPassword").Value, Password), SayOptions.NoticeNever)

        '' Set the gender.
        'If Gender <> "male" Then
        '    WriteMessage(1, 7, "Setting gender (" & Gender & ")")
        '    Say(Connection, ArenaNickname, "!setgender " & Gender, SayOptions.NoticeNever)
        'End If

        '' Set the description
        'If Description <> Nothing Then
        '    WriteMessage(1, 7, "Setting description (my character " & Description & ")")
        '    Say(Connection, ArenaNickname, "!cdesc " & Description, SayOptions.NoticeNever)
        'End If

        WriteMessage(1, 7, "Finished setting up my character!")
        LoggedIn = Nick(Connection)
    End Sub

    <ArenaRegex("^\x034A character with the name\x02 .* \x02already exists\. If this is you, use the \x02!id\x02 command with your password in query to log in\. If not, please change your nick and try again\.$")>
    Public Sub OnNewCharExists(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        'WriteMessage(1, 7, Nick(Connection) & " is already registered. Attempting to log in...")
        'Say(Connection, ArenaNickname, "!id " & Password, SayOptions.NoticeNever)
    End Sub

    <ArenaRegex("^(?:\x033\x02|\x02\x033)Your \x02gender has been set to\x02 (?<Gender>male|female|neither|none|its)$")>
    Public Sub OnGender(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        Select Case Match.Groups("Gender").Value
            Case "male"
                Characters(LoggedIn).Gender = "Male"
            Case "female"
                Characters(LoggedIn).Gender = "Female"
            Case "neither", "none", "its"
                Characters(LoggedIn).Gender = "None"
            Case Else
                Characters(LoggedIn).Gender = Nothing
        End Select
        WriteMessage(1, 7, "My gender has been reset to " & If(Characters(LoggedIn).Gender, "Unknown").ToLower & ".")
    End Sub

    '<ArenaRegex("^\x033You spend\x02 (?<Orbs>\d+) \x02Red Orbs")>
    'Public Sub OnOrbSpend(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
    '    RedOrbs -= Match.Groups("Orbs").Value
    'End Sub

    <ArenaRegex("^\x033You spend\x02 (?<Cost>[\d,]*) \x02.* for\x02 (?<Quantity>\d*) (?<Item>[^ ]*)\(s\)\x02!$")>
    Public Sub OnOrbSpendItems(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        If Not Characters.ContainsKey(LoggedIn) Then Return

        Dim Ic = Characters(LoggedIn)

        Ic.RedOrbs -= Match.Groups("Cost").Value.Replace(",", "")

        If Ic.Items Is Nothing Then Ic.Items = New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)

        Dim NewCount As Integer = 0
        If Ic.Items.ContainsKey(Match.Groups("Item").Value) Then
            NewCount = Ic.Items(Match.Groups("Item").Value)
            Ic.Items.Remove(Match.Groups("Item").Value)
        End If
        NewCount += Match.Groups("Quantity").Value
        Ic.Items.Add(Match.Groups("Item").Value, NewCount)

        'If Not Items.ContainsKey(Match.Groups("Item").Value) Then
        '    Items.Add(Match.Groups("Item").Value, New ItemData With {.Name = Match.Groups("Item").Value, .Cost = Match.Groups("Cost").Value.Replace(",", "")})
        '    DoBattlePrivate("!view-info item " & Match.Groups("Item").Value)
        'End If
    End Sub

    <ArenaRegex("^\x033You spend\x02 (?<Cost>[\d,]*) \x02.* for\x02 \+(?<Quantity>\d*) \x02to your\x02 (?<Item>[^ ]*) technique\x02!$")>
    Public Sub OnOrbSpendTechniques(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        If Not Characters.ContainsKey(LoggedIn) Then Return

        Dim Ic = Characters(LoggedIn)

        Ic.RedOrbs -= Match.Groups("Cost").Value.Replace(",", "")

        If Ic.Techniques Is Nothing Then Ic.Techniques = New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)

        Dim NewCount As Integer = 0
        If Ic.Techniques.ContainsKey(Match.Groups("Item").Value) Then
            NewCount = Ic.Techniques(Match.Groups("Item").Value)
            Ic.Techniques.Remove(Match.Groups("Item").Value)
        End If
        NewCount += Match.Groups("Quantity").Value
        Ic.Techniques.Add(Match.Groups("Item").Value, NewCount)

        If Not Techniques.ContainsKey(Match.Groups("Item").Value) Then
            Techniques.Add(Match.Groups("Item").Value, New TechniqueData With {.Name = Match.Groups("Item").Value, .Cost = Match.Groups("Cost").Value.Replace(",", "")})
            DoBattlePrivate("!view-info tech " & Match.Groups("Item").Value)
        End If
        If Not Weapons(Ic.EquippedWeapon).Techniques.Contains(Match.Groups("Item").Value) Then Weapons(Ic.EquippedWeapon).Techniques.Add(Match.Groups("Item").Value)
        If Not Ic.EquippedWeaponTechs.Contains(Match.Groups("Item").Value) Then Ic.EquippedWeaponTechs.Add(Match.Groups("Item").Value)
    End Sub

    <ArenaRegex("^\x033You spend\x02 (?<Cost>[\d,]*) \x02.* for\x02 \+(?<Quantity>\d*) \x02to your\x02 (?<Item>[^ ]*) skill\x02!$")>
    Public Sub OnOrbSpendSkills(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        If Not Characters.ContainsKey(LoggedIn) Then Return

        Dim Ic = Characters(LoggedIn)

        Ic.RedOrbs -= Match.Groups("Cost").Value.Replace(",", "")

        If Ic.Skills Is Nothing Then Ic.Skills = New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)

        Dim NewCount As Integer = 0
        If Ic.Skills.ContainsKey(Match.Groups("Item").Value) Then
            NewCount = Ic.Skills(Match.Groups("Item").Value)
            Ic.Skills.Remove(Match.Groups("Item").Value)
        End If
        NewCount += Match.Groups("Quantity").Value
        Ic.Skills.Add(Match.Groups("Item").Value, NewCount)
    End Sub

    <ArenaRegex("^\x033You spend\x02 (?<Cost>[\d,]*) \x02.* for\x02 \+(?<Quantity>[\d,]*) \x02to your (?<Item>[^ ]*)!$")>
    Public Sub OnOrbSpendAttributes(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        If Not Characters.ContainsKey(LoggedIn) Then Return

        Dim Ic = Characters(LoggedIn)

        Ic.RedOrbs -= Match.Groups("Cost").Value.Replace(",", "")

        Select Case Match.Groups("Item").Value.ToUpper
            Case "HP"
                Ic.bHP += Match.Groups("Quantity").Value
            Case "TP"
                Ic.bTP += Match.Groups("Quantity").Value
            Case "IG"
                Ic.bIG += Match.Groups("Quantity").Value
            Case "STR"
                Ic.bSTR += Match.Groups("Quantity").Value
            Case "DEF"
                Ic.bDEF += Match.Groups("Quantity").Value
            Case "INT"
                Ic.bINT += Match.Groups("Quantity").Value
            Case "SPD"
                Ic.bSPD += Match.Groups("Quantity").Value
        End Select
    End Sub

    <ArenaRegex("^\x033You spend\x02 (?<Cost>[\d,]*) \x02.* to upgrade your\x02 (?<Item>[^ ]*)\x02!$")>
    Public Sub OnOrbSpendUpgrades(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        If Not Characters.ContainsKey(LoggedIn) Then Return

        Dim Ic = Characters(LoggedIn)

        Ic.RedOrbs -= Match.Groups("Cost").Value.Replace(",", "")
    End Sub

    <ArenaRegex("^\x033You spend\x02 (?<Cost>[\d,]*) \x02black orb\(s\) to purchase\x02 (?<Item>[^ ]*)\x02!$")>
    Public Sub OnOrbSpendWeapons(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        If Not Characters.ContainsKey(LoggedIn) Then Return

        Dim Ic = Characters(LoggedIn)

        Ic.BlackOrbs -= Match.Groups("Cost").Value.Replace(",", "")

        If Ic.Weapons Is Nothing Then Ic.Weapons = New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)

        Ic.Weapons.Add(Match.Groups("Item").Value, 1)

        If Not Weapons.ContainsKey(Match.Groups("Item").Value) Then
            Weapons.Add(Match.Groups("Item").Value, New WeaponData With {.Name = Match.Groups("Item").Value, .Cost = Match.Groups("Cost").Value.Replace(",", "")})
            DoBattlePrivate("!view-info weapon " & Match.Groups("Item").Value)
        End If
    End Sub

    <ArenaRegex("^\x033You spend\x02 (?<Cost>[\d,]*) \x02black orb\(s\) to purchase\x02 (?<Item>[^ ]*)\x02!$")>
    Public Sub OnOrbSpendStyles(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        If Not Characters.ContainsKey(LoggedIn) Then Return

        Dim Ic = Characters(LoggedIn)

        Ic.BlackOrbs -= Match.Groups("Cost").Value.Replace(",", "")

        If Ic.Styles Is Nothing Then Ic.Styles = New Dictionary(Of String, Short)(StringComparer.OrdinalIgnoreCase)

        Ic.Styles.Add(Match.Groups("Item").Value, 1)
    End Sub

    <ArenaRegex("^\x033You spend\x02 (?<Cost>[\d,]*) \x02black orb\(s\) to purchase\x02 (?<Item>[^ ]*)\x02!$")>
    Public Sub OnOrbSpendIgnitions(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        If Not Characters.ContainsKey(LoggedIn) Then Return

        Dim Ic = Characters(LoggedIn)

        Ic.BlackOrbs -= Match.Groups("Cost").Value.Replace(",", "")

        If Ic.Ignitions Is Nothing Then Ic.Ignitions = New List(Of String)

        Ic.Ignitions.Add(Match.Groups("Item").Value)

        'If Not Ignitions.ContainsKey(Match.Groups("Item").Value) Then
        '    Ignitions.Add(Match.Groups("Item").Value, New IgnitionData With {.Name = Match.Groups("Item").Value, .Cost = Match.Groups("Cost").Value.Replace(",", "")})
        '    DoBattlePrivate("!view-info ignition " & Match.Groups("Item").Value)
        'End If
    End Sub

    <ArenaRegex("^\x033You spend\x02 (?<Cost>[\d,]*) \x02black orb\(s\) for \x02(?<Quantity>[\d,]*) .*!$")>
    Public Sub OnOrbSpendOrbs(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        If Not Characters.ContainsKey(LoggedIn) Then Return

        Dim Ic = Characters(LoggedIn)

        Ic.BlackOrbs -= Match.Groups("Cost").Value.Replace(",", "")
        Ic.RedOrbs += Match.Groups("Quantity").Value.Replace(",", "")
    End Sub

    <ArenaRegex("^\x034The \x02Ai System\x02 has been turned (off|on)\.")>
    Public Sub OnAIToggle(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        If IsOwnerChecking And Now - IsOwnerChecked < TimeSpan.FromSeconds(15) Then
            IsOwner = True
            IsOwnerChecking = False
            IsOwnerCheckTimer.Interval = 21600000
            IsOwnerCheckTimer.Start()
            DoBattlePrivate("!toggle AI system")
            WriteMessage(2, 7, "I'm a bot admin! :-)")
        End If
    End Sub

    Private Sub IsOwnerCheckTimer_Elapsed(ByVal sender As Object, ByVal e As Timers.ElapsedEventArgs) Handles IsOwnerCheckTimer.Elapsed
        If IsOwnerChecking Then
            IsOwner = False
            IsOwnerChecking = False
            IsOwnerCheckTimer.Interval = 21600000
            IsOwnerCheckTimer.Start()
            WriteMessage(2, 7, "I'm not a bot admin.")
        Else
            IsOwnerChecking = True
            IsOwnerChecked = Now
            DoBattlePrivate("!toggle AI system")
            IsOwnerCheckTimer.Interval = 15000
            IsOwnerCheckTimer.Start()
        End If
    End Sub

    <ArenaRegex("^\x0310\x02(?<Name>[^\x02]*) \x02(?<Description>.*)")>
    Public Sub OnIdentify(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        If CurrentTurn IsNot Nothing AndAlso Characters(CurrentTurn).Name = Match.Groups("Name").Value Then Return
        If LoggedIn = Nothing Then
            If Match.Groups("Name").Value = Nick(Connection) Then
                LoggedIn = Nick(Connection)
                WriteMessage(2, 7, "Identified successfully.")

                IsOwner = False
                IsOwnerChecking = True
                IsOwnerChecked = Now
                DoBattlePrivate("!toggle AI system")
                IsOwnerCheckTimer.Interval = 15000
                IsOwnerCheckTimer.Start()

                If Not OwnCharacters.ContainsKey(Nick(Connection)) Then
                    OwnCharacters.Add(Nick(Connection), New OwnCharacterData With {.FullName = Match.Groups("Name").Value, .Password = ""})
                Else
                End If

                If Not Characters.ContainsKey(Nick(Connection)) Then
                    Characters.Add(Nick(Connection), New CharacterData With {.ShortName = Nick(Connection), .Name = Match.Groups("Name").Value, .Category = CharacterCategory.Player})
                End If

                If Not EnableParticipation Then Return
                Dim GetAbilitiesThread = New Threading.Thread(AddressOf GetAbilities)
                GetAbilitiesThread.Start()
            End If
        ElseIf Characters(LoggedIn).Name = Match.Groups("Name").Value Then
            LoggedIn = Nick(Connection)
            WriteMessage(2, 7, "Identified successfully.")

            IsOwner = False
            IsOwnerChecking = True
            IsOwnerChecked = Now
            DoBattlePrivate("!toggle AI system")
            IsOwnerCheckTimer.Interval = 15000
            IsOwnerCheckTimer.Start()
        End If
    End Sub


#Region "!view-info"

    <ArenaRegex("\[\x034Name\x0312 (?<Name>[^]]*)\x031\] \[\x034Weapon Type\x0312 (?<Type>[^]]*)\x031\] (\[\x034Weapon Size\x0312 (?<Size>[^]]*)\x031\] )?\[\x034# of Hits ?\x0312 (?<Hits>[^]]*)\x031\]")>
    Public Sub OnViewInfoWeapon1(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        Dim Name As String = Match.Groups("Name").Value, Type As String = Match.Groups("Type").Value, Hits As String = Match.Groups("Hits").Value

        ViewingInfoWeapon = Name

        If Not Weapons.ContainsKey(Name) Then Weapons.Add(Name, New WeaponData With {.Name = Name})

        Weapons(Name).Category = Type
        If Short.TryParse(Hits, Weapons(Name).Hits) Then
            Weapons(Name).HitsMin = Weapons(Name).Hits
            Weapons(Name).HitsMax = Weapons(Name).Hits
        Else
            Weapons(Name).Hits = 0
            Dim m = Regex.Match(Hits, "random\(\s*(\d+)\s*,\s*-\s*(\d+)\s*\)", RegexOptions.IgnoreCase)
            If m.Success Then
                Weapons(Name).HitsMin = Short.Parse(m.Groups(1).Value)
                Weapons(Name).HitsMax = Short.Parse(m.Groups(2).Value)
                If Weapons(Name).HitsMin > Weapons(Name).HitsMax Then
                    Dim s = Weapons(Name).HitsMin
                    Weapons(Name).HitsMin = Weapons(Name).HitsMax
                    Weapons(Name).HitsMax = s
                End If
            Else
                Weapons(Name).HitsMin = 0
                Weapons(Name).HitsMax = 0
            End If
        End If

        If Match.Groups("Size").Success Then
            If Match.Groups("Size").Value.Equals("small", StringComparison.OrdinalIgnoreCase) Then
                Weapons(Name).Size = Size.Small
            ElseIf Match.Groups("Size").Value.Equals("medium", StringComparison.OrdinalIgnoreCase) Then
                Weapons(Name).Size = Size.Medium
            ElseIf Match.Groups("Size").Value.Equals("large", StringComparison.OrdinalIgnoreCase) Then
                Weapons(Name).Size = Size.Large
            Else
                Weapons(Name).Size = Size.Other
            End If
        End If

        WriteMessage(2, 10, String.Format("Registered weapon from !view-info: {0} ({1})", Weapons(Name).Name, Weapons(Name).Category))
    End Sub

    <ArenaRegex("\[\x034Base Power\x0312 (?<Power>.*)\x031\] \[\x034Cost\x0312 (?<Cost>.*) black orb\(s\)\x031\] \[\x034Element of Weapon\x0312 (?<Element>.*)\x031\]")>
    Public Sub OnViewInfoWeapon2(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        If ViewingInfoWeapon = "" Then Return
        Dim Name As String = ViewingInfoWeapon

        Weapons(Name).Power = Match.Groups("Power").Value
        Weapons(Name).Cost = Match.Groups("Cost").Value
        Weapons(Name).Element = Match.Groups("Element").Value

        WriteMessage(2, 10, String.Format("Registered weapon info for {0} from !view-info  Power: {1}  Cost: {2}  Element: {3}", Name, Weapons(Name).Power, Weapons(Name).Cost, Weapons(Name).Element))
    End Sub

    <ArenaRegex("\[\x034Abilities of the Weapon\x0312 (?<Techniques>[^, ]+(, [^, ]+)*)?\x031\]")>
    Public Sub OnViewInfoWeapon3(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        If ViewingInfoWeapon = "" Then Return
        Dim Name As String = ViewingInfoWeapon

        Weapons(Name).Techniques = New List(Of String)
        For Each Technique In Match.Groups("Techniques").Value.Split({", "}, StringSplitOptions.RemoveEmptyEntries)
            Weapons(Name).Techniques.Add(Technique)
        Next
        Weapons(Name).IsWellKnown = True

        WriteMessage(2, 10, String.Format("Registered technique list for {0} from !view-info: {1}", Name, String.Join(", ", Weapons(Name).Techniques)))
    End Sub

    <ArenaRegex("\[\x034Name\x0312 (?<Name>[^ \]]*)\x031\] \[\x034Target Type\x0312 (?<Type>[^\]]*)\x031\] \[\x034TP needed to use\x0312 (?<TP>\d+)\x031\]( \[\x034# of Hits\x0312 (?<Hits>[^\]]*)\x031\])?( \[\x034Stats Type\x0312 (?<Status>[^\]]*)\x031\])?(?<Magic> \[\x034Magic\x0312 Yes\x031\])?( \[\x034Ignore Target Defense by\x0312 (?<IgnoreDefense>[^\]]*)%\x031\])?")>
    Public Sub OnViewInfoTechnique1(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        Dim Name As String = Match.Groups("Name").Value, Type As String = "None", TP As Short = Match.Groups("TP").Value, Status As String = Match.Groups("Status").Value, IsMagic As Boolean = Match.Groups("Magic").Success
        Select Case Match.Groups("Type").Value.ToLower
            Case "boost" : Type = "Boost"
            Case "finalgetsuga" : Type = "Final Getsuga"
            Case "heal" : Type = "Heal"
            Case "heal-aoe" : Type = "AoE Heal"
            Case "single" : Type = "Attack"
            Case "suicide" : Type = "Suicide"
            Case "suicide-aoe" : Type = "AoE Suicide"
            Case "status" : Type = "Attack"
            Case "stealpower" : Type = "Steal Power"
            Case "aoe" : Type = "AoE Attack"
            Case "buff" : Type = "Buff"
            Case "clearstatusnegative" : Type = "Clear Status Negative"
            Case "clearstatuspositive" : Type = "Clear Status Positive"
        End Select

        If Not Match.Groups("Status").Success Then Status = "None"

        ViewingInfoTechnique = Name

        If Not Techniques.ContainsKey(Name) Then Techniques.Add(Name, New TechniqueData With {.Name = Name})
        Techniques(Name).Type = Type
        Techniques(Name).TP = TP
        Techniques(Name).Status = Status
        Techniques(Name).IsMagic = IsMagic

        If Match.Groups("Hits").Success Then
            Techniques(Name).Hits = Match.Groups("Hits").Value
        Else
            'Legacy response: assume one hit.
            Techniques(Name).Hits = 1
        End If

        'If Techniques(Name).Type = "Buff" Then Techniques(Name).IsWellKnown = True

        WriteMessage(2, 10, String.Format("Registered technique from !view-info: {0} ({1}  TP cost: {2}  Effect: {3}  Magic: {4})", Techniques(Name).Name, Techniques(Name).Type, Techniques(Name).TP, Techniques(Name).Status, If(Techniques(Name).IsMagic, "Yes", "No")))
    End Sub

    <ArenaRegex("\[\x034Base Power\x0312 (?<Power>\d*)\x031\] \[\x034Base Cost \(before Shop Level\)\x0312 (?<Cost>\d+) red orbs\x031\] \[\x034Element of Tech\x0312 (?<Element>[^\]]+)\x031\]")>
    Public Sub OnViewInfoTechnique2(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        If ViewingInfoTechnique = "" Then Return
        Dim Name As String = ViewingInfoTechnique

        Techniques(Name).Power = If(Match.Groups("Power").Value = "", 0, Match.Groups("Power").Value)
        Techniques(Name).Cost = Match.Groups("Cost").Value
        Techniques(Name).Element = Match.Groups("Element").Value
        Techniques(Name).IsWellKnown = True

        WriteMessage(2, 10, String.Format("Registered technique info for {0} from !view-info  Power: {1}  Cost: {2}  Element: {3}", Name, Techniques(Name).Power, Techniques(Name).Cost, Techniques(Name).Element))

        ViewingInfoTechnique = ""
    End Sub

    <ArenaRegex("^\x034\x02(Error:\x02 )?Invalid (weapon|technique|item|skill|ignition)$")>
    Public Sub OnViewInfoInvalid(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        RepeatCommand += 1
    End Sub

    <ArenaRegex("^\x033You analyze (?<Monster>.*) and determine (?<Gender>he|she|it|they) (has|have)\x02 (?<HP>\d*) \x02HP and\x02 (?<TP>\d*) \x02TP left\.$")>
    Public Sub OnAnalysis1(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        If Not EnableAnalysis Then Return

        ' Find the monster.
        Dim mc As CharacterData, mb As Combatant
        For Each Character In BattleList
            If Character.Value.Name = Match.Groups("Monster").Value Then
                mc = Characters(Character.Key)
                mb = BattleList(Character.Key)
            End If
        Next
        If mc Is Nothing Then Return

        ' Record data.
        mb.HP = Match.Groups("HP").Value
        mc.bHP = mb.HP + mb.Damage

        If Match.Groups("TP").Success Then
            mb.TP = Match.Groups("TP").Value
        End If

        Select Case Match.Groups("Gender").Value.ToLower
            Case "he"
                mc.Gender = "Male"
            Case "she"
                mc.Gender = "Female"
            Case "it"
                mc.Gender = "None"
            Case "they"
                mc.Gender = "Unspecified"
        End Select

        DoBattle("$k11A report on $b" & mc.Name & "$b.")
        DoBattle(String.Format("$k12{0} has $b{1}$b HP " & If(Match.Groups("TP").Success, "and $b{2}$b TP ", "") & "left.", mc.GenderPronoun2, mb.HP, mb.TP))
    End Sub

    <ArenaRegex("^\x033You also determine (?<Monster>.*) has the following stats: \[str:\x02 (?<STR>\d*)\x02\] \[def:\x02 (?<DEF>\d*)\x02\] \[int:\x02 (?<INT>\d*)\x02\] \[spd:\x02 (?<SPD>\d*)\x02\]$")>
    Public Sub OnAnalysis2(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        If Not EnableAnalysis Then Return

        ' Find the monster.
        Dim mc As CharacterData, mb As Combatant
        For Each Character In BattleList
            If Character.Value.Name = Match.Groups("Monster").Value Then
                mc = Characters(Character.Key)
                mb = BattleList(Character.Key)
            End If
        Next
        If mc Is Nothing Then Return

        ' Record data.
        mb.STR = Match.Groups("STR").Value
        mb.DEF = Match.Groups("DEF").Value
        mb.INT = Match.Groups("INT").Value
        mb.SPD = Match.Groups("SPD").Value

        mc.bSTR = Match.Groups("STR").Value
        mc.bDEF = Match.Groups("DEF").Value
        mc.bINT = Match.Groups("INT").Value
        mc.bSPD = Match.Groups("SPD").Value

        DoBattle(String.Format("$k12{0} has $b{1}$b strength, $b{2}$b defense, $b{3}$b magical power, $b{4}$b speed.", mc.GenderPronoun2, mb.STR, mb.DEF, mb.INT, mb.SPD))
    End Sub

    <ArenaRegex("^\x033(?<Monster>.*) is also (resistant|strong) against the following weapon types:\x02 (?<RWeapons>[^\x02]*) \x02and is (resistant|strong) against the following elements:\x02 (?<RElements>[^\x02]*)( \x02\| [^\x02]* is weak against the following weapon types:\x02 (?<WWeapons>[^\x02]*) \x02and weak against the following elements:\x02 (?<WElements>[^\x02]*))?$")>
    Public Sub OnAnalysis3(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        If Not EnableAnalysis Then Return

        ' Find the monster.
        Dim mc As CharacterData, mb As Combatant
        For Each Character In BattleList
            If Character.Value.Name = Match.Groups("Monster").Value Then
                mc = Characters(Character.Key)
                mb = BattleList(Character.Key)
            End If
        Next
        If mc Is Nothing Then Return

        ' Record data.
        mc.WeaponResistances = New List(Of String)(If(Match.Groups("RWeapons").Value = "none", {}, Match.Groups("RWeapons").Value.Split({", "}, StringSplitOptions.None)))
        mc.ElementalResistances = New List(Of String)(If(Match.Groups("RElements").Value = "none", {}, Match.Groups("RElements").Value.Split({", "}, StringSplitOptions.None)))

        If Match.Groups("WElements").Success Then
            mc.WeaponWeaknesses = New List(Of String)(If(Match.Groups("WWeapons").Value = "none", {}, Match.Groups("WWeapons").Value.Split({", "}, StringSplitOptions.None)))
            mc.ElementalWeaknesses = New List(Of String)(If(Match.Groups("WElements").Value = "none", {}, Match.Groups("WElements").Value.Split({", "}, StringSplitOptions.None)))
        End If

        If Match.Groups("RWeapons").Value = "none" And Match.Groups("RElements").Value = "none" Then
            DoBattle(String.Format("$k12{0} has no resistances.", mc.GenderPronoun2))
        ElseIf Match.Groups("RWeapons").Value = "none" And Match.Groups("RElements").Value <> "none" Then
            DoBattle(String.Format("$k12{0} is resistant to $b{1}$b.", mc.GenderPronoun2, String.Join(", ", mc.ElementalResistances)))
        ElseIf Match.Groups("RWeapons").Value <> "none" And Match.Groups("RElements").Value = "none" Then
            DoBattle(String.Format("$k12{0} is resistant to $b{1}$b attacks.", mc.GenderPronoun2, String.Join(", ", mc.WeaponResistances)))
        Else
            DoBattle(String.Format("$k12{0} is resistant to $b{1}$b, and also to $b{2}$b attacks.", mc.GenderPronoun2, String.Join(", ", mc.ElementalResistances), String.Join(", ", mc.WeaponResistances)))
        End If

        If Match.Groups("WElements").Success Then
            If Match.Groups("WWeapons").Value = "none" And Match.Groups("WElements").Value = "none" Then
                DoBattle(String.Format("$k12{0} has no weaknesses.", mc.GenderPronoun2))
            ElseIf Match.Groups("WWeapons").Value = "none" And Match.Groups("WElements").Value <> "none" Then
                DoBattle(String.Format("$k12{0} is weak against $b{1}$b.", mc.GenderPronoun2, String.Join(", ", mc.ElementalWeaknesses)))
            ElseIf Match.Groups("WWeapons").Value <> "none" And Match.Groups("WElements").Value = "none" Then
                DoBattle(String.Format("$k12{0} is weak against $b{1}$b attacks.", mc.GenderPronoun2, String.Join(", ", mc.WeaponWeaknesses)))
            Else
                DoBattle(String.Format("$k12{0} is weak against $b{1}$b, and also against $b{2}$b attacks.", mc.GenderPronoun2, String.Join(", ", mc.ElementalWeaknesses), String.Join(", ", mc.WeaponWeaknesses)))
            End If
        End If

        mc.IsWellKnown = True
    End Sub

    <ArenaRegex("^\x033(?<Monster>.*) is completely immune to the following elements:\x02 (?<IElements>.*)$")>
    Public Sub OnAnalysis4(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        If Not EnableAnalysis Then Return

        ' Find the monster.
        Dim mc As CharacterData, mb As Combatant
        For Each Character In BattleList
            If Character.Value.Name = Match.Groups("Monster").Value Then
                mc = Characters(Character.Key)
                mb = BattleList(Character.Key)
            End If
        Next
        If mc Is Nothing Then Return

        ' Record data.
        mc.ElementalImmunities = New List(Of String)(If(Match.Groups("IElements").Value = "none", {}, Match.Groups("IElements").Value.Split(", ")))

        If Match.Groups("IElements").Value <> "none" Then _
            DoBattle(String.Format("$k12{0} is immune to $b{1}$b.", mc.GenderPronoun2, String.Join(", ", mc.ElementalImmunities)))
    End Sub

    <ArenaRegex("^\x033(?<Monster>.*) will( absorb and)? be healed by the following elements:\x02 (?<AElements>.*)$")>
    Public Sub OnAnalysis5(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        If Not EnableAnalysis Then Return

        ' Find the monster.
        Dim mc As CharacterData, mb As Combatant
        For Each Character In BattleList
            If Character.Value.Name = Match.Groups("Monster").Value Then
                mc = Characters(Character.Key)
                mb = BattleList(Character.Key)
            End If
        Next
        If mc Is Nothing Then Return

        ' Record data.
        mc.ElementalAbsorbs = New List(Of String)(If(Match.Groups("AElements").Value = "none", {}, Match.Groups("AElements").Value.Split(", ")))

        If Match.Groups("AElements").Value <> "none" Then _
             DoBattle(String.Format("$k12{0} can absorb $b{1}$b.", mc.GenderPronoun2, String.Join(", ", mc.ElementalAbsorbs)))
    End Sub
#End Region

#End Region

#Region "Battle preparation"

    <ArenaRegex({"^\x034A dimensional portal has been detected\. The enemy force will arrive in (?<Time>\d+(\.\d+)?) (?<TimeUnit>minute(s|\(s\))?|second(s|\(s\))?)\. type \x02!enter\x02 if you wish to join the battle!",
            "^\x034The doors to the \x02gauntlet\x02 are open\. Anyone willing to brave the gauntlet has (?<Time>\d+(\.\d+)?) (?<TimeUnit>minute(s|\(s\))?|second(s|\(s\))?) to enter before the doors close\. Type \x02!enter\x02 if you wish to join the battle!",
            "\x0314\x02The President of the Allied Forces\x02 has been \x02kidnapped by monsters\x02! Are you a bad enough dude to save the president\? \x034The rescue party will depart in (?<Time>\d+(\.\d+)?) (?<TimeUnit>minute(s|\(s\))?|second(s|\(s\))?)\. Type \x02!enter\x02 if you wish to join the battle!",
            "\x034An \x02evil treasure chest Mimic\x02 is ready to fight\S? The battle will begin in (?<Time>\d+(\.\d+)?) (?<TimeUnit>minute(s|\(s\))?|second(s|\(s\))?)\. Type \x02!enter\x02 if you wish to join the battle!"})>
    Public Sub OnBattleOpen(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match, ByRef Handled As Boolean)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        Dim TimeS As Integer
        If Match.Groups("TimeUnit").Value.StartsWith("minute") Then TimeS = Match.Groups("Time").Value * 60
        If Match.Groups("TimeUnit").Value.StartsWith("second") Then TimeS = Match.Groups("Time").Value

        Dim e As New BattleOpenEventArgs(BattleType.Normal, TimeS)
        RaiseEvent BattleOpen(Me, e)
        If e.Cancel Then Return

        WriteMessage(1, 8, "A battle is starting.")

        IsNPCBattle = False
        IsBattleOpen = True ' This is needed for the time command.

        'If Not EnableAnalysis Then Return
        currentPlayers.Clear()
        currentMonsters.Clear()
        currentAllies.Clear()
        BattleList.Clear()
        UnmatchedFullNames.Clear()
        UnmatchedShortNames.Clear()
        'EntryStopwatch = Stopwatch.StartNew()

        TurnNumber = 0

        If EnableParticipation And MinimumPlayersToEnter <= 0 Then
            If Not Characters(LoggedIn).IsReadyToControl Then Return
            ' Let's enter the battle.
            Dim eThread As New Threading.Thread(AddressOf DelayEnter)
            eThread.Start(Rnd() * Math.Min(5000, TimeS * 500) + Math.Min(3000, TimeS * 300))
        End If

        'For Each Player In Controlling
        '    Threading.Thread.Sleep(600)
        '    Say(Connection, Channel, Player & " enters the battle.")
        'Next
    End Sub

    <ArenaRegex("\x034A \x021 vs 1 AI Match\x02 is about to begin! The battle will begin in 30 seconds.")>
    Public Sub OnBattleOpenNPC(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match, ByRef Handled As Boolean)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        Dim TimeS As Integer
        If Match.Groups("TimeUnit").Value.StartsWith("minute") Then TimeS = Match.Groups("Time").Value * 60
        If Match.Groups("TimeUnit").Value.StartsWith("second") Then TimeS = Match.Groups("Time").Value

        If CallEvent(MyKey, "BattleOpen", {{"time", TimeS}}) Then Return

        WriteMessage(1, 8, "A battle is starting.")

        IsNPCBattle = True
        IsBattleOpen = True ' This is needed for the time command.

        'If Not EnableAnalysis Then Return
        currentPlayers.Clear()
        currentMonsters.Clear()
        currentAllies.Clear()
        BattleList.Clear()
        UnmatchedFullNames.Clear()
        UnmatchedShortNames.Clear()
        TurnNumber = 0
    End Sub

    <ArenaRegex("^\x032The betting period is now\x02 open$")>
    Public Sub OnBettingPeriodOpen(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match, ByRef Handled As Boolean)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return

        WriteMessage(1, 8, "Betting is now open.", Match.Value)

        If Not EnableGambling Then Return
        If Characters(LoggedIn).DoubleDollars < 10 Then Return

        ' Place a bet based on the ratings.
        Dim cAlly As CharacterData, cMonster As CharacterData
        For Each Combatant In BattleList
            If Combatant.Value.Category = CharacterCategory.Ally Then
                cAlly = Characters(Combatant.Key)
            Else
                cMonster = Characters(Combatant.Key)
            End If
        Next

        WriteMessage(1, 12, String.Format("Combatants:  {0} [{1}]  vs.  {2} [{3}]", cAlly.Name, cAlly.Rating, cMonster.Name, cMonster.Rating), Match.Value)

        Dim RatingDifference = cAlly.Rating - cMonster.Rating
        Threading.Thread.Sleep(5000 + Rnd() * 10000)

        If RatingDifference >= 0 Then
            BetAmount = 10
            BetOnAlly = True
            WriteMessage(1, 12, "Betting $$10 on " & cAlly.Name & ".", Match.Value)
            Say(ArenaConnection, ArenaNickname, "!bet NPC 10", SayOptions.NoticeNever)
        Else
            BetAmount = 10
            BetOnAlly = False
            WriteMessage(1, 12, "Betting $$10 on " & cMonster.Name & ".", Match.Value)
            Say(ArenaConnection, ArenaNickname, "!bet monster 10", SayOptions.NoticeNever)
        End If
    End Sub

    <ArenaRegex("^\x032The betting period is now\x02 closed$")>
    Public Sub OnBettingPeriodClose(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match, ByRef Handled As Boolean)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        WriteMessage(1, 8, "Betting is now closed.", Match.Value)
    End Sub

    Public Sub DelayEnter(ByVal Delay As Integer)
        Entering = True
        Threading.Thread.Sleep(Delay)
        DoBattle("!enter")
    End Sub

    <ArenaRegex("^!enter")>
    Public Sub OnPlayerEntry(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match, ByRef Handled As Boolean)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        If Not EnableAnalysis Then Return
        PendingPlayers.Add(Sender.Split("!"c)(0), Now)
        'If Not UnmatchedShortNames.Contains("Player/" & Sender.Split("!")(0)) And Not currentPlayers.Contains(Sender.Split("!")(0)) Then _
        '    UnmatchedShortNames.Add("Player/" & Sender.Split("!")(0))
    End Sub

    <ArenaRegex("^!summon (monster|boss) (?<Name>[^ ]*)")>
    Public Sub OnMonsterSummon(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match, ByRef Handled As Boolean)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        If Not EnableAnalysis Then Return
        '    If Not UnmatchedShortNames.Contains("Monster/" & Sender.Split("!")(0)) And Not currentMonsters.Contains(Sender.Split("!")(0)) Then _
        '        UnmatchedShortNames.Add("Monster/" & Sender.Split("!")(0))
    End Sub

    <ArenaRegex("^!summon npc (?<Name>[^ ]*)")>
    Public Sub OnAllySummon(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match, ByRef Handled As Boolean)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        If Not EnableAnalysis Then Return
        '    If Not UnmatchedShortNames.Contains("Ally/" & Sender.Split("!")(0)) And Not currentAllies.Contains(Sender.Split("!")(0)) Then _
        '        UnmatchedShortNames.Add("Ally/" & Sender.Split("!")(0))
    End Sub

    <ArenaRegex({"^\x034\x02(?<Name>[^\x02]*) \x02has entered the battle!$",
            "^\x02\x034(?<Name>[^\x02]*) \x02has entered the battle(?<IsAlly> to help the forces of good)?!$"})>
    Public Sub OnEntry(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match, ByRef Handled As Boolean)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        ' Someone has entered the battle.
        If Not EnableAnalysis Then Return
        ' See if we're already familiar with this entrant.
        Dim Character As CharacterData, Found As String, Category As CharacterCategory = 7
        For Each item In Characters
            If item.Value.Name = Match.Groups("Name").Value And Not BattleList.ContainsKey(item.Key) And Not SlainCharacters.Contains(item.Key) And Not item.Key.StartsWith("*") AndAlso
               Not ((item.Value.Category And CharacterCategory.Ally) = 0 And Match.Groups("IsAlly").Success) AndAlso
               Not (Not IsBattleOpen And Not Match.Groups("IsAlly").Success AndAlso (item.Value.Category And CharacterCategory.Monster) = 0) Then
                If Found IsNot Nothing Then Found = "." Else Found = item.Key
                Category = Category And item.Value.Category
            End If
        Next
        If Found Is Nothing Then
            ' This is a new combatant, so we should register them now.
            EnteredNewCharacter(Match.Groups("Name").Value, Nothing, Match.Groups("IsAlly").Success)
        ElseIf Found = "." Then
            If Not IsBattleOpen Then
                If Match.Groups("IsAlly").Success Then Category = CharacterCategory.Ally Else Category = CharacterCategory.Monster
            End If
            ' Multiple matching characters were found (e.g. Alucard).
            UnmatchedFullNames.Add(New UnmatchedName With {.Name = "." & Match.Groups("Name").Value, .Category = Category})
            SelfEntryCheck()
        Else
            Character = Characters(Found)
            Entered(Character)
            If UnmatchedShortNames.Count <> 0 Then MatchNames()
            If WaitingForRegistration IsNot Nothing Then AITurn()
            SelfEntryCheck()
        End If
    End Sub

    <ArenaRegex({"^\x0312\x02(?<Name>[^\x02]*) \x02(?<Description>.*)$"})>
    Public Sub OnEntryDescription(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match, ByRef Handled As Boolean)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return

        If CurrentTurn Is Nothing Then  ' Otherwise it's probably a skill description.
            ' See if this is a known or unmatched character with no known description.
            Dim UnknownDescription As Boolean = True
            Dim _Character As CharacterData, _UnmatchedName As UnmatchedName
            For Each Character In Characters
                If Character.Value.Name = Match.Groups("Name").Value Then
                    If Character.Value.Description IsNot Nothing Then
                        UnknownDescription = False
                    Else
                        _Character = Character.Value
                    End If
                    Exit For
                End If
            Next
            If UnknownDescription Then
                For Each Entry In UnmatchedFullNames
                    If Entry.Name = Match.Groups("Name").Value Then
                        If Entry.Description IsNot Nothing Then
                            UnknownDescription = False
                        Else
                            _UnmatchedName = Entry
                        End If
                        Exit For
                    End If
                Next
            End If
            If UnknownDescription Then
                If _Character IsNot Nothing Then
                    _Character.Description = Match.Groups("Description").Value
                ElseIf _UnmatchedName IsNot Nothing Then
                    _UnmatchedName.Description = Match.Groups("Description").Value
                End If
            End If

            ' See if this is one of the ambiguous names.
            For i = 0 To UnmatchedFullNames.Count - 1
                Dim Entry = UnmatchedFullNames(i)
                If Entry.Name = "." & Match.Groups("Name").Value And Entry.Description = Nothing Then
                    Dim Found As String
                    For Each Character In Characters
                        If Character.Value.Name = Match.Groups("Name").Value AndAlso (Character.Value.Description Is Nothing OrElse Character.Value.Description = Match.Groups("Description").Value) Then
                            If Found Is Nothing Then : Found = Character.Key
                            Else
                                Found = "."
                                Exit For
                            End If
                        End If
                    Next

                    If Found Is Nothing Then
                        UnmatchedFullNames.RemoveAt(i)
                        EnteredNewCharacter(Match.Groups("Name").Value, Match.Groups("Description").Value, False)
                    ElseIf Found = "." Then
                        Entry.Description = Match.Groups("Description").Value
                    Else
                        Characters(Found).Description = Match.Groups("Description").Value
                        UnmatchedFullNames.RemoveAt(i)
                        Entered(Characters(Found))
                    End If
                End If
            Next

            ' If there are multiple characters with the same name, but different descriptions, and the bot has only seen one of them, it may think it's the wrong character.
            ' When we see the other character's description, correct it.
            For i = BattleList.Count - 1 To 0 Step -1
                Dim Entry = BattleList.Values(i)
                If Entry.Name = Match.Groups("Name").Value Then
                    If Characters(Entry.ShortName).Description IsNot Nothing AndAlso Characters(Entry.ShortName).Description <> Match.Groups("Description").Value Then
                        BattleList.Remove(Entry.ShortName)
                        currentPlayers.Remove(Entry.ShortName)
                        currentMonsters.Remove(Entry.ShortName)
                        currentAllies.Remove(Entry.ShortName)
                        UnmatchedFullNames.Add(New UnmatchedName With {.Name = Match.Groups("Name").Value, .Description = Match.Groups("Description").Value, .Category = 7})
                    End If
                End If
            Next
        End If
    End Sub

    Private Sub Entered(Character As CharacterData)
        WriteMessage(1, 12, Character.Name & " (" & Character.ShortName & ") enters the battle.")
        Select Case Character.Category
            Case CharacterCategory.Player
                currentPlayers.Add(Character.ShortName)
            Case CharacterCategory.Monster
                currentMonsters.Add(Character.ShortName)
            Case CharacterCategory.Ally
                currentAllies.Add(Character.ShortName)
        End Select

        Dim newCombatant As Combatant
        newCombatant = New Combatant(Character)
        BattleList.Add(Character.ShortName, newCombatant)

        PendingPlayers.Remove(Character.ShortName)
        PendingMonsters.Remove(Character.ShortName)
        PendingAllies.Remove(Character.ShortName)
    End Sub

    Private Sub EnteredNewCharacter(FullName As String, Description As String, IsAlly As Boolean)
        If IsBattleOpen Then
            ' The entry phase is still open. This entrant can be a player, monster or ally.
            ' See if it's a player who just used !enter.
            WriteMessage(1, 12, FullName & " (?) enters the battle.")
            If PendingPlayers.ContainsKey(FullName) Then
                Dim newCharacter As CharacterData
                If Characters.ContainsKey("*" & FullName) Then
                    newCharacter = Characters("*" & FullName)
                    newCharacter.ShortName = FullName
                    Characters.Remove("*" & FullName)
                    WriteMessage(2, 9, "Reregistering *" & FullName & ".")
                Else
                    newCharacter = New CharacterData With {.Name = FullName, .ShortName = FullName, .Category = CharacterCategory.Player, .Description = Description}
                End If
                Characters.Add(FullName, newCharacter)
                WriteMessage(2, 9, "Registered " & FullName & " to " & FullName & ".")
                PendingPlayers.Remove(newCharacter.ShortName)
                currentPlayers.Add(newCharacter.ShortName)

                Dim newCombatant As Combatant
                'If newCharacter.Name = Nick(Connection) Then
                '    newCombatant = New Combatant With {.Category = "Player", .HP = OwnHP, .Name = newCharacter.Name, .ShortName = newCharacter.Name, .TP = OwnTP}
                'Else
                newCombatant = New Combatant(newCharacter)
                'End If
                BattleList.Add(newCharacter.Name, newCombatant)
            Else
                UnmatchedFullNames.Add(New UnmatchedName With {.Name = FullName, .Category = -1, .Description = Description})
                'ListRequestTimer = New Timers.Timer(5000) With {.AutoReset = False, .Enabled = True}
            End If
        Else
            ' The entry phase is closed. Players can no longer enter, and monsters and allies can no longer be summoned.
            If IsAlly Then
                ' It's an ally.
                WriteMessage(1, 12, "An ally " & FullName & " (?) enters the battle.")
                UnmatchedFullNames.Add(New UnmatchedName With {.Name = FullName, .Category = CharacterCategory.Ally, .Description = Description})
            Else
                ' It's a monster.
                WriteMessage(1, 12, "An enemy " & FullName & " (?) enters the battle.")
                UnmatchedFullNames.Add(New UnmatchedName With {.Name = FullName, .Category = CharacterCategory.Monster, .Description = Description})
                number_of_monsters_needed += 1
            End If
        End If

    End Sub

    Public Sub SelfEntryCheck()
        If Not IsBattleOpen Then Return
        If Not EnableParticipation Then Return
        If Not Entering Then
            Dim PlayersEntered As Integer = 0
            For Each Combatant In BattleList
                If (Combatant.Value.Category And CharacterCategory.Player) <> 0 AndAlso (Combatant.Value.Category = CharacterCategory.Player OrElse ArenaConnection.Channels(ArenaChannel).Users.ContainsKey(Combatant.Key)) Then PlayersEntered += 1
            Next
            If PlayersEntered >= MinimumPlayersToEnter Then
                If Not Characters(LoggedIn).IsReadyToControl Then Return
                ' Let's enter the battle.
                Dim eThread As New Threading.Thread(AddressOf DelayEnter)
                eThread.Start(Rnd() * 3000 + 1500)
            End If
        End If
    End Sub

    <ArenaRegex("^\x032You place a (?<Amount>\d*) double dollar bet on\x02 (?<Target>(?>.*))$")>
    Public Sub OnBetPlaced(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match, ByRef Handled As Boolean)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        Characters(LoggedIn).DoubleDollars -= Match.Groups("Amount").Value
    End Sub

    Private Sub RequestList(ByVal sender As Object, ByVal e As Timers.ElapsedEventArgs) Handles ListRequestTimer.Elapsed
        ' Before the battle starts, we request the list of combatants. This is used mainly to match short names with long names.
        SayToAllChannels("!bat info", SayOptions.NoticeNever)
    End Sub

    <ArenaRegex("^\x032\x02(?<Name>.*) \x02looks at the heroes and says ""(?<Quote>.*)""$")>
    Public Sub OnBossQuote(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match, ByRef Handled As Boolean)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        ' It's a boss! Boss battles award black orbs and rare items, and also get a different time limit.
        IsBossBattle = True
    End Sub

    <ArenaRegex("^\x0310\x02The\x02 weather changes. It is now\x02 (?<Weather>(?>.*))$")>
    Public Sub OnWeather(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match, ByRef Handled As Boolean)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        'SerialPort.Write("WE" & Match.Groups("Weather").Value)

        Handled = True
        ' This is the first message that the Arena sends after the entry period closes.
        IsBattleOpen = False

        Weather = Match.Groups("Weather").Value

        ' Let DCC users know that the battle has started.
        If dccSocket IsNot Nothing AndAlso dccSocket.Connected Then DCCSend(ChrW(3) & "11" & Chr(2) & "The battle has started.", Nothing)

        PendingPlayers.Clear()
        PendingMonsters.Clear()
        PendingAllies.Clear()
    End Sub

    '\x034\[Turn #:\x0312\x02 (?<Turn>\d*)\x02\x034\] ?\[Weather:\x0312\x02 (?<Weather>[^\x03]*)\x034\x02\] (\[Moon Phase:\x0312\x02 (?<Moon>[^\x03]*)\x034\x02\] )?\[Battlefield:\x0312\x02 (?<Battlefield>[^\x03]*)\x02\x034\]

    <ArenaRegex("^\x034\[Darkness will occur in:\x0312\x02 (?<Time>\d*) ?\x02\x034turns\]$")>
    Public Sub OnDarknessTime(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match, ByRef Handled As Boolean)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        TurnsToDarkness = Match.Groups("Time").Value
        DarknessHasRisen = False
    End Sub
    <ArenaRegex("^\x034\[Darkness\x02\x0312 has overcome \x02\x034the battlefield\]$")>
    Public Sub OnDarknessOvercome(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match, ByRef Handled As Boolean)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        TurnsToDarkness = -1
        DarknessHasRisen = True
    End Sub

    '<ArenaRegex(".[\[\(<\|].* (order|sequence): (?<List>((\x03(12|5|3|4).*,)*(\x03(12|5|3|4).*))?)(\x03.*)[\]\)>\|]")>
    <ArenaRegex("^\x034\[Battle Order: (?<List>((\x03(12|5|3|4).*,)*(\x03(12|5|3|4).*))?)\x034\]$")>
    Public Sub OnBattleList(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match, ByRef Handled As Boolean)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        ' We've received a list of who's in the battle.
        Handled = True
        ' Check that there are monsters in the battle. If there are no monsters in the battle, it cannot continue.
        If IsBattleStarted And NoMonsterFix And Not IsPVPBattle And Not Match.Groups("List").Value.Contains(Chr(3) & "5") Then
            DoBattle("There are no monsters on the battlefield.")
            DoBattlePrivate("!end battle victory")
            Return
        End If

        Dim Entries = Match.Groups("List").Value.Split(", ")
        If Not EnableAnalysis Then
            ' If analysis is off, don't try to match names. Instead, just read the list.
            For Each Entry In Entries
                Dim Key = IRCConnection.RemoveCodes(Entry)
                If Entry.Trim.StartsWith(Chr(3) & "3") Then
                    BattleList.Add(Key, New Combatant With {.Category = CharacterCategory.Player, .HP = -1, .ShortName = Key})
                ElseIf Entry.Trim.StartsWith(Chr(3) & "5") Then
                    BattleList.Add(Key, New Combatant With {.Category = CharacterCategory.Monster, .HP = -1, .ShortName = Key})
                ElseIf Entry.Trim.StartsWith(Chr(3) & "12") Then
                    BattleList.Add(Key, New Combatant With {.Category = CharacterCategory.Ally, .HP = -1, .ShortName = Key})
                End If
            Next
            Return
        End If

        ' Check for characters that aren't listed.
        For i = BattleList.Count - 1 To 0 Step -1
            Dim Entry = BattleList.Values(i)
            Dim Found As Boolean = False
            For Each Entry2 In Entries
                If IRCConnection.RemoveCodes(Entry2.Trim) = Entry.ShortName Then
                    Found = True
                    Exit For
                End If
            Next
            If Not Found Then
                BattleList.Remove(Entry.ShortName)
                currentPlayers.Remove(Entry.ShortName)
                currentMonsters.Remove(Entry.ShortName)
                currentAllies.Remove(Entry.ShortName)
                UnmatchedFullNames.Add(New UnmatchedName With {.Name = Entry.Name, .Category = -1})
            End If
        Next

        If IsBattleOpen Then
            ' In this case, the combatants are listed in the order they entered. We can deduce who's who from this.
            For Each Entry In Entries
                Dim Key = IRCConnection.RemoveCodes(Entry)
                ' Skip it if they're already registered.
                If currentPlayers.Contains(Key) Then Continue For
                If currentMonsters.Contains(Key) Then Continue For
                If currentAllies.Contains(Key) Then Continue For

                ' Pick the first name from the list of unmatched names, and match it with this entry.
                Dim FullName = UnmatchedFullNames(0).Name.TrimStart({"."c})
                Dim newCharacter As CharacterData
                UnmatchedFullNames.RemoveAt(0)
                If Characters.ContainsKey("*" & FullName) Then
                    newCharacter = Characters("*" & FullName)
                    newCharacter.ShortName = Key
                    Characters.Remove("*" & FullName)
                    WriteMessage(2, 9, "Reregistering *" & FullName)
                Else
                    newCharacter = New CharacterData With {.Name = FullName, .ShortName = Key,
                                                               .IsUndead = (.Name.ToLower.Contains("undead") Or .Name.ToLower.Contains("ghost") Or .Name.ToLower.Contains("zombie")), .IsElemental = .Name.ToLower.Contains("elemental")}
                End If
                If Entry.Trim.StartsWith(Chr(3) & "5") Then
                    newCharacter.Category = CharacterCategory.Monster
                    currentMonsters.Add(Key)
                ElseIf Entry.Trim.StartsWith(Chr(3) & "3") Then
                    newCharacter.Category = CharacterCategory.Player
                    currentPlayers.Add(Key)
                ElseIf Entry.Trim.StartsWith(Chr(3) & "12") Then
                    newCharacter.Category = CharacterCategory.Ally
                    currentAllies.Add(Key)
                End If

                Characters.Add(Key, newCharacter)
                WriteMessage(2, 9, "Registered " & FullName & " to " & Key)
                RegisterEnteredCharacter(newCharacter)
            Next
            ListRequestTimer.Stop()
        ElseIf Not IsBattleStarted Or UnmatchedFullNames.Count > 0 Then
            ' Try to deduce who's who by matching the short names to the full names.
            For Each Entry In Entries
                Dim tEntry = Entry.Trim
                Dim ShortName = IRCConnection.RemoveCodes(Entry)

                ' Skip it if they're already registered.
                If currentPlayers.Contains(ShortName) Then Continue For
                If currentMonsters.Contains(ShortName) Then Continue For
                If currentAllies.Contains(ShortName) Then Continue For

                If tEntry.StartsWith(Chr(3) & "5") Then
                    UnmatchedShortNames.Add(New UnmatchedName With {.Name = ShortName, .Category = CharacterCategory.Monster})
                ElseIf tEntry.StartsWith(Chr(3) & "3") Then
                    UnmatchedShortNames.Add(New UnmatchedName With {.Name = ShortName, .Category = CharacterCategory.Player})
                ElseIf tEntry.StartsWith(Chr(3) & "12") Then
                    UnmatchedShortNames.Add(New UnmatchedName With {.Name = ShortName, .Category = CharacterCategory.Ally})
                Else  ' This shouldn't ever happen, but anyway...
                    UnmatchedShortNames.Add(New UnmatchedName With {.Name = ShortName, .Category = -1})
                End If
            Next

            MatchNames()
        End If

        Threading.Thread.Sleep(600)
        If WaitingForRegistration IsNot Nothing Then AITurn()
    End Sub

    <ArenaRegex("^\x0312AI Battle information: \[NPC\]\x02 (?<Ally>.*?) \x02vs \[Monster\]\x02 (?<Monster>.*?) \x02on \[Streak Level\]\x02 (?<Level>\d*) \x02\[Favorite to Win\]\x034\x02 ((?<AllyFavourite>\k<Ally>)|(?<MonsterFavourite>\k<Monster>)|(?>.*))$")>
    Public Sub OnNPCBattleInfo(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match, ByRef Handled As Boolean)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        IsBattleOpen = False

        If Match.Groups("Ally").Value = Match.Groups("Monster").Value Then
            IsBattleOpen = False
            IsBattleStarted = False
            Return
        End If

        Dim cAlly As CharacterData, bAlly As Combatant, nAlly As String
        Dim cMonster As CharacterData, bMonster As Combatant, nMonster As String
        ' Check the Ally.
        ' Do we know them?
        For Each Character In Characters
            If Character.Value.Name = Match.Groups("Ally").Value Then
                cAlly = Character.Value
                nAlly = Character.Key
                Exit For
            End If
        Next
        If cAlly Is Nothing Then
            ' Register a new character.
            cAlly = New CharacterData With {.Name = Match.Groups("Ally").Value, .ShortName = "*" & .Name, .Category = CharacterCategory.Ally}
            nAlly = cAlly.ShortName
            Characters.Add(nAlly, cAlly)
        End If
        ' Check the monster.
        For Each Character In Characters
            If Character.Value.Name = Match.Groups("Monster").Value Then
                cMonster = Character.Value
                nMonster = Character.Key
                Exit For
            End If
        Next
        If cMonster Is Nothing Then
            ' Register a new character.
            cMonster = New CharacterData With {.Name = Match.Groups("Monster").Value, .ShortName = "*" & .Name, .Category = CharacterCategory.Monster}
            nMonster = cMonster.ShortName
            Characters.Add(nMonster, cMonster)
        End If

        bAlly = New Combatant(cAlly)
        bMonster = New Combatant(cMonster)
        BattleList.Add(bAlly.ShortName, bAlly)
        BattleList.Add(bMonster.ShortName, bMonster)

        ' Let's award a few rating points to the favourite.
        If Match.Groups("AllyFavourite").Success Then
            cAlly.Rating += 50
        Else
            cMonster.Rating += 50
        End If
    End Sub

    <ArenaRegex("^\x0312\[(?<Side>NPC|Monster)\]\x02 (?<Name>.*?) \x02Information \[Number of Techs:\x02 (?<Techniques>\d*)\x02\] \[Has an Ignition:\x02 (?<Ignition>yes|no)\x02\] \[Has a Mech:\x02 (?<Mech>yes|no)\x02\]$")>
    Public Sub OnNPCInfo(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match, ByRef Handled As Boolean)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return

        Dim Character As CharacterData
        For Each c In Characters.Values
            If c.Name = Match.Groups("Name").Value Then
                Character = c
                Exit For
            End If
        Next
        If Character Is Nothing Then Return

        Character.NumberOfTechniques = Match.Groups("Techniques").Value
        Character.HasIgnition = Match.Groups("Ignition").Value = "yes"
        Character.HasMech = Match.Groups("Mech").Value = "yes"
    End Sub

    <ArenaRegex("^\x034\[Total Betting Amount:\x0312\x02 \$\$(?<TotalBet>(?>\d*))\x02\x034\] \[Odds:\x0312\x02 (?<OddsAlly>(?>[0-9.]*)):(?<OddsMonster>(?>[0-9.]*))\x02\x034\]$")>
    Public Sub OnNPCBattleOdds(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match, ByRef Handled As Boolean)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        If TotalBetAmount <> 0 Then Return
        TotalBetAmount = Match.Groups("TotalBet").Value

        For Each Combatant In BattleList
            If Combatant.Value.Category = CharacterCategory.Ally Then
                Combatant.Value.Odds = Match.Groups("OddsAlly").Value
                Continue For
            ElseIf Combatant.Value.Category = CharacterCategory.Ally Then
                Combatant.Value.Odds = Match.Groups("OddsMonster").Value
                Continue For
            End If
        Next
    End Sub

    <ArenaRegex("^\x0310-=BATTLE LIST=-$")>
    Public Sub OnBattleListLegacyHeader(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match, ByRef Handled As Boolean)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        ' Prepare for the battle list. This is for compatibility with older Battle Arena bots.
        WaitingForBattleList = True
        IsPVPBattle = False
    End Sub

    Private lBattleListLegacyAliveColour As String
    Private lBattleListLegacyAliveColourLock As New Object
    Public ReadOnly Property BattleListLegacyAliveColour As String
        Get
            SyncLock lBattleListLegacyAliveColourLock
                Return lBattleListLegacyAliveColour
            End SyncLock
        End Get
    End Property

    ''' <summary>Handles the old-format battle list.</summary>
    ''' <param name="Message">The message text containing the battle list.</param>
    Public Sub OnBattleListLegacy(Message As String)
        ' We've received a list of who's in the battle.
        Message = Message.Replace("Â ", " ").Trim
        Dim Entries = Message.Split({", ", "," & ChrW(160), ","}, System.StringSplitOptions.None)

        If Not EnableAnalysis Then
            ' If analysis is off, don't try to match names. Instead, just read the list.
            For Each Entry In Entries
                Dim Key = IRCConnection.RemoveCodes(Entry).Trim
                BattleList.Add(Key, New Combatant With {.HP = -1, .ShortName = Key})
            Next
            Return
        End If

        ' Check for characters that aren't listed.
        For i = BattleList.Count - 1 To 0 Step -1
            Dim Entry = BattleList.Values(i)
            Dim Found As Boolean = False
            For Each Entry2 In Entries
                If IRCConnection.RemoveCodes(Entry2.Trim) = Entry.ShortName Then
                    Found = True
                    Exit For
                End If
            Next
            If Not Found Then
                BattleList.Remove(Entry.ShortName)
                currentPlayers.Remove(Entry.ShortName)
                currentMonsters.Remove(Entry.ShortName)
                currentAllies.Remove(Entry.ShortName)
                UnmatchedFullNames.Add(New UnmatchedName With {.Name = Entry.Name, .Category = -1})
            End If
        Next

        If Not IsBattleStarted Then
            ' Every character will be alive before the battle starts. Use that to find out the colour codes.
            SyncLock lBattleListLegacyAliveColourLock
                lBattleListLegacyAliveColour = Message.Substring(0, 2)
                If Char.IsDigit(Message(2)) Then lBattleListLegacyAliveColour &= Message(2)
            End SyncLock
        End If

        If IsBattleOpen Then
            ' In this case, the combatants are listed in the order they entered. We can deduce who's who from this.
            For Each Entry In Entries
                Dim tEntry = Entry.Trim
                Dim rEntry = IRCConnection.RemoveCodes(tEntry)
                ' Skip it if they're already registered.
                If currentPlayers.Contains(rEntry) Then Continue For
                If currentMonsters.Contains(rEntry) Then Continue For
                If currentAllies.Contains(rEntry) Then Continue For

                ' Pick the first name from the list of unmatched names, and match it with this entry.
                Dim FullName = UnmatchedFullNames(0).Name
                UnmatchedFullNames.RemoveAt(0)
                Dim newCharacter = New CharacterData With {.Name = FullName, .ShortName = rEntry,
                                                           .IsUndead = (.Name.ToLower.Contains("undead") Or .Name.ToLower.Contains("ghost") Or .Name.ToLower.Contains("zombie")), .IsElemental = .Name.ToLower.Contains("elemental")}
                'If Entry.Trim.StartsWith(Chr(3) & "5") Then
                '    newCharacter.Category = "Monster"
                '    currentMonsters.Add(rEntry)
                'ElseIf Entry.Trim.StartsWith(Chr(3) & "3") Then
                '    newCharacter.Category = "Player"
                '    currentPlayers.Add(rEntry)
                'ElseIf Entry.Trim.StartsWith(Chr(3) & "12") Then
                '    newCharacter.Category = "Ally"
                '    currentAllies.Add(rEntry)
                'End If

                Characters.Add(rEntry, newCharacter)
                WriteMessage(2, 9, "Registered " & FullName & " to " & rEntry)
                RegisterEnteredCharacter(newCharacter)
            Next
            ListRequestTimer.Stop()
        ElseIf Not IsBattleStarted Or UnmatchedFullNames.Count > 0 Then
            ' Try to deduce who's who by matching the short names to the full names.
            For Each Entry In Entries
                Dim tEntry = Entry.Trim
                Dim rEntry = IRCConnection.RemoveCodes(tEntry)
                UnmatchedShortNames.Add(New UnmatchedName With {.Category = If(rEntry = Nick(ArenaConnection), CharacterCategory.Player, -1), .Name = rEntry})
            Next
            MatchNames()
        End If

        Threading.Thread.Sleep(600)
        If WaitingForRegistration IsNot Nothing Then AITurn()
    End Sub

    Public Sub MatchNames()
        ' Match the short names with the full names.

        ' Resolve ambiguous names.
        For i = UnmatchedFullNames.Count - 1 To 0 Step -1
            Dim Entry = UnmatchedFullNames(i)
            If Entry.Name.StartsWith(".") Then
                For j = UnmatchedShortNames.Count - 1 To 0 Step -1
                    Dim Entry2 = UnmatchedShortNames(j)
                    If Characters(Entry2.Name).Name = Entry.Name.Trim({"."c}) Then
                        UnmatchedFullNames.RemoveAt(i)
                        UnmatchedShortNames.RemoveAt(j)
                        Entered(Characters(Entry2.Name))
                        WriteMessage(2, 9, "Registered " & Characters(Entry2.Name).Name & " to " & Entry.Name.Trim({"."c}) & ".")
                    End If
                Next
            End If
        Next

        ' We'll compare each full name with each short name, and go with the pairs that match best.
        Dim Matches As New List(Of Object())
        For i = UnmatchedFullNames.Count - 1 To 0 Step -1
            Dim FullName = UnmatchedFullNames(i)

            Dim PossibleMatches As New List(Of Object())
            For Each ShortName In UnmatchedShortNames
                If (ShortName.Category And FullName.Category) = 0 Then Continue For
                Dim currentMatchScore As Single = NameMatch(ShortName.Name, FullName.Name) / (FullName.Name.Length * 2)
                WriteMessage(3, 3, String.Format("{0,-16} < {1,-20} : {2,6:0.00} %", ShortName.Name, FullName.Name, currentMatchScore * 100))
                PossibleMatches.Add({ShortName, FullName, currentMatchScore})
            Next

            If PossibleMatches.Count = 1 And UnmatchedFullNames.Count = UnmatchedShortNames.Count Then
                ' This full name only matches with one short name. This often happens with allies, because there's usually only one of them, and so only one blue listing in any given battle.
                Dim mShortName = DirectCast(PossibleMatches(0)(0), UnmatchedName)
                Dim mFullName = DirectCast(PossibleMatches(0)(1), UnmatchedName)
                Dim newCharacter As CharacterData
                If Characters.ContainsKey("*" & mFullName.Name) Then
                    newCharacter = Characters("*" & mFullName.Name)
                    newCharacter.ShortName = mShortName.Name
                    Characters.Remove("*" & mFullName.Name)
                    WriteMessage(2, 9, "Reregistering *" & mFullName.Name & ".")
                Else
                    newCharacter = New CharacterData With {.Name = mFullName.Name, .ShortName = mShortName.Name, .Description = mFullName.Description,
                                                               .IsUndead = (.Name.ToLower.Contains("undead") Or .Name.ToLower.Contains("ghost") Or .Name.ToLower.Contains("zombie")), .IsElemental = .Name.ToLower.Contains("elemental")}
                End If
                Select Case mShortName.Category
                    Case CharacterCategory.Monster
                        newCharacter.Category = CharacterCategory.Monster
                        currentMonsters.Add(mShortName.Name)
                    Case CharacterCategory.Player
                        newCharacter.Category = CharacterCategory.Player
                        currentPlayers.Add(mShortName.Name)
                    Case CharacterCategory.Ally
                        newCharacter.Category = CharacterCategory.Ally
                        currentAllies.Add(mShortName.Name)
                End Select
                Characters.Add(mShortName.Name, newCharacter)
                RegisterEnteredCharacter(newCharacter)
                UnmatchedFullNames.RemoveAt(i)
                UnmatchedShortNames.Remove(mShortName)
                WriteMessage(2, 9, "Registered " & newCharacter.Name & " to " & newCharacter.ShortName & ".")
                ' Remove this character's from the list of already considered matches.
                For j = Matches.Count - 1 To 0 Step -1
                    If DirectCast(Matches(j)(1), UnmatchedName).Name = FullName.Name Then Matches.RemoveAt(j)
                Next
            Else
                ' There's more than one short name that matches this full name.
                Matches.AddRange(PossibleMatches.ToArray)
            End If
        Next

        Do While Matches.Count > 0 And UnmatchedFullNames.Count > 0 And UnmatchedShortNames.Count > 0
            ' We now take the best match we found and register it.
            Dim BestMatch As Object() = Nothing
            For Each lMatch In Matches
                If BestMatch Is Nothing OrElse CSng(lMatch(2)) > CSng(BestMatch(2)) Then
                    BestMatch = lMatch
                End If
            Next

            Dim ShortName As UnmatchedName = BestMatch(0)
            Dim FullName As UnmatchedName = BestMatch(1)
            Matches.Remove(BestMatch)

            If Not UnmatchedFullNames.Contains(FullName) Or Characters.ContainsKey(ShortName.Name) Then Continue Do

            UnmatchedFullNames.Remove(FullName)
            UnmatchedShortNames.Remove(ShortName)

            Dim newCharacter As CharacterData
            If Characters.ContainsKey("*" & FullName.Name) Then
                newCharacter = Characters("*" & FullName.Name)
                newCharacter.ShortName = ShortName.Name
                Characters.Remove("*" & FullName.Name)
                WriteMessage(2, 9, "Reregistering *" & FullName.Name)
            Else
                newCharacter = New CharacterData With {.Name = FullName.Name, .ShortName = ShortName.Name, .Description = FullName.Description,
                                                                           .IsUndead = (.Name.ToLower.Contains("undead") Or .Name.ToLower.Contains("ghost") Or .Name.ToLower.Contains("zombie")), .IsElemental = .Name.ToLower.Contains("elemental")}
            End If

            Select Case ShortName.Category
                Case CharacterCategory.Monster
                    newCharacter.Category = CharacterCategory.Monster
                    currentMonsters.Add(ShortName.Name)
                Case CharacterCategory.Player
                    newCharacter.Category = CharacterCategory.Player
                    currentPlayers.Add(ShortName.Name)
                Case CharacterCategory.Ally
                    newCharacter.Category = CharacterCategory.Ally
                    currentAllies.Add(ShortName.Name)
            End Select
            Characters.Add(ShortName.Name, newCharacter)
            RegisterEnteredCharacter(newCharacter)

            WriteMessage(2, 9, "Registered " & newCharacter.Name & " to " & newCharacter.ShortName)
        Loop
        UnmatchedFullNames.Clear()
    End Sub

    ''' <summary>
    ''' Enters a registered character into the (end of the) battle list.
    ''' </summary>
    ''' <param name="Character">Data on the character to add.</param>
    ''' <remarks>The ShortName field in the character data will be used to add them to the battle list.</remarks>
    Public Sub RegisterEnteredCharacter(Character As CharacterData)
        Dim newCombatant As Combatant
        newCombatant = New Combatant(Character)
        BattleList.Add(Character.ShortName, newCombatant)
    End Sub

    Public Overrides Sub OnChannelMessage(Connection As IRCConnection, Sender As String, Channel As String, Message As String)
        If WaitingForBattleList AndAlso Regex.IsMatch(Message, "^\x03\d{1,2}[A-}](?>[0-9A-}]*)(,Â?\s\x03\d{1,2}[A-}](?>[0-9A-}]*))*(?:Â?\s){0,2}$") Then
            WaitingForBattleList = False
            OnBattleListLegacy(Message)
        Else
            RunArenaRegex(Connection, Sender, Channel, Message)
        End If

        MyBase.OnChannelMessage(Connection, Sender, Channel, Message)
    End Sub

    <ArenaRegex("^\x0314\x02What a horrible night for a curse!$")>
    Public Sub OnCurse(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match, ByRef Handled As Boolean)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        ' A curse falls on the battlefield, draining everyone's TP.
        If Not EnableParticipation Then Return
        CurrentEvent = BattleEvents.CurseNight
        Characters(LoggedIn).Status.Add("Cursed")

        For Each Combatant In BattleList
            Combatant.Value.TP = 0
        Next
    End Sub

    <ArenaRegex("^\x0314\x02An ancient Melee-Only symbol glows on the ground of the battlefield\.$")>
    Public Sub OnMeleeLock(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match, ByRef Handled As Boolean)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        ' A curse falls on the battlefield, draining everyone's TP.
        If Not EnableParticipation Then Return
        CurrentEvent = BattleEvents.MeleeLock
    End Sub

    <ArenaRegex("^(?:\x032\x02|\x02\x032)(?<Name>.*) \x02steps up first in the battle!$")>
    Public Sub OnBattleStart(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match, ByRef Handled As Boolean)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        ' This is the last message that is sent during battle initialisation. At this point, we know that the battle has started.

        IsBattleStarted = True
        BattleStartTime = Now

        If currentMonsters.Count = 0 And lBattleListLegacyAliveColour = Nothing Then IsPVPBattle = True
        TurnNumber = 1

        If Not EnableAnalysis Then Return

        WriteMessage(3, 11, String.Join(", ", currentPlayers.ToArray) & " are in this battle.")
        WriteMessage(3, 11, String.Join(", ", currentMonsters.ToArray) & " are in this battle.")
        WriteMessage(3, 11, String.Join(", ", currentAllies.ToArray) & " are in this battle.")

        CurrentTurn = FindShortName(Match.Groups("Name").Value, True)
        BattleList(CurrentTurn).TurnNumber += 1

        ' If it's the bot's turn, attack.
        If ShouldAct() Then AITurn()
    End Sub

#End Region

#Region "Battle events"

    '<ArenaRegex("^(?<Attacker>[^ ]*) uses [^ ]* (?<Technique>[^ ]*) on (?<Target>[^ ]*)")>
    'Public Sub OnForceTechniqueCommand(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match, ByRef Handled As Boolean)
    '    If Not EnableAnalysis Then Return
    '    If Characters.ContainsKey(Match.Groups("Attacker").Value) AndAlso
    '        Characters(Match.Groups("Attacker").Value).Name = CurrentTurn And
    '        CurrentAbility = Nothing Then
    '        CurrentAbility = Match.Groups("Technique").Value
    '    End If
    'End Sub

    '<ArenaRegex("^\x01ACTION uses [^ ]* (?<Technique>[^ ]*) on (?<Target>[^ ]*)")>
    'Public Sub OnTechniqueCommand(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match, ByRef Handled As Boolean)
    '    If Not EnableAnalysis Then Return
    '    If Characters.ContainsKey(Sender.Split("!"c)(0)) AndAlso
    '        Characters(Sender.Split("!"c)(0)).Name = CurrentTurn And
    '        CurrentAbility = Nothing Then
    '        CurrentAbility = Match.Groups("Technique").Value
    '    End If
    'End Sub

    <ArenaRegex("^\x034\x02(?<Attacker>.*) \x02performs an? (?<Hits>double|triple|four hit|five hit|six hit|seven hit|eight hit) attack( against\x02 (?<Target>.*)\x02)?!$")>
    Public Sub OnAttackMultiHit(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match, ByRef Handled As Boolean)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        CurrentTurn = FindShortName(Match.Groups("Attacker").Value, True)
        If Match.Groups("Target").Success Then CurrentTarget = FindShortName(Match.Groups("Target").Value, True)
        CurrentAoEHit = False
    End Sub

    <ArenaRegex("^\x02\x033(?<Attacker>.*)\x02 (?<Action>.*\x033\.)$")>
    Public Sub OnAttackStandard(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match, ByRef Handled As Boolean)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        ' Messages from standard attacks get \x033. appended to them.
        ' Messages from techniques don't end with \x033, unless that's written in the technique description.
        If Not EnableAnalysis Then Return
        If FindShortName(Match.Groups("Attacker").Value, True) <> CurrentTurn Then CurrentTarget = "" ' This is to indicate that we haven't registered the action yet.
        If Counterer Is Nothing Then
            CurrentTurn = FindShortName(Match.Groups("Attacker").Value, True)
        Else
            ' This is a counterattack.
            CurrentTarget = CurrentTurn
        End If
        CurrentAction = Match.Groups("Action").Value
        CurrentAoEHit = False

        WriteMessage(3, 3, "Standard attack detected.", Match.Value)
    End Sub

    <ArenaRegex("^\x037\x02(?<Attacker>.*)\x02's melee attack is countered by (?<Target>.*)!$")>
    Public Sub OnCounter(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match, ByRef Handled As Boolean)
        Counterer = FindShortName(Match.Groups("Target").Value, True)
    End Sub

    <ArenaRegex("^\x033\x02(?<Attacker>(?!Battle Chat|Who's Online|Your|With\x02)[^\x02\x03]*)(?<!'s)(\x02 | \x02)(?!has entered the dimensional rift to join the battle arena\.$)(?!has (gained( the)?|restored|regained|been healed for)\x02)(?!absorbs\x02)(?!is wearing\x02)(?!'s (HP|TP|Ignition Gauge) is:\x02)(?!has\x02)(?!has the following (\x0312)?(weapons|resistances|monster killer traits|\+?\w+ items|gems|keys|\w+ armor|accessor(y|ies)|runes))(?!currently has no)(?!is roughly level\x02)(?!is currently using the\x02)(?!has switched to the\x02)(?!knows the following (\x0312)?(styles|(ignition )?techniques|(passive|active)(\x033)? skills|augments))(?!currently (knows|has) no)(?!does not know any)(?!has obtained the following Ignitions)(?!has equipped\x02)(?!unequipped the)(?!'s status is currently:\x03)(?!has (equipped|removed) the( accessory)?\x02)(?!is no[tw] wearing the \x02)(?!has (saved|reloaded) winning streak #\x02)(?!currently has winning streak #\x02)(?! gives \d+)(?!'s [^ ]+ style has leveled up! It is now\x02)(?!has a difficulty of\x02)(?!sets \w+ difficulty to\x02)(?!'s\x02 \w+ is now augmented)(?!uses \x021 RepairHammer\x02 and\x02)(?!has not unlocked any achievements yet\.$)(?!has unlocked the following achievements:\x02)(?!has no augments currently activated\.$)(?!has been defeated\x02)(?!is currently undefeated!$)(?!drops a small \w+ orb that restores\x02)(?!has regained interest in the battle\.$)(?!has sobered up\.$)(?!'s body has fought off the virus\.$)(?!has broken\x02)(?!attack goes right through\x02)(?!has become corporeal\.$)(?!has successfully dug up a\(n\)\x02)(?!has stolen and absorbs\x02)(?!absorbs\x02 [0-9,]+ HP)(?!is no longer (surrounded by a reflective magical barrier|confused)\.$)(?!weapon lock has broken\.)(?!((defense|strength|int) down status|(zombie|physical protect|magic shell) status|melee weapon enchantment|\w+) has worn off.\$)(?<Action>.*)(?<!\x033\.)$")>
    Public Sub OnAttackTechnique(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match, ByRef Handled As Boolean)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        If Not EnableAnalysis Then Return
        ' Messages from techniques don't end with \x033, unless that's written in the technique description.
        If FindShortName(Match.Groups("Attacker").Value, True) <> CurrentTurn Then CurrentTarget = "" ' This is to indicate that we haven't registered the action yet.
        CurrentTurn = FindShortName(Match.Groups("Attacker").Value, True)
        CurrentAction = Match.Groups("Action").Value
        CurrentAoEHit = False
        WriteMessage(3, 3, "Technique detected.", Match.Value)
    End Sub

    Public Sub RegisterAttack()
        If Not EnableAnalysis Then Return
        If CurrentTurn = Nothing Then Return
        'Check if the attack was a technique.
        Dim IsTechnique As Boolean = Not CurrentAction.EndsWith(Chr(3) & "3.")
        Dim TechniqueUsed As String
        Dim TechniqueElement As String
        Dim IsHealingTechnique As Boolean = IsTechnique AndAlso CurrentAction.Contains("healing")

        CurrentAction = Regex.Replace(CurrentAction, "\b" & Regex.Escape(CurrentTurn) & "\b", "%User%")
        CurrentAction = Regex.Replace(CurrentAction, "\b(him|her|it)\b", "%Gender%")
        CurrentAction = Regex.Replace(CurrentAction, "\b(his|her|its)\b", "%Gender2%")
        If CurrentTarget <> Nothing Then CurrentAction = Regex.Replace(CurrentAction, "\b" & Regex.Escape(Characters(CurrentTarget).Name) & "(?='?\b)", "%Target%")

        If IsTechnique Then
            For Each Technique In Techniques
                TechniqueUsed = Technique.Key
                If Technique.Value.Description = CurrentAction Then GoTo Found
            Next

            ' We're not yet familiar with this technique. Let's note all we know about it.
            If CurrentAbility = Nothing Or CurrentAbility = "?" Then
                For i = 0 To Integer.MaxValue
                    TechniqueUsed = "UnknownTechnique" & i
                    If Not Techniques.ContainsKey(TechniqueUsed) Then Exit For
                Next
            Else
                TechniqueUsed = CurrentAbility
            End If
            If Not Techniques.ContainsKey(TechniqueUsed) OrElse Not Techniques(TechniqueUsed).IsWellKnown Then
                ' Guess the element of the technique.
                If Regex.IsMatch(CurrentAction, "\b(ice|icy)", RegexOptions.IgnoreCase) Then
                    TechniqueElement = "~Ice"
                ElseIf Regex.IsMatch(CurrentAction, "\bwater", RegexOptions.IgnoreCase) Then
                    TechniqueElement = "~Water"
                ElseIf Regex.IsMatch(CurrentAction, "\btsunami\b", RegexOptions.IgnoreCase) Then
                    TechniqueElement = "~Water"
                ElseIf Regex.IsMatch(CurrentAction, "\btidal wave\b", RegexOptions.IgnoreCase) Then
                    TechniqueElement = "~Water"
                ElseIf Regex.IsMatch(CurrentAction, "\bthunder\b", RegexOptions.IgnoreCase) Then
                    TechniqueElement = "~Lightning"
                ElseIf Regex.IsMatch(CurrentAction, "\blightning\b", RegexOptions.IgnoreCase) Then
                    TechniqueElement = "~Lightning"
                ElseIf Regex.IsMatch(CurrentAction, "\bholy\b", RegexOptions.IgnoreCase) Then
                    TechniqueElement = "~Light"
                ElseIf Regex.IsMatch(CurrentAction, "\bcurse", RegexOptions.IgnoreCase) Then
                    TechniqueElement = "~Dark"
                ElseIf Regex.IsMatch(CurrentAction, "\bdark(ness)?\b", RegexOptions.IgnoreCase) Then
                    TechniqueElement = "~Dark"
                ElseIf Regex.IsMatch(CurrentAction, "\bfire(?!(d|s|ing))", RegexOptions.IgnoreCase) Then
                    TechniqueElement = "~Fire"
                ElseIf Regex.IsMatch(CurrentAction, "\b(flame|flaming)", RegexOptions.IgnoreCase) Then
                    TechniqueElement = "~Fire"
                ElseIf Regex.IsMatch(CurrentAction, "\bwinds?\b", RegexOptions.IgnoreCase) Then
                    TechniqueElement = "~Wind"
                ElseIf Regex.IsMatch(CurrentAction, "\btornado", RegexOptions.IgnoreCase) Then
                    TechniqueElement = "~Wind"
                ElseIf Regex.IsMatch(CurrentAction, "\bcyclone", RegexOptions.IgnoreCase) Then
                    TechniqueElement = "~Wind"
                ElseIf Regex.IsMatch(CurrentAction, "\bearth", RegexOptions.IgnoreCase) Then
                    TechniqueElement = "~Earth"
                ElseIf Regex.IsMatch(CurrentAction, "\bground\b", RegexOptions.IgnoreCase) Then
                    TechniqueElement = "~Earth"
                ElseIf Regex.IsMatch(CurrentAction, "\bboulder", RegexOptions.IgnoreCase) Then
                    TechniqueElement = "~Earth"
                ElseIf Regex.IsMatch(CurrentAction, "\brock") Then
                    TechniqueElement = "~Earth"
                ElseIf Regex.IsMatch(CurrentAction, "\bstone") Then
                    TechniqueElement = "~Stone"
                ElseIf Regex.IsMatch(CurrentAction, "\blight\b", RegexOptions.IgnoreCase) Then
                    TechniqueElement = "~Light"
                ElseIf Regex.IsMatch(CurrentAction, "\bshock(?!.?wave)\b", RegexOptions.IgnoreCase) Then
                    TechniqueElement = "~Lightning"
                End If
                Dim newTechnique = New TechniqueData With {.Description = CurrentAction, .Element = TechniqueElement}
                Techniques.Remove(TechniqueUsed)
                Techniques.Add(TechniqueUsed, newTechnique)
                WriteMessage(2, 9, "Found new technique " & TechniqueUsed & ". (Element: " & TechniqueElement & ")")
            End If

Found:
            If CurrentAbility <> Nothing And CurrentAbility <> "?" And CurrentAbility <> TechniqueUsed Then
                Dim Technique = Techniques(TechniqueUsed)
                WriteMessage(2, 9, "Reregistering " & TechniqueUsed & " as " & CurrentAbility & ")")
                Techniques.Remove(TechniqueUsed)
                For Each Character In Characters
                    If Character.Value.EquippedWeaponTechs Is Nothing Then Continue For
                    If Character.Value.EquippedWeaponTechs.Contains(TechniqueUsed) Then
                        Character.Value.EquippedWeaponTechs.Remove(TechniqueUsed)
                        Character.Value.EquippedWeaponTechs.Add(CurrentAbility)
                    End If
                Next
                TechniqueUsed = CurrentAbility
                Technique.Name = TechniqueUsed
                If Not Techniques.ContainsKey(TechniqueUsed) Then Techniques.Add(TechniqueUsed, Technique)
            End If
            BattleList(CurrentTurn).LastAction = TechniqueUsed
            WriteMessage(2, 8, CurrentTurn & " uses " & TechniqueUsed & " on " & CurrentTarget & ".")
            BattleList(CurrentTurn).TP -= Techniques(TechniqueUsed).TP
            'If CurrentTurn = LoggedIn Then DoBattle(Chr(3) & "4[TP" & Chr(3) & "12 " & BattleList(CurrentTurn).TP & Chr(3) & "4] [Used" & Chr(3) & "12 " & Techniques(TechniqueUsed).TP & Chr(3) & "4]")
        Else
            TechniqueUsed = "Attack"
            BattleList(CurrentTurn).LastAction = Characters(CurrentTurn).EquippedWeapon
            WriteMessage(2, 8, CurrentTurn & " attacks " & CurrentTarget & ".")
        End If

        CurrentAbility = Nothing

        ' Notice monsters that attack other monsters. 
        If CurrentTarget = Nothing Then Return
        If Not IsHealingTechnique And Not (BattleList(CurrentTurn).Category < CharacterCategory.Monster Xor BattleList(CurrentTarget).Category < CharacterCategory.Monster) Then
            Characters(CurrentTurn).AttacksAllies = True
        End If
    End Sub

    <ArenaRegex("^The attack did\x034\x02 (?<Damage>\d+) \x02\x0Fdamage( \[(?<Style>.*)\])?$")>
    Public Sub OnDamageLegacy(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match, ByRef Handled As Boolean)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        If Not EnableAnalysis Then Return

        Dim Occurrences As Integer
        For Each Character In BattleList
            If Character.Key = CurrentTurn Then Continue For
            Dim pos As Integer = 0, i As Integer
            For i = 0 To Integer.MaxValue
                If pos < 0 Then Exit For
                pos = CurrentAction.IndexOf(Character.Value.Name, pos + 1)
            Next
            If i > 0 AndAlso (CurrentTarget = Nothing OrElse i > Occurrences OrElse (i = Occurrences And Character.Value.Name.Length > Characters(CurrentTarget).Name.Length)) Then
                Occurrences = i
                CurrentTarget = Character.Key
            End If
        Next
        ' If Occurrences = 0 Then CurrentTarget = Nothing

        RegisterAttack()

        CurrentAbility = Nothing
        CurrentTurn = Nothing
    End Sub

    <ArenaRegex("^The attack did\x034\x02 (?<Damage>\d+) \x02\x0Fdamage to (?<Target>[^[]*)( \[(?<Style>.*)\])?$")>
    Public Sub OnDamageSingle(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match, ByRef Handled As Boolean)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        If Not EnableAnalysis Then Return

        CurrentTarget = FindShortName(Match.Groups("Target").Value, True)

        RegisterAttack()

        CurrentAbility = Nothing
        CurrentTurn = Nothing
    End Sub

    <ArenaRegex("^\x031The first attack did\x034\x02 (?<Damage1>\d+) \x02\x0Fdamage\.( +The second attack did\x034\x02 (?<Damage2>\d+) \x03\x0Fdamage\.( +The third attack did\x034\x02 (?<Damage3>\d+) \x02\x0Fdamage\.( +The fourth attack did\x034\x02 (?<Damage4>\d+) \x02\x0Fdamage\.( +The fifth attack did\x034\x02 (?<Damage5>\d+) \x02\x0Fdamage\.( +The sixth attack did\x034\x02 (?<Damage6>\d+) \x02\x0Fdamage\.( +The seventh attack did\x034\x02 (?<Damage7>\d+) \x02\x0Fdamage\.( +The eighth? attack did\x034\x02 (?<Damage8>\d+) \x02\x0Fdamage\.)?)?)?)?)?)?)? +Total physical damage:\x034\x02 (?<Damage>\d+) \x0F(?<Style>.*)?$")>
    Public Sub OnDamageMulti(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match, ByRef Handled As Boolean)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        If Not EnableAnalysis Then Return

        'If CurrentTarget <> "" Then Return

        If CurrentTarget = Nothing Then
            Dim Occurrences As Integer
            For Each Character In BattleList
                If Character.Key = CurrentTurn Then Continue For
                Dim pos As Integer = 0, i As Integer
                For i = 0 To Integer.MaxValue
                    If pos < 0 Then Exit For
                    pos = CurrentAction.IndexOf(Character.Value.Name, pos + 1)
                Next
                If i > Occurrences Then
                    Occurrences = i
                    CurrentTarget = Character.Key
                End If
            Next
            'If Occurrences = 0 Then Stop
        End If

        RegisterAttack()

        CurrentAbility = Nothing
        CurrentTurn = Nothing
    End Sub

    <ArenaRegex("^The attack did\x034\x02 (?<Damage>\d+) \x02\x0Fdamage \x0Fto\x02 (?<Target>[^\x02]*)\x02! (?<Status>\x034\x02[^\x02]*\x02.* )?\x0F(\[(?<Style>.*)\])?(?<Status> \x02\x034.*\x02.*\x02.*\x02!)?$")>
    Public Sub OnDamageAoE(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match, ByRef Handled As Boolean)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        If Not EnableAnalysis Then Return
        If Not CurrentAoEHit Then ' Otherwise the AoE attack would be registered once for each hit.
            CurrentTarget = FindShortName(Match.Groups("Target").Value, True)
            RegisterAttack()
        End If
    End Sub

    <ArenaRegex("^\x02\x034(?>[^\x02]*)\x02can only attack monsters!$")>
    Public Sub OnFailedAttackAlly(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match, ByRef Handled As Boolean)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        If CurrentTurn = LoggedIn Then
            Dim tB = BattleList(CurrentTarget), tC = Characters(CurrentTarget)
            tB.Category = tB.Category And 3
            tC.Category = tC.Category And 3
            WriteMessage(2, 9, "Registered " & tC.ShortName & " as an ally (" & tC.Category & ").", Match.Value)

            Threading.Thread.Sleep(1000)
            AITurn()
        End If
    End Sub

    <ArenaRegex("^\x02\x034(?<Name>[^\x02]*) \x02is now\x02 (?<Status>[^\x02]*)\x02!$")>
    Public Sub OnStatusInflicted(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match, ByRef Handled As Boolean)
        Dim cC = Characters(FindShortName(Match.Groups("Name").Value, True)), cB = BattleList(FindShortName(Match.Groups("Name").Value, True))
        Dim Effect As String
        Select Case Match.Groups("Status").Value
            Case "frozen in time" : Effect = "stop"
            Case "poisoned" : Effect = "poison"
            Case "silenced" : Effect = "silence"
            Case "blind" : Effect = "blind"
            Case "inflicted with a virus" : Effect = "virus"
            Case "inflicted with amnesia" : Effect = "amnesia"
            Case "paralyzed" : Effect = "paralysis"
            Case "a zombie" : Effect = "zombie"
            Case "slowed" : Effect = "slow"
            Case "stunned" : Effect = "stun"
            Case "cursed" : Effect = "curse" : cB.TP = 0
            Case "charmed" : Effect = "charm"
            Case "intimidated" : Effect = "intimidate"
            Case "inflicted with defense down" : Effect = "defensedown"
            Case "inflicted with strength down" : Effect = "strengthdown"
            Case "inflicted with int down" : Effect = "intdown"
            Case "petrified" : Effect = "petrify"
            Case "bored of the battle " : Effect = "bored"
            Case "confused" : Effect = "confuse"
            Case "no longer boosted" : Effect = "removeboost"
            Case Else : WriteMessage(1, 4, "Unrecognised status effect: " & Match.Groups("Status").Value & ".", Match.Value) : Return
        End Select

        If cB.Status.Contains(Effect) Then
            If Effect = "poison" Then
                Effect = "poison-heavy"
                cB.Status(Array.IndexOf(cB.Status, "poison")) = "poison-heavy"
                WriteMessage(1, 12, cB.Name & " is now inflicted with " & Effect & ".", Match.Value)
            End If
        Else
            ReDim cB.Status(UBound(cB.Status) + 1)
            cB.Status(UBound(cB.Status)) = Effect
            WriteMessage(1, 12, cB.Name & " is now inflicted with " & Effect & ".", Match.Value)
        End If
    End Sub

    <ArenaRegex("^\x034\x02(?<Name>[^\x02]*) \x02is immune to the (?<Status>[^ ]*) status!$")>
    Public Sub OnStatusImmune(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match, ByRef Handled As Boolean)
        Dim cC = Characters(FindShortName(Match.Groups("Name").Value, True))
        If Not cC.StatusImmunities.Contains(Match.Groups("Status").Value) Then cC.StatusImmunities.Add(Match.Groups("Status").Value)
        WriteMessage(2, 12, cC.Name & " is immune to the " & Match.Groups("Status").Value & " effect.", Match.Value)
    End Sub

    <ArenaRegex("^(?:\x033\x02|\x02\x033)(?<Attacker>.*)'s\x02 attack goes right through\x02 (?<Target>.*) \x02doing no damage!$")>
    Public Sub OnEtherealAvoid(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match, ByRef Handled As Boolean)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        If Not EnableAnalysis Then Return

        CurrentTarget = FindShortName(Match.Groups("Target").Value, True)

        Characters(CurrentTarget).IsEthereal = True
        If Not BattleList(CurrentTarget).Status.Contains("ethereal") Then AppendArray(BattleList(CurrentTarget).Status, "ethereal")

        RegisterAttack()

        CurrentAbility = Nothing
        CurrentTurn = Nothing
    End Sub

    <ArenaRegex("^\x037\x02(?<Target>.*) \x02is immune to the (?<Element>fire|ice|water|lightning|earth|wind|light|dark) element!$")>
    Public Sub OnElementalImmunity(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match, ByRef Handled As Boolean)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        If Not EnableAnalysis Then Return

        CurrentTarget = FindShortName(Match.Groups("Target").Value, True)

        If Not Characters(CurrentTarget).ElementalImmunities.Contains(Match.Groups("Element").Value.ToLower) Then _
            Characters(CurrentTarget).ElementalImmunities.Add(Match.Groups("Element").Value.ToLower)

        RegisterAttack()

        CurrentAbility = Nothing
        CurrentTurn = Nothing
    End Sub

    <ArenaRegex("^\x032\x02(?<Name>.*) \x02looks at (?<Target>.*) and says ""(?<Quote>.*)""\x0F$")>
    Public Sub OnTaunt(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        If Not EnableAnalysis Then Return
        CurrentAction = "taunt"
    End Sub

    <ArenaRegex("^\x034The\x02 (?<Item>[^ ]+) \x02explodes and summons\x02 (?<Character>.*)\x02! \x02\x03(12\k<Character> \x02(?<Description>.*))?$")>
    Public Sub OnSummon(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match, ByRef Handled As Boolean)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        ' Register the new character.
        Dim ShortName As String = Match.Groups("Character").Value.Replace(" "c, "_"c)

        If Not Characters.ContainsKey(ShortName) Then
            Characters.Add(ShortName, New CharacterData With {.ShortName = ShortName, .Name = Match.Groups("Character").Value, .IsSummon = True, .Category = CharacterCategory.Ally})
            If Match.Groups("Description").Success Then Characters(ShortName).Description = Match.Groups("Description").Value
        End If

        BattleList.Add(CurrentTurn & "_summon", New Combatant With {.Name = Match.Groups("Character").Value, .ShortName = ShortName, .Category = CharacterCategory.Ally})
        currentAllies.Add(CurrentTurn & "_summon")
    End Sub

    <ArenaRegex("^\x01ACTION uses [^ ]+ (?<Technique>[^ ]+) on (?<Target>[^ ]+)")>
    Public Sub OnCommandTechnique(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Not EnableAnalysis Then Return
        If CurrentTurn <> Sender.Split("!"c)(0) Then Return
        If CurrentAbility = Nothing Then
            CurrentAbility = Match.Groups("Technique").Value
        ElseIf CurrentAbility <> Match.Groups("Technique").Value Then
            CurrentAbility = "?"
        End If
        WriteMessage(3, 3, "Technique attempt detected; CurrentAbility := " & CurrentAbility & ".", Match.Value)
    End Sub

    <ArenaRegex("^\x033It is\x02 (?<Name>[^\x02]*)\x02's turn \[[^:]*Health[^:]*: (?<Health>\x02?\x03[01]?\d\x02?.*)\x02\x033\] \[[^:]*Status[^:]*:\x02?\x034\x02?(?<Status>[^\x5D]*)\x02\x033\]")>
    Public Sub OnTurn(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        If Not IsBattleStarted Then Return

        If Not EnableAnalysis Then Return

        CurrentAction = ""
        If Match.Groups("Name").Value = "Demon Portal" Then SlainCharacters.Clear()

        ' Make sure I tracked the person whose turn it is.
        For Each uName In UnmatchedFullNames
            If uName.Name = Match.Groups("Name").Value Then GoTo 2
        Next
        For Each Character In Characters
            If Character.Value.Name = Match.Groups("Name").Value Then
                CurrentTurn = Character.Key
                GoTo 1
            End If
        Next

        ' I haven't.
        ' Check for clones.
        If Match.Groups("Name").Value.StartsWith("Clone of ") Then
            For Each Character In BattleList
                If Character.Value.Name = Match.Groups("Name").Value.Substring(9) Then
                    ' It is a clone. Register it as such.
                    RegisterClone(Character.Key, Match.Groups("Name").Value)
                    CurrentTurn = Character.Key & "_clone"
                    GoTo 1
                End If
            Next
        End If

        ' I'll need to find them in the battle list.
        UnmatchedFullNames.Add(New UnmatchedName With {.Name = Match.Groups("Name").Value, .Category = -1})
2:      If IsNPCBattle Then Return
        Dim newCharacter = New CharacterData With {.Name = Match.Groups("Name").Value, .ShortName = "*" & Match.Groups("Name").Value}
        Characters.Add("*" & Match.Groups("Name").Value, newCharacter)
        BattleList.Add("*" & Match.Groups("Name").Value, New Combatant(newCharacter))
        'Say(Connection, Channel, "!bat info")
        Return

1:
        If Not BattleList.ContainsKey(CurrentTurn) Then
            Dim c = Characters(CurrentTurn)
            BattleList.Add(CurrentTurn, New Combatant(c))
        End If

        BattleList(CurrentTurn).TurnNumber += 1
        If TurnNumber < BattleList(CurrentTurn).TurnNumber Then
            TurnNumber += 1
            If TurnsToDarkness > 0 Then TurnsToDarkness -= 1
            If HolyAuraTurns > 0 Then HolyAuraTurns -= 1
        ElseIf TurnNumber > BattleList(CurrentTurn).TurnNumber Then
            BattleList(CurrentTurn).TurnNumber = TurnNumber
        End If

        ' Check health.
        Dim Health As String = IRCConnection.RemoveCodes(Match.Groups("Health").Value)
        BattleList(CurrentTurn).Health = Health

        ' Check status.
        Dim Status As String = IRCConnection.RemoveCodes(Match.Groups("Status").Value)
        BattleList(CurrentTurn).Status = {}
        If Status <> "none" And Status <> "Normal" Then
            BattleList(CurrentTurn).Status = Status.Split(" | ")
        End If

        ' Count the character's TP.
        If Not BattleList(CurrentTurn).Status.Contains("cursed") Then
            BattleList(CurrentTurn).TP += 5
            If Characters(LoggedIn).Skills IsNot Nothing AndAlso Characters(LoggedIn).Skills.ContainsKey("Zen") Then
                BattleList(CurrentTurn).TP += Characters(LoggedIn).Skills("Zen") * 5
            End If
            If BattleList(CurrentTurn).TP > Characters(CurrentTurn).bTP Then BattleList(CurrentTurn).TP = Characters(CurrentTurn).bTP
            'If CurrentTurn = LoggedIn Then DoBattle(Chr(3) & "4[TP" & Chr(3) & "12 " & BattleList(CurrentTurn).TP & Chr(3) & "4]")
        End If

        ' Check that there are monsters in the battle.
        If Not IsPVPBattle Then
            If NoMonsterFix And currentMonsters.Count = 0 Then
                For Each Entry In BattleList
                    If Entry.Value.Category = Nothing Then GoTo MonsterFound
                Next
                If WaitingForRegistration Is Nothing And UnmatchedFullNames.Count = 0 Then
                    WaitingForRegistration = {CurrentTurn, BattleList(CurrentTurn).Health, String.Join(", ", BattleList(CurrentTurn).Status)}
                    Dim RecheckThread = New Threading.Thread(AddressOf RecheckList)
                    RecheckThread.Start()
                    Return
                End If
            End If
        End If
MonsterFound:

        ' Control the character. But first, check that they're actually able to act.
        If BattleList(CurrentTurn).Status.Contains("staggered") Or
            BattleList(CurrentTurn).Status.Contains("blind") Or
            BattleList(CurrentTurn).Status.Contains("petrified") Or
            BattleList(CurrentTurn).Status.Contains("evolving") Or
            BattleList(CurrentTurn).Status.Contains("intimidated") Or
            BattleList(CurrentTurn).Status.Contains("asleep") Or
            BattleList(CurrentTurn).Status.Contains("stunned") Or
            BattleList(CurrentTurn).Status.Contains("frozen in time") Or
            BattleList(CurrentTurn).Status.Contains("charmed") Or
            BattleList(CurrentTurn).Status.Contains("confused") Or
            BattleList(CurrentTurn).Status.Contains("paralyzed") Or
            BattleList(CurrentTurn).Status.Contains("bored") Then Return

        If ShouldAct() Then AITurn()
    End Sub

    Private Sub RecheckList()
        Threading.Thread.Sleep(5000)
        If currentMonsters.Count = 0 Then
            For Each Entry In BattleList
                If Entry.Value.Category = Nothing Then Return
            Next
            WaitingForRegistration = {CurrentTurn, BattleList(CurrentTurn).Health, String.Join(", ", BattleList(CurrentTurn).Status)}
            DoBattle("!bat info")
        End If
    End Sub

    <ArenaRegex("^\x033It is\x02 (?<Name>[^\x02]*)'s (?<Mech>[^\x02]*)\x02's turn \[Health Status: \x0312\x02(?<Health>\x02?\x03[01]?\d\x02?.*)\x02\x02\x033\] \[Energy Level:\x02\x034 (?<Energy>\d*)%\x02\x033\]$")>
    Public Sub OnTurnMech(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
    End Sub

    <ArenaRegex("^\x0312\x02(?<Name>.*) \x02gets another turn.$")>
    Public Sub OnTurnExtra(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        CurrentTurn = FindShortName(Match.Groups("Name").Value, True)
        If ShouldAct() Then
            Threading.Thread.Sleep(5000)
            AITurn()
        End If

        'Threading.Thread.Sleep(3000)

        'If EnableParticipation And Match.Groups("Name").Value = OwnName Then
        '    AISelf(Match.Groups("Health").Value, Match.Groups("Status").Value)
        'Else
        '    ' See if this player has asked me to control them.

        '    For Each Character In Controlling
        '        If Characters.ContainsKey(Character) AndAlso Characters(Character).Name = Match.Groups("Name").Value Then
        '            AIOther(Character, Match.Groups("Health").Value, Match.Groups("Status").Value)
        '            Exit For
        '        End If
        '    Next
        'End If
    End Sub

    Public Sub RegisterClone(ByVal Original As String, ByVal Name As String)
        Dim bOriginal = BattleList(Original)
        Dim cOriginal = Characters(Original)
        Characters.Add(Original & "_clone", New CharacterData With {
                       .ElementalAbsorbs = cOriginal.ElementalAbsorbs, .ElementalImmunities = cOriginal.ElementalImmunities, .bDEF = cOriginal.bDEF, .bHP = Math.Round(bOriginal.HP * 0.4),
                       .IgnitionCharge = cOriginal.IgnitionCharge, .bIG = cOriginal.bIG, .bINT = cOriginal.bIG, .bSPD = cOriginal.bSPD, .bSTR = cOriginal.bSTR, .bTP = cOriginal.bTP, .RoyalGuardCharge = cOriginal.RoyalGuardCharge,
                        .Category = If(cOriginal.Category <= CharacterCategory.Ally, CharacterCategory.Ally, CharacterCategory.Monster),
                       .EquippedAccessory = cOriginal.EquippedAccessory, .EquippedWeapon = cOriginal.EquippedWeapon, .EquippedWeaponTechs = New List(Of String)(If(cOriginal.EquippedWeaponTechs, New List(Of String)).ToArray),
                       .IsReadyToControl = cOriginal.IsReadyToControl, .Name = Name,
                       .ElementalResistances = cOriginal.ElementalResistances, .WeaponResistances = cOriginal.WeaponResistances, .Skills = cOriginal.Skills, .Techniques = cOriginal.Techniques,
                       .IsUndead = cOriginal.IsUndead, .ElementalWeaknesses = cOriginal.ElementalWeaknesses, .WeaponWeaknesses = cOriginal.WeaponWeaknesses, .Weapons = cOriginal.Weapons})
        If Characters(Original & "_clone").Skills IsNot Nothing Then Characters(Original & "_clone").Skills.Remove("ShadowCopy")
        BattleList.Add(Original & "_clone", New Combatant With {
                       .Category = If(bOriginal.Category < CharacterCategory.Monster, CharacterCategory.Ally, CharacterCategory.Monster),
                       .DEF = bOriginal.DEF, .Health = "Perfect", .HP = Math.Round(bOriginal.HP * 0.4),
                       .INT = bOriginal.INT, .IsManaWalled = bOriginal.IsManaWalled,
                       .IsRoyalGuarded = bOriginal.IsRoyalGuarded, .IsUsingElementalSeal = bOriginal.IsUsingElementalSeal,
                       .IsUsingMightyStrike = bOriginal.IsUsingMightyStrike, .Name = Name,
                       .SPD = bOriginal.SPD, .Status = bOriginal.Status.Clone, .STR = bOriginal.STR,
                       .TP = bOriginal.TP, .UtsusemiShadows = bOriginal.UtsusemiShadows})
        WriteMessage(2, 9, "Registered " & Name & " as " & Characters(Original).Name & "'s clone.")
    End Sub

    <ArenaRegex({"^\x034\x02(?<Name>.*)(?: \x02|\x02 )has been defeated by\x02 (?<Killer>.*)\x02!(?<Overkill> \x037\<\<\x02OVERKILL\x02\>\>)?$"})>
    Public Sub OnDefeat(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        If Not EnableAnalysis Then Return

        Dim CharacterAlias As String = FindShortName(Match.Groups("Name").Value, True)

        WriteMessage(2, 8, Match.Groups("Name").Value & " is defeated.")

        currentPlayers.Remove(CharacterAlias)
        currentMonsters.Remove(CharacterAlias)
        currentAllies.Remove(CharacterAlias)

        If MissingPlayers.Count > 0 Then
            If BattleList.ContainsKey(CharacterAlias) Then
                Select Case BattleList(CharacterAlias).Category
                    Case CharacterCategory.Player
                        'TODO: Something
                End Select
            End If
        End If

        If IsNPCBattle Then Return
        BattleList.Remove(CharacterAlias)

        SlainCharacters.Add(CharacterAlias)
    End Sub

    <ArenaRegex("^(?:\x033\x02|\x02\x033)(?<Character>.*) \x02has equipped\x02 (?<Weapon>[^ ]*)$")>
    Public Sub OnEquip(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        Dim c As CharacterData = Nothing

        For Each c In Characters.Values
            If c.Name = Match.Groups("Character").Value Then
                c.EquippedWeapon = Match.Groups("Weapon").Value
                c.EquippedWeaponTechs = New List(Of String)

                If c.Techniques Is Nothing Then Return

                If Weapons.ContainsKey(c.EquippedWeapon) Then
                    For Each Technique In Weapons(c.EquippedWeapon).Techniques
                        If c.Techniques.ContainsKey(Technique) Then c.EquippedWeaponTechs.Add(Technique)
                    Next
                Else
                    Weapons.Add(c.EquippedWeapon, New WeaponData With {.Name = c.EquippedWeapon, .Techniques = New List(Of String)})
                End If

                Return
            End If
        Next
    End Sub

    <ArenaRegex({"^\x034\x02Darkness\x02 will overcome the battlefield in 5 minutes\.",
            "^\x034\x02The heroes\x02 estimate they have 5 minutes before the wall will crush them at the end of the hall\.$"})>
    Public Sub OnDarknessWarning(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        FiveMinuteWarning = Now
        WriteMessage(1, 8, "Darkness will overcome the battlefield in 5 minutes.")
    End Sub

    <ArenaRegex({"^\x034\x02Darkness\x02 covers the battlefield enhancing the strength of all remaining monsters!"})>
    Public Sub OnDarkness(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        DarknessHasRisen = True
        WriteMessage(1, 8, "Darkness has overcome the battlefield.")
    End Sub

    <ArenaRegex({"^\x0312\x02(?<Name>.*) \x02releases a holy aura that covers the battlefield and keeps the darkness at bay for\x02 (?<Time>\d+) minute\(s\)\x02\."})>
    Public Sub OnHolyAura(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        If FiveMinuteWarning <> Nothing Then
            FiveMinuteWarning = FiveMinuteWarning - TimeSpan.FromMinutes(Match.Groups("Time").Value)
        End If
        BattleStartTime = BattleStartTime - TimeSpan.FromMinutes(Match.Groups("Time").Value)
        HolyAuraTurns = -1
        HolyAuraUser = Match.Groups("Name").Value
        HolyAuraEnd = Now + TimeSpan.FromMinutes(Match.Groups("Time").Value)
        WriteMessage(1, 8, "Darkness will be held back for " & Match.Groups("Time").Value & " minutes.")
    End Sub

    <ArenaRegex({"^\x0312\x02(?<Name>.*) \x02releases a holy aura that covers the battlefield and keeps the darkness at bay for an additional\x02 (?<Time>\d+) turns?\x02\."})>
    Public Sub OnHolyAuraTurns(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        TurnsToDarkness += Match.Groups("Time").Value
        HolyAuraUser = Match.Groups("Name").Value
        HolyAuraTurns = Match.Groups("Time").Value
        HolyAuraEnd = Nothing
        WriteMessage(1, 8, "Darkness will be held back for " & Match.Groups("Time").Value & " turns!")
    End Sub

    <ArenaRegex({"^\x0312\x02(?<Name>.*)x02's holy aura has faded\. The darkness begins to move towards the battlefield once more\."})>
    Public Sub OnHolyAuraEnd(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        HolyAuraUser = Nothing
        HolyAuraEnd = Nothing
        HolyAuraTurns = -1
        WriteMessage(1, 8, "The holy aura fades.")
    End Sub

    <ArenaRegex({"^\x034\x02(?<Name>.*) \x02uses all of (?<Gender>his|her|its|their) health to perform this technique!$"})>
    Public Sub OnSuicide(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        If Not EnableAnalysis Then Return

        Dim CharacterAlias As String = FindShortName(Match.Groups("Name").Value, True)

        WriteMessage(2, 8, Match.Groups("Name").Value & " is committing suicide!")

        currentPlayers.Remove(CharacterAlias)
        currentMonsters.Remove(CharacterAlias)
        currentAllies.Remove(CharacterAlias)
    End Sub

    <ArenaRegex({"^\x034\x02(?<Name>.*) \x02disappears back into (?<Master>.*)'s shadow\.$"})>
    Public Sub OnCloneDisappearance(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        If Not EnableAnalysis Then Return

        Dim CharacterAlias As String = FindShortName(Match.Groups("Name").Value, True)

        WriteMessage(1, 8, Match.Groups("Name").Value & " disappears.")

        currentPlayers.Remove(CharacterAlias)
        currentMonsters.Remove(CharacterAlias)
        currentAllies.Remove(CharacterAlias)
    End Sub

    <ArenaRegex({"^\x034\x02(?<Name>.*) \x02fades away\.$"})>
    Public Sub OnSummonDisappearance(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        If Not EnableAnalysis Then Return

        Dim CharacterAlias As String = FindShortName(Match.Groups("Name").Value, True)

        WriteMessage(1, 8, Match.Groups("Name").Value & " fades away.")

        currentPlayers.Remove(CharacterAlias)
        currentMonsters.Remove(CharacterAlias)
        currentAllies.Remove(CharacterAlias)
    End Sub

    <ArenaRegex({"\x034\x02(?<Name>.*) \x02has run away from the battle!$"})>
    Public Sub OnFlee(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        If Not EnableAnalysis Then Return

        Dim CharacterAlias As String = FindShortName(Match.Groups("Name").Value, True)

        WriteMessage(1, 8, Match.Groups("Name").Value & " flees.")

        currentPlayers.Remove(CharacterAlias)
        currentMonsters.Remove(CharacterAlias)
        currentAllies.Remove(CharacterAlias)
    End Sub
#End Region

#Region "Battle conclusion"

    <ArenaRegex({"^\x034The Battle is Over!",
            "^\x034There were no players to meet the monsters on the battlefield! \x02The battle is over\x02.$"})>
    Public Sub OnBattleEnd(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        WriteMessage(1, 8, "The battle has ended.")
        ClearBattle()
    End Sub

    <ArenaRegex({"^\x034The Battle is Over! \x0312Winner: \[(?<Side>NPC|Monster)\]\x02 (?<Name>(?>.*))$"})>
    Public Sub OnBattleEndNPC(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        WriteMessage(1, 8, "The battle is over.", Match.Value)
        Dim WinnerOdds As Decimal, LoserOdds As Decimal
        Dim cWinner As CharacterData, cLoser As CharacterData
        For Each Combatant In BattleList
            Characters(Combatant.Key).NPCBattlesFought += 1
            If (Combatant.Value.Category = CharacterCategory.Ally And Match.Groups("Side").Value = "NPC") Or
                (Combatant.Value.Category = CharacterCategory.Monster And Match.Groups("Side").Value = "Monster") Then
                ' The winner.
                WinnerOdds = Combatant.Value.Odds
                cWinner = Characters(Combatant.Key)
            Else
                LoserOdds = Combatant.Value.Odds
                cLoser = Characters(Combatant.Key)
            End If
        Next

        ' Award rating points.
        Dim RatingDifference = cWinner.Rating - cLoser.Rating
        Dim PointsToTake As Decimal = Int(WinnerOdds * 100)

        Select Case RatingDifference
            Case Is >= 4000
                PointsToTake += 50
            Case Is >= 2000
                PointsToTake += 100
            Case Is >= 1000
                PointsToTake += 150
            Case Is >= 0
                PointsToTake += 200
            Case Is >= -1000
                PointsToTake += 250
            Case Is >= -2000
                PointsToTake += 300
            Case Is >= -4000
                PointsToTake += 350
            Case Else
                PointsToTake += 400
        End Select

        cWinner.Rating += PointsToTake
        cLoser.Rating -= PointsToTake

        PendingPlayers.Clear()
        ClearBattle()
    End Sub

    Public Sub ClearBattle()
        PendingPlayers.Clear()
        PendingMonsters.Clear()
        PendingAllies.Clear()

        Entering = False

        currentPlayers.Clear()
        currentMonsters.Clear()
        currentAllies.Clear()
        BattleList.Clear()
        SlainCharacters.Clear()

        UnmatchedFullNames.Clear()
        UnmatchedShortNames.Clear()

        BetAmount = 0
        BetOnAlly = False
        TotalBetAmount = 0

        CurrentTurn = Nothing
        CurrentAction = Nothing
        CurrentAbility = Nothing

        IsBattleStarted = False
        IsBossBattle = False
        IsPVPBattle = False
        BattleStartTime = Nothing
        FiveMinuteWarning = Nothing
        TurnsToDarkness = -1
        DarknessHasRisen = False
        CurrentEvent = 0
        number_of_monsters_needed = 0
        HolyAuraEnd = Nothing
        HolyAuraTurns = -1
        HolyAuraUser = Nothing

        ' Remove summons from the database.
        For i = Characters.Count - 1 To 0 Step -1
            If Characters.Keys(i).EndsWith("_summon") Then Characters.Remove(Characters.Keys(i))
            If Characters.Keys(i).EndsWith("_clone") Then Characters.Remove(Characters.Keys(i))
        Next
    End Sub

    <ArenaRegex({"^\x034\x02Another\x02 wave of monsters has arrived to the battlefield!( \[\x02Gauntlet Round: (?<Gauntlet>\d+)\x02\])?$"})>
    Public Sub OnWave(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        currentMonsters.Clear()
        For i = BattleList.Count - 1 To 0 Step -1
            If BattleList.Values(i).Category = CharacterCategory.Monster Then BattleList.Remove(BattleList.Keys(i))
        Next

        CurrentTurn = Nothing
        CurrentAction = Nothing
        CurrentAbility = Nothing

        SlainCharacters.Clear()

        WriteMessage(1, 8, "Another wave of monsters draws near!")
    End Sub

    <ArenaRegex("^\x02(?!\x03)([^\x02]*) \x02(.*)$")>
    Public Sub OnPortal(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match, ByRef Handled As Boolean)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return

        ' Clear the battlefield.
        BattleList.Clear()
        currentMonsters.Clear()
    End Sub

    <ArenaRegex("^\x0312The monsters were protecting a\x02 .* \x02treasure chest. It can be unlocked with a\x02 (?<RequiredKey>.*) \x02; the chest will disappear in (?<Time>\d+(\.\d*)) seconds\.$")>
    Public Sub OnChestAppearance(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return

    End Sub

    <ArenaRegex("^\x0310\x02(?<Player>.*) \x02finds\x02 (?<Number>\d+) (?<Item>.*key)s?\x02!$")>
    Public Sub OnKeyAppearance(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
    End Sub

    <ArenaRegex("^\x034The unopened chest fades from existence, its contents never to be revealed\.\.\.$")>
    Public Sub OnChestDisappearance(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
    End Sub

    <ArenaRegex("^\x0310\x02(?<Player>.*) \x02unlocks the treasure chest and obtains\x02 (?<Number>\d+) (?<Item>.*)\x02! The chest then disappears\.$")>
    Public Sub OnChestOpen(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
    End Sub

    <ArenaRegex("^\x03\x02Players\x02 have been rewarded with\x02 (?<Orbs>\d+) \x02Red Orbs for their (efforts\.|victory!)$")>
    Public Sub OnOrbRewardLegacy(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        Characters(LoggedIn).RedOrbs += Match.Groups("Orbs").Value
        If Characters(LoggedIn).EquippedAccessory = "Blood-Ring" Then Characters(LoggedIn).RedOrbs += Match.Groups("Orbs").Value * 0.1
        If Characters(LoggedIn).EquippedAccessory = "Blood-Pendant" Then Characters(LoggedIn).RedOrbs += Match.Groups("Orbs").Value * 0.15
        Characters(LoggedIn).RedOrbs += Characters(LoggedIn).SkillLevel("OrbHunter") * 15
    End Sub

    <ArenaRegex({"^\x0312The forces of good have won this battle \(level\x02 (?<Level>\d*)\x02\) in (?<TurnCount>\d*) ?turn\(s\)! \[Current record is: (?<LevelRecord>\d*)\]$"})>
    Public Sub OnBattleVictory(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        BattleLevel = CInt(Match.Groups("Level").Value) + 1
    End Sub

    <ArenaRegex({"^\x0312The forces of good have won this special battle event in (?<TurnCount>\d*) ?turn\(s\)!$"})>
    Public Sub OnBattleSpecialVictory(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
    End Sub

    <ArenaRegex({"^\x0312The forces of evil have won this battle \(level\x02 (?<Level>\d*)\x02\) after (?<TurnCount>\d*) ?turn\(s\)! The heroes have lost\x02 (?<Streak>\d*) \x02battle\(s\) in a row!$"})>
    Public Sub OnBattleDefeat(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        BattleLevel = -CInt(Match.Groups("Streak").Value)
    End Sub

    <ArenaRegex({"^\x0312The forces of evil have won this special battle event in (?<TurnCount>\d*) ?turn\(s\)!$"})>
    Public Sub OnBattleSpecialDefeat(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
    End Sub

    <ArenaRegex({"^\x0312The battle has come to a draw after (?<TurnCount>\d*) ?turn\(s\)!$"})>
    Public Sub OnBattleDraw(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
    End Sub

    <ArenaRegex({"^\x0312Neither heroes nor monsters have won this battle as it comes to a draw after (?<TurnCount>\d*) ?turn\(s\)!$"})>
    Public Sub OnBattleSpecialDraw(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
    End Sub

    '<ArenaRegex("^\x03\x02Players\x02 have been rewarded with\x02 (?<Orbs>\d+) \x02Red Orbs for their (efforts\.|victory!)$")>
    Public Sub OnOrbReward(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        Characters(LoggedIn).RedOrbs += Match.Groups("Orbs").Value
    End Sub

    <ArenaRegex("^\x033Gambling Winners?: (?<Winners>\x02[^\x02]*\x02\(\$\$\d*\)(, \x02[^\x02]*\x02\(\$\$\d*\))*)$")>
    Public Sub OnBettingReward(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match)
        If Sender.Split("!"c)(0) <> ArenaNickname Then Return
        For Each Winner In Match.Groups("Winners").Value.Split(","c)
            Dim m = Regex.Match(Winner.Trim, "^\x02(?<Player>[^\x02]*)\x02\(\$\$(?<Amount>\d*)\)$")
            If m.Success Then
                For Each Character In Characters
                    If Character.Key = m.Groups("Player").Value Then
                        Character.Value.DoubleDollars += m.Groups("Amount").Value
                        GoTo 1
                    End If
                Next
            End If
1:      Next
    End Sub

#End Region

    Public Function FindShortName(ByVal FullName As String, ByVal InBattle As Boolean) As String
        If Not InBattle Then Return FindShortName(FullName)
        For Each Character In BattleList
            If Character.Value.Name = FullName Then Return Character.Key
        Next
        Return Nothing
    End Function
    Public Function FindShortName(ByVal FullName As String) As String
        Dim Result As String = Nothing
        For Each Character In Characters
            If Character.Value.Name = FullName Then If Result IsNot Nothing Then Return Nothing Else Result = Character.Key
        Next
        Return Result
    End Function

    Public Shared Function NameMatch(ByVal ShortName As String, ByVal FullName As String) As Short
        ' Substitute in spaces for underscores.
        ShortName = ShortName.Replace("_"c, " "c)
        ShortName = ShortName.ToLower
        FullName = FullName.ToLower

        Dim currentMatchScore As Short = 0, CharPos As New Dictionary(Of Char, Integer), PrevPos As Integer = -1
        For i = 0 To FullName.Length - 1
            Dim SearchingFor As Char = FullName(i)
            If PrevPos > -2 AndAlso PrevPos < ShortName.Length - 1 AndAlso ShortName(PrevPos + 1) = SearchingFor Then
                currentMatchScore += 2
                PrevPos += 1
            ElseIf Not CharPos.ContainsKey(SearchingFor) Then
                If ShortName.Contains(SearchingFor) Then
                    If ShortName.IndexOf(SearchingFor) = PrevPos + 1 Then currentMatchScore += 2 Else currentMatchScore += 1
                    PrevPos = ShortName.IndexOf(SearchingFor)
                    CharPos.Add(SearchingFor, PrevPos)
                Else
                    PrevPos = -2
                End If
            Else
                If ShortName.Substring(CharPos(SearchingFor) + 1).Contains(SearchingFor) Then
                    If ShortName.IndexOf(SearchingFor, CharPos(SearchingFor) + 1) = PrevPos + 1 Then currentMatchScore += 2 Else currentMatchScore += 1
                    PrevPos = ShortName.IndexOf(SearchingFor, CharPos(SearchingFor) + 1)
                    CharPos.Remove(SearchingFor)
                    CharPos.Add(SearchingFor, PrevPos)
                Else
                    PrevPos = -2
                End If
            End If
        Next
        Return currentMatchScore
    End Function

    Public Function AttackMultiplier(ByVal NumberOfHits As Short) As Decimal
        Return AttackMultiplier(NumberOfHits, True)
    End Function
    Public Function AttackMultiplier(ByVal NumberOfHits As Short, ByVal IsTechnique As Boolean) As Decimal
        Return AttackMultiplier(NumberOfHits, IsTechnique, Version >= New ArenaVersion(2, 3, 1))
    End Function
    Public Shared Function AttackMultiplier(ByVal NumberOfHits As Short, ByVal IsTechnique As Boolean, ByVal NewVersion As Boolean) As Decimal
        If Not IsTechnique And NumberOfHits > 6 Then NumberOfHits = 6
        If Not IsTechnique And NumberOfHits < 1 Then NumberOfHits = 1

        Select Case NumberOfHits
            Case Is <= 1
                If Not IsTechnique Then
                    If NewVersion Then Return 1 + 1 / 21 ' We add 1/21 to account for random double attacks.
                    Return 1 + 1 / 30
                End If
                Return 1
            Case 2
                If NewVersion Then Return 1 + 1 / 2.1
                Return 1 + 1 / 3
            Case 3
                If NewVersion Then Return 1 + 1 / 2.1 + 1 / 3.2
                Return 1 + 1 / 2.1 + 1 / 2.2
            Case 4
                If NewVersion Then Return 1 + 1 / 2.1 + 1 / 3.2 + 1 / 4.1
                Return 1 + 1 / 2.1 + 1 / 3.2 + 1 / 3.9
            Case 5
                Return 1 + 1 / 2.1 + 1 / 3.2 + 1 / 4.1 + 1 / 4.9
            Case 6
                Return 1 + 1 / 2.1 + 1 / 3.2 + 1 / 4.1 + 1 / 4.9 + 1 / 6.9
            Case 7
                Return 1 + 1 / 2.1 + 1 / 3.2 + 1 / 4.1 + 1 / 4.9 + 1 / 6.9 + 1 / 8.9
            Case Is >= 8
                Return 1 + 1 / 2.1 + 1 / 3.2 + 1 / 4.1 + 1 / 4.9 + 1 / 6.9 + 1 / 8.9 + 1 / 9.9
            Case Else
                If Not IsTechnique Then
                    If NewVersion Then Return 1 + 1 / 21
                    Return 1 + 1 / 30
                End If
                Return 1
        End Select
    End Function

#Region "Debug"

    Dim DebugMode As Short = 3, DebugTargetConnection As IRCConnection = Nothing, DebugTargetChannel As String = "!MainCommands/Console"

    <Command({"debug"}, 1, 1,
"debug <level>",
"Changes debug settings.",
 ".debug")>
    Public Sub CommandDebug(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        DebugMode = Short.Parse(args(0))
        DebugTargetConnection = Connection
        If Connection Is Nothing Then
            DebugTargetChannel = Channel
        Else
            DebugTargetChannel = Sender.Split("!"c)(0)
        End If
    End Sub

    Public Sub WriteMessage(ByVal Level As Short, ByVal Colour As Short, ByVal Message As String)
        WriteMessage(Level, Colour, Message, Nothing)
    End Sub
    Public Sub WriteMessage(ByVal Level As Short, ByVal Colour As Short, ByVal Message As String, ByVal Line As String)
        If DebugMode >= Level Then
            If DebugTargetChannel IsNot Nothing Then
                If Line IsNot Nothing And DebugTargetChannel <> "!MainCommands/Console" Then Say(DebugTargetConnection, DebugTargetChannel, ChrW(3) & "15> " & ChrW(15) & Line, SayOptions.NoParse + SayOptions.NoticeNever)
                Say(DebugTargetConnection, DebugTargetChannel, MinorLabel & ChrW(3) & Colour & "***" & ChrW(15) & " " & Message, SayOptions.NoticeNever)
            End If
        End If
    End Sub
#End Region

End Class
