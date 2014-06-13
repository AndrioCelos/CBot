Imports System.Text
Imports ScriptEngine.ScriptEngine

Public Class Tokeniser
    Private _Script As Script

    Public TokenType As TokenTypes
    Public TokenValue As Object
    Public Line As Integer
    Public Column As Integer
    Public Offset As Integer
    Public Lines() As Integer

    Private b As New StringBuilder, c As Char

    Public Sub New(Script As Script)
        Me._Script = Script
        Line = 1
        Column = 1
    End Sub

    Public Sub FindLines()
        Dim _Lines As New List(Of Integer)
        If _Script.Script Is Nothing Then Throw New InvalidOperationException("An attempt was made to read when no script was loaded.")
        _Lines.Add(0)
        For i = 0 To _Script.Script.Length - 1
            If _Script.Script(i) = ChrW(10) Then _Lines.Add(i + 1)
        Next
        Lines = _Lines.ToArray()
    End Sub

    Public Sub Seek(Offset As Integer)
        If Offset < 0 Then Throw New ArgumentOutOfRangeException("Offset", "Offset cannot be negative.")
        Me.Offset = Offset
        ' Use a binary search procedure to find out what line number this is on.
        Dim MinLine As Integer = 0, MaxLine As Integer = UBound(Lines)
        Dim Pivot As Integer = 0
        Do While MaxLine - MinLine > 1
            Pivot = (MaxLine + MinLine) / 2  ' This can't be equal to MaxLine because of the while loop condition mandating that the bounds are too far apart for that.
            If Offset = Lines(Pivot) Then
                Exit Do
            ElseIf Offset > Lines(Pivot) Then
                MinLine = Pivot
            Else
                MaxLine = Pivot
            End If
        Loop
        Line = Pivot + 1
        Column = Offset - Lines(Pivot) + 1
    End Sub

    Public Function NextChar() As Integer
        If Offset >= _Script.Script.Length Then Return -1
        c = _Script.Script(Offset)
        Offset += 1
        Return AscW(c)
    End Function

    Public Function PeekChar() As Integer
        If Offset >= _Script.Script.Length Then Return -1
        c = _Script.Script(Offset)
        Return AscW(c)
    End Function

    Public Function PeekToken(Optional ReturnNewline As Boolean = False) As TokenTypes
        Dim _Line = Line
        Dim _Column = Column
        Dim _Offset = Offset

        NextToken(ReturnNewline)

        Line = _Line
        Column = _Column
        Offset = _Offset

        Return TokenType
    End Function

    Public Function NextToken(Optional ReturnNewline As Boolean = False) As TokenTypes
        If _Script.Script Is Nothing Then Throw New InvalidOperationException("An attempt was made to read a token when no script was loaded.")
        If Offset >= _Script.Script.Length Then
            TokenType = TokenTypes.tEndOfScript
            TokenValue = Nothing
            Return TokenTypes.tEndOfScript
        End If

        TokenType = TokenTypes.tNone
        Do While TokenType = TokenTypes.tNone
            c = _Script.Script(Offset)
            Select Case c
                Case " ", ChrW(9)
                    Offset += 1 : Column += 1
                Case ChrW(13)
                    Offset += 1
                    If Offset <= _Script.Script.Length AndAlso _Script.Script(Offset) = ChrW(10) Then Offset += 1
                    Column = 1
                    Line += 1
                    If ReturnNewline Then TokenType = TokenTypes.tEndOfLine
                Case ChrW(10)  ' This won't happen in a CR+LF, because the CR case above will eat the LF too.
                    Column = 1
                    Line += 1
                    If ReturnNewline Then TokenType = TokenTypes.tEndOfLine
                Case "A"c To "Z"c, "a"c To "z"c, "_"c, "#"c
                    TokenValue = ParseWord()
                    Select Case DirectCast(TokenValue, String).ToUpper()
                        Case "IF"
                            TokenType = TokenTypes.tIf
                        Case "ELSE"
                            TokenType = TokenTypes.tElse
                        Case "WHILE"
                            TokenType = TokenTypes.tWhile
                        Case "RETURN"
                            TokenType = TokenTypes.tReturn
                        Case "HALT"
                            TokenType = TokenTypes.tHalt
                        Case "HALTDEF"
                            TokenType = TokenTypes.tHaltDef
                        Case "SET", "UNSET", "INC", "DEC", "VAR"
                            TokenType = TokenTypes.tVariableStatement
                        Case "EVENT", "FUNCTION"
                            TokenType = TokenTypes.tFunction
                        Case "AND"
                            TokenType = TokenTypes.tOperator
                            TokenValue = OperatorType.oAnd
                        Case "OR"
                            TokenType = TokenTypes.tOperator
                            TokenValue = OperatorType.oOr
                        Case "XOR"
                            TokenType = TokenTypes.tOperator
                            TokenValue = OperatorType.oXor
                        Case Else
                            TokenType = TokenTypes.tText
                    End Select
                Case "0"c To "9"c
                    TokenType = TokenTypes.tNumber
                    TokenValue = ParseNumber()
                Case """"c
                    TokenType = TokenTypes.tText
                    TokenValue = ParseString(True)
                Case "'"c
                    TokenType = TokenTypes.tText
                    TokenValue = ParseString(False)
                Case "$"c
                    TokenType = TokenTypes.tFunction
                    TokenValue = ParseFunction()
                Case "%"c
                    TokenType = TokenTypes.tVariable
                    TokenValue = ParseVariable()
                Case "("c
                    Offset += 1 : Column += 1
                    TokenType = TokenTypes.tOpenParenthesis
                    TokenValue = Nothing
                Case ")"c
                    Offset += 1 : Column += 1
                    TokenType = TokenTypes.tCloseParenthesis
                    TokenValue = Nothing
                Case "["c
                    Offset += 1 : Column += 1
                    TokenType = TokenTypes.tOpenBracket
                    TokenValue = Nothing
                Case "]"c
                    Offset += 1 : Column += 1
                    TokenType = TokenTypes.tCloseBracket
                    TokenValue = Nothing
                Case "{"c
                    Offset += 1 : Column += 1
                    TokenType = TokenTypes.tOpenBrace
                    TokenValue = Nothing
                Case "}"c
                    Offset += 1 : Column += 1
                    TokenType = TokenTypes.tCloseBrace
                    TokenValue = Nothing
                Case ","c
                    Offset += 1 : Column += 1
                    TokenType = TokenTypes.tComma
                    TokenValue = Nothing
                Case "."c
                    Offset += 1 : Column += 1
                    TokenType = TokenTypes.tDot
                    TokenValue = Nothing
                Case ";"c
                    Offset += 1 : Column += 1
                    TokenType = TokenTypes.tSemicolon
                    TokenValue = Nothing
                Case "&"c
                    Offset += 1 : Column += 1
                    TokenType = TokenTypes.tOperator
                    If Offset < _Script.Script.Length AndAlso _Script.Script(Offset) = "&"c Then
                        Offset += 1 : Column += 1
                        TokenValue = OperatorType.oBooleanAnd
                    Else
                        TokenValue = OperatorType.oAnd
                    End If
                Case "|"c
                    Offset += 1 : Column += 1
                    TokenType = TokenTypes.tOperator
                    If Offset < _Script.Script.Length AndAlso _Script.Script(Offset) = "|"c Then
                        Offset += 1 : Column += 1
                        TokenValue = OperatorType.oBooleanOr
                    Else
                        TokenValue = OperatorType.oOr
                    End If
                Case "^"c
                    Offset += 1 : Column += 1
                    TokenType = TokenTypes.tOperator
                    If Offset < _Script.Script.Length AndAlso _Script.Script(Offset) = "^"c Then
                        Offset += 1 : Column += 1
                        TokenValue = OperatorType.oBooleanXor
                    Else
                        TokenValue = OperatorType.oXor
                    End If
                Case "="c
                    Offset += 1 : Column += 1
                    TokenType = TokenTypes.tOperator
                    If Offset < _Script.Script.Length Then
                        Select Case _Script.Script(Offset)
                            Case "="c
                                Offset += 1 : Column += 1
                                TokenValue = OperatorType.oEquals
                            Case ">"c
                                Offset += 1 : Column += 1
                                TokenValue = OperatorType.oGreaterThanOrEqualTo
                            Case "<"c
                                Offset += 1 : Column += 1
                                TokenValue = OperatorType.oLessThanOrEqualTo
                            Case Else
                                TokenValue = OperatorType.oEquals
                        End Select
                    Else
                        TokenValue = OperatorType.oEquals
                    End If
                Case "<"c
                    Offset += 1 : Column += 1
                    TokenType = TokenTypes.tOperator
                    If Offset < _Script.Script.Length Then
                        Select Case _Script.Script(Offset)
                            Case "="c
                                Offset += 1 : Column += 1
                                TokenValue = OperatorType.oLessThanOrEqualTo
                            Case "<"c
                                Offset += 1 : Column += 1
                                TokenValue = OperatorType.oShiftLeft
                            Case ">"c
                                Offset += 1 : Column += 1
                                TokenValue = OperatorType.oNotEqualTo
                            Case Else
                                TokenValue = OperatorType.oLessThan
                        End Select
                    Else
                        TokenValue = OperatorType.oLessThan
                    End If
                Case ">"c
                    Offset += 1 : Column += 1
                    TokenType = TokenTypes.tOperator
                    If Offset < _Script.Script.Length Then
                        Select Case _Script.Script(Offset)
                            Case "="c
                                Offset += 1 : Column += 1
                                TokenValue = OperatorType.oGreaterThanOrEqualTo
                            Case ">"c
                                Offset += 1 : Column += 1
                                TokenValue = OperatorType.oShiftRight
                            Case "<"c
                                Offset += 1 : Column += 1
                                TokenValue = OperatorType.oNotEqualTo
                            Case Else
                                TokenValue = OperatorType.oGreaterThan
                        End Select
                    Else
                        TokenValue = OperatorType.oGreaterThan
                    End If
                Case "!"c
                    Offset += 1 : Column += 1
                    TokenType = TokenTypes.tOperator
                    If Offset < _Script.Script.Length AndAlso _Script.Script(Offset) = "="c Then
                        Offset += 1 : Column += 1
                        TokenValue = OperatorType.oNotEqualTo
                    Else
                        TokenValue = OperatorType.oExclamationMark
                    End If
                Case "+"c
                    Offset += 1 : Column += 1
                    TokenType = TokenTypes.tOperator
                    If Offset < _Script.Script.Length AndAlso _Script.Script(Offset) = "+"c Then
                        Offset += 1 : Column += 1
                        TokenValue = OperatorType.oIncrement
                    Else
                        TokenValue = OperatorType.oAddition
                    End If
                Case "-"c
                    Offset += 1 : Column += 1
                    TokenType = TokenTypes.tOperator
                    If Offset < _Script.Script.Length AndAlso (_Script.Script(Offset) = "-"c Or _Script.Script(Offset) = "–"c) Then
                        Offset += 1 : Column += 1
                        TokenValue = OperatorType.oDecrement
                    Else
                        TokenValue = OperatorType.oMinus
                    End If
                Case "–"c
                    Offset += 1 : Column += 1
                    TokenType = TokenTypes.tOperator
                    If Offset < _Script.Script.Length AndAlso (_Script.Script(Offset) = "-"c Or _Script.Script(Offset) = "–"c) Then
                        Offset += 1 : Column += 1
                        TokenValue = OperatorType.oDecrement
                    Else
                        TokenValue = OperatorType.oSubtraction
                    End If
                Case "%"c
                    Offset += 1 : Column += 1
                    TokenType = TokenTypes.tOperator
                    TokenValue = OperatorType.oModulo
                Case "\"c
                    Offset += 1 : Column += 1
                    TokenType = TokenTypes.tOperator
                    TokenValue = OperatorType.oDivisionInteger
                Case "*"c
                    Offset += 1 : Column += 1
                    If Offset < _Script.Script.Length AndAlso _Script.Script(Offset) = "/"c Then
                        Offset += 1 : Column += 1
                        TokenType = TokenTypes.tCommentEnd
                        TokenValue = "*/"
                    Else
                        TokenType = TokenTypes.tOperator
                        TokenValue = OperatorType.oMultiplication
                    End If
                Case "×"c
                    Offset += 1 : Column += 1
                    TokenType = TokenTypes.tOperator
                    TokenValue = OperatorType.oMultiplication
                Case "/"c
                    Offset += 1 : Column += 1
                    If Offset < _Script.Script.Length Then
                        Select Case _Script.Script(Offset)
                            Case "/"c
                                Offset += 1 : Column += 1
                                TokenType = TokenTypes.tCommentLine
                                TokenValue = "//"
                            Case "*"c
                                Offset += 1 : Column += 1
                                TokenType = TokenTypes.tCommentStart
                                TokenValue = "/*"
                            Case Else
                                TokenType = TokenTypes.tOperator
                                TokenValue = OperatorType.oDivision
                        End Select
                    Else
                        TokenType = TokenTypes.tOperator
                        TokenValue = OperatorType.oDivision
                    End If
                Case Else
                    TokenType = TokenTypes.tUnrecognised
                    TokenValue = Nothing
            End Select
        Loop
        Return TokenType
    End Function

    Private Function ParseWord() As String
        b.Clear()
        Do
            b.Append(c)
            Offset += 1 : Column += 1
            If Offset >= _Script.Script.Length Then Exit Do
            c = _Script.Script(Offset)
        Loop While (c >= "A"c And c <= "Z"c) Or (c >= "a"c And c <= "z"c) Or (c >= "0"c And c <= "9"c) Or c = "_"c
        Return b.ToString()
    End Function

    Private Function ParseNumber() As Decimal
        b.Clear()
        Do
            b.Append(c)
            Offset += 1 : Column += 1
            If Offset >= _Script.Script.Length Then Exit Do
            c = _Script.Script(Offset)
        Loop While c >= "0"c And c <= "9"c
        If Offset < _Script.Script.Length And c = "."c Then
            Do
                b.Append(c)
                Offset += 1 : Column += 1
                If Offset >= _Script.Script.Length Then Exit Do
                c = _Script.Script(Offset)
            Loop While c >= "0"c And c <= "9"c
        End If
        Return Decimal.Parse(b.ToString())
    End Function

    Private Function ParseString(IsDoubleQuoted As Boolean) As String
        b.Clear()
        Dim Closing = If(IsDoubleQuoted, """"c, "'"c)
        Offset += 1 : Column += 1
        Do
            If Offset >= _Script.Script.Length Then Throw New SyntaxException("'" & Closing & "' missing")
            Dim c As Char = _Script.Script(Offset)

            Select Case c
                Case Closing
                    Offset += 1 : Column += 1
                    ' Check for a double-quotation-character escape sequence.
                    If Offset >= _Script.Script.Length Then Exit Do
                    c = _Script.Script(Offset)
                    If c = Closing Then
                        b.Append(Closing)
                        Offset += 1 : Column += 1
                    Else
                        Exit Do
                    End If
                    Continue Do
                Case "\"c
                    If Not IsDoubleQuoted Then Exit Select
                    ' Handle an escape sequence.
                    Offset += 1 : Column += 1
                    If Offset >= _Script.Script.Length Then
                        b.Append("\"c)
                        Exit Do
                    End If
                    c = _Script.Script(Offset)
                    Select Case Char.ToUpper(c)
                        Case "'"c : b.Append("'"c) : Offset += 1 : Column += 1 '     Single quote
                        Case """"c : b.Append(""""c) : Offset += 1 : Column += 1 '   Double quote
                        Case "\"c : b.Append("\"c) : Offset += 1 : Column += 1 '     Backslash
                        Case "0"c : b.Append(ChrW(0)) : Offset += 1 : Column += 1 '  Null
                        Case "A"c : b.Append(ChrW(7)) : Offset += 1 : Column += 1 '  Alert (bell)
                        Case "B"c : b.Append(ChrW(8)) : Offset += 1 : Column += 1 '  Backspace
                        Case "F"c : b.Append(ChrW(12)) : Offset += 1 : Column += 1 ' Form feed
                        Case "N"c : b.Append(ChrW(10)) : Offset += 1 : Column += 1 ' New line
                        Case "R"c : b.Append(ChrW(13)) : Offset += 1 : Column += 1 ' Carriage return
                        Case "T"c : b.Append(ChrW(9)) : Offset += 1 : Column += 1 '  Tab
                        Case "V"c : b.Append(ChrW(11)) : Offset += 1 : Column += 1 ' Vertical tab
                        Case "U"
                            ' Unicode escape
                            Dim lIndex As UShort
                            For i = 0 To 3
                                Offset += 1 : Column += 1
                                If Offset >= _Script.Script.Length Then Throw New FormatException("Escape sequence \u is missing its parameter.")
                                c = Char.ToUpper(_Script.Script(Offset))
                                Dim Digit As Short
                                Select Case c
                                    Case "0"c To "9"c
                                        Digit = AscW(c) - AscW("0"c)
                                    Case "A"c To "F"c
                                        Digit = AscW(c) - AscW("A"c) + 10
                                    Case Else
                                        Throw New FormatException("Unexpected character '" & c & "' in escape sequence \u.")
                                End Select
                                lIndex = lIndex Or (Digit << ((3 - i) * 4))
                            Next
                            b.Append(ChrW(lIndex))
                            Offset += 1
                        Case "X"
                            ' ASCII escape
                            Dim lIndex As Byte
                            For i = 0 To 1
                                Offset += 1 : Column += 1
                                If Offset >= _Script.Script.Length Then Throw New FormatException("Escape sequence \x is missing its parameter.")
                                c = Char.ToUpper(_Script.Script(Offset))
                                Dim Digit As Short
                                Select Case c
                                    Case "0"c To "9"c
                                        Digit = AscW(c) - AscW("0"c)
                                    Case "A"c To "F"c
                                        Digit = AscW(c) - AscW("A"c) + 10
                                    Case Else
                                        Throw New FormatException("Unexpected character '" & c & "' in escape sequence \x.")
                                End Select
                                lIndex = lIndex Or (Digit << ((1 - i) * 4))
                            Next
                            b.Append(ChrW(lIndex))
                            Offset += 1 : Column += 1
                        Case Else
                            b.Append("\"c)
                    End Select
                    Continue Do
                Case Else
                    b.Append(c)
                    Offset += 1
                    If c = ChrW(13) AndAlso Offset <= _Script.Script.Length AndAlso _Script.Script(Offset) = ChrW(10) Then Offset += 1
                    If c = ChrW(13) Or c = ChrW(10) Then
                        Column = 1
                        Line += 1
                    Else
                        Column += 1
                    End If
            End Select
        Loop
        Return b.ToString()
    End Function

    Private Function ParseFunction() As String
        b.Clear()
        Offset += 1 : Column += 1
        c = _Script.Script(Offset)
        Do While Offset < _Script.Script.Length And ((c >= "A"c And c <= "Z"c) Or (c >= "a"c And c <= "z"c) Or (c >= "0"c And c <= "9"c) Or c = "_"c)
            c = _Script.Script(Offset)
            b.Append(c)
            Offset += 1 : Column += 1
        Loop
        If b.Length = 0 Then Throw New SyntaxException("Function prefix '$' without a function name")
        Return b.ToString()
    End Function

    Private Function ParseVariable() As String
        b.Clear()
        Offset += 1 : Column += 1
        c = _Script.Script(Offset)
        Do While Offset < _Script.Script.Length And ((c >= "A"c And c <= "Z"c) Or (c >= "a"c And c <= "z"c) Or (c >= "0"c And c <= "9"c) Or c = "_"c)
            c = _Script.Script(Offset)
            b.Append(c)
            Offset += 1 : Column += 1
        Loop
        If b.Length = 0 Then Throw New SyntaxException("Variable prefix '%' without a function name")
        Return b.ToString()
    End Function

    Public Overrides Function ToString() As String
        Select Case TokenType
            Case TokenTypes.tNone
                Return "None"
            Case TokenTypes.tEndOfScript
                Return "End"
            Case TokenTypes.tNumber
                Return "Number:" & TokenValue
            Case TokenTypes.tText
                Return "Text:""" & TokenValue & """"
            Case TokenTypes.tFunction
                Return "Function:" & TokenValue
            Case TokenTypes.tVariable
                Return "Variable:" & TokenValue
            Case TokenTypes.tIf
                Return "If"
            Case TokenTypes.tElse
                Return "Else"
            Case TokenTypes.tWhile
                Return "While"
            Case TokenTypes.tHalt
                Return "Halt"
            Case TokenTypes.tHaltDef
                Return "HaltDef"
            Case TokenTypes.tVariableStatement
                Return "VariableCommand:" & TokenValue
            Case TokenTypes.tOpenParenthesis
                Return "Delimiter:("
            Case TokenTypes.tCloseParenthesis
                Return "Delimiter:)"
            Case TokenTypes.tOpenBracket
                Return "Delimiter:["
            Case TokenTypes.tCloseBracket
                Return "Delimiter:]"
            Case TokenTypes.tOpenBrace
                Return "Delimiter:{"
            Case TokenTypes.tCloseBrace
                Return "Delimiter:}"
            Case TokenTypes.tComma
                Return "Delimiter:,"
            Case TokenTypes.tDot
                Return "Delimiter:."
            Case TokenTypes.tSemicolon
                Return "Delimiter:;"
            Case TokenTypes.tOperator
                Select Case DirectCast(TokenValue, OperatorType)
                    Case OperatorType.oAnd
                        Return "Operator:&"
                    Case OperatorType.oOr
                        Return "Operator:|"
                    Case OperatorType.oXor
                        Return "Operator:^"
                    Case OperatorType.oBooleanAnd
                        Return "Operator:&&"
                    Case OperatorType.oBooleanOr
                        Return "Operator:||"
                    Case OperatorType.oBooleanXor
                        Return "Operator:^^"
                    Case OperatorType.oBooleanImplication
                        Return "Operator:Imp"
                    Case OperatorType.oEquals
                        Return "Operator:=="
                    Case OperatorType.oLessThan
                        Return "Operator:<"
                    Case OperatorType.oGreaterThan
                        Return "Operator:>"
                    Case OperatorType.oLessThanOrEqualTo
                        Return "Operator:<="
                    Case OperatorType.oGreaterThanOrEqualTo
                        Return "Operator:>="
                    Case OperatorType.oNotEqualTo
                        Return "Operator:!="
                    Case OperatorType.oShiftLeft
                        Return "Operator:<<"
                    Case OperatorType.oShiftRight
                        Return "Operator:>>"
                    Case OperatorType.oConcatenation
                        Return "Operator:Concatenation"
                    Case OperatorType.oAddition
                        Return "Operator:+"
                    Case OperatorType.oSubtraction
                        Return "Operator:–"
                    Case OperatorType.oModulo
                        Return "Operator:%"
                    Case OperatorType.oDivisionInteger
                        Return "Operator:\"
                    Case OperatorType.oMultiplication
                        Return "Operator:×"
                    Case OperatorType.oDivision
                        Return "Operator:/"
                    Case OperatorType.oNot
                        Return "Operator:!"
                    Case OperatorType.oNegation
                        Return "Operator:-"
                    Case OperatorType.oIncrementPrefix
                        Return "Operator:++x"
                    Case OperatorType.oIncrementPostfix
                        Return "Operator:x++"
                    Case OperatorType.oDecrementPrefix
                        Return "Operator:--x"
                    Case OperatorType.oDecrementPostfix
                        Return "Operator:x--"
                    Case OperatorType.oMinus
                        Return "Symbol:-"
                    Case OperatorType.oAmpersand
                        Return "Symbol:&"
                    Case OperatorType.oIncrement
                        Return "Synbol:++"
                    Case OperatorType.oDecrement
                        Return "Symbol:--"
                    Case Else
                        Return "Operator:Unknown"
                End Select
            Case TokenTypes.tCommentLine
                Return "Comment"
            Case TokenTypes.tCommentStart
                Return "CommentStart"
            Case TokenTypes.tCommentEnd
                Return "CommentEnd"
            Case Else
                Return "Unknown"
        End Select
    End Function
End Class
