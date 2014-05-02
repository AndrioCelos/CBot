' General to-do list:
'   TODO: Complete the superflat command.

Imports VBot
Imports System.Text.RegularExpressions
Imports System.Text

Public Class MinecraftInfoPlugin
    Inherits Plugin

    Public Items As New Dictionary(Of UShort, Item)
    Public Potions As New Dictionary(Of UShort, Potion)
    Public Effects As New Dictionary(Of UShort, Effect)
    Public Enchantments As New Dictionary(Of UShort, Enchantment)

    Public Problems As New List(Of String)

    Public Class Item
        ''' <summary>
        ''' Contains values representing the conditions under which an item can be obtained in a player's inventory.
        ''' </summary>
        Enum ObtainableModes As Short
            ''' <summary>The item can be obtained legitimately in Survival mode.</summary>
            Normal
            ''' <summary>The item can be obtained in Survival mode using enchantments, and in Creative mode normally.</summary>
            Enchantment
            ''' <summary>The item can be obtained in Survival mode using enchantments, but not in Creative mode.</summary>
            EnchantmentNoCreative
            ''' <summary>The item can be obtained in Survival mode by trading with villagers, or in Creative mode.</summary>
            Trading
            ''' <summary>The item can be obtained in Creative mode using the item list, but is unavailable in Survival mode.</summary>
            Creative
            ''' <summary>The item can be obtained in Creative mode by block picking, but is unlisted, and unavailable in Survival mode.</summary>
            CreativeNoList
            ''' <summary>The item cannot be obtained without mods or server commands.</summary>
            Editing
            ''' <summary>The item cannot be obtained by any means other than save file hacking.</summary>
            Unobtainable
        End Enum

        ''' <summary>
        ''' The unique numeric index of the item.
        ''' </summary>
        Public ID As UShort
        ''' <summary>
        ''' The display name of the item.
        ''' </summary>
        Public Name As String
        ''' <summary>
        ''' Alternative names that can be used to refer to an item.
        ''' </summary>
        Public Nicknames As New List(Of String)
        '''' <summary>
        '''' Conditions under which the item can be obtained.
        '''' </summary>
        'Public Obtainable As ObtainableModes
        'Public BlastResistance As Integer
        'Public Tool As Integer
        'Public Description As String
        Public HowToObtain As String
        Public Subtypes As New Dictionary(Of Short, Subtype)
        Friend CommandName As String

        Public Class Subtype
            ''' <summary>The unique numeric index of the item.</summary>
            Public Metadata As UShort
            ''' <summary>The display name of the item. The key indicates the damage value that the name applies to; a key of -1 indicates that the name applies to any unlisted value.</summary>
            Public Name As String
            ''' <summary>Alternative names that can be used to refer to an item. The key indicates the damage value that the name applies to; a key of -1 indicates that the name applies to any unlisted value.</summary>
            Public Nicknames As New List(Of String)
            ''' <summary>Conditions under which the item can be obtained.</summary>
            Public Obtainable As ObtainableModes
            ''' <summary>A description of the item.</summary>
            Public Description As String
            ''' <summary>Instructions on how to get the item.</summary>
            Public HowToObtain As String

            Sub New(ByVal Metadata As UShort, ByVal Name As String)
                Me.Metadata = Metadata
                Me.Name = Name
            End Sub
        End Class
    End Class

    Public Class Potion
        Public Metadata As UShort
        Public Name As String
        Public Boosts As UShort
        Public Duration As UInteger
        Public Nicknames As String()
        Public Description As String
        Public HowToObtain As String
    End Class

    Public Class Effect
        Public ID As UShort
        Public Name As String
        Public Nicknames As String()
        Public Description As String
    End Class

    Public Class Enchantment
        Public ID As UShort
        Public Name As String
        Public MaximumLevel As Short
        Public Nicknames As String()
        Public Description As String
    End Class

    Enum ItemsColumns As Integer
        ColumnID = 0
        ColumnName = 1
        ColumnInternalName = 2
        ColumnNicknames = 3
        ColumnHowToObtain = 4
    End Enum

    Public Overrides ReadOnly Property Name As String
        Get
            Return "Minecraft Info"
        End Get
    End Property
    Public Overrides ReadOnly Property UseGlobalKeyCommand As Boolean
        Get
            Return True
        End Get
    End Property
    'Public Overrides ReadOnly Property ListenInMinorChannels As Boolean
    '    Get
    '        Return Waiting.Count > 0
    '    End Get
    'End Property

    Public Sub New()
        LoadItems()
        LoadPotions()
        LoadEffects()
        LoadEnchantments()
    End Sub

    <Regex({"What.* (ID|(data )?value) of (a |an |the |some )?((block(s)? |piece(s)? |unit(s)? |bit(s)? |item(s)? |pinch(es)? |pile(s)? )(of )?)?(?<Item>.*?)( block(s)?| item(s)?)?\??\Z",
            "What (item |block )?.* (ID |(data )?value )(of )?(?<Item>\d*(:\d*?)?)\??$"})>
    Public Sub RegexItem(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As System.Text.RegularExpressions.Match)
        CommandItem(Connection, Sender, Channel, {Match.Groups("Item").Value})
    End Sub
    <Command("item", 1, 1,
        "item <name|id[:metadata]>",
        "Looks up an item to find its name or ID.")>
    Public Sub CommandItem(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim Identifier As String = args(0)
        Dim ID As Integer, Metadata As Integer, Name As String

        ' Check if the identifier is a number or a name.
        If Integer.TryParse(Identifier, ID) Then
            If ID < 0 Then
                Say(Connection, Channel, Choose("", "Sorry " & Sender.Split("!"c)(0) & ", ") & Choose("that isn't a valid item ID.", "the item ID you entered isn't valid."), True)
                Return
            Else
                Metadata = -1
            End If
        ElseIf Identifier.Contains(":") AndAlso (Integer.TryParse(Identifier.Split({":"c}, 2)(0), ID) And Integer.TryParse(Identifier.Split({":"c}, 2)(1), Metadata)) Then
            If ID < 0 Then
                Say(Connection, Channel, Choose("", "Sorry " & Sender.Split("!"c)(0) & ", ") & Choose("that isn't a valid item ID.", "the item ID you entered isn't valid."), True)
                Return
            End If
        Else
            Name = Identifier
            ID = -1
            Metadata = -1
        End If

        ' If the identifier is an ID, find its name.
        If ID = 373 And Metadata > 0 Then
            ' A potion. (373:0 is a water bottle.)
            Dim Effect As Short, IsSplashPotion As Boolean, IsExtended As Boolean, IsBoosted As Boolean, IsReverted As Boolean, IsUnused As Boolean
            Effect = Metadata And 15
            IsSplashPotion = (Metadata And 16384)
            IsExtended = (Metadata And 64)
            IsBoosted = (Metadata And 32)

SearchAgain:
            If Potions Is Nothing OrElse Not Potions.ContainsKey(Effect) Then
                Say(Connection, Channel, "The item with ID $k12" & ID & "$o is $k13a potion$o.")
                Return
            End If

            Dim Potion = Potions(Effect)
            If Potion.Duration = 0 And Not IsUnused Then
                ' It's an unused potion. Not IsUnused prevents an infinite loop.
                IsUnused = True
                If Potions.ContainsKey(Metadata And 63) Then
                    Effect = Metadata And 63
                    GoTo SearchAgain
                End If
            End If

            ' Check if the potion supports the boosts specified.
            If (Potion.Boosts And 32) = 0 And IsBoosted Then
                IsBoosted = False
            End If
            If (Potion.Boosts And 64) = 0 And IsExtended Then
                IsExtended = False
                IsReverted = True
            End If

            ' Determine the item's name.
            Name = If(IsSplashPotion, "Splash ", "") & String.Format(If(Potion.Name.ToUpper.Contains("POTION"), "{0}", If(IsUnused, "{0} Potion", "Potion of {0}")), Potion.Name) & If(IsBoosted, " II", "") & If(IsExtended, " (extended)", "") & If(IsReverted, " (reverted)", "")
            Say(Connection, Channel, "The item with ID $k12" & ID & "$o and data value $k12" & Metadata & "$o is $k09" & Name & "$o.")
        ElseIf ID >= 0 Then
            ' Check if there's an item with the specified ID.
            If Not Items.ContainsKey(ID) Then
                Say(Connection, Channel, Choose("", "Sorry " & Sender.Split("!"c)(0) & ", ") & Choose("there's no item with ID ", "there isn't any item with ID ", "I don't have records of ID ", "I couldn't find any item with ID ") & IRCColours.Red & ID & "$o.", SayOptions.Capitalise)
                Return
            End If

            Dim Item = Items(ID)
            If Item.Subtypes IsNot Nothing AndAlso Item.Subtypes.ContainsKey(Metadata) AndAlso Item.Subtypes(Metadata).Name <> Nothing Then
                Say(Connection, Channel, String.Format("The item with ID $k12{0}$o ($k12{3}$o) and data value $k12{1}$o is $k09{2}$o.", ID, Metadata, Item.Subtypes(Metadata).Name, Item.CommandName))
            Else
                Say(Connection, Channel, String.Format("The item with ID $k12{0}$o ($k12{3}$o) is $k09{2}$o.", ID, Metadata, Item.Name, Item.CommandName))
            End If
        Else
            Dim Output = GetItemID(Name)

            If Output(0) >= 0 Then
                If Output(1) = -1 Then
                    Say(Connection, Channel, String.Format(Choose("The item ID of $k12{0}$o ($k12{2}$o) is {1}.", "$k12{0}$o ($k12{2}$o) has " & Choose("item ", "") & "ID $k09{1}$o."), Items(Output(0)).Name, Output(0), Items(Output(0)).CommandName), SayOptions.Capitalise)
                ElseIf Output(0) = 373 Then
                    Say(Connection, Channel, String.Format(Choose("The item ID of $k12{0}$o ($k12{3}$o) is {1},", "$k12{0}$o ($k12{3}$o) has " & Choose("item ", "") & "ID $k09{1}$o,") & " with {4} $k09{2}$o.", Name, Output(0), Output(1), Items(Output(0)).CommandName, Choose(Choose("metadata value", "damage value", "data value"), "a " & Choose("metadata value", "damage value", "data value") & " of")), SayOptions.Capitalise)
                Else
                    Say(Connection, Channel, String.Format(Choose("The item ID of $k12{0}$o ($k12{3}$o) is {1},", "$k12{0}$o ($k12{3}$o) has " & Choose("item ", "") & "ID $k09{1}$o,") & " with {4} $k09{2}$o.", Items(Output(0)).Subtypes(Output(1)).Name, Output(0), Output(1), Items(Output(0)).CommandName, Choose(Choose("metadata value", "damage value", "data value"), "a " & Choose("metadata value", "damage value", "data value") & " of")), SayOptions.Capitalise)
                End If
            ElseIf Output(0) = -16 Then
                Say(Connection, Channel, "That potion cannot be reverted.")
            ElseIf Output(0) = -32 Then
                Say(Connection, Channel, "That potion cannot be boosted.")
            ElseIf Output(0) = -64 Then
                Say(Connection, Channel, "That potion cannot be extended.")
            Else
                Say(Connection, Channel, Choose("", "Sorry " & Sender.Split("!"c)(0) & ", ") & String.Format(Choose("I don't recognise {0}.", "I don't have records of any item named {0}.", "I don't know of any item named {0}.", "I don't know what you mean by {0}.", "There" & Choose(" isn't an ", " isn't any ", " is no ", "'s no ") & "item named {0}."), IRCColours.Red & Name & IRCColours.ClearFormat), SayOptions.Capitalise)
            End If
        End If

    End Sub

    <Regex("(How|Where) .*(get|find|obtain|collect|make|craft) (a |an |the |some )?((block(s)? |piece(s)? |unit(s)? |bit(s)? |item(s)? |pinch(es)? |pile(s)? )(of )?)?(?<Item>.*?)( block(s)?| item(s)?)?\??$")>
    Public Sub RegexHowToGet(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As System.Text.RegularExpressions.Match)
        CommandHowToGet(Connection, Sender, Channel, {Match.Groups("Item").Value})
    End Sub
    <Command("howtoget", 1, 1,
        "howtoget <name|id[:metadata]>",
        "Returns information on how to find or obtain an item.")>
    Public Sub CommandHowToGet(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim Identifier As String = args(0)
        Dim ID As Integer, Metadata As Integer, Name As String

        ' Check if the identifier is a number or a name.
        If Integer.TryParse(Identifier, ID) Then
            If ID < 0 Then
                Say(Connection, Channel, Choose("", "Sorry " & Sender.Split("!"c)(0) & ", ") & Choose("that isn't a valid item ID.", "he item ID you entered isn't valid."), SayOptions.Capitalise)
                Return
            Else
                Metadata = -1
            End If
        ElseIf Identifier.Contains(":") AndAlso (Integer.TryParse(Identifier.Split({":"c}, 2)(0), ID) And Integer.TryParse(Identifier.Split({":"c}, 2)(1), Metadata)) Then
            If ID < 0 Then
                Say(Connection, Channel, Choose("", "Sorry " & Sender.Split("!"c)(0) & ", ") & Choose("that isn't a valid item ID.", "he item ID you entered isn't valid."), SayOptions.Capitalise)
                Return
            End If
        Else
            Name = Identifier
            ID = -1
            Metadata = -1
        End If

        ' If the identifier is an ID, find its name.
        If ID >= 0 Then
            ' Check if there's an item with the specified ID.
            If Not Items.ContainsKey(ID) Then
                Say(Connection, Channel, Choose("", "Sorry " & Sender.Split("!"c)(0) & ", ") & Choose("there's no item with ID ", "there isn't any item with ID ", "I don't have records of ID ", "I couldn't find any item with ID $k04") & ID & "$o.", SayOptions.Capitalise)
                Return
            End If
        Else
            Dim Output = GetItemID(Name)
            If Output(0) < 0 Then
                If Name.EndsWith("s") Then
                    Output = GetItemID(Name.Remove(Name.Length - 1))
                    If Output(0) > 0 Then GoTo founditem
                End If
                If Name.EndsWith("ies") Then
                    Output = GetItemID(Name.Remove(Name.Length - 3) & "y")
                    If Output(0) > 0 Then GoTo founditem
                End If
            End If

FoundItem:
            If Output(0) >= 0 Then
                ID = Output(0)
                Metadata = Output(1)
            Else
                Say(Connection, Channel, Choose("", "Sorry " & Sender.Split("!"c)(0) & ", ") & String.Format(Choose("I don't recognise {0}.", "I don't have records of any item named {0}.", "I don't know of any item named {0}.", "I don't know what you mean by {0}.", "There" & Choose(" isn't an ", " isn't any ", " is no ", "'s no ") & "item named $k04{0}$o."), Name), SayOptions.Capitalise)
                Return
            End If
        End If

        ' Look up the instructions.
        Dim Instructions As String = "", ItemName As String
        If Metadata >= 0 AndAlso (Items(ID).Subtypes.ContainsKey(Metadata) AndAlso Items(ID).Subtypes(Metadata).HowToObtain <> Nothing) Then
            Instructions = Items(ID).Subtypes(Metadata).HowToObtain
            ItemName = Items(ID).Subtypes(Metadata).Name
        Else
            Instructions = Items(ID).HowToObtain
            ItemName = Items(ID).Name
        End If

        For Each Line In Instructions.Split(vbLf)
            Say(Connection, Channel, "$k2[$k12" & ItemName & "$k2]$o" & " " & Line)
        Next
    End Sub

    Public Function GetItemID(ByVal Name As String) As Integer()
        ' If the identifier is a name, find the ID of the item with that name.
        ' First check if it matches an official name.
        For Each Item In Items.Values
            If Name.ToLower.Replace(" ", "") = Item.Name.ToLower.Replace(" ", "") Then
                Return {CInt(Item.ID), -1}
            ElseIf Name.ToLower.Replace(" ", "") = Item.CommandName.ToLower.Replace(" ", "") Then
                Return {CInt(Item.ID), -1}
            ElseIf Item.Subtypes IsNot Nothing Then
                For Each Subtype In Item.Subtypes.Values
                    If Subtype.Name <> Nothing AndAlso (Name.ToLower.Replace(" ", "") = Subtype.Name.ToLower.Replace(" ", "")) Then
                        Return {CInt(Item.ID), CInt(Subtype.Metadata)}
                    End If
                Next
            End If
        Next

        ' Is it a potion?
        If Name.Contains("pot") And Not Name.Contains("clay") Then
            Dim PotionType As String, Modifiers As UShort
            For Each Word In Name.Split(" "c)
                Select Case Word.ToLower.TrimStart("("c).TrimEnd(")"c)
                    Case "of", "potion", "pot"
                    Case "g", "great", "greater", "s", "super", "b", "better", "good", "up", "upgraded", "deluxe", "d", "dx", "2", "ii", "ex"
                        Modifiers = Modifiers Or 32
                    Case "ext", "extended", "long", "longer"
                        Modifiers = Modifiers Or 64
                    Case "rev", "reverted"
                        Modifiers = Modifiers Or 16
                    Case "spl", "splash", "explosive", "exploding", "grenade", "nade"
                        Modifiers = Modifiers Or 16384
                    Case Else
                        If PotionType <> Nothing Then
                            PotionType &= " " & Word.ToLower
                        Else
                            PotionType = Word.ToLower
                        End If
                End Select
            Next

            If PotionType = Nothing Then Return {-1, -1}
            ' Find the potion data.
            Dim Potion As Potion
            For Each lPotion In Potions
                If lPotion.Value.Name.ToUpper = PotionType.ToUpper Then
                    Potion = lPotion.Value
                    Exit For
                End If
            Next
            If Potion.Name = Nothing Then
                For Each lPotion In Potions
                    For Each AltName In lPotion.Value.Nicknames
                        If System.Text.RegularExpressions.Regex.IsMatch(PotionType.ToLower.Replace(" ", ""), "\A" & AltName.ToLower.Replace(" ", "") & "\Z", RegexOptions.IgnoreCase + RegexOptions.IgnorePatternWhitespace) Then
                            Potion = lPotion.Value
                            Exit For
                        End If
                    Next
                Next
            End If
            If Potion.Name = Nothing Then Return {-1, -1}

            Dim DataValue As UShort = Potion.Metadata

            ' Check the modifiers.
            If (Modifiers And 32) = 32 And (Potion.Boosts And 32) = 0 Then
                Return {-32, DataValue}
            End If
            If (Modifiers And 64) = 64 And (Potion.Boosts And 64) = 0 Then
                Return {-64, DataValue}
            End If
            If (Modifiers And 16) = 16 And (Potion.Boosts And 96) = 96 Then
                Return {-16, DataValue}
            ElseIf (Modifiers And 16) = 16 And (Potion.Boosts And 64) = 0 Then
                Modifiers = Modifiers Xor 80
            ElseIf (Modifiers And 16) = 16 And (Potion.Boosts And 32) = 0 Then
                Modifiers = Modifiers Xor 48
            End If

            DataValue = DataValue Or Modifiers
            If Potion.Duration > 0 And (Modifiers And 16384) = 0 Then DataValue = DataValue Or 8192
            If DataValue = 0 Then DataValue = 8192

            Return {373, DataValue}
        End If

        ' Next, check if it matches an alternate name.
        For Each Item In Items.Values
            If Item.Nicknames IsNot Nothing Then
                For Each AltName In Item.Nicknames
                    If System.Text.RegularExpressions.Regex.IsMatch(Name.ToLower.Replace(" ", ""), "^" & AltName.ToLower.Replace(" ", "") & "$") Then
                        Return {CInt(Item.ID), -1}
                    End If
                Next
            End If
            If Item.Subtypes IsNot Nothing Then
                For Each Subtype In Item.Subtypes.Values
                    If Subtype.Nicknames IsNot Nothing Then
                        For Each AltName In Subtype.Nicknames
                            If System.Text.RegularExpressions.Regex.IsMatch(Name.ToLower.Replace(" ", ""), "^" & AltName.ToLower.Replace(" ", "") & "$") Then
                                Return {CInt(Item.ID), CInt(Subtype.Metadata)}
                            End If
                        Next
                    End If
                Next
            End If
        Next

        If Name.ToUpper.StartsWith("MINECRAFT:") Then
            Return GetItemID(Name.Substring(10))
        Else
            Return {-1, -1}
        End If
    End Function

    <Command({"effect", "buff"}, 1, 1,
       "effect <name|id>",
       "Looks up a status effect to find its name or ID.")>
    Public Sub CommandEffect(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim Identifier As String = args(0)
        Dim ID As Integer, Name As String

        ' Check if the identifier is a number or a name.
        If Integer.TryParse(Identifier, ID) Then
            If ID < 0 Then
                Say(Connection, Channel, Choose("", "Sorry " & Sender.Split("!"c)(0) & ", ") & Choose("that isn't a valid effect ID.", "the effect ID you entered isn't valid."), True)
                Return
            End If
        Else
            Name = Identifier
            ID = -1
        End If

        ' If the identifier is an ID, find its name.
        If ID >= 0 Then
            ' Check if there's an item with the specified ID.
            If Not Effects.ContainsKey(ID) Then
                Say(Connection, Channel, Choose("", "Sorry " & Sender.Split("!"c)(0) & ", ") & Choose("there's no effect with ID ", "there isn't any effect with ID ", "I don't have records of ID ", "I couldn't find any effect with ID ") & IRCColours.Red & ID & "$o.", SayOptions.Capitalise)
                Return
            End If

            Dim Effect = Effects(ID)
            Say(Connection, Channel, "The effect with ID $k12" & Effect.ID & "$o is $k09" & Effect.Name & "$o.")
        Else
            Dim Effect As Effect
            For Each lEffect In Effects.Values
                If lEffect.Name.ToLower.Replace(" ", "") = Name Then
                    Effect = lEffect
                    GoTo Found
                End If
            Next
            For Each lEffect In Effects.Values
                For Each lRegex In lEffect.Nicknames
                    If Regex.IsMatch(Name.Replace(" ", ""), "^" & lRegex & "$", RegexOptions.IgnorePatternWhitespace + RegexOptions.IgnoreCase) Then
                        Effect = lEffect
                        GoTo Found
                    End If
                Next
            Next
            Say(Connection, Channel, Choose("", "Sorry " & Sender.Split("!"c)(0) & ", ") & String.Format(Choose("I don't recognise {0}.", "I don't have records of any effect named {0}.", "I don't know of any effect named {0}.", "I don't know what you mean by {0}.", "There" & Choose(" isn't an ", " isn't any ", " is no ", "'s no ") & "effect named {0}."), IRCColours.Red & Name & IRCColours.ClearFormat), SayOptions.Capitalise)
            Return

Found:
            Say(Connection, Channel, String.Format(Choose("The effect ID of {0} is {1}.", "{0} has " & Choose("effect ", "") & "ID {1}."), IRCColours.Blue & Effect.Name & "$o", IRCColours.Green & Effect.ID & "$o"), SayOptions.Capitalise)
        End If

    End Sub

    <Command({"enchantment", "enchant"}, 1, 1,
       "enchantment <name|id>",
       "Looks up an enchantment to find its name or ID.")>
    Public Sub CommandEnchantment(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim Identifier As String = args(0)
        Dim ID As Integer, Name As String

        ' Check if the identifier is a number or a name.
        If Integer.TryParse(Identifier, ID) Then
            If ID < 0 Then
                Say(Connection, Channel, Choose("", "Sorry " & Sender.Split("!"c)(0) & ", ") & Choose("that isn't a valid enchantment ID.", "the enchantment ID you entered isn't valid."), True)
                Return
            End If
        Else
            Name = Identifier
            ID = -1
        End If

        ' If the identifier is an ID, find its name.
        If ID >= 0 Then
            ' Check if there's an item with the specified ID.
            If Not Enchantments.ContainsKey(ID) Then
                Say(Connection, Channel, Choose("", "Sorry " & Sender.Split("!"c)(0) & ", ") & Choose("there's no enchantment with ID ", "there isn't any enchantment with ID ", "I don't have records of ID ", "I couldn't find any enchantment with ID ") & IRCColours.Red & ID & "$o.", SayOptions.Capitalise)
                Return
            End If

            Dim Enchantment = Enchantments(ID)
            Say(Connection, Channel, "The enchantment with ID $k12" & Enchantment.ID & "$o is $k09" & Enchantment.Name & "$o.")
        Else
            Dim Enchantment As Enchantment
            For Each lEnchantment In Enchantments.Values
                If lEnchantment.Name.ToLower.Replace(" ", "") = Name.ToLower.Replace(" ", "") Then
                    Enchantment = lEnchantment
                    GoTo Found
                End If
            Next
            For Each lEnchantment In Enchantments.Values
                For Each lRegex In lEnchantment.Nicknames
                    If Regex.IsMatch(Name.Replace(" ", ""), "^" & lRegex & "$", RegexOptions.IgnorePatternWhitespace + RegexOptions.IgnoreCase) Then
                        Enchantment = lEnchantment
                        GoTo Found
                    End If
                Next
            Next
            Say(Connection, Channel, Choose("", "Sorry " & Sender.Split("!"c)(0) & ", ") & String.Format(Choose("I don't recognise {0}.", "I don't have records of any enchantment named {0}.", "I don't know of any enchantment named {0}.", "I don't know what you mean by {0}.", "There" & Choose(" isn't an ", " isn't any ", " is no ", "'s no ") & "enchantment named {0}."), IRCColours.Red & Name & IRCColours.ClearFormat), SayOptions.Capitalise)
            Return

Found:
            Say(Connection, Channel, String.Format(Choose("The enchantment ID of {0} is {1}.", "{0} has " & Choose("enchantment ", "") & "ID {1}."), IRCColours.Blue & Enchantment.Name & "$o", IRCColours.Green & Enchantment.ID & "$o"), SayOptions.Capitalise)
        End If

    End Sub

    <Command({"reloaditems", "reloaditem", "reloaditemdb", "itemreload", "itemsreload", "itemdbreload", "itemdbparse"}, 0, 0,
     "reloaditems",
     "Reloads the item database.",
      ".reload")>
    Public Sub CommandReloadItems(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        SyncLock Problems
            Problems.Clear()
            Try
                LoadItems()
            Catch ex As FormatException
                Say(Connection, Channel, ex.Message)
                Return
            End Try
            Say(Connection, Channel, "Loaded " & Items.Count & If(Items.Count = 1, " item", " items") & " from the data file and found " & Problems.Count & If(Problems.Count = 1, " error.", " errors."))
            For i = 0 To If(Problems.Count > 5, 4, Problems.Count - 1)
                Reply(Connection, Channel, Sender, Problems(i))
            Next
            If Problems.Count > 5 Then Reply(Connection, Channel, Sender, "plus " & Problems.Count - 5 & If(Problems.Count = 6, " more error", " more errors") & "; see the logs.")
        End SyncLock
    End Sub

    Public Sub LoadItems()
        Dim ColumnIndex() As Integer = {-1, -1, -1, -1, -1}, HeaderLine As String = Nothing
        Dim LineNumber As Integer = 0

        Dim sr = My.Computer.FileSystem.OpenTextFileReader("minecraft-items.csv")

        ' Find a header.
        Do Until sr.EndOfStream
            LineNumber += 1
            Dim s = sr.ReadLine
            If s.StartsWith("#") Then
                If s.StartsWith("# ") Then Continue Do
                HeaderLine = s.Substring(1)
                Continue Do
            End If
            Exit Do
        Loop

        If HeaderLine Is Nothing Then
            ReportProblem("minecraft-items.csv", LineNumber, "I don't see a header row.")
            sr.Close()
            Throw New FormatException("Parsing failed: the data file doesn't include a header row.")
        Else
            'Parse the header.
            Dim Fields = HeaderLine.Split({","c})
            For i = 0 To UBound(Fields)
                Dim FieldIndex As Integer
                Select Case Fields(i).ToUpper
                    Case "ID"
                        FieldIndex = ItemsColumns.ColumnID
                    Case "NAME"
                        FieldIndex = ItemsColumns.ColumnName
                    Case "INTERNAL NAME", "COMMAND NAME"
                        FieldIndex = ItemsColumns.ColumnInternalName
                    Case "NICKNAMES", "REGEXES", "REGEXPS", "EXPRESSIONS"
                        FieldIndex = ItemsColumns.ColumnNicknames
                    Case "HOW TO OBTAIN", "INSTRUCTIONS"
                        FieldIndex = ItemsColumns.ColumnHowToObtain
                    Case Else
                        Continue For
                End Select

                ColumnIndex(FieldIndex) = i
            Next

            If ColumnIndex(ItemsColumns.ColumnID) = -1 Or ColumnIndex(ItemsColumns.ColumnName) = -1 Then
                ReportProblem("minecraft-items.csv", LineNumber, "The header row doesn't include an 'ID' and 'Name' column.")
                sr.Close()
                Throw New FormatException("Parsing failed: the header row is missing an 'ID' and/or 'Name' field.")
            End If
        End If

        Items.Clear()

        Do Until sr.EndOfStream
            Dim fields() As String

            LineNumber += 1
            Dim s = sr.ReadLine
            If s.Trim = "" Or s.StartsWith("#") Then Continue Do 'It's a comment, so ignore it.

            fields = s.Split(","c)
            If fields.Length < 8 Then
                Debug.Print("I found an anomaly in minecraft-items.csv, line " & LineNumber & ":" & vbCrLf & "    The line doesn't have enough fields. Andrio, if you're there, please take a look at it.")
                OutputLine("\cWHITEI found an anomaly in minecraft-items.csv, line " & LineNumber & ":" & vbCrLf & "    The line doesn't have enough fields. Andrio, if you're there, please take a look at it.\r")
                Continue Do
            End If

            Dim newItem As New Item, _Name As String
            Dim SubtypeIndex As String = 0, MaxSubtypeIndex As Integer = 0, DefaultName As String = Nothing
            Dim lines() As String
            Dim i As Integer

            If fields.Length <= ColumnIndex(ItemsColumns.ColumnID) Then
                ReportProblem("minecraft-items.csv", LineNumber, "The entry has no ID.")
                Continue Do
            End If
            If Not Integer.TryParse(fields(ColumnIndex(ItemsColumns.ColumnID)), newItem.ID) Then
                ReportProblem("minecraft-items.csv", LineNumber, "The ID is invalid.")
                Continue Do
            End If

            If fields.Length <= ColumnIndex(ItemsColumns.ColumnName) Then
                ReportProblem("minecraft-items.csv", LineNumber, "The entry has no name.")
                Continue Do
            End If
            _Name = fields(ColumnIndex(ItemsColumns.ColumnName))

            'Parse the names.
            lines = _Name.Split("&"c)
            For Each line In lines
                If line.Trim = "" Then Continue For
                If line.Contains("\") Then
                    If Not UShort.TryParse(line.Split({"\"c}, 2)(0), SubtypeIndex) Then
                        ReportProblem("minecraft-items.csv", LineNumber, "The name field contains an entry with an invalid index.")
                        Continue Do
                    Else
                        If newItem.Subtypes Is Nothing Then newItem.Subtypes = New Dictionary(Of Short, Item.Subtype)
                        If newItem.Subtypes.ContainsKey(SubtypeIndex) Then
                            ReportProblem("minecraft-items.csv", LineNumber, "The name field contains duplicate indexes.")
                        Else
                            newItem.Subtypes.Add(SubtypeIndex, New Item.Subtype(SubtypeIndex, line.Split({"\"c}, 2)(1)))
                        End If
                    End If
                Else
                    If newItem.Name <> Nothing Then
                        ReportProblem("minecraft-items.csv", LineNumber, "There is more than one default name. I'll ignore default names after the first.")
                    Else
                        newItem.Name = line
                    End If
                End If
            Next

            If fields.Length > ColumnIndex(ItemsColumns.ColumnInternalName) And ColumnIndex(ItemsColumns.ColumnInternalName) <> -1 Then newItem.CommandName = fields(ColumnIndex(ItemsColumns.ColumnInternalName))

            If ColumnIndex(ItemsColumns.ColumnNicknames) <> -1 Then
                'Parse the alternate names.
                lines = fields(ColumnIndex(ItemsColumns.ColumnNicknames)).Split("&"c)
                For Each line In lines
                    If line.Trim = "" Then Continue For
                    If line.Contains("\") Then
                        If Not UShort.TryParse(line.Split({"\"c}, 2)(0), SubtypeIndex) Then
                            ReportProblem("minecraft-items.csv", LineNumber, "The expressions field contains an entry with an invalid index.")
                            Continue Do
                        Else
                            Try
                                System.Text.RegularExpressions.Regex.Match("", line.Split({"\"c}, 2)(1))
                            Catch ex As ArgumentException
                                OutputLine("\cWHITEI found an anomaly in minecraft-items.csv, line " & LineNumber & ":" & vbCrLf & "    An regular expression is invalid: " & ex.Message & "\r")
                                Continue For
                            End Try

                            If newItem.Subtypes Is Nothing Then newItem.Subtypes = New Dictionary(Of Short, Item.Subtype)
                            If Not newItem.Subtypes.ContainsKey(SubtypeIndex) Then newItem.Subtypes.Add(SubtypeIndex, New Item.Subtype(SubtypeIndex, Nothing))
                            newItem.Subtypes(SubtypeIndex).Nicknames.Add(line.Split({"\"c}, 2)(1))
                        End If

                    Else
                        Try
                            System.Text.RegularExpressions.Regex.Match("", line)
                        Catch ex As ArgumentException
                            OutputLine("\cWHITEI found an anomaly in minecraft-items.csv, line " & LineNumber & ":" & vbCrLf & "    An regular expression is invalid: " & ex.Message & "\r")
                            Continue For
                        End Try
                        If newItem.Nicknames Is Nothing Then newItem.Nicknames = New List(Of String)
                        newItem.Nicknames.Add(line)
                    End If
                Next
            End If

            If ColumnIndex(ItemsColumns.ColumnHowToObtain) <> -1 And UBound(fields) >= ColumnIndex(ItemsColumns.ColumnHowToObtain) Then
                ' Parse the obtaining instructions.
                Dim CurrentIndex As Integer = -1, CurrentInstructionsBuilder As StringBuilder
                Dim Lines3 = fields(ColumnIndex(ItemsColumns.ColumnHowToObtain)).Split("&"c)
                i = 0
                Do Until i > UBound(Lines3)
                    CurrentInstructionsBuilder = New StringBuilder
                    For i = i To UBound(Lines3)
                        Dim Line = Lines3(i)
                        If Line.Contains("\"c) Then
                            If Not UShort.TryParse(Line.Split({"\"c}, 2)(0), SubtypeIndex) Then
                                ReportProblem("minecraft-items.csv", LineNumber, "The instructions field contains an entry with an invalid index.")
                            ElseIf SubtypeIndex <> CurrentIndex Then
                                Exit For
                            Else
                                CurrentInstructionsBuilder.AppendLine(Line.Split({"\"c}, 2)(1).Replace("`"c, ","c))
                            End If
                        ElseIf -1 <> CurrentIndex Then
                            Exit For
                        Else
                            CurrentInstructionsBuilder.AppendLine(Line.Replace("`"c, ","c))
                        End If
                    Next

                    If CurrentIndex = -1 Then
                        newItem.HowToObtain &= CurrentInstructionsBuilder.ToString
                    Else
                        If newItem.Subtypes Is Nothing Then newItem.Subtypes = New Dictionary(Of Short, Item.Subtype)
                        If Not newItem.Subtypes.ContainsKey(CurrentIndex) Then newItem.Subtypes.Add(CurrentIndex, New Item.Subtype(CurrentIndex, Nothing))
                        newItem.Subtypes(CurrentIndex).HowToObtain &= CurrentInstructionsBuilder.ToString
                    End If
                    i += 1
                Loop
            End If

            If newItem.Name = Nothing Then
                For i = 0 To newItem.Subtypes.Count - 1
                    If newItem.Subtypes(i).Name <> Nothing Then
                        newItem.Name = newItem.Subtypes(i).Name
                        Exit For
                    End If
                Next
            End If

            If newItem.Name = Nothing Then
                ReportProblem("minecraft-items.csv", LineNumber, "It doesn't have a name.")
            Else
                Items.Add(newItem.ID, newItem)
            End If
        Loop

        sr.Close()
    End Sub

    <Command({"reloadeffects", "reloadeffect", "reloadeffectdb", "effectreload", "effectsreload", "effectdbreload", "effectdbparse",
            "reloadeffs", "reloadeff", "reloadeffdb", "effreload", "effsreload", "effdbreload", "effdbparse"}, 0, 0,
  "reloadeffects",
  "Reloads the effect database.",
   ".reload")>
    Public Sub CommandReloadEffects(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        SyncLock Problems
            Problems.Clear()
            LoadEffects()
            Say(Connection, Channel, "Loaded " & Effects.Count & If(Items.Count = 1, " effect", " effects") & " from the data file and found " & Problems.Count & If(Problems.Count = 1, " error.", " errors."))
            For i = 0 To If(Problems.Count > 5, 4, Problems.Count - 1)
                Reply(Connection, Channel, Sender, Problems(i))
            Next
            If Problems.Count > 5 Then Reply(Connection, Channel, Sender, "plus " & Problems.Count - 5 & If(Problems.Count = 6, " more error", " more errors") & "; see the logs.")
        End SyncLock
    End Sub

    Public Sub LoadEffects()
        Effects.Clear()
        Dim Problems As New List(Of String)

        Dim sr = My.Computer.FileSystem.OpenTextFileReader("minecraft-effects.csv")
        Do Until sr.EndOfStream
            Dim LineNumber As Integer
            LineNumber += 1
            Dim s = sr.ReadLine
            If s.Trim = "" Or s.StartsWith("#") Then Continue Do 'It's a comment, so ignore it.
            Dim fields = s.Split(","c) ' ID, Category, Name, Nicknames, Description

            If fields.Length < 5 Then
                ReportProblem("minecraft-effects.csv", LineNumber, "The line doesn't have enough fields.")
                Continue Do
            End If

            Dim newData As New Effect
            If Not UShort.TryParse(fields(0).Trim, newData.ID) Then
                ReportProblem("minecraft-effects.csv", LineNumber, "The ID isn't a valid number.")
                Continue Do
            End If

            newData.Name = fields(2)
            newData.Nicknames = If(fields(3) = "", {}, fields(3).Split("&"c))
            newData.Description = fields(4).Replace("&", vbCrLf)

            Effects.Add(newData.ID, newData)
        Loop

        sr.Close()
    End Sub

    <Command({"reloadenchantments", "reloadenchantment", "reloadenchantmentdb", "enchantmentreload", "enchantmentsreload", "enchantmentdbreload", "enchantmentdbparse",
            "reloadenchants", "reloadenchant", "reloadenchantdb", "enchantreload", "enchantsreload", "enchantdbreload", "enchantdbparse"}, 0, 0,
  "reloadeffects",
  "Reloads the enchantment database.",
   ".reload")>
    Public Sub CommandReloadEnchantments(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        SyncLock Problems
            Problems.Clear()
            LoadEnchantments()
            Say(Connection, Channel, "Loaded " & Enchantments.Count & If(Items.Count = 1, " enchantment", " enchantments") & " from the data file and found " & Problems.Count & If(Problems.Count = 1, " error.", " errors."))
            For i = 0 To If(Problems.Count > 5, 4, Problems.Count - 1)
                Reply(Connection, Channel, Sender, Problems(i))
            Next
            If Problems.Count > 5 Then Reply(Connection, Channel, Sender, "plus " & Problems.Count - 5 & If(Problems.Count = 6, " more error", " more errors") & "; see the logs.")
        End SyncLock
    End Sub

    Public Sub LoadEnchantments()
        Enchantments.Clear()
        Dim Problems As New List(Of String)

        Dim sr = My.Computer.FileSystem.OpenTextFileReader("minecraft-enchantments.csv")
        Do Until sr.EndOfStream
            Dim LineNumber As Integer
            LineNumber += 1
            Dim s = sr.ReadLine
            If s.Trim = "" Or s.StartsWith("#") Then Continue Do 'It's a comment, so ignore it.
            Dim fields = s.Split(","c) ' ID, Category, Name, Nicknames, Description

            If fields.Length < 6 Then
                ReportProblem("minecraft-enchantments.csv", LineNumber, "The line doesn't have enough fields.")
                Continue Do
            End If

            Dim newData As New Enchantment
            If Not UShort.TryParse(fields(0).Trim, newData.ID) Then
                ReportProblem("minecraft-enchantments.csv", LineNumber, "The ID isn't a valid number.")
                Continue Do
            End If

            newData.Name = fields(2)
            newData.Nicknames = If(fields(4) = "", {}, fields(4).Split("&"c))
            newData.Description = fields(5).Replace("&", vbCrLf)

            Enchantments.Add(newData.ID, newData)
        Loop

        sr.Close()
    End Sub

    <Command({"reloadpotions", "reloadpotion", "reloadpotiondb", "potionreload", "potionsreload", "potiondbreload", "potiondbparse",
            "reloadpots", "reloadpot", "reloadpotdb", "potreload", "potsreload", "potdbreload", "potdbparse"}, 0, 0,
  "reloadeffects",
  "Reloads the potion database.",
   ".reload")>
    Public Sub CommandReloadPotions(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        SyncLock Problems
            Problems.Clear()
            LoadPotions()
            Say(Connection, Channel, "Loaded " & Potions.Count & If(Items.Count = 1, " potion", " potions") & " from the data file and found " & Problems.Count & If(Problems.Count = 1, " error.", " errors."))
            For i = 0 To If(Problems.Count > 5, 4, Problems.Count - 1)
                Reply(Connection, Channel, Sender, Problems(i))
            Next
            If Problems.Count > 5 Then Reply(Connection, Channel, Sender, "plus " & Problems.Count - 5 & If(Problems.Count = 6, " more error", " more errors") & "; see the logs.")
        End SyncLock
    End Sub

    Public Sub LoadPotions()
        Potions.Clear()
        Dim Problems As New List(Of String)

        Dim sr = My.Computer.FileSystem.OpenTextFileReader("minecraft-potions.csv")
        Do Until sr.EndOfStream
            Dim LineNumber As Integer
            LineNumber += 1
            Dim s = sr.ReadLine
            If s.Trim = "" Or s.StartsWith("#") Then Continue Do 'It's a comment, so ignore it.
            Dim fields = s.Split(","c) ' ID, Category, Name, Nicknames, Description

            If fields.Length < 8 Then
                ReportProblem("minecraft-potions.csv", LineNumber, "The line doesn't have enough fields.")
                Continue Do
            End If

            Dim newData As New Potion
            If Not UShort.TryParse(fields(0).Trim, newData.Metadata) Then
                ReportProblem("minecraft-potions.csv", LineNumber, "The ID isn't a valid number.")
                Continue Do
            End If

            newData.Name = fields(1)
            If Not UShort.TryParse(fields(2).Trim, newData.Boosts) Then
                ReportProblem("minecraft-potions.csv", LineNumber, "The supported boosts isn't a valid number.")
                Continue Do
            End If

            If Not UInteger.TryParse(fields(3).Trim, newData.Duration) Then
                ReportProblem("minecraft-potions.csv", LineNumber, "The duration isn't a valid number.")
                Continue Do
            End If

            newData.Nicknames = If(fields(4) = "", {}, fields(4).Split("&"c))
            newData.Description = fields(6).Replace("&", vbCrLf)

            Potions.Add(newData.Metadata, newData)
        Loop

        sr.Close()
    End Sub

    Private Sub ReportProblem(ByVal File As String, ByVal LineNumber As Integer, ByVal Message As String)
        OutputLine("\cWHITEI found a problem with " & File & If(LineNumber <> 0, ", line " & LineNumber, "") & ":" & vbCrLf & "    " & Message & "\r")
        Problems.Add(If(LineNumber <> 0, "Line " & LineNumber & ": ", "") & Message)
    End Sub

    <Command({"superflat"}, 0, 0,
   "superflat",
   "Helps you to generate a superflat preset code. You can tell me what settings you want.", Nothing)>
    Public Sub CommandSuperflat(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim t As New Threading.Thread(New Threading.ParameterizedThreadStart(AddressOf RequestSuperflatInfo2))
        t.Start({Connection, Channel, Sender.Split("!"c)(0), If(args.ElementAtOrDefault(0), 0)})
    End Sub

    Public Sub RequestSuperflatInfo2(ByVal args() As Object)
        Dim Connection As IRCConnection = args(0)
        Dim Channel As String = args(1)
        Dim Nickname As String = args(2)

        ' Superflat descriptor format version 2 - Minecraft 1.4
        ' There currently isn't any other known version of the format than 2.
        Dim Version As String = "2"
        Dim Layers As New List(Of String)
        Dim Biome As Integer = 0
        Dim Features As New List(Of String)

        ' Block selection
        Dim CurrentLayer As Integer = 0
        Do
            Say(Connection, Channel, Nickname & ", what block type do you want for Y = " & CurrentLayer & "? If you're done, say $k11end$o.")

            Dim Response As String = WaitForMessage(Connection, Channel, Nickname, 60)
            If Response IsNot Nothing Then
                Response = Response.ToLower.Trim
            End If

            Dim Count As Integer = -1, Data As String, ID As Integer, Metadata As Integer

            If Response Is Nothing Or Response = "cancel" Then
                Return
            End If
            If {"end", "finish", "finished", "imdone", "imfinished", "thatsall", "thatsit"}.Contains(Response.ToLower.Replace(" ", "").Replace("'", "").Replace("_", "")) Then
                Exit Do
            End If

            Dim match As Text.RegularExpressions.Match
            match = Text.RegularExpressions.Regex.Match(Response, "^(?<Count>\d+)\s*(x|layers?( of)?| )\s*(?<Data>.*)$")
            If match.Success Then
                Count = match.Groups("Count").Value
                Data = match.Groups("Data").Value
            Else
                match = Text.RegularExpressions.Regex.Match(Response, "^(?<Data>.*)\s*(x| )\s*(?<Count>\d+)$")
                If match.Success Then
                    Count = match.Groups("Count").Value
                    Data = match.Groups("Data").Value
                Else
                    Data = Response
                End If
            End If


            If Count = -1 Then
                Do
                    Say(Connection, Channel, Nickname & ", how many layers of that do you want?")

                    Dim Response2 As String = WaitForMessage(Connection, Channel, Nickname, 60)
                    If Response2 IsNot Nothing Then
                        Response2 = Response2.ToLower.Trim
                    End If

                    If Response2 Is Nothing Or Response2 = "cancel" Then
                        Return
                    End If

                    If Not Text.RegularExpressions.Regex.IsMatch(Response2, "^\d+$") Then
                        Say(Connection, Channel, "Sorry " & Nickname & ", $k04" & Count & "$o isn't a valid number of layers. Please use digits.")
                        Continue Do
                    End If

                    Count = Response2

                    Exit Do
                Loop
            End If

            If Count < 1 Then
                Say(Connection, Channel, Nickname & ", $k04" & Count & "$o isn't a valid number of layers.")
                Continue Do
            ElseIf Count > 256 - CurrentLayer Then
                Say(Connection, Channel, Nickname & ", $k04" & Count & "$o layers won't fit.")
                Continue Do
            Else

                match = Text.RegularExpressions.Regex.Match(Data, "^(?<ID>\d+)(:(?<Metadata>\d+))?$")
                If match.Success Then
                    ID = match.Groups("ID").Value
                    Metadata = If(match.Groups("Metadata").Success, match.Groups("Metadata").Value, 0)

                    If ID < 0 Then
                        Say(Connection, Channel, Choose("", "Sorry " & Nickname & ", ") & Choose("that isn't a valid item ID.", "he item ID you entered isn't valid."), True)
                        Continue Do
                    ElseIf Not Items.ContainsKey(ID) Then
                        Say(Connection, Channel, Choose("", "Sorry " & Nickname & ", ") & Choose("there's no item with ID ", "there isn't any item with ID ", "I don't have records of ID ", "I couldn't find any item with ID ") & IRCColours.Red & ID & "$o.", SayOptions.Capitalise)
                        Continue Do
                    End If
                Else
                    Dim Output = GetItemID(Data)

                    If Output(0) >= 0 Then
                        If Output(1) = -1 Then
                            ID = Output(0)
                            Metadata = 0
                        Else
                            ID = Output(0)
                            Metadata = Output(1)
                        End If
                    Else
                        Say(Connection, Channel, Choose("", "Sorry " & Nickname & ", ") & String.Format(Choose("I don't recognise {0}.", "I don't have records of any item named {0}.", "I don't know of any item named {0}.", "I don't know what you mean by {0}.", "There" & Choose(" isn't an ", " isn't any ", " is no ", "'s no ") & "item named {0}."), IRCColours.Red & Data & IRCColours.ClearFormat), SayOptions.Capitalise)
                        Continue Do
                    End If
                End If

                CurrentLayer += Count
                Layers.Add(If(Count = 1, "", Count & "x") & ID & If(Metadata <> 0, ":" & Metadata, ""))
            End If

        Loop

        ' Biome selection
        Say(Connection, Channel, Nickname & ", what biome do you want your world to be?")

        Do
            Dim Response As String = WaitForMessage(Connection, Channel, Nickname, 60)
            If Response IsNot Nothing Then Response = Response.ToLower.Replace(" ", "")
            If Response Is Nothing Or Response = "cancel" Then Return

            Select Case Response
                Case "ocean", "sea", "water"
                    Biome = 0
                Case "plains", "plain"
                    Biome = 1
                Case "desert"
                    Biome = 2
                Case "extremehills", "extremehill", "extremehilly", "hills", "hill", "hilly"
                    Biome = 3
                Case "forest"
                    Biome = 4
                Case "taiga", "snow", "snowy", "snowbiome"
                    Biome = 5
                Case "swampland", "swamp"
                    Biome = 6
                Case "river", "stream"
                    Biome = 7
                Case "nether", "hell"
                    Biome = 8
                Case "end", "sky", "skylands", "skyland", "theend"
                    Biome = 9
                Case "frozenocean", "frozensea", "frozenwater", "iceocean", "icesea"
                    Biome = 10
                Case "frozenriver", "frozenstream", "iceriver", "icestream"
                    Biome = 11
                Case "iceplains", "iceplain", "ice", "icy", "icebiome", "icybiome", "frozen"
                    Biome = 12
                Case "icemountains", "icymountains", "frozenmountains"
                    Biome = 13
                Case "mushroom", "mushroombiome", "mushroomisland"
                    Biome = 14
                Case "mushroomshore", "mushroomislandshore", "mushroombeach", "mushroomislandbeach"
                    Biome = 15
                Case "beach", "shore"
                    Biome = 16
                Case "deserthills", "hillydesert", "deserthill", "hilldesert"
                    Biome = 17
                Case "foresthills", "hillyforest", "foresthill", "hillforest"
                    Biome = 18
                Case "taigahills", "hillytaiga", "taigahill", "hilltaiga", "snowhills", "hillysnow", "hillysnowbiome", "snowhill", "hillsnow"
                    Biome = 19
                Case "extremehillsedge", "extremehilledge", "hillsedge", "hilledge"
                    Biome = 20
                Case "jungle"
                    Biome = 21
                Case "junglehills", "hillyjungle", "junglehill", "hilljungle"
                    Biome = 22
                Case Else
                    Dim i As Short
                    If Not Short.TryParse(Response, i) Then
                        Say(Connection, Channel, "Sorry " & Nickname & ", I don't recognise that.")
                        Continue Do
                    ElseIf i >= 0 And i <= 22 Then
                        Biome = i
                    Else
                        Say(Connection, Channel, "Sorry " & Nickname & ", that isn't a valid biome ID. Use a name or an ID between 0 and 22.")
                        Continue Do
                    End If
            End Select
        Loop While False

        ' Structures
        Dim AvailableStructures As New List(Of String), AvailableStructuresDisplay As New List(Of String)

        If Biome = 1 Or Biome = 2 Then AvailableStructures.Add("village")
        AvailableStructures.Add("mineshaft")
        If {2, 17, 4, 18, 3, 20, 6, 5, 12, 13, 21, 22}.Contains(Biome) Then AvailableStructures.Add("stronghold")
        If {2, 17, 21, 22, 6}.Contains(Biome) Then AvailableStructures.Add("biome_1")
        AvailableStructures.Add("dungeon")
        AvailableStructures.Add("decoration")
        AvailableStructures.Add("lake")
        AvailableStructures.Add("lava_lake")

        If Biome = 1 Or Biome = 2 Then AvailableStructuresDisplay.Add("village")
        AvailableStructuresDisplay.Add("mineshaft")
        If {2, 17, 4, 18, 3, 20, 6, 5, 12, 13, 21, 22}.Contains(Biome) Then AvailableStructuresDisplay.Add("stronghold")
        If Biome = 2 Or Biome = 17 Then AvailableStructuresDisplay.Add("desert temple")
        If Biome = 21 Or Biome = 22 Then AvailableStructuresDisplay.Add("jungle temple")
        If Biome = 6 Then AvailableStructuresDisplay.Add("witch hut")
        AvailableStructuresDisplay.Add("dungeon")
        AvailableStructuresDisplay.Add("decoration")
        AvailableStructuresDisplay.Add("lake")
        AvailableStructuresDisplay.Add("lava lake")

        Say(Connection, Channel, "The following structures can be generated in this biome: $k12" & String.Join("$o, $k12", AvailableStructuresDisplay) & "$o.")
        Say(Connection, Channel, Nickname & ", which of these do you want to include?")

        Do
            Dim Response As String = WaitForMessage(Connection, Channel, Nickname, 60)
            If Response IsNot Nothing Then Response = Response.ToLower.Replace(" ", "").TrimEnd("s"c).Replace("s ", " ").Replace("'", "")
            If Response Is Nothing Or Response = "cancel" Then Return

            Dim StructureName As String, StructureDisplayName As String = "something", Remove As Boolean

            If Response.StartsWith("remove ") Or Response.StartsWith("cancel ") Or Response.StartsWith("delete ") Or Response.StartsWith("undo ") Then
                StructureName = Response.Split({" "c}, 2)(1).TrimEnd("*"c)
                Remove = True
            Else
                StructureName = Response
                Remove = False
            End If

            Select Case StructureName
                Case "village"
                    StructureName = "village"
                    StructureDisplayName = "villages"
                Case "mineshaft", "abandonedmineshaft", "abandonedmine", "mine"
                    StructureName = "mineshaft"
                    StructureDisplayName = "abandoned mine shafts"
                Case "stronghold"
                    StructureName = "stronghold"
                    StructureDisplayName = "strongholds"
                Case "deserttemple", "jungletemple", "witchhut", "witch", "hut", "temple", "biome1", "biome_1"
                    StructureName = "biome_1"
                    If Biome = 2 Or Biome = 17 Then StructureDisplayName = "desert temples"
                    If Biome = 21 Or Biome = 22 Then StructureDisplayName = "jungle temples"
                    If Biome = 6 Then StructureDisplayName = "witch huts"
                Case "dungeon"
                    StructureName = "dungeon"
                    StructureDisplayName = "dungeons"
                Case "decoration"
                    StructureName = "decoration"
                    StructureDisplayName = "decorations"
                Case "lake", "water lake", "pond"
                    StructureName = "lake"
                    StructureDisplayName = "lakes"
                Case "lavalake", "lava_lake"
                    StructureName = "lava_lake"
                    StructureDisplayName = "lava lakes"
                Case "no", "nothing", "none", "end", "finish", "finished", "imdone", "imfinished", "thatsall", "thatsit"
                    StructureName = Nothing
                Case "yes"
                    Reply(Connection, Channel, Nickname, "Well, which structure?")
                    Continue Do
                Case Else
                    StructureName = "?"
            End Select

            If StructureName = "?" Then
                Say(Connection, Channel, "Sorry " & Nickname & ", I don't recognise that.")
                Continue Do
            ElseIf StructureName = Nothing Then
                Exit Do
            ElseIf Remove Then
                For i = 0 To Features.Count - 1
                    If Features(i).Split("("c)(0) = StructureName Then
                        Features.RemoveAt(i)
                        Say(Connection, Channel, "Removed $k6" & StructureDisplayName & "$o. Anything else to add?")
                        Continue Do
                    End If
                Next
                Say(Connection, Channel, "You haven't selected $k11" & StructureDisplayName & "$o.")
            Else
                For i = 0 To Features.Count - 1
                    If Features(i).Split("("c)(0) = StructureName Then
                        If Features(i) = StructureName Then
                            Say(Connection, Channel, "You've already selected $k12" & StructureDisplayName & "$o. Anything else to add?")
                            Continue Do
                        Else
                            Features.RemoveAt(i)
                            Exit For
                        End If
                    End If
                Next

                Features.Add(StructureName)
                Say(Connection, Channel, "Added $k9" & StructureDisplayName & "$o. Anything else to add?")
            End If
        Loop

        Say(Connection, Channel, Nickname & ", your superflat preset code is:$k9 " & String.Join(";", Version, String.Join(",", Layers), Biome, String.Join(",", Features)))
    End Sub

    Public Sub AnalyseSuperflatInfo2(ByVal Code As String)
        Dim Version As String
        Dim Layers As String
        Dim Biome As String
        Dim Features As String

        Version = Code.Split(";"c).ElementAtOrDefault(0)
        Layers = Code.Split(";"c).ElementAtOrDefault(1)
        Biome = Code.Split(";"c).ElementAtOrDefault(2)
        Features = Code.Split(";"c).ElementAtOrDefault(3)

        If Version = Nothing Then

        End If
    End Sub
End Class

