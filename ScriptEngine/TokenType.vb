Public Enum TokenTypes As Byte
    ''' <summary>No token</summary>
    tNone = 0
    ''' <summary>The end of the line</summary>
    tEndOfLine
    ''' <summary>The end of the script</summary>
    tEndOfScript

    ''' <summary>A numeric literal</summary>
    tNumber
    ''' <summary>A text literal (that isn't a number)</summary>
    tText
    ''' <summary>A function</summary>
    tFunction
    ''' <summary>A variable</summary>
    tVariable

    ''' <summary>An 'if' statement</summary>
    tIf
    ''' <summary>An 'else' statement</summary>
    tElse
    ''' <summary>A 'while' statement</summary>
    tWhile
    ''' <summary>A 'return' statement</summary>
    tReturn
    ''' <summary>A 'halt' command</summary>
    tHalt
    ''' <summary>A 'haltdef' command</summary>
    tHaltDef
    ''' <summary>A 'set', 'unset', 'inc', 'dec' or 'unset' command</summary>
    tVariableStatement

    ''' <summary>An opening parenthesis</summary>
    tOpenParenthesis
    ''' <summary>A closing parenthesis</summary>
    tCloseParenthesis
    ''' <summary>An opening square bracket</summary>
    tOpenBracket
    ''' <summary>A closing square bracket</summary>
    tCloseBracket
    ''' <summary>An opening brace</summary>
    tOpenBrace
    ''' <summary>A closing brace</summary>
    tCloseBrace
    ''' <summary>A comma (parameter delimiter)</summary>
    tComma
    ''' <summary>A dot (that isn't a decimal point)</summary>
    tDot
    ''' <summary>A semicolon</summary>
    tSemicolon

    ''' <summary>A line comment marker</summary>
    tCommentLine
    ''' <summary>A block comment opening</summary>
    tCommentStart
    ''' <summary>A block comment closing</summary>
    tCommentEnd

    ''' <summary>An expression operator</summary>
    tOperator

    ''' <summary>An unrecognised token</summary>
    tUnrecognised = 255
End Enum
