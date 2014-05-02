Public Module IRCColours
    Public ReadOnly ColourCode As Char = Chr(3)
    Public ReadOnly White As String = Chr(3) & "00"
    Public ReadOnly Black As String = Chr(3) & "01"
    Public ReadOnly DarkBlue As String = Chr(3) & "02"
    Public ReadOnly DarkGreen As String = Chr(3) & "03"
    Public ReadOnly Red As String = Chr(3) & "04"
    Public ReadOnly DarkRed As String = Chr(3) & "05"
    Public ReadOnly Purple As String = Chr(3) & "06"
    Public ReadOnly Orange As String = Chr(3) & "07"
    Public ReadOnly Yellow As String = Chr(3) & "08"
    Public ReadOnly Green As String = Chr(3) & "09"
    Public ReadOnly Teal As String = Chr(3) & "10"
    Public ReadOnly Cyan As String = Chr(3) & "11"
    Public ReadOnly Blue As String = Chr(3) & "12"
    Public ReadOnly Magenta As String = Chr(3) & "13"
    Public ReadOnly DarkGray As String = Chr(3) & "14"
    Public ReadOnly Gray As String = Chr(3) & "15"
    Public ReadOnly DefaultColour As String = Chr(3) & "99"
    Public ReadOnly Bold As String = Chr(2)
    Public ReadOnly Underline As String = Chr(31)
    Public ReadOnly CTCP As String = Chr(1)
    Public ReadOnly ClearFormat As String = Chr(15)
    Public ReadOnly Reverse As String = Chr(22)

    Private ReadOnly NickColours() As Integer = {3, 4, 6, 8, 9, 10, 11, 12, 13}
    Public Function NicknameColour(ByVal Nickname As String) As String
        Dim Total As Integer = 0
        For Each c In Nickname
            Total += AscW(c)
        Next
        Return ColourCode & NickColours(Total Mod NickColours.Length).ToString("00")
    End Function
End Module
