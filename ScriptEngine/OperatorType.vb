Public Enum OperatorType As Byte
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
    oExponentiation = &HA0
    oNegation = &H98
    oNot = &H18
    oFactorial = &HA8
    oIncrementPrefix = &HC8
    oIncrementPostfix = &HB8
    oDecrementPrefix = &HC9
    oDecrementPostfix = &HB9

    oMinus = &H1
End Enum

