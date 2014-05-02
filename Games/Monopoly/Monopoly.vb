Imports VBot

Public Class MonopolyPlugin
    Inherits Plugin

    Public Overrides ReadOnly Property Name As String
        Get
            Return "Monopoly game"
        End Get
    End Property

    Public Overrides Function Help(ByVal Topic As String, ByVal IsMajorChannel As Boolean) As String
        Select Case If(Topic, "").ToLower
        End Select
    End Function

#Region "Subclasses"
    Public Class Board
        Public Name As String
        Public Spaces() As Space
        Public ChanceCards() As Card
        Public CommunityChestCards() As Card
        Public ChanceCardsRemaining As Short
        Public CommunityChestCardsRemaining As Short
    End Class

    Public Class Space
        Public Index As Short
        Public Type As SpaceType
        Public Name As String

        Public Sub New(ByVal Index As Short, ByVal Type As SpaceType, ByVal Name As String)
            Me.Index = Index
            Me.Type = Type
            Me.Name = Name
        End Sub
    End Class

    Public Enum SpaceType
        PropertySpace
        RailroadSpace
        UtilitySpace
        ChanceSpace
        CommunityChestSpace
        GoSpace
        JailSpace
        FreeParkingSpace
        GoToJailSpace
        IncomeTaxSpace
        SuperTaxSpace
    End Enum

    ''' <summary>The state of the game.</summary>
    Public Enum TurnState
        ''' <summary>A player is preparing to roll the dice.</summary>
        Rolling
        ''' <summary>A player is preparing to roll the dice to get out of jail.</summary>
        RollingJail
        ''' <summary>A player is preparing to buy a property.</summary>
        Buying
        ''' <summary>An auction is in progress.</summary>
        Auction
        ''' <summary>A player is rolling to determine rent on a utility.</summary>
        RollingRent
        ''' <summary>A player is rolling to determine increased rent from a chance card.</summary>
        RollingRentChance
        ''' <summary>A player is deciding which income tax option to take.</summary>
        IncomeTax
        ''' <summary>A player is finishing their turn.</summary>
        EndTurn
    End Enum

    Public MustInherit Class OwnableSpace
        Inherits Space

        Public Owner As Short = -1
        Public Price As Integer
        Public MustOverride ReadOnly Property Rent As Integer
        Public IsMortgaged As Boolean

        Public Sub New(ByVal Index As Short, ByVal Type As SpaceType, ByVal Name As String)
            MyBase.New(Index, Type, Name)
        End Sub
    End Class

    Public Class PropertySpace
        Inherits OwnableSpace

        Public District As Short
        Public ImprovementLevel As Short
        Public ImprovementCost As Integer
        Public RentAmounts(4) As Integer

        Public Overrides ReadOnly Property Rent As Integer
            Get
                ' TODO: Double rent on dominations if the property is unimproved.
                Return RentAmounts(ImprovementLevel)
            End Get
        End Property

        Public Sub New(ByVal Index As Short, ByVal Name As String, ByVal District As Short, ByVal Price As Integer, ByVal ImprovementCost As Integer, ByVal Rent As Integer, ByVal Rent1House As Integer, ByVal Rent2Houses As Integer, ByVal Rent3Houses As Integer, ByVal Rent4Houses As Integer, ByVal RentHotel As Integer)
            MyBase.New(Index, SpaceType.PropertySpace, Name)
            Me.District = District
            Me.Price = Price
            Me.ImprovementCost = ImprovementCost
            Me.RentAmounts = {Rent, Rent1House, Rent2Houses, Rent3Houses, Rent4Houses, RentHotel}
        End Sub
        Public Sub New(ByVal Index As Short, ByVal Name As String, ByVal District As Short, ByVal Price As Integer)
            MyClass.New(Index, Name, District, Price, 0, Price, 0, 0, 0, 0, 0)
        End Sub
    End Class

    Public Class RailroadSpace
        Inherits OwnableSpace
        Public Overrides ReadOnly Property Rent As Integer
            Get
                ' TODO: Finish him!
                Return 50
            End Get
        End Property

        Public Sub New(ByVal Index As Short, ByVal Name As String, ByVal Price As Integer)
            MyBase.New(Index, SpaceType.RailroadSpace, Name)
            Me.Price = Price
        End Sub
        Public Sub New(ByVal Index As Short, ByVal Name As String)
            MyClass.New(Index, Name, 0)
        End Sub
    End Class

    Public Class UtilitySpace
        Inherits OwnableSpace
        Public Overrides ReadOnly Property Rent As Integer
            Get
                ' TODO: Finish him!
                Return 4
            End Get
        End Property

        Public Sub New(ByVal Index As Short, ByVal Name As String, ByVal Price As Integer)
            MyBase.New(Index, SpaceType.UtilitySpace, Name)
            Me.Price = Price
        End Sub
    End Class

    Public Class Card
        Public IsCommunityChest As Boolean
        Public Text As String
        Public Subtext As String
        Public Delegate Sub CardAction(ByVal Game As Game)
        Public Action As CardAction
        Public IsTaken As Boolean

        Public Sub New(ByVal IsCommunityChest As Boolean, ByVal Text As String, ByVal Subtext As String, ByVal Action As CardAction)
            Me.IsCommunityChest = IsCommunityChest
            Me.Text = Text
            Me.Subtext = Subtext
            Me.Action = Action
        End Sub
    End Class

    Public Class Game
        Public Connection As IRCConnection
        Public Channel As String
        Public Players() As Player = {Nothing, Nothing, Nothing, Nothing}
        Public PlayerCount As Short
        Public IsOpen As Boolean = True
        Public IsJunior As Boolean

        Public Turn As Short
        Public TurnNumber As Short
        Public TurnState As TurnState
        Public RollCount As Short
        Public Doubles As Boolean

        Public AuctionProperties() As Object
        Public AuctionProperty As Short
        Public AuctionBid As Integer
        Public AuctionBidder As Short
        Public AuctionTimer As Timers.Timer
        Public AuctionTime As Short

        Public Board As Board
        Public Dice As New Random

        Public HousesRemaining As Short = 32
        Public HotelsRemaining As Short = 12
        Public FreeParkingBounty As Integer
    End Class

    ''' <summary>Represents a player in the Monopoly game.</summary>
    Public Class Player
        ''' <summary>The player's index</summary>
        Public Index As Short
        ''' <summary>The player's name</summary>
        Public Name As String

        ''' <summary>The index of the space the player is on</summary>
        Public Location As Short
        ''' <summary>The amount of cash the player is holding</summary>
        Public Cash As Integer = 1500
        ''' <summary>The player's net worth</summary>
        Public Worth As Integer = 1500

        ''' <summary>The properties the player owns.</summary>
        Public Properties As New List(Of OwnableSpace)
        ''' <summary>Bit flags: 1 if the player holds the orange card; 2 if the player holds the yellow card.</summary>
        Public JailFreeCards As Byte

        ''' <summary>If -1, the player is not in jail. Otherwise, this is the number of turns the player has spent in jail.</summary>
        Public JailTime As Short = -1

        ''' <summary>The list of properties on which the player wants to build a house.</summary>
        Public HousesRequested As New List(Of Short)
        ''' <summary>The list of properties on which the player wants to build a hotel.</summary>
        Public HotelsRequested As New List(Of Short)

        Public Sub New(ByVal Index As Short, ByVal Name As String)
            Me.Index = Index
            Me.Name = Name
        End Sub
    End Class

    Public Games As New Dictionary(Of String, Game)(StringComparer.OrdinalIgnoreCase)
