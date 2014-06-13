' General to-do list:
'   TODO: Fix the bug whereby the following expression doesn't parse: (-18 - 32) / 1.8
'   TODO: Implement permissions on scripts.
'   TODO: Improve the tokeniser so that alphabetical operators like 'and' work correctly.
'   TODO: Add more events and make the syntax more user-friendly.
'   TODO: Make it so that multiple commands and structures like if and while blocks can be put on one line.

Imports VBot
Imports System.Text.RegularExpressions

Public Class ScriptEngine
    Inherits Plugin

    ''' <summary>A keyed list of the variables that have been stored. The keys are the variable names, including the % prefix.</summary>
    Public Variables As New Dictionary(Of String, TypedValue)

    Friend Connection As IRCConnection, Channel As String, Sender As String
    Friend Parameter As String, e As EventArgs

    ''' <summary>Represents a value and its type.</summary>
    Public Class TypedValue
        Dim pType As BasicType
        Dim pInnerType As BasicType
        Dim pValue As Object

        Public Sub New(Value As Object)
            If Value Is Nothing Then
                pValue = Nothing
                pType = BasicType.tNull
                Return
            Else
                Select Case Value.GetType
                    Case GetType(Byte), GetType(SByte), GetType(Short), GetType(UShort), GetType(Integer), GetType(UInteger), GetType(Long), GetType(ULong),
                        GetType(Single), GetType(Double), GetType(Decimal)
                        pValue = CType(Value, Decimal)
                        pType = BasicType.tDecimal
                    Case GetType(String)
                        pValue = Value.ToString
                        pType = BasicType.tString
                    Case GetType(Boolean)
                        pValue = Value
                        pType = BasicType.tBoolean
                    Case GetType(Date)
                        pValue = Value
                        pType = BasicType.tDate
                    Case Else
                        If Value.GetType.IsArray Then
                            pType = BasicType.tArray
                            Select Case Value.GetType.GetElementType()
                                Case GetType(Byte), GetType(SByte), GetType(Short), GetType(UShort), GetType(Integer), GetType(UInteger), GetType(Long), GetType(ULong),
                                    GetType(Single), GetType(Double), GetType(Decimal)
                                    pInnerType = BasicType.tDecimal
                                Case GetType(String)
                                    pInnerType = BasicType.tString
                                Case GetType(Boolean)
                                    pInnerType = BasicType.tBoolean
                                Case GetType(Date)
                                    pInnerType = BasicType.tDate
                                Case Else
                                    If Value.GetType.IsArray Then
                                        pInnerType = BasicType.tArray
                                    Else
                                        pInnerType = BasicType.tObject
                                    End If
                            End Select
                        Else
                            pValue = CType(Value, Object)
                            pType = BasicType.tObject
                        End If
                End Select
            End If
        End Sub

        ''' <summary>Specifies the type of a variable</summary>
        Public Enum BasicType
            ''' <summary>Nothing</summary>
            tNull = 0
            ''' <summary>Any numeric type</summary>
            tDecimal = 1
            ''' <summary>A Boolean value</summary>
            tBoolean = 16
            ''' <summary>A string</summary>
            tString = 17
            ''' <summary>A date</summary>
            tDate = 18
            ''' <summary>An object with members</summary>
            tObject = 32
            ''' <summary>An array</summary>
            tArray = 33
        End Enum

        ''' <summary>The value of the variable</summary>
        Public ReadOnly Property Value As Object
            Get
                Return pValue
            End Get
        End Property

        ''' <summary>The type of the variable</summary>
        Public ReadOnly Property Type As BasicType
            Get
                Return pType
            End Get
        End Property

        ''' <summary>If Type indicates an array, InnerType indicates the type of its elements.</summary>
        Public ReadOnly Property InnerType As BasicType
            Get
                Return pInnerType
            End Get
        End Property

#Region "Primitive type conversions"
        Public Shared Narrowing Operator CType(Value As TypedValue) As Boolean
            Select Case Value.Type
                Case BasicType.tBoolean
                    Return Value.Value
                Case BasicType.tDecimal
                    Return (Value.Value <> 0)
                Case BasicType.tString
                    If DirectCast(Value.Value, String).ToUpper = "TRUE" Then Return True
                    If DirectCast(Value.Value, String).ToUpper = "FALSE" Then Return False
                    Throw New InvalidCastException("String """ & Value.Value & """ cannot be converted to Boolean.")
                Case BasicType.tNull
                    Return Nothing
                Case Else
                    Throw New InvalidCastException("Type " & Value.Type.GetType.Name & " cannot be converted to Boolean.")
            End Select
        End Operator

        Public Shared Narrowing Operator CType(Value As TypedValue) As Byte
            Select Case Value.Type
                Case BasicType.tBoolean : Return If(Value.Value, 255, 0)
                Case BasicType.tDecimal, BasicType.tString : Return Value.Value
                Case BasicType.tNull : Return Nothing
                Case Else : Throw New InvalidCastException("Type " & Value.Type.GetType.Name & " cannot be converted to Byte.")
            End Select
        End Operator
        Public Shared Narrowing Operator CType(Value As TypedValue) As Short
            Select Case Value.Type
                Case BasicType.tBoolean : Return If(Value.Value, -1, 0)
                Case BasicType.tDecimal, BasicType.tString : Return Value.Value
                Case BasicType.tNull : Return Nothing
                Case Else : Throw New InvalidCastException("Type " & Value.Type.GetType.Name & " cannot be converted to Short.")
            End Select
        End Operator
        Public Shared Narrowing Operator CType(Value As TypedValue) As UShort
            Select Case Value.Type
                Case BasicType.tBoolean : Return If(Value.Value, 65535US, 0)
                Case BasicType.tDecimal, BasicType.tString : Return Value.Value
                Case BasicType.tNull : Return Nothing
                Case Else : Throw New InvalidCastException("Type " & Value.Type.GetType.Name & " cannot be converted to UShort.")
            End Select
        End Operator
        Public Shared Narrowing Operator CType(Value As TypedValue) As Integer
            Select Case Value.Type
                Case BasicType.tBoolean : Return If(Value.Value, -1, 0)
                Case BasicType.tDecimal, BasicType.tString : Return Value.Value
                Case BasicType.tNull : Return Nothing
                Case Else : Throw New InvalidCastException("Type " & Value.Type.GetType.Name & " cannot be converted to Integer.")
            End Select
        End Operator
        Public Shared Narrowing Operator CType(Value As TypedValue) As UInteger
            Select Case Value.Type
                Case BasicType.tBoolean : Return If(Value.Value, 4294967295UI, 0)
                Case BasicType.tDecimal, BasicType.tString : Return Value.Value
                Case BasicType.tNull : Return Nothing
                Case Else : Throw New InvalidCastException("Type " & Value.Type.GetType.Name & " cannot be converted to UInteger.")
            End Select
        End Operator
        Public Shared Narrowing Operator CType(Value As TypedValue) As Long
            Select Case Value.Type
                Case BasicType.tBoolean : Return If(Value.Value, -1, 0)
                Case BasicType.tDecimal, BasicType.tString : Return Value.Value
                Case BasicType.tNull : Return Nothing
                Case Else : Throw New InvalidCastException("Type " & Value.Type.GetType.Name & " cannot be converted to Long.")
            End Select
        End Operator
        Public Shared Narrowing Operator CType(Value As TypedValue) As ULong
            Select Case Value.Type
                Case BasicType.tBoolean : Return If(Value.Value, &HFFFFFFFFFFFFFFFFUL, 0)
                Case BasicType.tDecimal, BasicType.tString : Return Value.Value
                Case BasicType.tNull : Return Nothing
                Case Else : Throw New InvalidCastException("Type " & Value.Type.GetType.Name & " cannot be converted to ULong.")
            End Select
        End Operator
        Public Shared Narrowing Operator CType(Value As TypedValue) As Single
            Select Case Value.Type
                Case BasicType.tBoolean : Return If(Value.Value, -1, 0)
                Case BasicType.tDecimal, BasicType.tString : Return Value.Value
                Case BasicType.tNull : Return Nothing
                Case Else : Throw New InvalidCastException("Type " & Value.Type.GetType.Name & " cannot be converted to Single.")
            End Select
        End Operator
        Public Shared Narrowing Operator CType(Value As TypedValue) As Double
            Select Case Value.Type
                Case BasicType.tBoolean : Return If(Value.Value, -1, 0)
                Case BasicType.tDecimal, BasicType.tString : Return Value.Value
                Case BasicType.tNull : Return Nothing
                Case Else : Throw New InvalidCastException("Type " & Value.Type.GetType.Name & " cannot be converted to Double.")
            End Select
        End Operator
        Public Shared Narrowing Operator CType(Value As TypedValue) As Decimal
            Select Case Value.Type
                Case BasicType.tBoolean : Return If(Value.Value, 255, 0)
                Case BasicType.tDecimal, BasicType.tString : Return Value.Value
                Case BasicType.tNull : Return Nothing
                Case Else : Throw New InvalidCastException("Type " & Value.Type.GetType.Name & " cannot be converted to Decimal.")
            End Select
        End Operator

        Public Shared Narrowing Operator CType(Value As TypedValue) As String
            Select Case Value.Type
                Case BasicType.tArray
                    Return "{" & String.Join(", ", Value.Value) & "}"
                Case BasicType.tNull
                    Return ""
                Case Else
                    Return Value.Value.ToString
            End Select
        End Operator

        Public Shared Narrowing Operator CType(Value As TypedValue) As Date
            Select Case Value.Type
                Case BasicType.tDate
                    Return Value.Value
                Case BasicType.tNull
                    Return Nothing
                Case BasicType.tDecimal
                    Return Date.FromOADate(Value.Value)
                Case BasicType.tString
                    Return Date.Parse(Value.Value)
                Case Else
                    Throw New InvalidCastException("Type " & Value.Type.GetType.Name & " cannot be converted to Date.")
            End Select
        End Operator
#End Region

    End Class

    Public Scripts As New List(Of Script)
    Public _Commands As New Commands(Me)
    Public _Functions As New Functions(Me)

    Public Sub New()
        Dim h = New EventHandler(Me, "script.loaded")
        'AddHandler ScriptLoaded, AddressOf h.Run
    End Sub

    <Command({"loadscript"}, 1, 1,
"loadscript <filename>",
"Loads a script.",
".load", CommandAttribute.CommandScope.NoMinorChannel)>
    Public Sub CommandLoad(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim Index = Scripts.Count
        Dim Owner As New List(Of String)
        For Each ID In Identifications
            If Connection IsNot Nothing AndAlso ID.Key = Connection.Address & "/" & Sender.Split("!"c)(0) Then _
                Owner.Add(ID.Value.AccountName)
        Next

        Scripts.Add(New Script(args(0), Me) With {.Owner = Owner.ToArray})
        Reply(Connection, Channel, Sender, "Loaded.")
        RaiseEvent ScriptLoaded(Me, EventArgs.Empty)
    End Sub

    <Command({"evaluate", "eval"}, 1, 1,
"eval <expression>",
"Evaluates an expression and tells you the result.",
Nothing, CommandAttribute.CommandScope.NoMinorChannel)>
    Public Sub CommandEvaluate(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Me.Connection = Connection : Me.Channel = Channel : Me.Sender = Sender
        Dim Owner As New List(Of String)
        For Each ID In Identifications
            If Connection IsNot Nothing AndAlso ID.Key = Connection.Address & "/" & Sender.Split("!"c)(0) Then _
                Owner.Add(ID.Value.AccountName)
        Next

        Dim TempScript = New Script(Me) With {.Script = args(0), .Owner = Owner.ToArray}
        'Try
            Dim Result = TempScript.ParseExpression()
            Dim rValue = Result.Value
        Say(Connection, Channel, ChrW(3) & "4Result: " & rValue.GetType.Name & ChrW(3) & "12 " & rValue.ToString, SayOptions.NoParse)
        'Catch ex As FormatException
        '    Say(Connection, Channel, ChrW(3) & "4Parse error (line " & TempScript.Line & " column " & TempScript.Column & "): " & ex.GetType.FullName & " : " & ex.Message, SayOptions.NoParse)
        '    Say(Connection, Channel, TempScript.UserFriendlyErrorLocation, SayOptions.NoParse)
        'Catch ex As NotImplementedException
        '    Say(Connection, Channel, ChrW(3) & "4Parse error (line " & TempScript.Line & " column " & TempScript.Column & "): " & ex.GetType.FullName & " : " & ex.Message, SayOptions.NoParse)
        '    Say(Connection, Channel, TempScript.UserFriendlyErrorLocation, SayOptions.NoParse)
        'Catch ex As SyntaxException
        '    Say(Connection, Channel, ChrW(3) & "4Syntax error (line " & TempScript.Line & " column " & TempScript.Column & "): " & ex.Message, SayOptions.NoParse)
        '    Say(Connection, Channel, TempScript.UserFriendlyErrorLocation, SayOptions.NoParse)
        'Catch ex As Exception
        '    Say(Connection, Channel, ChrW(3) & "4Error (line " & TempScript.Line & " column " & TempScript.Column & "): " & ex.GetType.FullName & " : " & ex.Message, SayOptions.NoParse)
        '    Say(Connection, Channel, TempScript.UserFriendlyErrorLocation, SayOptions.NoParse)
        'Finally
        Me.Connection = Nothing : Me.Channel = Nothing : Me.Sender = Nothing
        'End Try
    End Sub

    <Regex("^~\?\s+(?<Expression>.*)$",
Nothing, CommandAttribute.CommandScope.NoMinorChannel, False)>
    Public Sub RegexEvaluate(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match, ByRef Handled As Boolean)
        CommandEvaluate(Connection, Sender, Channel, {Match.Groups("Expression").Value})
    End Sub

    <Regex("^~\s+(?<Command>.*)$",
".run", CommandAttribute.CommandScope.NoMinorChannel, False)>
    Public Sub RegexCommand(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Match As Match, ByRef Handled As Boolean)
        Me.Connection = Connection : Me.Channel = Channel : Me.Sender = Sender
        Dim TempScript = New Script(Me) With {.Script = Match.Groups("Command").Value}
        'Try
        Dim Result As Script.ExecuteResults = Script.ExecuteResults.OK
        Do
            Dim lResult = TempScript.RunInstruction(Connection, Channel)
            Select Case lResult
                Case Script.ExecuteResults.DefaultHalted : Result = Script.ExecuteResults.DefaultHalted
                Case Script.ExecuteResults.EndOfFile : Exit Do
                Case Script.ExecuteResults.ErrorOccurred : Result = Script.ExecuteResults.ErrorOccurred : Exit Do
                Case Script.ExecuteResults.Halted : Result = Script.ExecuteResults.Halted : Exit Do
            End Select
        Loop
        Select Case Result
            Case Script.ExecuteResults.DefaultHalted
            Case Script.ExecuteResults.EndOfBlock, Script.ExecuteResults.EndOfFile, Script.ExecuteResults.OK, Script.ExecuteResults.Halted
                Say(Connection, Sender.Split("!"c)(0), ChrW(3) & "9The command completed successfully.", SayOptions.NoParse)
            Case Script.ExecuteResults.ErrorOccurred
                Say(Connection, Sender.Split("!"c)(0), ChrW(3) & "4The command failed.", SayOptions.NoParse)
        End Select
        'Catch ex As FormatException
        '    Say(Connection, Channel, ChrW(3) & "4Parse error (line " & TempScript.Line & " column " & TempScript.Column & "): " & ex.GetType.FullName & " : " & ex.Message, SayOptions.NoParse)
        '    Say(Connection, Channel, TempScript.UserFriendlyErrorLocation, SayOptions.NoParse)
        'Catch ex As NotImplementedException
        '    Say(Connection, Channel, ChrW(3) & "4Parse error (line " & TempScript.Line & " column " & TempScript.Column & "): " & ex.GetType.FullName & " : " & ex.Message, SayOptions.NoParse)
        '    Say(Connection, Channel, TempScript.UserFriendlyErrorLocation, SayOptions.NoParse)
        'Catch ex As SyntaxException
        '    Say(Connection, Channel, ChrW(3) & "4Syntax error (line " & TempScript.Line & " column " & TempScript.Column & "): " & ex.Message, SayOptions.NoParse)
        '    Say(Connection, Channel, TempScript.UserFriendlyErrorLocation, SayOptions.NoParse)
        'Catch ex As Exception
        '    Say(Connection, Channel, ChrW(3) & "4Error (line " & TempScript.Line & " column " & TempScript.Column & "): " & ex.GetType.FullName & " : " & ex.Message, SayOptions.NoParse)
        '    Say(Connection, Channel, TempScript.UserFriendlyErrorLocation, SayOptions.NoParse)
        'Finally
        Me.Connection = Nothing : Me.Channel = Nothing : Me.Sender = Nothing
        'End Try
    End Sub

    'Public Function RunEvent(Connection As IRCConnection, Channel As String, Sender As String, Name As String, Parameter As String, e As EventArgs, IsPluginEvent As Boolean) As Script.ExecuteResults
    '    Dim Result As Script.ExecuteResults = Script.ExecuteResults.OK
    '    For Each script In Scripts

    '        Me.Connection = Connection
    '        Me.Channel = Channel
    '        Me.Sender = Sender
    '        Me.e = e
    '        Me.Parameter = Parameter
    '        Dim lResult = script.RunEvent(Connection, Channel, Sender, Name, Parameter, e, IsPluginEvent)
    '        Me.Connection = Nothing
    '        Me.Channel = Nothing
    '        Me.Sender = Nothing
    '        Me.e = Nothing
    '        Me.Parameter = Nothing

    '        Select Case lResult
    '            Case Script.ExecuteResults.OK, Script.ExecuteResults.EndOfBlock, Script.ExecuteResults.EndOfFile
    '            Case Script.ExecuteResults.DefaultHalted
    '                Result = Script.ExecuteResults.DefaultHalted
    '            Case Script.ExecuteResults.Halted
    '                Return Script.ExecuteResults.Halted
    '            Case Script.ExecuteResults.ErrorOccurred
    '                Return Script.ExecuteResults.ErrorOccurred
    '        End Select
    '    Next
    '    Return Result
    'End Function

    <Command({"token"}, 1, 1,
"token <script>",
"Tests the tokeniser.",
Nothing, CommandAttribute.CommandScope.NoMinorChannel)>
    Public Sub CommandToken(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim TempScript = New Script(Me) With {.Script = args(0)}
        Do
            TempScript.Tokeniser.NextToken()
            Console.Write(TempScript.Tokeniser.ToString())
            Console.Write(" ")
        Loop Until TempScript.Tokeniser.TokenType = TokenTypes.tEndOfScript
        Console.WriteLine()
    End Sub

    Public Event ScriptLoaded(sender As Object, e As EventArgs)

End Class

Public Class EventHandler
    Private _EventName As String, _Plugin As ScriptEngine

    Public Sub New(Plugin As ScriptEngine, EventName As String)
        _Plugin = Plugin
        _EventName = EventName
    End Sub

    'Public Sub Run(sender As Object, e As EventArgs)
    '    _Plugin.RunEvent(Nothing, Nothing, Nothing, _EventName, Nothing, e, True)
    'End Sub
End Class

''' <summary>The exception that is thrown when a syntax problem is encountered in a script</summary>
<Serializable()> _
Public Class SyntaxException
    Inherits System.Exception

    Public Sub New(ByVal message As String)
        MyBase.New(message)
    End Sub

    Public Sub New(ByVal message As String, ByVal inner As Exception)
        MyBase.New(message, inner)
    End Sub

    Public Sub New( _
        ByVal info As System.Runtime.Serialization.SerializationInfo, _
        ByVal context As System.Runtime.Serialization.StreamingContext)
        MyBase.New(info, context)
    End Sub
End Class
