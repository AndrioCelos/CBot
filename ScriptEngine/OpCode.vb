Imports VBot

' The following concept was 'borrowed' from Pascal Ganaye's evaluator.
' http://www.codeproject.com/Articles/13779/The-expression-evaluator-revisited-Eval-function-i

Public MustInherit Class OpCode
    Public MustOverride Sub Invoke()
End Class

Public MustInherit Class ValueOpCode
    Inherits OpCode

    Public MustOverride ReadOnly Property Value As Object

    Public Overrides Sub Invoke()
        Dim x = Value
    End Sub
End Class

Public Class OpCodeConstant
    Inherits ValueOpCode

    Private lValue As Object

    Public Sub New(Value As Object)
        lValue = Value
    End Sub

    Public Overrides ReadOnly Property Value As Object
        Get
            Return lValue
        End Get
    End Property
End Class

Public Class OpCodeUnary
    Inherits ValueOpCode

    Private lValue As ValueOpCode
    Public Operation As OperatorType

    Private Calculate As CalculateDelegate
    Protected Delegate Function CalculateDelegate() As Object

    Public Sub New(ByVal Operation As OperatorType, ByVal Value As OpCode)
        lValue = Value
        Me.Operation = Operation
    End Sub

    Public Overrides ReadOnly Property Value As Object
        Get
            Dim v1 = lValue.Value
            Select Case Operation
                Case OperatorType.oNegation
                    Select Case v1.GetType
                        Case GetType(Decimal)
                            Return -DirectCast(v1, Decimal)
                        Case GetType(Boolean)
                            Return Not DirectCast(v1, Boolean)
                        Case Else
                            'Throw New ArgumentException("The negation operation cannot be performed on this value of type " & Value.GetType.Name & ".")
                            Return -v1
                    End Select
                Case OperatorType.oNot
                    Select Case v1.GetType
                        Case GetType(Decimal)
                            Return CType(Not DirectCast(v1, Decimal), Decimal)
                        Case GetType(Boolean)
                            Return Not DirectCast(v1, Boolean)
                        Case Else
                            'Throw New ArgumentException("The Not operation cannot be performed on this value of type " & Value.GetType.Name & ".")
                            Return Not v1
                    End Select
                Case OperatorType.oFactorial
                    Select Case v1.GetType
                        Case GetType(Decimal)
                            Return OperationFactorial(v1)
                        Case Else
                            Throw New ArgumentException("The factorial operation cannot be performed on this value of type " & Value.GetType.Name & ".")
                    End Select
                Case Else
                    Throw New ArgumentException(Operation & " is not a known unary operation.")
            End Select
        End Get
    End Property

    Private Function OperationFactorial(ByVal v1 As Decimal) As Decimal
        If v1 <> Int(v1) Then Throw New ArgumentException("The factorial operation on a non-integer is not valid.")
        If v1 > 0 Then
            For i As Decimal = v1 - 1 To 1 Step -1
                v1 *= i
            Next
            Return v1
        ElseIf v1 = 0 Then
            Return 1D
        Else
            For i As Decimal = -v1 - 1 To 1 Step -1
                v1 *= i
            Next
            Return v1
        End If
    End Function
End Class

