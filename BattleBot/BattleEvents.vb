Imports System.ComponentModel

Partial Public Class BattleBot
    Public Event BattleOpen(sender As Object, e As BattleOpenEventArgs)
End Class

Public Class BattleOpenEventArgs
    Inherits CancelEventArgs

    ''' <summary>The number of seconds until the battle starts.</summary>
    Public Property Time As Integer
    ''' <summary>The type of battle.</summary>
    Public Property Type As BattleType

    Public Sub New(Type As BattleType, Time As Integer)
        _Type = Type
        _Time = Time
    End Sub
End Class

Public Enum BattleType
    Normal = 0
    Gauntlet = 1
    Mimic = 2
    NPC = 3
    SaveThePresident = 4
End Enum