Imports VBot

Friend Class NamedEntry
    Public ID As UInteger
    Public Category As String
    Public Name As String
    Public Nicknames As New List(Of String)
    Public Description As String
End Class

Friend Class Item
    Inherits NamedEntry

    Public HowToObtain As String
End Class

Friend Class Buff
    Inherits NamedEntry
End Class

Friend Class NPC
    Inherits NamedEntry

    Public HP As UInteger
    Public Attack As Integer
    Public Defense As Integer
End Class

Public Class TerrariaInfoPlugin
    Inherits Plugin

    Public Overrides ReadOnly Property Name As String
        Get
            Return "Terraria Info"
        End Get
    End Property
    Public Overrides ReadOnly Property UseGlobalKeyCommand As Boolean
        Get
            Return True
        End Get
    End Property

End Class