Public Class OpCodeBinary
    Inherits ValueOpCode

    Private Value1 As ValueOpCode
    Private Value2 As ValueOpCode
    Public Operation As OperatorType

    Private Calculate As CalculateDelegate
    Protected Delegate Function CalculateDelegate() As Object

    Public Sub New(ByVal Operation As OperatorType, ByVal Value1 As OpCode, ByVal Value2 As OpCode)
        Me.Value1 = Value1
        Me.Value2 = Value2
        Me.Operation = Operation
    End Sub

    Public Overrides ReadOnly Property Value As Object
        Get
            Dim v1 = Value1.Value, v2 = Value2.Value
            Select Case Operation
                Case OperatorType.oBooleanAnd
                    Try
                        Return CType(v1, Boolean) AndAlso CType(v2, Boolean)
                    Catch ex As InvalidCastException
                        Throw New ArgumentException("Conversion of operands for Boolean AND to Boolean failed.")
                    End Try
                Case OperatorType.oBooleanOr
                    Try
                        Return CType(v1, Boolean) OrElse CType(v2, Boolean)
                    Catch ex As InvalidCastException
                        Throw New ArgumentException("Conversion of operands for Boolean OR to Boolean failed.")
                    End Try
                Case OperatorType.oBooleanXor
                    Try
                        Return CType(v1, Boolean) Xor CType(v2, Boolean)
                    Catch ex As InvalidCastException
                        Throw New ArgumentException("Conversion of operands for Boolean XOR to Boolean failed.")
                    End Try
                Case OperatorType.oBooleanImplication
                    Try
                        Return (Not CType(v1, Boolean)) OrElse CType(v2, Boolean)
                    Catch ex As InvalidCastException
                        Throw New ArgumentException("Conversion of operands for Boolean implication to Boolean failed.")
                    End Try
                Case OperatorType.oAnd
                    If TypeOf v1 Is Boolean And TypeOf v2 Is Boolean Then
                        Return DirectCast(v1, Boolean) AndAlso DirectCast(v2, Boolean)
                    ElseIf TypeOf v1 Is Decimal And TypeOf v2 Is Decimal Then
                        Return CType(DirectCast(v1, Decimal) And DirectCast(v2, Decimal), Decimal)
                    Else
                        Throw New ArgumentException("The And operation cannot be performed on these value of type " & v1.GetType.Name & " and " & v2.GetType.Name & ".")
                    End If
                Case OperatorType.oOr
                    If TypeOf v1 Is Boolean And TypeOf v2 Is Boolean Then
                        Return DirectCast(v1, Boolean) OrElse DirectCast(v2, Boolean)
                    ElseIf TypeOf v1 Is Decimal And TypeOf v2 Is Decimal Then
                        Return CType(DirectCast(v1, Decimal) Or DirectCast(v2, Decimal), Decimal)
                    Else
                        Throw New ArgumentException("The Or operation cannot be performed on these value of type " & v1.GetType.Name & " and " & v2.GetType.Name & ".")
                    End If
                Case OperatorType.oXor
                    If TypeOf v1 Is Boolean And TypeOf v2 Is Boolean Then
                        Return DirectCast(v1, Boolean) Xor DirectCast(v2, Boolean)
                    ElseIf TypeOf v1 Is Decimal And TypeOf v2 Is Decimal Then
                        Return CType(DirectCast(v1, Decimal) Xor DirectCast(v2, Decimal), Decimal)
                    Else
                        Throw New ArgumentException("The Xor operation cannot be performed on these value of type " & v1.GetType.Name & " and " & v2.GetType.Name & ".")
                    End If
                Case OperatorType.oEquals
                    If v1.GetType Is v2.GetType Then
                        Return DirectCast(v1 = v2, Boolean)
                    Else
                        Return False
                    End If
                Case OperatorType.oLessThan
                    If TypeOf v1 Is Decimal And TypeOf v2 Is Decimal Then
                        Return DirectCast(v1 < v2, Boolean)
                    Else
                        Throw New ArgumentException("The less than comparison cannot be performed on these value of type " & v1.GetType.Name & " and " & v2.GetType.Name & ".")
                    End If
                Case OperatorType.oGreaterThan
                    If TypeOf v1 Is Decimal And TypeOf v2 Is Decimal Then
                        Return DirectCast(v1 > v2, Boolean)
                    Else
                        Throw New ArgumentException("The greater than comparison cannot be performed on these value of type " & v1.GetType.Name & " and " & v2.GetType.Name & ".")
                    End If
                Case OperatorType.oLessThanOrEqualTo
                    If TypeOf v1 Is Decimal And TypeOf v2 Is Decimal Then
                        Return DirectCast(v1 <= v2, Boolean)
                    Else
                        Throw New ArgumentException("The less than or equal to comparison cannot be performed on these value of type " & v1.GetType.Name & " and " & v2.GetType.Name & ".")
                    End If
                Case OperatorType.oGreaterThanOrEqualTo
                    If TypeOf v1 Is Decimal And TypeOf v2 Is Decimal Then
                        Return DirectCast(v1 >= v2, Boolean)
                    Else
                        Throw New ArgumentException("The greater than or equal to comparison cannot be performed on these value of type " & v1.GetType.Name & " and " & v2.GetType.Name & ".")
                    End If
                Case OperatorType.oNotEqualTo
                    If v1.GetType Is v2.GetType Then
                        Return DirectCast(v1 <> v2, Boolean)
                    Else
                        Return True
                    End If
                Case OperatorType.oShiftLeft
                    If TypeOf v1 Is Decimal And TypeOf v2 Is Decimal Then
                        Return CType(v1 << v2, Decimal)
                    Else
                        Throw New ArgumentException("The left shift operation cannot be performed on these value of type " & v1.GetType.Name & " and " & v2.GetType.Name & ".")
                    End If
                Case OperatorType.oShiftRight
                    If TypeOf v1 Is Decimal And TypeOf v2 Is Decimal Then
                        Return CType(v1 >> v2, Decimal)
                    Else
                        Throw New ArgumentException("The right shift operation cannot be performed on these value of type " & v1.GetType.Name & " and " & v2.GetType.Name & ".")
                    End If
                Case OperatorType.oConcatenation
                    Return v1.ToString & v2.ToString
                Case OperatorType.oAddition
                    If TypeOf v1 Is Decimal And TypeOf v2 Is Decimal Then
                        Return CType(v1 + v2, Decimal)
                    ElseIf TypeOf v1 Is String Or TypeOf v2 Is String Then
                        Return v1.ToString() & v2.ToString()
                    Else
                        Throw New ArgumentException("The addition operation cannot be performed on these value of type " & v1.GetType.Name & " and " & v2.GetType.Name & ".")
                    End If
                Case OperatorType.oSubtraction
                    If TypeOf v1 Is Decimal And TypeOf v2 Is Decimal Then
                        Return CType(v1 - v2, Decimal)
                    Else
                        Throw New ArgumentException("The subtraction operation cannot be performed on these value of type " & v1.GetType.Name & " and " & v2.GetType.Name & ".")
                    End If
                Case OperatorType.oModulo
                    If TypeOf v1 Is Decimal And TypeOf v2 Is Decimal Then
                        Return CType(v1 Mod v2, Decimal)
                    Else
                        Throw New ArgumentException("The modulo operation cannot be performed on these value of type " & v1.GetType.Name & " and " & v2.GetType.Name & ".")
                    End If
                Case OperatorType.oDivisionInteger
                    If TypeOf v1 Is Decimal And TypeOf v2 Is Decimal Then
                        Return CType(v1 \ v2, Decimal)
                    Else
                        Throw New ArgumentException("The integer division operation cannot be performed on these value of type " & v1.GetType.Name & " and " & v2.GetType.Name & ".")
                    End If
                Case OperatorType.oMultiplication
                    If TypeOf v1 Is Decimal And TypeOf v2 Is Decimal Then
                        Return CType(v1 * v2, Decimal)
                    Else
                        Throw New ArgumentException("The multiplication operation cannot be performed on these value of type " & v1.GetType.Name & " and " & v2.GetType.Name & ".")
                    End If
                Case OperatorType.oDivision
                    If TypeOf v1 Is Decimal And TypeOf v2 Is Decimal Then
                        Return CType(v1 / v2, Decimal)
                    Else
                        Throw New ArgumentException("The division operation cannot be performed on these value of type " & v1.GetType.Name & " and " & v2.GetType.Name & ".")
                    End If
                Case Else
                    Throw New ArgumentException(Operation & " is not a known operation.")
            End Select
        End Get
    End Property