#End Region

    Public Function InitialiseBoardUK() As Board
        ' Initialise the board.
        Dim Board = New Board
        Board.Name = "UK"
        ReDim Board.Spaces(39)

        Board.Spaces(0) = New Space(0, SpaceType.GoSpace, "Go")
        Board.Spaces(1) = New PropertySpace(1, ChrW(3) & "5Old Kent Road" & ChrW(3) & "12", 0, 60, 50, 2, 10, 30, 90, 160, 250)
        Board.Spaces(2) = New Space(2, SpaceType.CommunityChestSpace, "Community Chest")
        Board.Spaces(3) = New PropertySpace(3, ChrW(3) & "5Whitechapel Road" & ChrW(3) & "12", 0, 60, 50, 4, 20, 60, 180, 320, 450)
        Board.Spaces(4) = New Space(4, SpaceType.IncomeTaxSpace, "Income Tax")
        Board.Spaces(5) = New RailroadSpace(5, "Kings Cross Station", 200)
        Board.Spaces(6) = New PropertySpace(6, ChrW(3) & "11The Angel Islington" & ChrW(3) & "12", 1, 100, 50, 6, 30, 90, 270, 400, 550)
        Board.Spaces(7) = New Space(7, SpaceType.ChanceSpace, "Chance")
        Board.Spaces(8) = New PropertySpace(8, ChrW(3) & "11Euston Road" & ChrW(3) & "12", 1, 100, 50, 6, 30, 90, 270, 400, 550)
        Board.Spaces(9) = New PropertySpace(9, ChrW(3) & "11Pentonville Road" & ChrW(3) & "12", 1, 120, 50, 8, 40, 100, 300, 450, 600)
        Board.Spaces(10) = New Space(10, SpaceType.JailSpace, "Jail")
        Board.Spaces(11) = New PropertySpace(11, ChrW(3) & "13Pall Mall" & ChrW(3) & "12", 2, 140, 100, 10, 50, 150, 450, 625, 750)
        Board.Spaces(12) = New UtilitySpace(12, "Electric Company", 150)
        Board.Spaces(13) = New PropertySpace(13, ChrW(3) & "13Whitehall" & ChrW(3) & "12", 2, 140, 100, 10, 50, 150, 450, 625, 750)
        Board.Spaces(14) = New PropertySpace(14, ChrW(3) & "13Northumberland Avenue" & ChrW(3) & "12", 2, 160, 100, 12, 60, 180, 500, 700, 900)
        Board.Spaces(15) = New RailroadSpace(15, "Marylebone Station", 200)
        Board.Spaces(16) = New PropertySpace(16, ChrW(3) & "7Bow Street" & ChrW(3) & "12", 3, 180, 100, 14, 70, 200, 550, 750, 950)
        Board.Spaces(17) = New Space(17, SpaceType.CommunityChestSpace, "Community Chest")
        Board.Spaces(18) = New PropertySpace(18, ChrW(3) & "7Marlborough Street" & ChrW(3) & "12", 3, 180, 100, 14, 70, 200, 550, 750, 950)
        Board.Spaces(19) = New PropertySpace(19, ChrW(3) & "7Vine Street" & ChrW(3) & "12", 3, 200, 100, 16, 80, 220, 600, 800, 1000)
        Board.Spaces(20) = New Space(20, SpaceType.FreeParkingSpace, "Free Parking")
        Board.Spaces(21) = New PropertySpace(21, ChrW(3) & "4Strand" & ChrW(3) & "12", 4, 220, 150, 18, 90, 250, 700, 875, 1050)
        Board.Spaces(22) = New Space(22, SpaceType.ChanceSpace, "Chance")
        Board.Spaces(23) = New PropertySpace(23, ChrW(3) & "4Fleet Street" & ChrW(3) & "12", 4, 220, 150, 18, 90, 250, 700, 875, 1050)
        Board.Spaces(24) = New PropertySpace(24, ChrW(3) & "4Trafalgar Square" & ChrW(3) & "12", 4, 240, 150, 20, 100, 300, 750, 925, 1100)
        Board.Spaces(25) = New RailroadSpace(25, "Fenchurch Street Station", 200)
        Board.Spaces(26) = New PropertySpace(26, ChrW(3) & "8Leicester Square" & ChrW(3) & "12", 5, 260, 150, 22, 110, 330, 800, 975, 1150)
        Board.Spaces(27) = New PropertySpace(27, ChrW(3) & "8Coventry Street" & ChrW(3) & "12", 5, 260, 150, 22, 110, 330, 800, 975, 1150)
        Board.Spaces(28) = New UtilitySpace(28, "Water Works", 150)
        Board.Spaces(29) = New PropertySpace(29, ChrW(3) & "8Piccadilly" & ChrW(3) & "12", 5, 280, 150, 24, 120, 360, 850, 1025, 1200)
        Board.Spaces(30) = New Space(30, SpaceType.GoToJailSpace, "Go to Jail")
        Board.Spaces(31) = New PropertySpace(31, ChrW(3) & "9Regent Street" & ChrW(3) & "12", 6, 300, 200, 26, 130, 390, 900, 1100, 1275)
        Board.Spaces(32) = New PropertySpace(32, ChrW(3) & "9Oxford Street" & ChrW(3) & "12", 6, 300, 200, 26, 130, 390, 900, 1100, 1275)
        Board.Spaces(33) = New Space(33, SpaceType.CommunityChestSpace, "Community Chest")
        Board.Spaces(34) = New PropertySpace(34, ChrW(3) & "9Bond Street" & ChrW(3) & "12", 6, 320, 200, 28, 150, 450, 1000, 1200, 1400)
        Board.Spaces(35) = New RailroadSpace(35, "Liverpool Street Station", 200)
        Board.Spaces(36) = New Space(36, SpaceType.ChanceSpace, "Chance")
        Board.Spaces(37) = New PropertySpace(37, ChrW(3) & "12Park Lane", 7, 350, 200, 35, 175, 500, 1100, 1300, 1500)
        Board.Spaces(38) = New Space(38, SpaceType.SuperTaxSpace, "Super Tax")
        Board.Spaces(39) = New PropertySpace(39, ChrW(3) & "12Mayfair", 7, 400, 200, 50, 200, 600, 1400, 1700, 2000)

        ReDim Board.ChanceCards(15)
        Board.ChanceCardsRemaining = 16
        Board.ChanceCards(0) = New Card(False, "Advance to Go.", "Collect $200", Nothing)
        Board.ChanceCards(1) = New Card(False, "The bank pays you a dividend of $50.", Nothing, Nothing)
        Board.ChanceCards(2) = New Card(False, "Go back 3 spaces.", Nothing, Nothing)
        Board.ChanceCards(3) = New Card(False, "Advance to the nearest utility.", "If unowned, you may buy it from the bank. If owned, roll the dice and pay the owner ten times the amount rolled.", Nothing)
        Board.ChanceCards(4) = New Card(False, "Go directly to jail.", "Do not pass Go; do not collect $200.", Nothing)
        Board.ChanceCards(5) = New Card(False, "Pay poor tax of $15.", Nothing, Nothing)
        Board.ChanceCards(6) = New Card(False, "Advance to Pall Mall.", "If you pass Go, collect $200.", Nothing)
        Board.ChanceCards(7) = New Card(False, "You have been elected chairman of the board. Pay each player $50.", Nothing, Nothing)
        Board.ChanceCards(8) = New Card(False, "Advance to the nearest railroad and pay the owner twice the rent to which they would otherwise be entitled.", "If the railroad is not owned, you may buy it from the bank.", Nothing)
        Board.ChanceCards(9) = New Card(False, "Take a ride from Kings Cross.", "If you pass Go, collect $200.", Nothing)
        Board.ChanceCards(10) = New Card(False, "Advance to the nearest railroad and pay the owner twice the rent to which they would otherwise be entitled.", "If the railroad is not owned, you may buy it from the bank.", Nothing)
        Board.ChanceCards(11) = New Card(False, "Take a walk on the board walk. Advance to Mayfair.", Nothing, Nothing)
        Board.ChanceCards(12) = New Card(False, "Your building loan matures. Collect $150.", Nothing, Nothing)
        Board.ChanceCards(13) = New Card(False, "Advance to Trafalgar Square.", Nothing, Nothing)
        Board.ChanceCards(14) = New Card(False, "Get out of jail free!", "This card may be kept until needed or sold.", Nothing)
        Board.ChanceCards(15) = New Card(False, "Make general repairs on all your property.", "For each house, pay $25; for each hotel, $100.", Nothing)

        ReDim Board.CommunityChestCards(14)
        Board.CommunityChestCardsRemaining = 15
        Board.CommunityChestCards(0) = New Card(True, "Grand opera opening. Collect $50 from every other player.", Nothing, Nothing)
        Board.CommunityChestCards(1) = New Card(True, "Receive, for services, $25.", Nothing, Nothing)
        Board.CommunityChestCards(2) = New Card(True, "Advance to Go.", "Collect $200.", Nothing)
        Board.CommunityChestCards(3) = New Card(True, "Pay hospital $100.", Nothing, Nothing)
        Board.CommunityChestCards(4) = New Card(True, "Doctor's fee: pay $50.", Nothing, Nothing)
        Board.CommunityChestCards(5) = New Card(True, "Get out of jail free!", "This card may be kept until needed or sold.", Nothing)
        Board.CommunityChestCards(6) = New Card(True, "From sale of stock, you get $45.", Nothing, Nothing)
        Board.CommunityChestCards(7) = New Card(True, "You inherit $100.", Nothing, Nothing)
        Board.CommunityChestCards(8) = New Card(True, "Go to jail.", "Go directly to Jail. Do not pass Go; do not collect $200.", Nothing)
        Board.CommunityChestCards(9) = New Card(True, "Life insurance matures; collect $100.", Nothing, Nothing)
        Board.CommunityChestCards(10) = New Card(True, "You have won second prize in a beauty contest. Collect $10.", Nothing, Nothing)
        Board.CommunityChestCards(11) = New Card(True, "Christmas fund matures; collect $100.", Nothing, Nothing)
        Board.CommunityChestCards(12) = New Card(True, "You are assessed for street repairs: $40 per house, $115 per hotel.", Nothing, Nothing)
        Board.CommunityChestCards(13) = New Card(True, "Bank error in your favour; collect $200.", Nothing, Nothing)
        Board.CommunityChestCards(14) = New Card(True, "Income tax refund; collect $20.", Nothing, Nothing)

        Return Board
    End Function

    Public Function InitialiseBoardJunior() As Board
        ' Initialise the board.
        Dim Board = New Board
        Board.Name = "Junior"
        ReDim Board.Spaces(31)

        Board.Spaces(0) = New Space(0, SpaceType.GoSpace, "Go")
        Board.Spaces(1) = New Space(1, SpaceType.ChanceSpace, "Chance")
        Board.Spaces(2) = New PropertySpace(2, ChrW(3) & "5Balloon Stand" & ChrW(3) & "12", 0, 1)
        Board.Spaces(3) = New PropertySpace(3, ChrW(3) & "5Cotton Candy" & ChrW(3) & "12", 0, 1)
        Board.Spaces(4) = New Space(4, SpaceType.ChanceSpace, "Chance")
        Board.Spaces(5) = New RailroadSpace(5, "Yellow Line Railroad")
        Board.Spaces(6) = New PropertySpace(6, ChrW(3) & "11Puppet Show" & ChrW(3) & "12", 1, 2)
        Board.Spaces(7) = New PropertySpace(7, ChrW(3) & "11Magic Show" & ChrW(3) & "12", 1, 2)
        Board.Spaces(8) = New Space(8, SpaceType.IncomeTaxSpace, "Fireworks")
        Board.Spaces(9) = New Space(9, SpaceType.ChanceSpace, "Chance")
        Board.Spaces(10) = New Space(10, SpaceType.JailSpace, "Lunch")
        Board.Spaces(11) = New PropertySpace(11, ChrW(3) & "13Roller Coaster" & ChrW(3) & "12", 2, 2)
        Board.Spaces(12) = New PropertySpace(12, ChrW(3) & "13Paddle Boats" & ChrW(3) & "12", 2, 2)
        Board.Spaces(13) = New RailroadSpace(13, "Green Line Railroad")
        Board.Spaces(14) = New PropertySpace(14, ChrW(3) & "7Haunted House" & ChrW(3) & "12", 3, 3)
        Board.Spaces(15) = New PropertySpace(15, ChrW(3) & "7Video Arcade" & ChrW(3) & "12", 3, 3)
        Board.Spaces(16) = New Space(16, SpaceType.FreeParkingSpace, "Mr. Monopoly's Loose Change")
        Board.Spaces(17) = New Space(17, SpaceType.ChanceSpace, "Chance")
        Board.Spaces(18) = New PropertySpace(18, ChrW(3) & "4Pony Ride" & ChrW(3) & "12", 4, 3)
        Board.Spaces(19) = New PropertySpace(19, ChrW(3) & "4Water Slide" & ChrW(3) & "12", 4, 3)
        Board.Spaces(20) = New Space(20, SpaceType.ChanceSpace, "Chance")
        Board.Spaces(21) = New RailroadSpace(21, "Blue Line Railroad")
        Board.Spaces(22) = New PropertySpace(22, ChrW(3) & "8Helicopter Ride" & ChrW(3) & "12", 5, 4)
        Board.Spaces(23) = New PropertySpace(23, ChrW(3) & "8Miniature Golf" & ChrW(3) & "12", 5, 4)
        Board.Spaces(24) = New Space(24, SpaceType.SuperTaxSpace, "Water Show")
        Board.Spaces(25) = New Space(25, SpaceType.ChanceSpace, "Chance")
        Board.Spaces(26) = New Space(26, SpaceType.GoToJailSpace, "Bus to Lunch")
        Board.Spaces(27) = New PropertySpace(27, ChrW(3) & "9Bumper Cars" & ChrW(3) & "12", 6, 4)
        Board.Spaces(28) = New PropertySpace(28, ChrW(3) & "9Ferris Wheel" & ChrW(3) & "12", 6, 4)
        Board.Spaces(29) = New RailroadSpace(29, "Red Line Railroad")
        Board.Spaces(30) = New PropertySpace(30, ChrW(3) & "12Loop-the-Loop", 7, 5)
        Board.Spaces(31) = New PropertySpace(31, ChrW(3) & "12Merry-Go-Round", 7, 5)

        ReDim Board.ChanceCards(23)
        Board.ChanceCardsRemaining = 24
        Board.ChanceCards(0) = New Card(False, "Free ticket booth (district A)", Nothing, Nothing)
        Board.ChanceCards(1) = New Card(False, "Free ticket booth (district B)", Nothing, Nothing)
        Board.ChanceCards(2) = New Card(False, "Free ticket booth (district B)", Nothing, Nothing)
        Board.ChanceCards(3) = New Card(False, "Free ticket booth (district C)", Nothing, Nothing)
        Board.ChanceCards(4) = New Card(False, "Free ticket booth (district D)", Nothing, Nothing)
        Board.ChanceCards(5) = New Card(False, "Free ticket booth (district D)", Nothing, Nothing)
        Board.ChanceCards(6) = New Card(False, "Free ticket booth (district E)", Nothing, Nothing)
        Board.ChanceCards(7) = New Card(False, "Free ticket booth (district F)", Nothing, Nothing)
        Board.ChanceCards(8) = New Card(False, "Free ticket booth (district F)", Nothing, Nothing)
        Board.ChanceCards(9) = New Card(False, "Free ticket booth (district G)", Nothing, Nothing)
        Board.ChanceCards(10) = New Card(False, "Free ticket booth (district H)", Nothing, Nothing)
        Board.ChanceCards(11) = New Card(False, "Free ticket booth (district H)", Nothing, Nothing)
        Board.ChanceCards(12) = New Card(False, "Pay $3 to take the bus to lunch.", Nothing, Nothing)
        Board.ChanceCards(13) = New Card(False, "Take a ride on the Red Line Railroad,", "and roll again.", Nothing)
        Board.ChanceCards(14) = New Card(False, "Take a ride on the Green Line Railroad,", "and roll again.", Nothing)
        Board.ChanceCards(15) = New Card(False, "Take a ride on the Blue Line Railroad,", "and roll again.", Nothing)
        Board.ChanceCards(16) = New Card(False, "Go to the Bumper Cars.", Nothing, Nothing)
        Board.ChanceCards(17) = New Card(False, "Go to the Merry-Go-Round.", Nothing, Nothing)
        Board.ChanceCards(18) = New Card(False, "Go to the Video Arcade.", Nothing, Nothing)
        Board.ChanceCards(19) = New Card(False, "Go to the Loop-the-Loop.", Nothing, Nothing)
        Board.ChanceCards(20) = New Card(False, "Go to the Water Slide.", Nothing, Nothing)
        Board.ChanceCards(21) = New Card(False, "Go to the Water Show,", "and pay $2.", Nothing)
        Board.ChanceCards(22) = New Card(False, "Go to the Firework Show,", "and pay $2.", Nothing)
        Board.ChanceCards(23) = New Card(False, "Go to Go.", "Collect $2 allowance as you pass.", Nothing)

        Return Board
    End Function

    <Command("maddgame", 0, 0, "maddgame",
        "Starts a game in this channel.",
        ".debug")>
    Public Sub CommandAddGame(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim Key = Connection.Address & "/" & Channel
        If Games.ContainsKey(Key) Then
            Say(Connection, Channel, "$k4A game is already in progress.")
            Return
        End If

        Dim Game = New Game
        Game.Connection = Connection
        Game.Channel = Channel
        Game.Board = InitialiseBoardUK()

        Games.Add(Key, Game)
        Say(Connection, Channel, "$k7A game is added.")
    End Sub

    <Command("mjoin", 0, 0, "mjoin",
    "Joins a game in this channel.")>
    Public Sub CommandJoin(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim Key = Connection.Address & "/" & Channel
        Dim Game As Game
        If Not Games.TryGetValue(Key, Game) Then
            Say(Connection, Channel, "$k4There's no game going on at the moment.")
            Return
        Else
            ' Make sure the player isn't already in.
            For i = 0 To UBound(Game.Players)
                If Game.Players(i) IsNot Nothing AndAlso Game.Players(i).Name = Sender.Split("!"c)(0) Then
                    Say(Connection, Channel, "$k4You're already in the game.")
                    Return
                End If
            Next

            If Game.PlayerCount = Game.Players.Count Then ReDim Preserve Game.Players(UBound(Game.Players) + 2)
            Game.Players(Game.PlayerCount) = New Player(Game.PlayerCount, Sender.Split("!"c)(0))
            Game.PlayerCount += 1

            Say(Connection, Channel, "$k7$b" & Sender.Split("!"c)(0) & "$b has joined the game.")
        End If
    End Sub

    <Command("mroll", 0, 0, "mroll",
"Rolls the dice.")>
    Public Sub CommandRoll(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim Key = Connection.Address & "/" & Channel
        Dim Game As Game
        If Not Games.TryGetValue(Key, Game) Then
            Say(Connection, Channel, "$k4There's no game going on at the moment.")
            Return
        Else
            Dim cPlayer = Game.Players(Game.Turn)
            If cPlayer Is Nothing OrElse cPlayer.Name <> Sender.Split("!"c)(0) Then
                Say(Connection, Channel, "$k4It's not your turn.")
                Return
            End If
            If Game.TurnState = TurnState.Rolling Or Game.TurnState = TurnState.RollingJail Then
                Dim Roll = RollDice(Game), Move As Boolean = True
                Game.RollCount += 1
                ' Check for doubles.
                Game.Doubles = (Roll(0) = Roll(1))
                ' Is the player in jail?
                If cPlayer.JailTime >= 0 Then
                    If Game.Doubles Then
                        ' Escape from jail.
                        Game.Doubles = False
                        cPlayer.JailTime = -1
                    Else
                        cPlayer.JailTime += 1
                        Say(Game.Connection, Game.Channel, "$k12$b" & cPlayer.Name & "$b has spent $b" & cPlayer.JailTime & "$b " & If(cPlayer.JailTime = 1, "turn ", "turns ") & "in jail.")
                        Move = False
                        If cPlayer.JailTime >= 3 Then
                            Say(Game.Connection, Game.Channel, "$k12$b" & cPlayer.Name & "$b is released for a $50 fine.")
                            cPlayer.Cash -= 50
                            cPlayer.Worth -= 50
                            cPlayer.JailTime = -1
                        End If
                    End If
                End If
                If Game.Doubles And Game.RollCount >= 3 Then
                    ' Three doubles! Go to jail.
                    Say(Game.Connection, Game.Channel, "$k12$b" & cPlayer.Name & "$b rolled doubles three times and is sent to $bjail$b.")
                    SendToJail(Game)
                    Move = False
                End If
                If Move Then
                    cPlayer.Location += Roll(0) + Roll(1)
                    Dim PassedGo As Boolean = False
                    If cPlayer.Location >= Game.Board.Spaces.Count Then
                        cPlayer.Location = cPlayer.Location Mod Game.Board.Spaces.Count
                        PassedGo = True
                    End If
                    Say(Game.Connection, Game.Channel, "$k12$b" & cPlayer.Name & "$b moves $b" & Roll(0) + Roll(1) & "$b spaces to $b" & Game.Board.Spaces(cPlayer.Location).Name & "$b.")
                    If PassedGo Then
                        cPlayer.Cash += 200
                        cPlayer.Worth += 200
                        Say(Game.Connection, Game.Channel, "$k12$b" & cPlayer.Name & "$b receives a salary of $b$200$b for passing GO.")
                    End If
                    Land(Game)
                Else
                    EndTurn(Game)
                End If
            ElseIf Game.TurnState = TurnState.RollingRent Or Game.TurnState = TurnState.RollingRentChance Then
                Dim Roll = RollDice(Game), Rent As Integer
                Dim Space = Game.Board.Spaces(cPlayer.Location)
                If Game.TurnState = TurnState.RollingRentChance Then
                    Rent = (Roll(0) + Roll(1)) * 10
                Else
                    ' Check if the owner also owns the other utility.
                    For i = 0 To UBound(Game.Board.Spaces)
                        If Game.Board.Spaces(i) IsNot Space And Game.Board.Spaces(i).Type = SpaceType.UtilitySpace Then
                            If DirectCast(Game.Board.Spaces(i), UtilitySpace).Owner = DirectCast(Space, OwnableSpace).Owner Then
                                Rent = (Roll(0) + Roll(1)) * 10
                            Else
                                Rent = (Roll(0) + Roll(1)) * 4
                            End If
                            Exit For
                        End If
                    Next
                End If
                cPlayer.Cash -= Rent
                cPlayer.Worth -= Rent
                Game.Players(DirectCast(Space, OwnableSpace).Owner).Cash += Rent
                Game.Players(DirectCast(Space, OwnableSpace).Owner).Worth += Rent
                Say(Game.Connection, Game.Channel, "$k12$b" & cPlayer.Name & "$b pays $b$" & Rent & "$b rent to $b" & Game.Players(DirectCast(Space, OwnableSpace).Owner).Name & "$b.")
                EndTurn(Game)
            Else
                Say(Connection, Channel, "$k4You may not roll now.")
                Return
            End If
        End If
    End Sub

    Public Function RollDice(ByVal Game As Game) As Short()
        Dim Dice() As Short = {Game.Dice.Next(6) + 1, Game.Dice.Next(6) + 1}
        Dim Faces() As String = {" · ", " : ", "···", ": :", ":·:", ":::"}
        Say(Game.Connection, Game.Channel, "$k12$b" & Game.Players(Game.Turn).Name & "$b rolls $k1,0$b" & Faces(Dice(0) - 1) & "$o $k1,0$b" & Faces(Dice(1) - 1))
        Return Dice
    End Function

    Public Sub SendToJail(ByVal Game As Game)
        For i = 0 To UBound(Game.Board.Spaces)
            If Game.Board.Spaces(i).Type = SpaceType.JailSpace Then
                Game.Players(Game.Turn).Location = i
                Exit For
            End If
        Next
        Game.Players(Game.Turn).JailTime = 0
    End Sub

    Public Sub Land(ByVal Game As Game)
        Dim Space = Game.Board.Spaces(Game.Players(Game.Turn).Location)
        Select Case Space.Type
            Case SpaceType.GoSpace, SpaceType.FreeParkingSpace, SpaceType.JailSpace
                ' Nothing happens.
                EndTurn(Game)
            Case SpaceType.PropertySpace, SpaceType.RailroadSpace, SpaceType.UtilitySpace
                Game.TurnState = TurnState.Buying
                ' Is the property owned?
                If DirectCast(Space, OwnableSpace).Owner = Game.Turn Then
                    Say(Game.Connection, Game.Channel, "$k12You already own this property.")
                    EndTurn(Game)
                ElseIf DirectCast(Space, OwnableSpace).Owner <> -1 Then
                    If DirectCast(Space, OwnableSpace).IsMortgaged Then
                        Say(Game.Connection, Game.Channel, "$k12The property is currently mortgaged.")
                    Else
                        Say(Game.Connection, Game.Channel, "$k12$b" & Game.Players(DirectCast(Space, OwnableSpace).Owner).Name & "$b owns this property.")
                        Dim Rent As Integer
                        Select Case Space.Type
                            Case SpaceType.PropertySpace
                                Rent = DirectCast(Space, PropertySpace).RentAmounts(DirectCast(Space, PropertySpace).ImprovementLevel)
                                If CheckMonopoly(Game, DirectCast(Space, PropertySpace).Owner, DirectCast(Space, PropertySpace).District) Then Rent *= 2
                            Case SpaceType.RailroadSpace
                                Rent = 25
                                For Each p In Game.Players(Game.Turn).Properties
                                    If p IsNot Space And p.Type = SpaceType.RailroadSpace Then Rent *= 2
                                Next
                            Case SpaceType.UtilitySpace
                                Say(Game.Connection, Game.Channel, "$k12Roll the dice to determine rent.")
                                Game.TurnState = TurnState.RollingRent
                                Return
                        End Select
                        Game.Players(Game.Turn).Cash -= Rent
                        Game.Players(Game.Turn).Worth -= Rent
                        Game.Players(DirectCast(Space, OwnableSpace).Owner).Cash += Rent
                        Game.Players(DirectCast(Space, OwnableSpace).Owner).Worth += Rent
                        Say(Game.Connection, Game.Channel, "$k12$b" & Game.Players(Game.Turn).Name & "$b pays $b$" & Rent & "$b rent to $b" & Game.Players(DirectCast(Space, OwnableSpace).Owner).Name & "$b.")
                    End If
                    EndTurn(Game)
                Else
                    If Game.Players(Game.Turn).Cash < DirectCast(Space, OwnableSpace).Price Then
                        Say(Game.Connection, Game.Channel, "$k12No one owns the property. You don't have enough cash to buy it.")
                    Else
                        Say(Game.Connection, Game.Channel, "$k12No one owns the property. Will you buy or auction it?")
                    End If
                End If
            Case SpaceType.ChanceSpace
                ' Draw a chance card.
                Dim r = Game.Dice.Next(Game.Board.ChanceCardsRemaining)
                Dim i
                For i = 0 To UBound(Game.Board.ChanceCards)
                    If Not Game.Board.ChanceCards(i).IsTaken Then
                        If r = 0 Then Exit For
                        r -= 1
                    End If
                Next
                Game.Board.ChanceCards(i).IsTaken = True
                Game.Board.ChanceCardsRemaining -= 1
                Say(Game.Connection, Game.Channel, "$k12Your Chance card: $k1,7$b " & Game.Board.ChanceCards(i).Text & "$b " & Game.Board.ChanceCards(i).Subtext & " ")
                'Game.Board.ChanceCards(i).Action.Invoke(Game)
                EndTurn(Game)
            Case SpaceType.CommunityChestSpace
                ' Draw a Community Chest card.
                Dim r = Game.Dice.Next(Game.Board.CommunityChestCardsRemaining)
                Dim i
                For i = 0 To UBound(Game.Board.CommunityChestCards)
                    If Not Game.Board.CommunityChestCards(i).IsTaken Then
                        If r = 0 Then Exit For
                        r -= 1
                    End If
                Next
                Game.Board.CommunityChestCards(i).IsTaken = True
                Game.Board.CommunityChestCardsRemaining -= 1
                Say(Game.Connection, Game.Channel, "$k12Your Community Chest card: $k1,8$b " & Game.Board.CommunityChestCards(i).Text & "$b " & Game.Board.CommunityChestCards(i).Subtext & " ")
                'Game.Board.CommunityChestCards(i).Action.Invoke(Game)
                EndTurn(Game)
            Case SpaceType.IncomeTaxSpace
                Say(Game.Connection, Game.Channel, "$k12Will you pay $b10%$b of your worth, or $b$200$b?")
                Game.TurnState = TurnState.IncomeTax
            Case SpaceType.SuperTaxSpace
                Say(Game.Connection, Game.Channel, "$k12You pay a tax of $100.")
                Game.Players(Game.Turn).Cash -= 100
                Game.Players(Game.Turn).Worth -= 100
                EndTurn(Game)
            Case SpaceType.GoToJailSpace
                Say(Game.Connection, Game.Channel, "$k12You are sent to jail.")
                SendToJail(Game)
                EndTurn(Game)
        End Select
    End Sub

    <Command("mbuy", 0, 0, "mbuy",
"Buys the property you are standing on.")>
    Public Sub CommandBuy(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim Key = Connection.Address & "/" & Channel
        Dim Game As Game
        If Not Games.TryGetValue(Key, Game) Then
            Say(Connection, Channel, "$k4There's no game going on at the moment.")
            Return
        Else
            If Game.Players(Game.Turn) Is Nothing OrElse Game.Players(Game.Turn).Name <> Sender.Split("!"c)(0) Then
                Say(Connection, Channel, "$k4It's not your turn.")
                Return
            End If
            If Game.TurnState = TurnState.Buying Then
                Dim Space = Game.Board.Spaces(Game.Players(Game.Turn).Location)
                Game.Players(Game.Turn).Cash -= DirectCast(Space, OwnableSpace).Price
                DirectCast(Space, OwnableSpace).Owner = Game.Turn
                Game.Players(Game.Turn).Properties.Add(Space)
                Say(Connection, Channel, "$k12You bought $b" & Space.Name & "$b for $b$" & DirectCast(Space, OwnableSpace).Price & "$b.")
                If Space.Type = SpaceType.PropertySpace AndAlso CheckMonopoly(Game, Game.Turn, DirectCast(Space, PropertySpace).District) Then _
                    Say(Connection, Channel, "$k7$b" & Game.Players(Game.Turn).Name & "$b achieves a monopoly in district " & ChrW(65 + DirectCast(Space, PropertySpace).District) & "!")
                EndTurn(Game)
            Else
                Say(Connection, Channel, "$k4There's nothing to buy.")
            End If
        End If
    End Sub

    <Command("mpass", 0, 0, "mpass",
"Ends your turn without buying a property.")>
    Public Sub CommandPass(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim Key = Connection.Address & "/" & Channel
        Dim Game As Game = Nothing
        If Not Games.TryGetValue(Key, Game) Then
            Say(Connection, Channel, "$k4There's no game going on at the moment.")
            Return
        Else
            If Game.Players(Game.Turn) Is Nothing OrElse Game.Players(Game.Turn).Name <> Sender.Split("!"c)(0) Then
                Say(Connection, Channel, "$k4It's not your turn.")
                Return
            End If
            If Game.TurnState = TurnState.Buying Then
                EndTurn(Game)
            Else
                Say(Connection, Channel, "$k4There's nothing to pass up.")
            End If
        End If
    End Sub

    Public Sub EndTurn(ByVal Game As Game)
        Threading.Thread.Sleep(2000)
        If Game.Doubles Then
            Say(Game.Connection, Game.Channel, "$k12$b" & Game.Players(Game.Turn).Name & "$b, you rolled doubles. Roll again.")
            Game.TurnState = TurnState.Rolling
        Else
            ' Go to the next player.
            For i = 0 To UBound(Game.Players)
                Game.Turn += 1
                If Game.Turn > UBound(Game.Players) Then Game.Turn = 0
                If Game.Players(Game.Turn) IsNot Nothing Then
                    Say(Game.Connection, Game.Channel, "$k12It is $b" & Game.Players(Game.Turn).Name & "$b's turn to roll.")
                    Exit For
                End If
            Next
            Game.RollCount = 0
            Game.Doubles = False
            If Game.Players(Game.Turn).JailTime <> -1 Then
                Game.TurnState = TurnState.RollingJail
            Else
                Game.TurnState = TurnState.Rolling
            End If
        End If
    End Sub

    <Regex("^(?:(10%?)|(\$?200))(?:\.|!)?$")>
    Public Sub RegexIncomeTax(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As System.Text.RegularExpressions.Match)
        Dim Key = Connection.Address & "/" & Channel
        Dim Game As Game
        If Games.TryGetValue(Key, Game) Then
            If Game.Players(Game.Turn) IsNot Nothing AndAlso Game.Players(Game.Turn).Name = Sender.Split("!"c)(0) And Game.TurnState = TurnState.IncomeTax Then
                Dim Amount As Integer
                If Match.Groups(1).Success Then
                    Amount = Game.Players(Game.Turn).Worth / 10
                Else
                    Amount = 200
                End If
                Say(Game.Connection, Game.Channel, "$k12You pay a tax of $b$" & Amount & "$b.")
                Game.Players(Game.Turn).Cash -= Amount
                Game.Players(Game.Turn).Worth -= Amount
                EndTurn(Game)
            End If
        End If
    End Sub

    <Command("mmoney", 0, 1, "mmoney [player]",
"Shows you your cash on hand and net worth.")>
    Public Sub CommandMoney(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim Key = Connection.Address & "/" & Channel
        Dim Game As Game
        If Not Games.TryGetValue(Key, Game) Then
            Say(Connection, Channel, "$k4There's no game going on at the moment.")
            Return
        Else
            Dim Player As Player
            For i = 0 To UBound(Game.Players)
                If Game.Players(i) Is Nothing Then Continue For
                If args.Count = 0 Then
                    If Game.Players(i).Name = Sender.Split("!"c)(0) Then
                        Player = Game.Players(i)
                        Exit For
                    End If
                Else
                    If Game.Players(i).Name = args(0) Then
                        Player = Game.Players(i)
                        Exit For
                    ElseIf Game.Players(i).Name.StartsWith(args(0), StringComparison.OrdinalIgnoreCase) Then
                        If Player IsNot Nothing Then
                            Reply(Connection, Channel, Sender, "$k4Multiple matching players were found.")
                            Return
                        End If
                        Player = Game.Players(i)
                    End If
                End If
            Next
            If Player Is Nothing Then
                Reply(Connection, Channel, Sender, "$k4No such player is present.")
                Return
            End If
            If Game.TurnState = TurnState.IncomeTax And Player Is Game.Players(Game.Turn) Then
                Say(Connection, Channel, "$k12$b" & Player.Name & "$b holds $b$" & Player.Cash & "$b.")
            Else
                Say(Connection, Channel, "$k12$b" & Player.Name & "$b holds $b$" & Player.Cash & "$b, and is currently worth $b$" & Player.Worth & "$b.")
            End If
        End If
    End Sub

    <Command("mcheck", 1, 1, "mcheck <property/district>",
"Checks the status of a property or district.")>
    Public Sub CommandCheck(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim Key = Connection.Address & "/" & Channel
        Dim Game As Game
        If Not Games.TryGetValue(Key, Game) Then
            Say(Connection, Channel, "$k4There's no game going on at the moment.")
            Return
        Else
            Dim Name = args(0).ToUpper().Trim(), District As Short = -1
            If Name = "A" Or Name = "1" Or Name = "BROWN" Then
                District = 0
            ElseIf Name = "B" Or Name = "2" Or Name = "LIGHTBLUE" Or Name = "CYAN" Or Name = "TEAL" Then
                District = 1
            ElseIf Name = "C" Or Name = "3" Or Name = "PURPLE" Or Name = "MAGENTA" Then
                District = 2
            ElseIf Name = "D" Or Name = "4" Or Name = "ORANGE" Then
                District = 3
            ElseIf Name = "E" Or Name = "5" Or Name = "RED" Then
                District = 4
            ElseIf Name = "F" Or Name = "6" Or Name = "YELLOW" Then
                District = 5
            ElseIf Name = "G" Or Name = "7" Or Name = "GREEN" Then
                District = 6
            ElseIf Name = "H" Or Name = "8" Or Name = "BLUE" Then
                District = 7
            ElseIf Name = "R" Or Name = "RAILROADS" Or Name = "RAILRAYS" Or Name = "RAIL" Or Name = "RAILS" Or Name = "TRAINS" Or Name = "STATIONS" Then
                District = -2
            ElseIf Name = "U" Or Name = "UTILITIES" Then
                District = -3
            End If
            If District <> -1 Then
                CheckDistrict(Game, District)
            Else
                'CheckProperty(Game, Name)
            End If
        End If
    End Sub

    Public Sub CheckDistrict(ByVal Game As Game, ByVal District As Short)
        Dim Message As String = ""
        For i = 0 To UBound(Game.Board.Spaces)
            If District < -1 Then
                If (District = -2 And Game.Board.Spaces(i).Type = SpaceType.RailroadSpace) Or (District = -3 And Game.Board.Spaces(i).Type = SpaceType.UtilitySpace) Then
                    Dim Space = DirectCast(Game.Board.Spaces(i), OwnableSpace)
                    Message &= " $k15| $k12$b" & Space.Name & ChrW(2)
                    If Space.Owner = -1 Then
                        Message &= " unowned"
                    Else
                        Message &= " owned by " & Game.Players(Space.Owner).Name
                        If Space.IsMortgaged Then
                            Message &= ", mortgaged for $" & Int(Space.Price / 2)
                        End If
                    End If
                End If
            Else
                If Game.Board.Spaces(i).Type = SpaceType.PropertySpace AndAlso DirectCast(Game.Board.Spaces(i), PropertySpace).District = District Then
                    Dim Space = DirectCast(Game.Board.Spaces(i), PropertySpace)
                    Message &= " $k15| $k12$b" & Space.Name & ChrW(2)
                    If Space.Owner = -1 Then
                        Message &= " unowned"
                    Else
                        Message &= " owned by " & Game.Players(Space.Owner).Name
                        If Space.IsMortgaged Then
                            Message &= ", mortgaged for $" & Int(Space.Price / 2)
                        Else
                            Message &= {", unimproved", " with 1 house", " with 2 houses", " with 3 houses", " with 4 houses", " with a hotel"}(Space.ImprovementLevel)
                        End If
                    End If
                End If
            End If
        Next
        Say(Game.Connection, Game.Channel, Message)
    End Sub

    ''' <summary>Returns true if the player at the given index has a monopoly in the given district.</summary>
    ''' <param name="Game">The game to check.</param>
    ''' <param name="Player">The player index to check.</param>
    ''' <param name="District">The district to check.</param>
    Public Function CheckMonopoly(ByVal Game As Game, ByVal Player As Short, ByVal District As Short)
        For i = 0 To UBound(Game.Board.Spaces)
            If Game.Board.Spaces(i).Type = SpaceType.PropertySpace AndAlso DirectCast(Game.Board.Spaces(i), PropertySpace).District = District AndAlso DirectCast(Game.Board.Spaces(i), PropertySpace).Owner <> Player Then
                Return False
                Exit For
            End If
        Next
        Return True
    End Function

    <Command("mmortgage", 1, 1, "mmortgage <property>",
"Mortgages your properties.")>
    Public Sub CommandUnmortgage(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim Key = Connection.Address & "/" & Channel
        Dim Game As Game
        Dim Player As Integer
        If Not Games.TryGetValue(Key, Game) Then
            Say(Connection, Channel, "$k4There's no game going on at the moment.")
            Return
        Else
            For Player = 0 To UBound(Game.Players)
                If Game.Players(Player).Name = Sender.Split("!"c)(0) Then Exit For
            Next
            If Player > UBound(Game.Players) Then
                Say(Connection, Channel, "$k4You're not in this game.")
                Return
            End If
            Dim Space As OwnableSpace = Nothing
            For i = 0 To UBound(Game.Board.Spaces)
                If Game.Board.Spaces(i).Type <= SpaceType.UtilitySpace Then
                    If IRCConnection.RemoveCodes(Game.Board.Spaces(i).Name).StartsWith(args(0), StringComparison.OrdinalIgnoreCase) Then
                        If Space IsNot Nothing Then
                            Say(Connection, Channel, "$k4More than one matching property was found.")
                            Return
                        End If
                        Space = Game.Board.Spaces(i)
                    End If
                End If
            Next
            If Space Is Nothing Then
                Say(Connection, Channel, "$k4No matching property was found.")
                Return
            End If

            If Space.Owner <> Player Then
                Say(Connection, Channel, "$k4You don't own " & Space.Name & "$k4.")
                Return
            End If
            If Space.IsMortgaged Then
                Say(Connection, Channel, Space.Name & "$k4 is already mortgaged.")
                Return
            End If
            If Space.Type = SpaceType.PropertySpace AndAlso DirectCast(Space, PropertySpace).ImprovementLevel > 0 Then
                Say(Connection, Channel, "$k4You can't mortgage " & Space.Name & "$k4 with buildings on it.")
                Return
            End If
            Space.IsMortgaged = True
            Game.Players(Player).Cash += CInt(Space.Price / 2)
            Say(Connection, Channel, "$k12You mortgage " & Space.Name & "$k12 for $" & CInt(Space.Price / 2) & ".")
        End If
    End Sub

    <Command("munmortgage", 1, 1, "munmortgage <property>",
"Unmortgages your properties.")>
    Public Sub CommandMortgage(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim Key = Connection.Address & "/" & Channel
        Dim Game As Game
        Dim Player As Integer
        If Not Games.TryGetValue(Key, Game) Then
            Say(Connection, Channel, "$k4There's no game going on at the moment.")
            Return
        Else
            For Player = 0 To UBound(Game.Players)
                If Game.Players(Player).Name = Sender.Split("!"c)(0) Then Exit For
            Next
            If Player > UBound(Game.Players) Then
                Say(Connection, Channel, "$k4You're not in this game.")
                Return
            End If
            Dim Space As OwnableSpace = Nothing
            For i = 0 To UBound(Game.Board.Spaces)
                If Game.Board.Spaces(i).Type <= SpaceType.UtilitySpace Then
                    If IRCConnection.RemoveCodes(Game.Board.Spaces(i).Name).StartsWith(args(0), StringComparison.OrdinalIgnoreCase) Then
                        If Space IsNot Nothing Then
                            Say(Connection, Channel, "$k4More than one matching property was found.")
                            Return
                        End If
                        Space = Game.Board.Spaces(i)
                    End If
                End If
            Next
            If Space Is Nothing Then
                Say(Connection, Channel, "$k4No matching property was found.")
                Return
            End If

            If Space.Owner <> Player Then
                Say(Connection, Channel, "$k4You don't own " & Space.Name & "$k4.")
                Return
            End If
            If Not Space.IsMortgaged Then
                Say(Connection, Channel, "$k4You haven't taken out a mortgage on " & Space.Name & "$k4.")
                Return
            End If
            If Game.Players(Player).Cash < CInt(Space.Price * 0.55) Then
                Say(Connection, Channel, "$k4You don't have enough cash to pay off the mortgage. You need $" & CInt(Space.Price * 0.55) & ".")
                Return
            End If
            Space.IsMortgaged = False
            Game.Players(Player).Cash -= CInt(Space.Price * 0.55)
            Game.Players(Player).Worth -= CInt(Space.Price * 0.05)
            Say(Connection, Channel, "$k12You unmortgage " & Space.Name & "$k12 for $" & CInt(Space.Price * 0.55) & ".")
        End If
    End Sub

End Class
