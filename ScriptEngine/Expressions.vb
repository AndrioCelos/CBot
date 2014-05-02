Imports System.Text

Partial Public Class ScriptEngine

    ' The following concept was 'borrowed' from Pascal Ganaye's evaluator.
    ' http://www.codeproject.com/Articles/13779/The-expression-evaluator-revisited-Eval-function-i

    Partial Public Class ScriptPointer
        Dim o As Byte
        Public Function ParseExpression(Optional Priority As Short = 0) As OpCode
            Dim t1 As OpCode, t2 As OpCode
            t1 = ParseTerm()
            o = ParseOperator()
            Do
                If o = Nothing Then Exit Do
                If (o And 8) Then
                    ' Postfix unary operator.
                    If Priority < (o >> 4) Then
                        t1 = New OpCodeUnary(o, t1)
                        o = ParseOperator()
                    Else
                        Exit Do
                    End If
                Else
                    ' Binary operator.
                    If Priority < (o >> 4) Then
                        Dim lo = o
                        t2 = ParseExpression(lo >> 4)
                        t1 = New OpCodeBinary(lo, t1, t2)
                    Else
                        Exit Do
                    End If
                End If
            Loop
            Return t1
        End Function

        Private Function ParseTerm() As OpCode
            Do
                If ColumnIndex >= Script(LineIndex).Length Then Throw New SyntaxException("Unexpected end of line.")
                Dim c As Char = Script(LineIndex)(ColumnIndex)

                Select Case c
                    Case " "c, ChrW(9)
                        ColumnIndex += 1
                    Case "0"c To "9"c, "."c
                        Return ParseNumber()
                    Case "A"c To "Z"c, "a"c To "z"c
                        Dim Result = ParseStringImpromptu()
                        If Result.Length = 3 AndAlso Result.ToUpper = "NOT" Then
                            ' Unary NOT operator.
                            Return New OpCodeUnary(OperatorType.oNot, ParseExpression(1))
                        Else
                            Return New OpCodeConstant(Result)
                        End If
                    Case "$"c
                        ColumnIndex += 1
                        Return ParseFunction()
                    Case "%"c
                        ColumnIndex += 1
                        Dim Result = ParseVariable()
                        Return New OpCodeVariable(Result, lPlugin)
                    Case """"c
                        Return ParseStringQuoted(True)
                    Case "'"c
                        Return ParseStringQuoted(False)
                    Case "("c
                        ColumnIndex += 1
                        Dim Result = ParseExpression()
                        If ColumnIndex >= Script(LineIndex).Length Then Throw New SyntaxException("')' missing")
                        If Script(LineIndex)(ColumnIndex) = ")"c Then
                            ColumnIndex += 1
                            Return Result
                        Else
                            Throw New SyntaxException("Unexpected character: '" & c & "'.")
                        End If
                    Case ")"c
                        Throw New SyntaxException("')' was not expected here.")
                    Case "-"c
                        ColumnIndex += 1
                        Return New OpCodeUnary(OperatorType.oNegation, ParseExpression(8))
                    Case "!"c, "~"c
                        ColumnIndex += 1
                        Return New OpCodeUnary(OperatorType.oNot, ParseExpression(1))
                    Case Else
                        Throw New NotImplementedException("Unexpected character: '" & c & "'.")
                End Select
            Loop
        End Function

        Private Function ParseOperator() As Short
            Dim b As New StringBuilder
            Do
                If ColumnIndex >= Script(LineIndex).Length Then Return Nothing
                Dim c As Char = Script(LineIndex)(ColumnIndex)

                Select Case c
                    Case " "c, ChrW(9)
                        ColumnIndex += 1
                    Case "0"c To "9"c
                        Throw New SyntaxException("A number was not expected here.")
                    Case "A"c To "Z"c, "a"c To "z"c
                        ' Alphabetical operator
                        b.Append(c)
                        ColumnIndex += 1
                        Do
                            If ColumnIndex >= Script(LineIndex).Length Then Return b.ToString
                            c = Script(LineIndex)(ColumnIndex)
                            Select Case c
                                Case "A"c To "Z"c, "a"c To "z"c
                                    b.Append(c)
                                    ColumnIndex += 1
                                Case Else
                                    Select Case b.ToString.ToUpper
                                        Case "AND"
                                            Return OperatorType.oAnd
                                        Case "OR"
                                            Return OperatorType.oOr
                                        Case "XOR"
                                            Return OperatorType.oXor
                                        Case "IMP"
                                            Return OperatorType.oBooleanImplication
                                        Case "MOD"
                                            Return OperatorType.oModulo
                                        Case Else
                                            Throw New SyntaxException("'" & b.ToString & "' is not a known binary operator.")
                                    End Select
                            End Select
                        Loop
                    Case ","c, ")"c
                        Return Nothing
                    Case "!"c
                        ColumnIndex += 1
                        If ColumnIndex < Script(LineIndex).Length AndAlso Script(LineIndex)(ColumnIndex) = "="c Then
                            ColumnIndex += 1
                            Return OperatorType.oNotEqualTo
                        Else
                            Return OperatorType.oFactorial
                        End If
                    Case "&"c
                        ColumnIndex += 1
                        If ColumnIndex < Script(LineIndex).Length AndAlso Script(LineIndex)(ColumnIndex) = "&"c Then
                            ColumnIndex += 1
                            Return OperatorType.oBooleanAnd
                        Else
                            Return OperatorType.oConcatenation
                        End If
                    Case "|"c
                        ColumnIndex += 1
                        If ColumnIndex < Script(LineIndex).Length AndAlso Script(LineIndex)(ColumnIndex) = "|"c Then
                            ColumnIndex += 1
                            Return OperatorType.oBooleanOr
                        Else
                            Return OperatorType.oOr
                        End If
                    Case "^"c
                        ColumnIndex += 1
                        If ColumnIndex < Script(LineIndex).Length AndAlso Script(LineIndex)(ColumnIndex) = "^"c Then
                            ColumnIndex += 1
                            Return OperatorType.oBooleanXor
                        Else
                            Return OperatorType.oExponentiation
                        End If
                    Case "="
                        ColumnIndex += 1
                        If ColumnIndex < Script(LineIndex).Length Then
                            Select Case Script(LineIndex)(ColumnIndex)
                                Case "="c
                                    ColumnIndex += 1
                                    Return OperatorType.oEquals
                                Case "<"c
                                    ColumnIndex += 1
                                    Return OperatorType.oLessThanOrEqualTo
                                Case ">"c
                                    ColumnIndex += 1
                                    Return OperatorType.oGreaterThanOrEqualTo
                                Case Else
                                    Return OperatorType.oEquals
                            End Select
                        Else
                            Return OperatorType.oEquals
                        End If
                    Case "<"c
                        ColumnIndex += 1
                        If ColumnIndex < Script(LineIndex).Length Then
                            Select Case Script(LineIndex)(ColumnIndex)
                                Case "="c
                                    ColumnIndex += 1
                                    Return OperatorType.oLessThanOrEqualTo
                                Case "<"c
                                    ColumnIndex += 1
                                    Return OperatorType.oShiftLeft
                                Case ">"c
                                    ColumnIndex += 1
                                    Return OperatorType.oNotEqualTo
                                Case Else
                                    Return OperatorType.oLessThan
                            End Select
                        Else
                            Return OperatorType.oLessThan
                        End If
                    Case ">"c
                        ColumnIndex += 1
                        If ColumnIndex < Script(LineIndex).Length Then
                            Select Case Script(LineIndex)(ColumnIndex)
                                Case "="c
                                    ColumnIndex += 1
                                    Return OperatorType.oGreaterThanOrEqualTo
                                Case "<"c
                                    ColumnIndex += 1
                                    Return OperatorType.oNotEqualTo
                                Case ">"c
                                    ColumnIndex += 1
                                    Return OperatorType.oShiftRight
                                Case Else
                                    Return OperatorType.oGreaterThan
                            End Select
                        Else
                            Return OperatorType.oGreaterThan
                        End If
                    Case "+"c
                        ColumnIndex += 1
                        Return OperatorType.oAddition
                    Case "-"c
                        ColumnIndex += 1
                        Return OperatorType.oSubtraction
                    Case "\"c
                        ColumnIndex += 1
                        Return OperatorType.oDivisionInteger
                    Case "%"c
                        ColumnIndex += 1
                        Return OperatorType.oModulo
                    Case "*"c
                        ColumnIndex += 1
                        Return OperatorType.oMultiplication
                    Case "/"c
                        ColumnIndex += 1
                        Return OperatorType.oDivision
                    Case "^"c
                        ColumnIndex += 1
                        Return OperatorType.oExponentiation
                    Case Else
                        Throw New NotImplementedException("Unexpected character: '" & c & "'.")
                End Select
            Loop
        End Function

        Private Function ParseNumber() As OpCode
            Dim b As New StringBuilder
            Do
                If ColumnIndex >= Script(LineIndex).Length Then Return New OpCodeConstant(Decimal.Parse(b.ToString))
                Dim c As Char = Script(LineIndex)(ColumnIndex)

                Select Case c
                    Case "0"c To "9"c
                        b.Append(c)
                        ColumnIndex += 1
                    Case "."c
                        b.Append(c)
                        Exit Do
                    Case Else
                        Return New OpCodeConstant(Decimal.Parse(b.ToString))
                End Select
            Loop
            ColumnIndex += 1
            Do
                If ColumnIndex >= Script(LineIndex).Length Then Return New OpCodeConstant(Decimal.Parse(b.ToString))
                Dim c As Char = Script(LineIndex)(ColumnIndex)

                Select Case c
                    Case "0"c To "9"c
                        b.Append(c)
                        ColumnIndex += 1
                    Case "."c
                        Throw New SyntaxException("'.' was not expected here.")
                    Case Else
                        Return New OpCodeConstant(Decimal.Parse(b.ToString))
                End Select
            Loop
        End Function

        Private Function ParseStringImpromptu() As String
            Dim b As New StringBuilder
            Do
                If ColumnIndex >= Script(LineIndex).Length Then Return b.ToString
                Dim c As Char = Script(LineIndex)(ColumnIndex)

                Select Case c
                    Case "A"c To "Z"c, "a"c To "z"c, "0"c To "9"c, "#"c, "&"c
                        b.Append(c)
                        ColumnIndex += 1
                    Case " "c, ChrW(9)
                        Return b.ToString
                    Case "$"c
                        Throw New NotImplementedException
                    Case "%"c
                        Throw New NotImplementedException
                    Case Else
                        Return b.ToString
                End Select
            Loop
        End Function

        Private Function ParseStringQuoted(IsDoubleQuoted As Boolean) As OpCode
            Dim b As New StringBuilder
            Dim Closing = If(IsDoubleQuoted, """"c, "'"c)
            ColumnIndex += 1
            Do
                If ColumnIndex >= Script(LineIndex).Length Then Throw New SyntaxException("'" & Closing & "' missing")
                Dim c As Char = Script(LineIndex)(ColumnIndex)

                Select Case c
                    Case Closing
                        ColumnIndex += 1
                        If ColumnIndex >= Script(LineIndex).Length Then Exit Do
                        c = Script(LineIndex)(ColumnIndex)
                        If c = Closing Then
                            b.Append(Closing)
                            ColumnIndex += 1
                        Else
                            Exit Do
                        End If
                    Case "\"c
                        If Not IsDoubleQuoted Then GoTo Append
                        ' Handle an escape sequence.
                        ColumnIndex += 1
                        If ColumnIndex >= Script(LineIndex).Length Then
                            b.Append("\"c)
                            Exit Do
                        End If
                        c = Script(LineIndex)(ColumnIndex)
                        Select Case Char.ToUpper(c)
                            Case "'"c : b.Append("'"c) : ColumnIndex += 1 '     Single quote
                            Case """"c : b.Append(""""c) : ColumnIndex += 1 '   Double quote
                            Case "\"c : b.Append("\"c) : ColumnIndex += 1 '     Backslash
                            Case "0"c : b.Append(ChrW(0)) : ColumnIndex += 1 '  Null
                            Case "A"c : b.Append(ChrW(7)) : ColumnIndex += 1 '  Alert (bell)
                            Case "B"c : b.Append(ChrW(8)) : ColumnIndex += 1 '  Backspace
                            Case "F"c : b.Append(ChrW(12)) : ColumnIndex += 1 ' Form feed
                            Case "N"c : b.Append(ChrW(10)) : ColumnIndex += 1 ' New line
                            Case "R"c : b.Append(ChrW(13)) : ColumnIndex += 1 ' Carriage return
                            Case "T"c : b.Append(ChrW(9)) : ColumnIndex += 1 '  Tab
                            Case "V"c : b.Append(ChrW(11)) : ColumnIndex += 1 ' Vertical tab
                            Case "U"
                                ' Unicode escape
                                Dim lIndex As UShort
                                For i = 0 To 3
                                    ColumnIndex += 1
                                    If ColumnIndex >= Script(LineIndex).Length Then Throw New FormatException("Escape sequence \u is missing its parameter.")
                                    c = Char.ToUpper(Script(LineIndex)(ColumnIndex))
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
                                ColumnIndex += 1
                            Case "X"
                                ' ASCII escape
                                Dim lIndex As Byte
                                For i = 0 To 1
                                    ColumnIndex += 1
                                    If ColumnIndex >= Script(LineIndex).Length Then Throw New FormatException("Escape sequence \x is missing its parameter.")
                                    c = Char.ToUpper(Script(LineIndex)(ColumnIndex))
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
                                ColumnIndex += 1
                            Case Else
                                b.Append("\"c)
                        End Select
                    Case Else
Append:
                        b.Append(c)
                        ColumnIndex += 1
                End Select
            Loop
            Return New OpCodeConstant(b.ToString)
        End Function

        Private Function ParseVariable() As String
            Dim b As New StringBuilder("%")
            Do
                If ColumnIndex >= Script(LineIndex).Length Then Return b.ToString
                Dim c As Char = Script(LineIndex)(ColumnIndex)

                Select Case c
                    Case "A"c To "Z"c, "a"c To "z"c, "0"c To "9"c, "_"c, "."c
                        b.Append(c)
                        ColumnIndex += 1
                    Case " "c, ChrW(9)
                        Return b.ToString
                    Case Else
                        Throw New SyntaxException("Expected an identifier, but found '" & Script(LineIndex)(ColumnIndex) & "'.")
                End Select
            Loop
        End Function

        Private Function ParseFunction() As OpCode
            Dim b As New StringBuilder, MethodName As String, Args As New List(Of OpCode) '
            Dim IsNumber As Boolean, StartIndex As Short = -1, EndIndex As Short = -1
            ' Parse the function name.
            Do
                If ColumnIndex >= Script(LineIndex).Length Then
                    MethodName = b.ToString
                    GoTo Build
                End If

                Dim c As Char = Script(LineIndex)(ColumnIndex)

                Select Case c
                    Case "A"c To "Z"c, "a"c To "z"c, "_"c
                        If IsNumber Then Throw New SyntaxException("Expected a digit or '-', but found '" & c & "'.")
                        b.Append(c)
                        ColumnIndex += 1
                    Case "0"c To "9"c
                        IsNumber = True
                        b.Append(c)
                        ColumnIndex += 1
                    Case "-"c
                        If b(0) < "0"c Or b(0) > "9"c Then Exit Do
                        If EndIndex <> -1 Then Throw New SyntaxException("Expected an expression continuation, ',' or ')', but found '-'.")
                        StartIndex = b.ToString
                        If StartIndex = 0 Then Throw New SyntaxException("'0' is not valid in a parameter range. Consider '1-' instead.")
                        b.Clear()
                        ColumnIndex += 1
                    Case " "c, ChrW(9), "("c, "."c
                        MethodName = b.ToString
                        Exit Do
                    Case ")"c, ","c
                        MethodName = b.ToString
                        GoTo Build
                    Case Else
                        Throw New SyntaxException("Expected an identifier or '(', but found '" & Script(LineIndex)(ColumnIndex) & "'.")
                End Select
            Loop
            ' Parse the function parameters.
            Do
                If ColumnIndex >= Script(LineIndex).Length Then Exit Do

                Dim c As Char = Script(LineIndex)(ColumnIndex)

                Select Case c
                    Case " "c, ChrW(9)
                        ColumnIndex += 1
                    Case "("c
                        ColumnIndex += 1
                        ParseArgumentsBrackets(Args)
                        If ColumnIndex >= Script(LineIndex).Length Then Throw New SyntaxException("')' missing")
                        ColumnIndex += 1
                        Exit Do
                    Case Else
                        Exit Do
                End Select
            Loop
Build:
            If IsNumber Then
                If StartIndex = -1 Then
                    StartIndex = b.ToString
                ElseIf b.Length > 0 Then
                    EndIndex = b.ToString
                Else
                    EndIndex = 0
                End If
            End If

            Dim Code As OpCode
            If IsNumber Then
                Dim fields = If(lPlugin.Parameter, "").Split({" "c}, StringSplitOptions.RemoveEmptyEntries)
                If StartIndex = 0 Then
                    Return New OpCodeConstant(fields.Count)
                ElseIf EndIndex = 0 Then
                    If StartIndex > fields.Count Then Return New OpCodeConstant("")
                    Return New OpCodeConstant(String.Join(" ", fields.Skip(StartIndex - 1)))
                ElseIf EndIndex = -1 Then
                    If StartIndex > fields.Count Then Return New OpCodeConstant("")
                    Return New OpCodeConstant(fields(StartIndex - 1))
                Else
                    If EndIndex < StartIndex Then Throw New SyntaxException("The end index is less than the start index.")
                    If EndIndex > fields.Count Then EndIndex = fields.Count
                    If StartIndex > fields.Count Then Return New OpCodeConstant("")
                    Return New OpCodeConstant(String.Join(" ", fields.Skip(StartIndex - 1).Take(EndIndex - StartIndex + 1)))
                End If
            Else
                Code = New OpCodeFunction(Me, MethodName, If(Args Is Nothing, {}, Args.ToArray), lPlugin.Connection, lPlugin.Channel, Nothing)
            End If
            If ColumnIndex >= Script(LineIndex).Length Then Return Code

            If Script(LineIndex)(ColumnIndex) = "."c Then
                ColumnIndex += 1
                Dim ICode = DirectCast(ParseFunction(), OpCodeFunction)
                Dim ICode2 = ICode
                Do Until ICode2._Target Is Nothing
                    ICode2 = ICode2._Target
                Loop
                ICode2._Target = Code
                Return ICode
            Else
                Return Code
            End If
        End Function
    End Class

End Class