End Class

Public Class OpCodeVariable
    Inherits ValueOpCode

    Private VariableName As Object
    Private lPlugin As ScriptEngine

    Public Sub New(VariableName As Object, lPlugin As ScriptEngine)
        Me.VariableName = VariableName
        Me.lPlugin = lPlugin
    End Sub

    Public Overrides ReadOnly Property Value As Object
        Get
            Dim _Value As Object
            If Not lPlugin.Variables.TryGetValue(VariableName, _Value) Then
                Throw New ArgumentException("Variable " & VariableName & " is not set.")
            End If
            Return _Value
        End Get
    End Property
End Class

Public Class OpCodeFunction
    Inherits ValueOpCode

    Private _Args() As ValueOpCode
    Private _MethodName As String
    Private _Script As Script
    Private _Connection As IRCConnection
    Private _Channel As String
    Public _Target As ValueOpCode

    Public Sub New(Script As Script, MethodName As String, Args() As OpCode, Connection As IRCConnection, Channel As String, Target As Object)
        _Script = Script
        _MethodName = MethodName
        _Args = Args
        _Connection = Connection
        _Channel = Channel
        _Target = Target
    End Sub

    Public Overrides ReadOnly Property Value As Object
        Get
            'If (_Target IsNot Nothing Or _MethodName.ToUpper <> "HASPERMISSION") And Not _Script.HasPermission("script.execute") Then Throw New Security.SecurityException("This function is not ready for release yet. Sorry.")

            'Dim rTarget = If(_Target Is Nothing, Nothing, _Target.Value)
            'If rTarget Is Nothing Then
            '    If _MethodName.ToUpper = "HASPERMISSION" Then
            '        If _Args.Count <> 1 Then Throw New SyntaxException("$haspermission must take exactly one parameter.")
            '        Return _Script.HasPermission(_Args(0).Value)
            '    End If
            '    For Each Plugin In Plugins
            '        If _MethodName.ToUpper = Plugin.Key.ToUpper Then Return Plugin.Value.Obj
            '    Next
            'ElseIf TypeOf rTarget Is ListEventArgs Then
            '    If DirectCast(rTarget, ListEventArgs).Parameters.ContainsKey(_MethodName) Then _
            '        Return DirectCast(rTarget, ListEventArgs).Parameters(_MethodName)
            '    Throw New MissingMemberException("Event parameter '" & _MethodName & "' not found.")
            'End If
            'Return _Script.RunCommand(_MethodName, _Args, _Connection, _Channel, True, rTarget, 1)
        End Get
    End Property
End Class