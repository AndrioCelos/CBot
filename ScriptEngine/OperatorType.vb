Public Enum OperatorType As Byte
    oNone = 0

    oAnd = &H10
    oOr = &H11
    oXor = &H12
    oBooleanAnd = &H13
    oBooleanOr = &H14
    oBooleanXor = &H15
    oBooleanImplication = &H16
    oEquals = &H20
    oLessThan = &H21
    oGreaterThan = &H22
    oLessThanOrEqualTo = &H23
    oGreaterThanOrEqualTo = &H24
    oNotEqualTo = &H25
    oShiftLeft = &H30
    oShiftRight = &H31
    oConcatenation = &H40
    oAddition = &H50
    oSubtraction = &H51
    oModulo = &H60
    oDivisionInteger = &H70
    oMultiplication = &H80
    oDivision = &H81
    oNot = &H18
    oNegation = &H98
    oFactorial = &HAC
    oIncrementPrefix = &HC8
    oIncrementPostfix = &HBC
    oDecrementPrefix = &HC9
    oDecrementPostfix = &HBD

    oMinus = &H1
    oAmpersand = &H2
    oIncrement = &H8
    oDecrement = &H9
    oExclamationMark = &HA

    oUnaryOperator = &H8
    oPostfixUnaryOperator = &HC
End Enum
