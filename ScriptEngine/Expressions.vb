Imports System.Text
' The following concept was 'borrowed' from Pascal Ganaye's evaluator.
' http://www.codeproject.com/Articles/13779/The-expression-evaluator-revisited-Eval-function-i

Partial Public Class Script
    Private o As OperatorType
    Public Function ParseExpression(Optional Priority As Short = 0) As ValueOpCode
        Dim t1 As ValueOpCode, t2 As ValueOpCode
        t1 = ParseTerm()
        If Priority > 0 And (Tokeniser.TokenType = TokenTypes.tEndOfScript Or Tokeniser.TokenType = TokenTypes.tCloseParenthesis Or Tokeniser.TokenType = TokenTypes.tComma) Then Return t1
        o = ParseOperator()
        Do
            If o = Nothing Then Exit Do
            If (o And OperatorType.oPostfixUnaryOperator) Then
                ' Postfix unary operator
                ' If the operator has a higher priority than the previous operator, stick it on t1.
                If (o >> 4) > Priority Then
                    t1 = New OpCodeUnary(o, t1)
                    o = ParseOperator()
                Else
                    Exit Do
                End If
            Else
                ' Binary operator
                ' If the operator has a higher prority than the previous operator, recurse it.
                If (o >> 4) > Priority Then
                    Dim _o = o
                    t2 = ParseExpression(_o >> 4)
                    t1 = New OpCodeBinary(_o, t1, t2)
                Else
                    Exit Do
                End If
            End If
        Loop
        Return t1
    End Function

    Private Function ParseTerm() As ValueOpCode
        Do
            Tokeniser.NextToken()
            If Tokeniser.TokenType = TokenTypes.tEndOfScript Then Throw New SyntaxException("Unexpected end of script")

            Select Case Tokeniser.TokenType
                Case TokenTypes.tNumber, TokenTypes.tText
                    Return New OpCodeConstant(Tokeniser.TokenValue)
                Case TokenTypes.tFunction
                Case TokenTypes.tVariable
                Case TokenTypes.tOpenParenthesis
                    Dim Result = ParseExpression()
                    If Tokeniser.TokenType <> TokenTypes.tCloseParenthesis Then Throw New SyntaxException("Expected ')' but found " & Tokeniser.TokenType)
                    Return Result
                Case TokenTypes.tOperator
                    Dim _Type As OperatorType = Tokeniser.TokenValue
                    If _Type = OperatorType.oMinus Then
                        _Type = OperatorType.oNegation
                    ElseIf _Type = OperatorType.oIncrement Then
                        _Type = OperatorType.oIncrementPrefix
                    ElseIf _Type = OperatorType.oDecrement Then
                        _Type = OperatorType.oDecrementPrefix
                    ElseIf _Type = OperatorType.oExclamationMark Then
                        _Type = OperatorType.oNot
                    End If
                    If (_Type And 8) = 0 Then Throw New SyntaxException("Expected a term but found a binary operator")
                    Dim Result = ParseExpression(_Type >> 4)
                    Return New OpCodeUnary(_Type, Result)
                Case Else
                    Throw New SyntaxException("Unexpected " & Tokeniser.TokenType)
            End Select
        Loop
    End Function

    Private Function ParseOperator() As OperatorType
        Dim b As New StringBuilder
        Do
            If Tokeniser.TokenType <> TokenTypes.tOperator OrElse (Tokeniser.TokenValue And OperatorType.oPostfixUnaryOperator) <> 0 Then Tokeniser.NextToken()

            Select Case Tokeniser.TokenType
                Case TokenTypes.tEndOfScript
                    Return Nothing
                Case TokenTypes.tCloseParenthesis, TokenTypes.tComma
                    Return Nothing
                Case TokenTypes.tOperator
                    If Tokeniser.TokenValue = OperatorType.oMinus Then Return OperatorType.oSubtraction
                    If Tokeniser.TokenValue = OperatorType.oExclamationMark Then Return OperatorType.oFactorial
                    If Tokeniser.TokenValue = OperatorType.oIncrement Then Return OperatorType.oIncrementPostfix
                    If Tokeniser.TokenValue = OperatorType.oDecrement Then Return OperatorType.oDecrementPostfix
                    If (Tokeniser.TokenValue And 8) <> 0 Then Throw New SyntaxException("Expected an expression continuation, ',' or ')' but found a unary operator")
                    Return Tokeniser.TokenValue
                Case Else
                    Throw New SyntaxException("Unexpected " & Tokeniser.TokenType)
            End Select
        Loop
    End Function

    '    Private Function ParseFunction() As OpCode
    '        Dim b As New StringBuilder, MethodName As String, Args As New List(Of OpCode) '
    '        Dim IsNumber As Boolean, StartIndex As Short = -1, EndIndex As Short = -1
    '        ' Parse the function name.
    '        Do
    '            If ColumnIndex >= Script(LineIndex).Length Then
    '                MethodName = b.ToString
    '                GoTo Build
    '            End If

    '            Dim c As Char = Script(LineIndex)(ColumnIndex)

    '            Select Case c
    '                Case "A"c To "Z"c, "a"c To "z"c, "_"c
    '                    If IsNumber Then Throw New SyntaxException("Expected a digit or '-', but found '" & c & "'.")
    '                    b.Append(c)
    '                    ColumnIndex += 1
    '                Case "0"c To "9"c
    '                    IsNumber = True
    '                    b.Append(c)
    '                    ColumnIndex += 1
    '                Case "-"c
    '                    If b(0) < "0"c Or b(0) > "9"c Then Exit Do
    '                    If EndIndex <> -1 Then Throw New SyntaxException("Expected an expression continuation, ',' or ')', but found '-'.")
    '                    StartIndex = b.ToString
    '                    If StartIndex = 0 Then Throw New SyntaxException("'0' is not valid in a parameter range. Consider '1-' instead.")
    '                    b.Clear()
    '                    ColumnIndex += 1
    '                Case " "c, ChrW(9), "("c, "."c
    '                    MethodName = b.ToString
    '                    Exit Do
    '                Case ")"c, ","c
    '                    MethodName = b.ToString
    '                    GoTo Build
    '                Case Else
    '                    Throw New SyntaxException("Expected an identifier or '(', but found '" & Script(LineIndex)(ColumnIndex) & "'.")
    '            End Select
    '        Loop
    '        ' Parse the function parameters.
    '        Do
    '            If ColumnIndex >= Script(LineIndex).Length Then Exit Do

    '            Dim c As Char = Script(LineIndex)(ColumnIndex)

    '            Select Case c
    '                Case " "c, ChrW(9)
    '                    ColumnIndex += 1
    '                Case "("c
    '                    ColumnIndex += 1
    '                    ParseArgumentsBrackets(Args)
    '                    If ColumnIndex >= Script(LineIndex).Length Then Throw New SyntaxException("')' missing")
    '                    ColumnIndex += 1
    '                    Exit Do
    '                Case Else
    '                    Exit Do
    '            End Select
    '        Loop
    'Build:
    '        If IsNumber Then
    '            If StartIndex = -1 Then
    '                StartIndex = b.ToString
    '            ElseIf b.Length > 0 Then
    '                EndIndex = b.ToString
    '            Else
    '                EndIndex = 0
    '            End If
    '        End If

    '        Dim Code As OpCode
    '        If IsNumber Then
    '            Dim fields = If(lPlugin.Parameter, "").Split({" "c}, StringSplitOptions.RemoveEmptyEntries)
    '            If StartIndex = 0 Then
    '                Return New OpCodeConstant(fields.Count)
    '            ElseIf EndIndex = 0 Then
    '                If StartIndex > fields.Count Then Return New OpCodeConstant("")
    '                Return New OpCodeConstant(String.Join(" ", fields.Skip(StartIndex - 1)))
    '            ElseIf EndIndex = -1 Then
    '                If StartIndex > fields.Count Then Return New OpCodeConstant("")
    '                Return New OpCodeConstant(fields(StartIndex - 1))
    '            Else
    '                If EndIndex < StartIndex Then Throw New SyntaxException("The end index is less than the start index.")
    '                If EndIndex > fields.Count Then EndIndex = fields.Count
    '                If StartIndex > fields.Count Then Return New OpCodeConstant("")
    '                Return New OpCodeConstant(String.Join(" ", fields.Skip(StartIndex - 1).Take(EndIndex - StartIndex + 1)))
    '            End If
    '        Else
    '            Code = New OpCodeFunction(Me, MethodName, If(Args Is Nothing, {}, Args.ToArray), lPlugin.Connection, lPlugin.Channel, Nothing)
    '        End If
    '        If ColumnIndex >= Script(LineIndex).Length Then Return Code

    '        If Script(LineIndex)(ColumnIndex) = "."c Then
    '            ColumnIndex += 1
    '            Dim ICode = DirectCast(ParseFunction(), OpCodeFunction)
    '            Dim ICode2 = ICode
    '            Do Until ICode2._Target Is Nothing
    '                ICode2 = ICode2._Target
    '            Loop
    '            ICode2._Target = Code
    '            Return ICode
    '        Else
    '            Return Code
    '        End If
    '    End Function
End Class
