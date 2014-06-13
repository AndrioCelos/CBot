Imports VBot
Imports System.Text
Imports System.Reflection

''' <summary>Represents a script and the current position within it.</summary>
Public Class Script
    Public Script As String
    Public Tokeniser As Tokeniser

    Public Filename As String
    Public lPlugin As ScriptEngine
    Public Owner() As String

    Friend LocalVariables As Dictionary(Of String, Object)

    Public Sub New(lPlugin As ScriptEngine)
        Me.Filename = Nothing
        Script = Nothing
        Me.lPlugin = lPlugin
        Tokeniser = New Tokeniser(Me)
    End Sub

    Public Sub New(Filename As String, lPlugin As ScriptEngine)
        Me.Filename = Filename
        Script = My.Computer.FileSystem.ReadAllText(Filename)
        Me.lPlugin = lPlugin
        Tokeniser = New Tokeniser(Me)
    End Sub

    ''' <summary>Codes that give information about the result of executing a command.</summary>
    Public Enum ExecuteResults As Short
        ''' <summary>The command executed without problems.</summary>
        OK = 0
        ''' <summary>The command was halted.</summary>
        Halted = 1
        ''' <summary>The command executed without problems, but the default action was cancelled.</summary>
        DefaultHalted = 2
        ''' <summary>The command encountered an error.</summary>
        ErrorOccurred = 3
        ''' <summary>Hit the end of the file.</summary>
        EndOfFile = 4
        ''' <summary>Hit the end of the block.</summary>
        EndOfBlock = 5
    End Enum

    '    Public Function HasPermission(Permission As String) As Boolean
    '        If Owner Is Nothing Then Return False
    '        For Each Account In Owner
    '            If UserHasPermission(Account, Permission) Then Return True
    '        Next
    '        Return False
    '    End Function

    '    Public Function RunEvent(Connection As IRCConnection, Channel As String, Sender As String, Name As String, Parameter As String, e As EventArgs, IsPluginEvent As Boolean) As ExecuteResults
    '        Dim Result As ExecuteResults

    '        Do Until Tokeniser.TokenType <> TokenTypes.tEndOfScript
    '            Dim token = Tokeniser.NextToken()
    '        Loop

    '        Do
    '            If LineIndex >= Script.Count Then Return Result
    '            Do
    '                If ColumnIndex >= Script(LineIndex).Length Then
    '                    LineIndex += 1
    '                    ColumnIndex = 0
    '                    GoTo nextline
    '                End If
    '                Dim c = Script(LineIndex)(ColumnIndex)

    '                Select Case c
    '                    Case " "c, ChrW(9)
    '                        ColumnIndex += 1
    '                        Continue Do
    '                    Case ";"c
    '                        LineIndex += 1
    '                        ColumnIndex = 0
    '                        GoTo nextline
    '                    Case "A"c To "Z"c, "a"c To "z"c, "_"c
    '                        Dim b As New StringBuilder
    '                        Do
    '                            If ColumnIndex >= Script(LineIndex).Length Then Exit Do
    '                            Dim d = Script(LineIndex)(ColumnIndex)

    '                            Select Case d
    '                                Case "A"c To "Z"c, "a"c To "z"c, "_"c
    '                                    ColumnIndex += 1
    '                                    b.Append(d)
    '                                Case Else
    '                                    Exit Do
    '                            End Select
    '                        Loop

    '                        Select Case b.ToString.ToUpper
    '                            Case "EVENT"
    '                                Dim tName As String, tParameter As String, tPermission As String, tChannel As String

    '                                Dim l = Script(LineIndex).IndexOf(":"c, ColumnIndex)
    '                                If l < 0 Then Throw New SyntaxException("Event is missing a name.")
    '                                tName = Script(LineIndex).Substring(ColumnIndex, l - ColumnIndex).Trim
    '                                ColumnIndex = l + 1

    '                                If Not IsPluginEvent Then
    '                                    If Parameter IsNot Nothing Then
    '                                        l = Script(LineIndex).IndexOf(":"c, ColumnIndex)
    '                                        If l < 0 Then Throw New SyntaxException("Event is missing a parameter.")
    '                                        tParameter = Script(LineIndex).Substring(ColumnIndex, l - ColumnIndex).Trim
    '                                        ColumnIndex = l + 1
    '                                    End If

    '                                    l = Script(LineIndex).IndexOf(":"c, ColumnIndex)
    '                                    If l < 0 Then Throw New SyntaxException("Event is missing a permission.")
    '                                    tPermission = Script(LineIndex).Substring(ColumnIndex, l - ColumnIndex).Trim
    '                                    ColumnIndex = l + 1

    '                                    l = Script(LineIndex).IndexOf(":"c, ColumnIndex)
    '                                    If l < 0 Then Throw New SyntaxException("Event is missing a channel.")
    '                                    tChannel = Script(LineIndex).Substring(ColumnIndex, l - ColumnIndex).Trim
    '                                    ColumnIndex = l + 1
    '                                End If

    '                                LineIndex += 1
    '                                ColumnIndex = 0

    '                                Dim Skip As Boolean = False
    '                                If Name.ToUpper <> tName.ToUpper Then
    '                                    Skip = True
    '                                ElseIf Not IsPluginEvent Then
    '                                    If tPermission <> "*" AndAlso Not UserHasPermission(Connection, Channel, Sender, tPermission) Then
    '                                        Skip = True
    '                                    ElseIf Not (Channel Like tChannel.Replace("#", "[#]")) Then
    '                                        Skip = True
    '                                    ElseIf Parameter IsNot Nothing AndAlso Not Parameter Like tParameter Then
    '                                        Skip = True
    '                                    End If
    '                                End If

    '                                Dim lResult = ParseLine(Connection, Channel, Skip)
    '                                Select Case lResult
    '                                    Case ExecuteResults.OK
    '                                    Case ExecuteResults.Halted : Return ExecuteResults.Halted
    '                                    Case ExecuteResults.DefaultHalted : Result = ExecuteResults.DefaultHalted
    '                                    Case ExecuteResults.ErrorOccurred : Return ExecuteResults.ErrorOccurred
    '                                    Case ExecuteResults.EndOfBlock, ExecuteResults.EndOfFile
    '                                        Throw New SyntaxException("Event is missing a procedure.")
    '                                End Select
    '                            Case Else
    '                                LineIndex += 1
    '                                ColumnIndex = 0
    '                                GoTo nextline
    '                        End Select
    '                        GoTo nextline
    '                    Case Else
    '                        LineIndex += 1
    '                        ColumnIndex = 0
    '                        GoTo nextline
    '                End Select
    '            Loop
    'NextLine:
    '        Loop
    '        Return Result
    '    End Function

    Public Function RunInstruction(Connection As IRCConnection, Channel As String, Optional ByVal Skip As Boolean = False) As ExecuteResults
        Tokeniser.NextToken()
        Select Case Tokeniser.TokenType
            Case TokenTypes.tEndOfScript
                Return ExecuteResults.EndOfFile
            Case TokenTypes.tOpenBrace
                Dim BlockEnd As Boolean = False, DefaultHalted As Boolean = False, ErrorOccurred As Boolean = False
                Do
                    Dim Result = RunInstruction(Connection, Channel, Skip Or ErrorOccurred)
                    If Result = 1 Then Return ExecuteResults.Halted
                    If Result = 2 Then DefaultHalted = True
                    If Result = 3 Then ErrorOccurred = True
                    If Result = 4 Or Result = 5 Then BlockEnd = True
                Loop Until BlockEnd
                Return If(ErrorOccurred, 3, If(DefaultHalted, 2, 0))
            Case TokenTypes.tCloseBrace
                Return ExecuteResults.EndOfBlock
            Case TokenTypes.tSemicolon  ' Empty statement
                Return ExecuteResults.OK
            Case TokenTypes.tText  ' Command
                Dim CommandText As New StringBuilder(DirectCast(Tokeniser.TokenValue, String))
                Dim Arguments As New List(Of ValueOpCode)

                Dim Bracketed As Boolean

                If Tokeniser.NextToken() = TokenTypes.tOpenParenthesis Then
                    Bracketed = True
                    If Tokeniser.PeekToken() <> TokenTypes.tCloseParenthesis Then
                        Do
                            Dim t1 = ParseExpression()
                            Arguments.Add(t1)
                            If Tokeniser.TokenType = TokenTypes.tCloseParenthesis Then Exit Do
                            If Tokeniser.TokenType <> TokenTypes.tComma Then
                                Throw New SyntaxException("Expected an expression continuation, ',' or ')', but found " & Tokeniser.TokenType)
                            End If
                        Loop
                    Else
                        Tokeniser.NextToken()
                    End If
                Else
                    Bracketed = False
                    Do
                        Select Case Tokeniser.TokenType
                            Case TokenTypes.tSemicolon, TokenTypes.tEndOfLine, TokenTypes.tEndOfScript
                                Exit Do
                            Case TokenTypes.tOpenParenthesis
                                Dim t1 = ParseExpression()
                                Arguments.Add(t1)
                                If Tokeniser.TokenType = TokenTypes.tCloseParenthesis Then Exit Do
                                Throw New SyntaxException("Expected an expression continuation or ')', but found " & Tokeniser.TokenType)
                            Case TokenTypes.tNumber, TokenTypes.tText
                                Arguments.Add(New OpCodeConstant(Tokeniser.TokenValue))
                        End Select
                        Tokeniser.NextToken(True)
                    Loop
                End If

                Tokeniser.NextToken()
                If Tokeniser.TokenType <> TokenTypes.tEndOfLine And Tokeniser.TokenType <> TokenTypes.tSemicolon And Tokeniser.TokenType <> TokenTypes.tEndOfScript Then Throw New SyntaxException("Unxpected " & Tokeniser.TokenType)

                Console.WriteLine("Command: {0}", CommandText.ToString())
                Console.Write("Parameters: ")
                For Each param In Arguments
                    Console.Write(param.Value & ", ")
                Next
                Console.WriteLine()
        End Select
    End Function

    '    ''' <summary>Executes the next command or command block.</summary>
    '    ''' <param name="Skip">If set to True, commands will be parsed, but not executed.</param>
    '    Public Function ParseLine(Connection As IRCConnection, Channel As String, Optional ByVal Skip As Boolean = False) As ExecuteResults
    '        Dim lLine As String, b As New StringBuilder

    '        If LineIndex >= Script.Length Then Return ExecuteResults.EndOfFile
    '        lLine = Script(LineIndex).TrimEnd

    '        Do
    '            If lLine(ColumnIndex) <> " "c And lLine(ColumnIndex) <> ChrW(9) Then Exit Do
    '            ColumnIndex += 1
    '            If ColumnIndex >= lLine.Length Then Return ExecuteResults.OK
    '        Loop

    '        If lLine(ColumnIndex) = "{" Then
    '            ' The start of a block.
    '            LineIndex += 1
    '            ColumnIndex = 0
    '            Dim BlockEnd As Boolean = False, DefaultHalted As Boolean = False, ErrorOccurred As Boolean = False
    '            Do
    '                Dim Result = ParseLine(Connection, Channel, Skip Or ErrorOccurred)
    '                If Result = 1 Then Return ExecuteResults.Halted
    '                If Result = 2 Then DefaultHalted = True
    '                If Result = 3 Then ErrorOccurred = True
    '                If Result = 4 Or Result = 5 Then BlockEnd = True
    '            Loop Until BlockEnd
    '            Return If(ErrorOccurred, 3, If(DefaultHalted, 2, 0))
    '        End If
    '        If lLine(ColumnIndex) = "}" Then
    '            LineIndex += 1
    '            ColumnIndex = 0
    '            Return ExecuteResults.EndOfBlock
    '        End If

    '        If Skip Then
    '            LineIndex += 1
    '            ColumnIndex = 0
    '            Return ExecuteResults.OK
    '        End If

    '        ' Extract the command name.

    '        b = New StringBuilder
    '        For Me.ColumnIndex = Me.ColumnIndex To lLine.Length - 1
    '            Select Case lLine(ColumnIndex)
    '                Case "0"c To "9"c
    '                    Throw New SyntaxException("A number cannot begin a line.")
    '                Case "A"c To "Z"c, "a"c To "z"c
    'Command:
    '                    Dim cName As String, Args As List(Of OpCode)
    '                    ParseCommand(cName)
    '                    Select Case cName.ToUpper
    '                        Case "IF"
    '                            Return ParseIf(Connection, Channel)
    '                        Case "ELSE"
    '                            Throw New SyntaxException("Found 'Else' without a corresponding 'If'.")
    '                        Case "WHILE"
    '                            Return ParseWhile(Connection, Channel)
    '                        Case "HALT"
    '                            LineIndex += 1
    '                            ColumnIndex = 0
    '                            Return ExecuteResults.Halted
    '                        Case "HALTDEF"
    '                            LineIndex += 1
    '                            ColumnIndex = 0
    '                            Return ExecuteResults.DefaultHalted
    '                        Case "SET", "UNSET", "INC", "DEC"
    '                            Return ParseSet(Connection, Channel, cName.ToUpper)
    '                        Case "EVENT", "FUNCTION"
    '                            ParseLine(Connection, Channel, True)
    '                        Case Else
    '                            Dim Target As Object = Nothing
    'Recheck:
    '                            If cName.Length = 0 Then Throw New SyntaxException("Command name expected.")
    '                            ParseArguments(Args)
    '                            If ColumnIndex >= Script(LineIndex).Length Then
    '                                RunCommand(cName, Args, Connection, Channel, False, Target, 1)
    '                            ElseIf Script(LineIndex)(ColumnIndex) = "."c Then
    '                                For Each Plugin In Plugins
    '                                    If cName.ToUpper = Plugin.Key.ToUpper Then
    '                                        Target = Plugin.Value.Obj
    '                                        ColumnIndex += 1
    '                                        ParseCommand(cName)
    '                                        GoTo Recheck
    '                                    End If
    '                                Next
    '                                Target = RunCommand(cName, {}, Connection, Channel, True, Target, 1)
    '                                GoTo Recheck
    '                            Else
    '                                RunCommand(cName, Args, Connection, Channel, False, Target, 1)
    '                            End If
    '                            LineIndex += 1
    '                            ColumnIndex = 0
    '                            Return ExecuteResults.OK
    '                    End Select
    '                Case "/"c
    '                    Me.ColumnIndex += 1
    '                    GoTo Command
    '                Case "$"c
    '                    Me.ColumnIndex += 1
    '                    'ParseFunction()
    '                Case "%"c
    '                    Me.ColumnIndex += 1
    '                    'ParseVariable()
    '                Case " "c, ChrW(9)
    '                Case "("c
    '                    ' Expression
    '                Case ")"c, ","c
    '                    Throw New SyntaxException("Unexpected character, '" & lLine(ColumnIndex) & "'")
    '                Case ";"
    '                    ' Comment
    '                    LineIndex += 1
    '                    ColumnIndex = 0
    '                    Return ExecuteResults.OK
    '                Case Else
    '                    If Char.IsLetter(lLine(ColumnIndex)) Then 'ParseCommand()
    '                    Else : Throw New SyntaxException("Unexpected character, '" & lLine(ColumnIndex) & "'")
    '                    End If
    '            End Select
    '        Next
    '    End Function

    '    Private Function ParseIf(Connection As IRCConnection, Channel As String) As ExecuteResults
    '        ' Parse the expression.
    '        Dim exprCode As OpCode
    '        Do
    '            If ColumnIndex >= Script(LineIndex).Length Then Throw New SyntaxException("If statement is missing an expression.")
    '            Dim c = Script(LineIndex)(ColumnIndex)
    '            Select Case c
    '                Case " "c, ChrW(9)
    '                    ColumnIndex += 1
    '                    Continue Do
    '                Case "("c
    '                    ColumnIndex += 1
    '                    Dim le = ParseExpression()
    '                    If Script(LineIndex)(ColumnIndex) <> ")"c Then Throw New SyntaxException("Unexpected character: '" & Script(LineIndex)(ColumnIndex) & "'.")
    '                    ColumnIndex += 1
    '                    exprCode = le
    '                    Exit Do
    '                Case Else
    '                    Dim le = ParseExpression()
    '                    If ColumnIndex < Script(LineIndex).Length Then Throw New SyntaxException("Unexpected character: '" & Script(LineIndex)(ColumnIndex) & "'.")
    '                    exprCode = le
    '                    Exit Do
    '            End Select
    '        Loop
    '        ' Evaluate the expression.
    '        Dim value = exprCode.Value
    '        value = CBool(value)
    '        Dim Result As ExecuteResults
    '        If value Then
    '            LineIndex += 1
    '            ColumnIndex = 0
    '            Result = ParseLine(Connection, Channel, False)
    '            Select Case Result
    '                Case ExecuteResults.DefaultHalted : Result = ExecuteResults.DefaultHalted
    '                Case ExecuteResults.EndOfFile : Result = ExecuteResults.EndOfFile
    '                Case ExecuteResults.Halted : Result = ExecuteResults.Halted
    '                Case Else : Result = ExecuteResults.OK
    '            End Select
    '        Else
    '            ' The expression is false, so skip the next statement.
    '            LineIndex += 1
    '            ColumnIndex = 0
    '            ParseLine(Connection, Channel, True)
    '            Result = ExecuteResults.OK
    '        End If
    '        ' Look for an 'Else'.
    '        Dim StartPointL As Integer = LineIndex, StartPointC As Integer = ColumnIndex
    '        Do
    '            If ColumnIndex >= Script(LineIndex).Length Then
    '                LineIndex += 1
    '                ColumnIndex = 0
    '                Continue Do
    '            End If
    '            Dim c = Script(LineIndex)(ColumnIndex)
    '            Select Case c
    '                Case " "c, ChrW(9)
    '                    ColumnIndex += 1
    '                Case ";"c
    '                    LineIndex += 1
    '                    ColumnIndex = 0
    '                Case "E"c, "e"c
    '                    Dim b1 = New StringBuilder(c)
    '                    ColumnIndex += 1
    '                    Do
    '                        If ColumnIndex >= Script(LineIndex).Length Then Exit Do
    '                        c = Script(LineIndex)(ColumnIndex)
    '                        Select Case c
    '                            Case "A"c To "Z"c, "a"c To "z"c
    '                                b1.Append(c)
    '                                ColumnIndex += 1
    '                            Case Else
    '                                Exit Do
    '                        End Select
    '                    Loop
    '                    If b1.ToString.ToUpper = "ELSE" Then
    '                        'If ColumnIndex >= Script(LineIndex).TrimEnd.Length Then
    '                        '    LineIndex += 1
    '                        '    ColumnIndex = 0
    '                        'End If
    '                        ' If the expression was false, evaluate the statement.
    '                        If value Then
    '                            LineIndex += 1
    '                            ColumnIndex = 0
    '                            ParseLine(Connection, Channel, True)
    '                        Else
    '                            LineIndex += 1
    '                            ColumnIndex = 0
    '                            Result = ParseLine(Connection, Channel, False)
    '                            Select Case Result
    '                                Case ExecuteResults.DefaultHalted : Result = ExecuteResults.DefaultHalted
    '                                Case ExecuteResults.EndOfFile : Result = ExecuteResults.EndOfFile
    '                                Case ExecuteResults.Halted : Result = ExecuteResults.Halted
    '                                Case Else : Result = ExecuteResults.OK
    '                            End Select
    '                        End If
    '                    Else
    '                        LineIndex = StartPointL
    '                        ColumnIndex = StartPointC
    '                    End If
    '                    Exit Do
    '                Case Else
    '                    ColumnIndex += 1
    '            End Select
    '        Loop
    '        Return Result
    '    End Function

    '    Private Function ParseWhile(Connection As IRCConnection, Channel As String) As ExecuteResults
    '        ' Parse the expression.
    '        Dim exprCode As OpCode
    '        Do
    '            If ColumnIndex >= Script(LineIndex).Length Then Throw New SyntaxException("If statement is missing an expression.")
    '            Dim c = Script(LineIndex)(ColumnIndex)
    '            Select Case c
    '                Case " "c, ChrW(9)
    '                    ColumnIndex += 1
    '                    Continue Do
    '                Case "("c
    '                    ColumnIndex += 1
    '                    Dim le = ParseExpression()
    '                    If Script(LineIndex)(ColumnIndex) <> ")"c Then Throw New SyntaxException("Unexpected character: '" & Script(LineIndex)(ColumnIndex) & "'.")
    '                    ColumnIndex += 1
    '                    exprCode = le
    '                    Exit Do
    '                Case Else
    '                    Dim le = ParseExpression()
    '                    If ColumnIndex < Script(LineIndex).Length Then Throw New SyntaxException("Unexpected character: '" & Script(LineIndex)(ColumnIndex) & "'.")
    '                    exprCode = le
    '                    Exit Do
    '            End Select
    '        Loop
    '        LineIndex += 1
    '        ColumnIndex = 0
    '        Dim StartPointL As Integer = LineIndex, StartPointC As Integer = ColumnIndex
    '        Dim DefaultHalted As Boolean

    '        Do
    '            ' Evaluate the expression.
    '            Dim value = exprCode.Value
    '            If CBool(value) Then
    '                Dim Result = ParseLine(Connection, Channel, False)
    '                Select Case Result
    '                    Case ExecuteResults.DefaultHalted : DefaultHalted = True
    '                    Case ExecuteResults.EndOfFile : Return ExecuteResults.EndOfFile
    '                    Case ExecuteResults.Halted : Return ExecuteResults.Halted
    '                End Select
    '                LineIndex = StartPointL
    '                ColumnIndex = StartPointC
    '            Else
    '                ' The expression is false, so skip the next statement.
    '                ParseLine(Connection, Channel, True)
    '                Return If(DefaultHalted, ExecuteResults.DefaultHalted, ExecuteResults.OK)
    '            End If
    '        Loop
    '    End Function

    '    Private Function ParseSet(Connection As IRCConnection, Channel As String, cName As String) As ExecuteResults
    '        Dim Target As Object, Args As List(Of OpCode), Variable As Object, lrArgs() As Object
    '        Dim vnBuilder As New StringBuilder
    '        ' Parse the varaible name.
    '        Do
    '            If ColumnIndex >= Script(LineIndex).Length Then Throw New SyntaxException("Set statement is missing a variable name.")
    '            Dim c = Script(LineIndex)(ColumnIndex)
    '            Select Case c
    '                Case " "c, ChrW(9)
    '                    ColumnIndex += 1
    '                    Continue Do
    '                Case "%"c
    '                    vnBuilder.Append(c)
    '                    ColumnIndex += 1
    '                    Exit Do
    '                Case "A"c To "Z"c, "a"c To "z"c
    '                    vnBuilder.Append(c)
    '                    ColumnIndex += 1
    '                    Exit Do
    '                Case Else
    '                    If ColumnIndex < Script(LineIndex).Length Then Throw New SyntaxException("Expected a %variable name, but found '" & Script(LineIndex)(ColumnIndex) & "'.")
    '            End Select
    '        Loop
    '        Do
    '            If ColumnIndex >= Script(LineIndex).Length Then
    '                If cName.ToUpper = "SET" Then Throw New SyntaxException("Set statement is missing a value.")
    '                GoTo space
    '            End If
    '            Dim c = Script(LineIndex)(ColumnIndex)
    '            Select Case c
    '                Case "A"c To "Z"c, "0"c To "9"c, "a"c To "z"c, "_"c, Is >= Chr(128)
    '                    vnBuilder.Append(c)
    '                    ColumnIndex += 1
    '                Case "("c
    '                    Args = New List(Of OpCode)
    '                    If vnBuilder(0) = "%" Then Throw New SyntaxException("'(' is not valid on a variable name.")
    '                    ColumnIndex += 1
    '                    ParseArgumentsBrackets(Args)
    '                    If ColumnIndex >= Script(LineIndex).Length Then Throw New SyntaxException("')' missing")
    '                    If Script(LineIndex)(ColumnIndex) <> ")"c Then Throw New SyntaxException("Unexpected character '" & Script(LineIndex)(ColumnIndex) & "'.")
    '                    ColumnIndex += 1
    '                    If ColumnIndex >= Script(LineIndex).Length Then
    '                        If cName.ToUpper = "SET" Then Throw New SyntaxException("Set statement is missing a value.")
    '                        Exit Do
    '                    End If
    '                    Select Case Script(LineIndex)(ColumnIndex)
    '                        Case "."
    '                            GoTo Period
    '                        Case " ", ChrW(9)
    '                            GoTo Space
    '                        Case Else
    '                            Throw New SyntaxException("Unexpected character '" & Script(LineIndex)(ColumnIndex) & "'.")
    '                    End Select
    '                Case "."c
    'Period:
    '                    If vnBuilder(0) = "%"c Then
    '                        Variable = vnBuilder.ToString
    '                        If lPlugin.Variables.ContainsKey(Variable) Then
    '                            Target = lPlugin.Variables(Variable)
    '                        Else
    '                            Throw New KeyNotFoundException("No such variable '" & Variable & "'.")
    '                        End If
    '                    Else
    '                        Variable = vnBuilder.ToString
    '                        For Each Plugin In VBot.Plugins
    '                            If DirectCast(Variable, String).ToUpper = Plugin.Key.ToUpper Then
    '                                Target = Plugin.Value.Obj
    '                                GoTo Found
    '                            End If
    '                        Next
    '                        Target = RunCommand(Variable, Args, Connection, Channel, True, Target, 1)
    '                        Args = New List(Of OpCode)
    'Found:
    '                    End If
    '                    vnBuilder.Clear()
    '                    ColumnIndex += 1
    '                Case " "c, ChrW(9)
    'Space:
    '                    If vnBuilder(0) = "%"c Then
    '                        Variable = vnBuilder.ToString
    '                    Else
    '                        Variable = vnBuilder.ToString
    '                        Variable = RunCommand(Variable, Args, Connection, Channel, True, Target, 0, lrArgs)
    '                    End If
    '                    ColumnIndex += 1
    '                    Exit Do
    '                Case Else
    '                    Throw New SyntaxException("Expected an identifier, but found '" & Script(LineIndex)(ColumnIndex) & "'.")
    '            End Select
    '        Loop
    '        ' Parse the expression.
    '        Dim Value As Object, ValueCode As OpCode, TypedValue As TypedValue
    '        If ColumnIndex < Script(LineIndex).Length Then
    '            ValueCode = ParseExpression()
    '            If ColumnIndex < Script(LineIndex).Length Then Throw New SyntaxException("Unexpected character: '" & Script(LineIndex)(ColumnIndex) & "'.")
    '            Value = ValueCode.Value
    '            TypedValue = New TypedValue(Value)
    '        End If
    '        Select Case cName.ToUpper
    '            Case "SET"
    '                If TypeOf Variable Is String Then
    '                    If lPlugin.Variables.ContainsKey(vnBuilder.ToString) Then
    '                        lPlugin.Variables(vnBuilder.ToString) = TypedValue
    '                    Else
    '                        lPlugin.Variables.Add(vnBuilder.ToString, TypedValue)
    '                    End If
    '                ElseIf TypeOf Variable Is FieldInfo Then
    '                    DirectCast(Variable, FieldInfo).SetValue(Target, CTypeDynamic(Value, DirectCast(Variable, FieldInfo).FieldType))
    '                ElseIf TypeOf Variable Is PropertyInfo Then
    '                    DirectCast(Variable, PropertyInfo).SetValue(Target, CTypeDynamic(Value, DirectCast(Variable, PropertyInfo).PropertyType), lrArgs)
    '                End If
    '            Case "UNSET"
    '                If TypeOf Variable Is String Then
    '                    lPlugin.Variables.Remove(vnBuilder.ToString)
    '                ElseIf TypeOf Variable Is FieldInfo Then
    '                    DirectCast(Variable, FieldInfo).SetValue(Target, Nothing)
    '                ElseIf TypeOf Variable Is PropertyInfo Then
    '                    DirectCast(Variable, PropertyInfo).SetValue(Target, Nothing, lrArgs)
    '                End If
    '            Case "INC"
    '                If TypedValue Is Nothing Then TypedValue = New TypedValue(1D)
    '                If TypeOf Variable Is String Then
    '                    If lPlugin.Variables.ContainsKey(vnBuilder.ToString) Then
    '                        If lPlugin.Variables(vnBuilder.ToString).Type = TypedValue.BasicType.tDecimal And TypedValue.Type = ScriptEngine.TypedValue.BasicType.tDecimal Then
    '                            lPlugin.Variables(vnBuilder.ToString) = New TypedValue(CDec(lPlugin.Variables(vnBuilder.ToString).Value) + TypedValue.Value)
    '                        Else
    '                            Throw New InvalidCastException("Cannot increment a non-numeric value.")
    '                        End If
    '                    ElseIf TypedValue.Type = ScriptEngine.TypedValue.BasicType.tDecimal Then
    '                        lPlugin.Variables.Add(vnBuilder.ToString, TypedValue)
    '                    Else
    '                        Throw New InvalidCastException("Cannot increment a non-numeric value.")
    '                    End If
    '                ElseIf TypeOf Variable Is FieldInfo Then
    '                    DirectCast(Variable, FieldInfo).SetValue(Target, CTypeDynamic(DirectCast(Variable, FieldInfo).GetValue(Target) + Value, DirectCast(Variable, FieldInfo).FieldType))
    '                ElseIf TypeOf Variable Is PropertyInfo Then
    '                    DirectCast(Variable, PropertyInfo).SetValue(Target, CTypeDynamic(DirectCast(Variable, FieldInfo).GetValue(Target) + Value, DirectCast(Variable, PropertyInfo).PropertyType), lrArgs)
    '                End If
    '            Case "DEC"
    '                If TypedValue Is Nothing Then TypedValue = New TypedValue(1D)
    '                If TypeOf Variable Is String Then
    '                    If lPlugin.Variables.ContainsKey(vnBuilder.ToString) Then
    '                        If lPlugin.Variables(vnBuilder.ToString).Type = TypedValue.BasicType.tDecimal And TypedValue.Type = ScriptEngine.TypedValue.BasicType.tDecimal Then
    '                            lPlugin.Variables(vnBuilder.ToString) = New TypedValue(CDec(lPlugin.Variables(vnBuilder.ToString).Value) - TypedValue.Value)
    '                        Else
    '                            Throw New InvalidCastException("Cannot decrement a non-numeric value.")
    '                        End If
    '                    ElseIf TypedValue.Type = ScriptEngine.TypedValue.BasicType.tDecimal Then
    '                        lPlugin.Variables.Add(vnBuilder.ToString, TypedValue)
    '                    Else
    '                        Throw New InvalidCastException("Cannot decrement a non-numeric value.")
    '                    End If
    '                ElseIf TypeOf Variable Is FieldInfo Then
    '                    DirectCast(Variable, FieldInfo).SetValue(Target, CTypeDynamic(DirectCast(Variable, FieldInfo).GetValue(Target) - Value, DirectCast(Variable, FieldInfo).FieldType))
    '                ElseIf TypeOf Variable Is PropertyInfo Then
    '                    DirectCast(Variable, PropertyInfo).SetValue(Target, CTypeDynamic(DirectCast(Variable, FieldInfo).GetValue(Target) - Value, DirectCast(Variable, PropertyInfo).PropertyType), lrArgs)
    '                End If
    '        End Select
    '        LineIndex += 1
    '        ColumnIndex = 0
    '        Return ExecuteResults.OK
    '    End Function

    '    ''' <summary>Parses a command name and its arguments.</summary>
    '    ''' <param name="CommandName">The name of the command.</param>
    '    ''' <param name="args">A list of the arguments to the command.</param>
    '    Private Sub ParseCommand(ByRef CommandName As String)
    '        Dim b As New StringBuilder

    '        ' Parse the command name.
    '        Do
    '            If ColumnIndex >= Script(LineIndex).Length Then
    '                CommandName = b.ToString
    '                Return
    '            End If
    '            Dim c = Script(LineIndex)(ColumnIndex)
    '            Select Case c
    '                Case "A"c To "Z"c, "a"c To "z"c, "0"c To "9"c
    '                    b.Append(c)
    '                    ColumnIndex += 1
    '                Case " "c, ChrW(9), "("c, "."c
    '                    Exit Do
    '                Case Else
    '                    Throw New SyntaxException("Unexpected character, '" & c & "'")
    '            End Select
    '        Loop
    '        CommandName = b.ToString
    '    End Sub

    '    Private Sub ParseArguments(ByRef args As List(Of OpCode))
    '        ' Parse the arguments.
    '        Do
    '            If ColumnIndex >= Script(LineIndex).Length Then
    '                args = New List(Of OpCode)
    '                Return
    '            End If
    '            Dim c = Script(LineIndex)(ColumnIndex)
    '            Select Case c
    '                Case "A" To "Z", "a" To "z", "0" To "9"
    '                    ParseArgumentsSpaced(args)
    '                    Exit Do
    '                Case " "c, ChrW(9)
    '                    ColumnIndex += 1
    '                    ParseArgumentsSpaced(args)
    '                    Exit Do
    '                Case "."c
    '                    args = New List(Of OpCode)
    '                    Return
    '                Case "("c
    '                    ParseArgumentsBrackets(args)
    '                    Exit Do
    '                Case Else
    '                    Throw New SyntaxException("Unexpected character, '" & c & "'")
    '            End Select
    '        Loop
    '    End Sub
    '    ''' <summary>Parses the arguments for a command in bracketed format.</summary>
    '    ''' <remarks>The format is as follows: arg1, arg2, (expr3), arg4)</remarks>
    '    Private Sub ParseArgumentsBrackets(ByRef args As List(Of OpCode))
    '        args = New List(Of OpCode)
    '        Do
    '            If ColumnIndex >= Script(LineIndex).Length Then Return
    '            Dim c = Script(LineIndex)(ColumnIndex)

    '            Select Case c
    '                Case " "c, ChrW(9)
    '                    ColumnIndex += 1
    '                    Continue Do
    '                Case ")"c
    '                    ' End of parameters
    '                    Return
    '                Case ","c
    '                    ' Empty parameter
    '                    ColumnIndex += 1
    '                    Continue Do
    '            End Select

    '            Dim Result = ParseExpression()
    '            args.Add(Result)

    '            Select Case Script(LineIndex)(ColumnIndex)
    '                Case ")"c
    '                    ' End of parameters
    '                    Return
    '                Case ","c
    '                    ' Empty parameter
    '                    ColumnIndex += 1
    '                    Continue Do
    '                Case Else
    '                    Throw New SyntaxException("Expected an expression continuation, ',' or ')' , but found '" & Script(LineIndex)(ColumnIndex) & "'.")
    '            End Select
    '        Loop
    '    End Sub
    '    ''' <summary>Parses the arguments for a command in open format.</summary>
    '    ''' <remarks>The format is as follows: arg1 arg2 (expr3) arg4 &lt;end of line&gt;</remarks>
    '    Private Sub ParseArgumentsSpaced(ByRef args As List(Of OpCode))
    '        args = New List(Of OpCode)
    '        Do
    '            If ColumnIndex >= Script(LineIndex).Length Then Return
    '            Dim c = Script(LineIndex)(ColumnIndex)

    '            Select Case c
    '                Case " "c, ChrW(9)
    '                    ColumnIndex += 1
    '                    Continue Do
    '                Case "("c
    '                    ColumnIndex += 1
    '                    Dim Result = ParseExpression()
    '                    If ColumnIndex >= Script(LineIndex).Length Then Throw New SyntaxException("')' missing")
    '                    If Script(LineIndex)(ColumnIndex) = ")"c Then
    '                        args.Add(Result)
    '                        ColumnIndex += 1
    '                    Else
    '                        Throw New SyntaxException("Unexpected character, ')'")
    '                    End If
    '                Case ")"c
    '                    Throw New SyntaxException("Unexpected character, ')'")
    '                Case ","c
    '                    Throw New SyntaxException("Unexpected character, ','")
    '                Case "$"c
    '                    ColumnIndex += 1
    '                    args.Add(ParseFunction())
    '                Case "%"c
    '                    ColumnIndex += 1
    '                    Dim Result = ParseVariable()
    '                    args.Add(New OpCodeVariable(Result, lPlugin))
    '                Case """"c, "'"c
    '                    Dim Result = ParseStringQuoted(c = """"c)
    '                    args.Add(Result)
    '                Case Else
    '                    Dim Result = ParseStringImpromptu()
    '                    args.Add(New OpCodeConstant(Result))
    '                    ColumnIndex += 1
    '            End Select
    '        Loop
    '    End Sub

    '    ''' <summary>Executes a command, given parsed arguments.</summary>
    '    ''' <param name="CommandName">The name of the command to execute.</param>
    '    ''' <param name="args">The list of arguments.</param>
    '    ''' <returns>If the command is a function, the return value of the function.</returns>
    '    Friend Function RunCommand(ByVal CommandName As String, ByVal args As IEnumerable(Of OpCode), Connection As IRCConnection, Channel As String, IsFunction As Boolean, Target As Object, Action As Short, Optional ByRef Value As Object = Nothing)
    '        Dim BestMatch As System.Reflection.MethodInfo, BestMatchData() As Short
    '        Dim largIsConnection As New List(Of Integer), largConnection As New List(Of IRCConnection)

    '        Dim largs As New List(Of TypedValue)
    '        If args IsNot Nothing Then
    '            For i = 0 To args.Count - 1
    '                largs.Add(New TypedValue(args(i).Value))
    '                largIsConnection.Add(0)
    '                largConnection.Add(Nothing)
    '            Next
    '        End If

    '        Dim Member As MemberInfo, Method As MethodInfo

    '        If Target IsNot Nothing And IsFunction Then
    '            For Each Field In Target.GetType.GetFields
    '                If Field.Name.ToUpper = CommandName.ToUpper Then
    '                    Select Case Action
    '                        Case 0
    '                            Return Field
    '                        Case 1
    '                            Return Field.GetValue(Target)
    '                        Case 2
    '                            Field.SetValue(Target, Value)
    '                            Return Nothing
    '                    End Select
    '                End If
    '            Next
    '        End If

    '        If Target Is Nothing Then Target = If(IsFunction, lPlugin._Functions, lPlugin._Commands)

    '        Member = RunCommandSub(CommandName, largs, largIsConnection, largConnection, IsFunction, 0, Target.GetType)
    '        If Member Is Nothing Then
    '            If Connection IsNot Nothing Then
    '                largs.Insert(0, New TypedValue(Connection))
    '                largIsConnection.Insert(0, 1)
    '                largConnection.Insert(0, Connection)
    '                Member = RunCommandSub(CommandName, largs, largIsConnection, largConnection, IsFunction, 1, Target.GetType)
    '                If Member Is Nothing Then
    '                    If Channel IsNot Nothing Then
    '                        largs.Insert(1, New TypedValue(Channel))
    '                        largIsConnection.Insert(1, -1)
    '                        largConnection.Insert(1, Nothing)
    '                        Member = RunCommandSub(CommandName, largs, largIsConnection, largConnection, IsFunction, 2, Target.GetType)
    '                        If Member Is Nothing Then
    '                            Throw New MissingMemberException("A method with the name " & CommandName & " with the supplied parameters was not found.")
    '                        End If
    '                    Else
    '                        Throw New MissingMemberException("A method with the name " & CommandName & " with the supplied parameters was not found.")
    '                    End If
    '                End If
    '            Else
    '                Throw New MissingMemberException("A method with the name " & CommandName & " with the supplied parameters was not found.")
    '            End If
    '        End If

    '        Dim Parameters() As ParameterInfo

    '        If TypeOf Member Is MethodInfo Then
    '            Parameters = DirectCast(Member, MethodInfo).GetParameters
    '        ElseIf TypeOf Member Is PropertyInfo Then
    '            Parameters = DirectCast(Member, PropertyInfo).GetIndexParameters
    '        End If

    '        Dim lrArgs As New List(Of Object)
    '        For i = 0 To Parameters.Count - 1
    '            If i >= largs.Count Then
    '                lrArgs.Add(Parameters(i).DefaultValue)
    '            Else
    '                If Parameters(i).ParameterType = GetType(IRCConnection) Then
    '                    lrArgs.Add(largConnection(i))
    '                ElseIf i = Parameters.Count - 1 AndAlso Parameters(i).ParameterType.IsArray And largs(i).Type <> TypedValue.BasicType.tArray AndAlso Parameters(i).ParameterType.GetArrayRank = 1 Then
    '                    Dim lrArray As New List(Of Object)

    '                    'For j = i To largs.Count - 1
    '                    '    lrArray.Add(CTypeDynamic(largs(i).Value, Method.GetParameters(i).ParameterType.GetElementType))
    '                    'Next

    '                    ' Create the array object using reflection.
    '                    Dim ArrayObject = Parameters(i).ParameterType.GetConstructors()(0).Invoke({largs.Count - i})
    '                    ' Populate the array.
    '                    For j = i To largs.Count - 1
    '                        Dim s As String()
    '                        ArrayObject.SetValue(CTypeDynamic(largs(i).Value, Parameters(i).ParameterType.GetElementType), j - i)
    '                    Next

    '                    lrArgs.Add(ArrayObject)
    '                    Exit For
    '                Else
    '                    lrArgs.Add(CTypeDynamic(largs(i).Value, Parameters(i).ParameterType))
    '                End If
    '            End If
    '        Next

    '        Select Case Action
    '            Case 0
    '                Value = lrArgs.ToArray
    '                Return Member
    '            Case 1
    '                If TypeOf Member Is MethodInfo Then
    '                    Return DirectCast(Member, MethodInfo).Invoke(Target, lrArgs.ToArray)
    '                ElseIf TypeOf Member Is PropertyInfo Then
    '                    If DirectCast(Member, PropertyInfo).CanRead Then
    '                        Return DirectCast(Member, PropertyInfo).GetValue(Target, If(lrArgs.Count > 0, lrArgs.ToArray, Nothing))
    '                    Else
    '                        Throw New InvalidOperationException("Property '" & Member.Name & "' is write-only.")
    '                    End If
    '                End If
    '            Case 2
    '                If TypeOf Member Is MethodInfo Then
    '                    Throw New InvalidOperationException("Cannot set a value to a procedure.")
    '                ElseIf TypeOf Member Is PropertyInfo Then
    '                    If DirectCast(Member, PropertyInfo).CanWrite Then
    '                        DirectCast(Member, PropertyInfo).SetValue(Target, Value, If(lrArgs.Count > 0, lrArgs.ToArray, Nothing))
    '                    Else
    '                        Throw New InvalidOperationException("Property '" & Member.Name & "' is read-only.")
    '                    End If
    '                End If
    '            Case Else
    '                Throw New ArgumentException("Action must be 0, 1 or 2.", "Action")
    '        End Select
    '    End Function
    '    Private Function RunCommandSub(CommandName As String, lArgs As List(Of TypedValue), ByRef largIsConnection As List(Of Integer), ByRef lArgConnection As List(Of IRCConnection), IsFunction As Boolean, Stage As Short, TargetType As Type) As MemberInfo
    '        Dim Result As MemberInfo
    '        ' Check the direct type first.
    '        Result = RunCommandSub2(CommandName, lArgs, largIsConnection, lArgConnection, IsFunction, Stage, TargetType)
    '        If Result IsNot Nothing Then Return Result

    '        ' Check interfaces.
    '        For Each I In TargetType.GetInterfaces
    '            Result = RunCommandSub2(CommandName, lArgs, largIsConnection, lArgConnection, IsFunction, Stage, I)
    '            If Result IsNot Nothing Then Return Result
    '        Next
    '        Return Nothing
    '    End Function
    '    ''' <summary>Chooses a method on a target object to execute, given a list of parameters.</summary>
    '    ''' <param name="CommandName">The name of the method or function to execute.</param>
    '    ''' <param name="lArgs">The list of arguments.</param>
    '    ''' <param name="largIsConnection">A list specifying whether the corresponding parameter represents an IRC connection.  0: unknown  1: yes  -1: no</param>
    '    ''' <param name="lArgConnection">The connection that the corresponding parameter refers to.</param>
    '    ''' <param name="IsFunction">If True, a function will be looked for. If False, a method will be looked for.</param>
    '    ''' <param name="Stage">0: parameters are as given in the script  1: the current IRC connection is prepended  2: the current connection and channel are prepended</param>
    '    ''' <param name="TargetType">The type to search on.</param>
    '    ''' <returns>A MethodInfo object representing the best match found, or Nothing if no match is found.</returns>
    '    Private Function RunCommandSub2(CommandName As String, lArgs As List(Of TypedValue), ByRef largIsConnection As List(Of Integer), ByRef lArgConnection As List(Of IRCConnection), IsFunction As Boolean, Stage As Short, TargetType As Type) As MemberInfo
    '        Dim BestMatch As MemberInfo = Nothing, BestMatchData() As Short = {0}, Method As MethodInfo

    '        For Each Procedure In TargetType.GetMethods
    '            If Procedure.Name.ToUpper <> CommandName.ToUpper Then Continue For
    '            If IsFunction And Procedure.ReturnType Is GetType(Void) Then Continue For ' Functions must return a value.

    '            Dim Parameters = Procedure.GetParameters
    '            Dim MatchData(Parameters.Count) As Short  ' One 'summary' element, plus one element for each parameter. 0 = wrong type; 1 = implicit conversion; 2 = correct type

    '            If Stage = 1 AndAlso Parameters(0).Name.ToUpper <> "CONNECTION" Then
    '                Continue For
    '            ElseIf Stage = 2 AndAlso (Parameters(0).Name.ToUpper <> "CONNECTION" And Parameters(1).Name.ToUpper <> "CHANNEL") Then
    '                Continue For
    '            End If

    '            If Parameters.Count = 0 And lArgs.Count = 0 Then
    '                MatchData(0) = 10
    '            Else
    '                For i = 0 To Parameters.Count - 1
    '                    If lArgs.Count = i Then If Parameters(i).IsOptional Then Exit For Else GoTo skip

    '                    ' If the final parameter is a one-dimensional array, we can take all arguments supplied after it as part of that array.
    '                    If i = Parameters.Count - 1 And Parameters(i).ParameterType.IsArray And lArgs(i).Type <> TypedValue.BasicType.tArray AndAlso Parameters(i).ParameterType.GetArrayRank = 1 Then
    '                        Dim mTotal = 0
    '                        For j = i To lArgs.Count - 1
    '                            Dim m = CheckType(Parameters(i).ParameterType.GetElementType, lArgs, largIsConnection, lArgConnection, j)
    '                            If m = 0 Then GoTo skip
    '                            mTotal += m
    '                        Next
    '                        MatchData(i + 1) = mTotal / 2
    '                        Continue For
    '                    End If

    '                    MatchData(i + 1) = CheckType(Parameters(i).ParameterType, lArgs, largIsConnection, lArgConnection, i)
    '                    If MatchData(i + 1) = 0 Then GoTo skip
    '                Next

    '                For i = 1 To UBound(MatchData)
    '                    MatchData(0) += MatchData(i)
    '                Next
    '            End If
    '            If MatchData(0) > BestMatchData(0) Then
    '                BestMatch = Procedure
    '                BestMatchData = MatchData
    '            End If
    'skip:
    '        Next

    '        If IsFunction Then
    '            For Each Prop In TargetType.GetProperties
    '                If Prop.Name.ToUpper <> CommandName.ToUpper Then Continue For
    '                If Not Prop.CanRead Then Continue For
    '                Dim Procedure = Prop.GetGetMethod

    '                Dim Parameters = Procedure.GetParameters
    '                Dim MatchData(Parameters.Count) As Short  ' One 'summary' element, plus one element for each parameter. 0 = wrong type; 1 = implicit conversion; 2 = correct type

    '                If Stage = 1 AndAlso Parameters(0).Name.ToUpper <> "CONNECTION" Then
    '                    Continue For
    '                ElseIf Stage = 2 AndAlso (Parameters(0).Name.ToUpper <> "CONNECTION" And Parameters(1).Name.ToUpper <> "CHANNEL") Then
    '                    Continue For
    '                End If

    '                If Parameters.Count = 0 And lArgs.Count = 0 Then
    '                    MatchData(0) = 10
    '                Else
    '                    For i = 0 To Parameters.Count - 1
    '                        If lArgs.Count = i Then If Parameters(i).IsOptional Then Exit For Else GoTo pskip

    '                        ' If the final parameter is a one-dimensional array, we can take all arguments supplied after it as part of that array.
    '                        If i = Parameters.Count - 1 And Parameters(i).ParameterType.IsArray And lArgs(i).Type <> TypedValue.BasicType.tArray AndAlso Parameters(i).ParameterType.GetArrayRank = 1 Then
    '                            Dim mTotal = 0
    '                            For j = i To lArgs.Count - 1
    '                                Dim m = CheckType(Parameters(i).ParameterType.GetElementType, lArgs, largIsConnection, lArgConnection, j)
    '                                If m = 0 Then GoTo pskip
    '                                mTotal += m
    '                            Next
    '                            MatchData(i + 1) = mTotal / 2
    '                            Continue For
    '                        End If

    '                        MatchData(i + 1) = CheckType(Parameters(i).ParameterType, lArgs, largIsConnection, lArgConnection, i)
    '                        If MatchData(i + 1) = 0 Then GoTo pskip
    '                    Next

    '                    For i = 1 To UBound(MatchData)
    '                        MatchData(0) += MatchData(i)
    '                    Next
    '                End If
    '                If MatchData(0) > BestMatchData(0) Then
    '                    BestMatch = Prop
    '                    BestMatchData = MatchData
    '                End If
    'pskip:
    '            Next
    '        End If

    '        Return BestMatch
    '    End Function
    '    Private Function CheckType(ExpectedType As Type, lArgs As List(Of TypedValue), ByRef largIsConnection As List(Of Integer), ByRef lArgConnection As List(Of IRCConnection), Index As Integer) As Short
    '        Dim lType As Type
    '        lType = ExpectedType

    '        Select Case lType
    '            Case GetType(Byte), GetType(SByte), GetType(Short), GetType(UShort), GetType(Integer), GetType(UInteger), GetType(Long), GetType(ULong),
    '                GetType(Single), GetType(Double), GetType(Decimal)
    '                Select Case lArgs(Index).Type
    '                    Case TypedValue.BasicType.tDecimal
    '                        Return 3
    '                    Case TypedValue.BasicType.tString, TypedValue.BasicType.tBoolean
    '                        Return 2
    '                    Case TypedValue.BasicType.tNull
    '                        Return 1
    '                    Case Else
    '                        Return 0
    '                End Select
    '            Case GetType(String)
    '                Select Case lArgs(Index).Type
    '                    Case TypedValue.BasicType.tString
    '                        Return 3
    '                    Case TypedValue.BasicType.tArray, TypedValue.BasicType.tBoolean, TypedValue.BasicType.tDate, TypedValue.BasicType.tDecimal
    '                        Return 2
    '                    Case TypedValue.BasicType.tNull
    '                        Return 1
    '                    Case Else
    '                        Return 0
    '                End Select
    '            Case GetType(Boolean)
    '                Select Case lArgs(Index).Type
    '                    Case TypedValue.BasicType.tBoolean
    '                        Return 3
    '                    Case TypedValue.BasicType.tDecimal, TypedValue.BasicType.tString
    '                        Return 2
    '                    Case TypedValue.BasicType.tNull
    '                        Return 1
    '                    Case Else
    '                        Return 0
    '                End Select
    '            Case GetType(Date)
    '                Select Case lArgs(Index).Type
    '                    Case TypedValue.BasicType.tDate
    '                        Return 3
    '                    Case TypedValue.BasicType.tDecimal, TypedValue.BasicType.tString
    '                        Return 2
    '                    Case TypedValue.BasicType.tNull
    '                        Return 1
    '                    Case Else
    '                        Return 0
    '                End Select
    '            Case GetType(IRCConnection)
    '                If largIsConnection(Index) = 0 Then
    '                    If lArgs(Index).Type = TypedValue.BasicType.tObject AndAlso TypeOf lArgs(Index).Value Is IRCConnection Then
    '                        largIsConnection(Index) = 1
    '                        lArgConnection(Index) = lArgs(Index).Value
    '                    ElseIf lArgs(Index).Type = TypedValue.BasicType.tString Then
    '                        ' Is the first parameter a connection address?
    '                        For Each _connection In Connections
    '                            If _connection.Address.ToUpper = DirectCast(lArgs(0).Value, String).ToUpper Then
    '                                largIsConnection(Index) = 1
    '                                lArgConnection(Index) = _connection
    '                                Return 1
    '                            End If
    '                        Next
    '                        If largIsConnection(Index) = 0 Then largIsConnection(Index) = -1
    '                    Else
    '                        Return 0
    '                    End If
    '                ElseIf largIsConnection(Index) = 1 Then
    '                    Return 1
    '                Else
    '                    Return 0
    '                End If
    '            Case Else
    '                Select Case lArgs(Index).Type
    '                    Case TypedValue.BasicType.tObject
    '                        Return 3
    '                    Case TypedValue.BasicType.tNull
    '                        Return 1
    '                    Case Else
    '                        Return 0
    '                End Select
    '        End Select
    '    End Function

    '    Private Sub ThrowError(ByVal Description As String)
    '        OutputLine(String.Format("\cREDScript error\cWHITE {0} line {1} column {2}\r", Filename, Line, Column))
    '        OutputLine("\cWHITE    " & Description & "\r")
    '    End Sub
    '    Private Sub ThrowWarning(ByVal Description As String)
    '        OutputLine(String.Format("\cYELLOWScript warning\cWHITE {0} line {1} column {2}\r", Filename, Line, Column))
    '        OutputLine("\cWHITE    " & Description & "\r")
    '    End Sub

    '    ''' <summary>Returns a string showing the current pointer location, with IRC colour codes.</summary>
    '    ''' <remarks>This shows the line where the error occurred, with the current character position highlighted.</remarks>
    '    Public Function UserFriendlyErrorLocation() As String
    '        Dim b As New StringBuilder
    '        b.Append(ChrW(3) & "5,99")
    '        'For i = Math.Max(0, Column - 3) To Math.Min(Script(Index).Length - 1, Column + 3)
    '        'For i = 0 To Script(LineIndex).Length - 1
    '        Dim StartIndex As Integer, EndIndex As Integer, i As Integer
    '        For i = Offset - 1 To Offset - 30 Step -1
    '            If i < 0 OrElse Script(i) = ChrW(10) OrElse Script(i) = ChrW(13) Then
    '                StartIndex = i + 1
    '                Exit For
    '            End If
    '        Next
    '        For i = Offset + 1 To Offset + 30
    '            If i >= Script.Length OrElse Script(i) = ChrW(10) OrElse Script(i) = ChrW(13) Then
    '                EndIndex = i - 1
    '                Exit For
    '            End If
    '        Next
    '        For i = StartIndex To EndIndex
    '            If i = ColumnIndex Then
    '                b.Append(ChrW(3) & "4" & ChrW(31))
    '                b.Append(Script(i))
    '                b.Append(ChrW(3) & "5" & ChrW(31))
    '            Else
    '                b.Append(Script(i))
    '            End If
    '        Next
    '        If Offset >= Script.Length OrElse Script(Offset) = ChrW(10) OrElse Script(Offset) = ChrW(13) Then b.Append(ChrW(31) & ChrW(3) & "4 ")
    '        Return b.ToString
    '    End Function
End Class
