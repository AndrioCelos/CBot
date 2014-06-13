Imports VBot
Imports System.Text

Public Class Functions
    Public Plugin As ScriptEngine

    Public Sub New(ByVal Plugin As ScriptEngine)
        Me.Plugin = Plugin
    End Sub

#Region "Event parameters"
    Public Function [event]() As EventArgs
        Return Plugin.e
    End Function

    Public Function nick() As String
        Return Plugin.Sender.Split("!"c)(0)
    End Function

    Public Function channel() As String
        Return Plugin.Channel
    End Function

    Public Function connection() As IRCConnection
        Return Plugin.Connection
    End Function
#End Region

#Region "VBot functions"
    Public Function haspermission(Connection As IRCConnection, Channel As String, User As String, Permission As String) As Boolean
        Return UserHasPermission(Connection, Channel, User, Permission)
    End Function
#End Region

#Region "IRC functions"

    Public Function away(Connection As IRCConnection) As Boolean
        Return Connection.Away
    End Function

    Public Function awayreason(Connection As IRCConnection) As String
        Return Connection.AwayReason
    End Function

    Public Function awaysince(Connection As IRCConnection) As Date
        Return Connection.AwaySince
    End Function

    Public Function [me](Connection As IRCConnection) As String
        Return Connection.Nickname
    End Function

    Public Function ison(Connection As IRCConnection, Channel As String, Nickname As String) As Boolean
        Return Connection.Channels.ContainsKey(Channel) AndAlso Connection.Channels(Channel).Users.ContainsKey(Nickname)
    End Function
    Public Function ishalfvoice(Connection As IRCConnection, Channel As String, Nickname As String) As Boolean
        Return Connection.Channels.ContainsKey(Channel) AndAlso Connection.Channels(Channel).Users.ContainsKey(Nickname) AndAlso Connection.Channels(Channel).Users(Nickname).ChannelAccess >= IRCConnection.ChannelAccessModes.HalfVoice
    End Function
    Public Function isvoice(Connection As IRCConnection, Channel As String, Nickname As String) As Boolean
        Return Connection.Channels.ContainsKey(Channel) AndAlso Connection.Channels(Channel).Users.ContainsKey(Nickname) AndAlso Connection.Channels(Channel).Users(Nickname).ChannelAccess >= IRCConnection.ChannelAccessModes.Voice
    End Function
    Public Function ishalfop(Connection As IRCConnection, Channel As String, Nickname As String) As Boolean
        Return Connection.Channels.ContainsKey(Channel) AndAlso Connection.Channels(Channel).Users.ContainsKey(Nickname) AndAlso Connection.Channels(Channel).Users(Nickname).ChannelAccess >= IRCConnection.ChannelAccessModes.HalfOp
    End Function
    Public Function isop(Connection As IRCConnection, Channel As String, Nickname As String) As Boolean
        Return Connection.Channels.ContainsKey(Channel) AndAlso Connection.Channels(Channel).Users.ContainsKey(Nickname) AndAlso Connection.Channels(Channel).Users(Nickname).ChannelAccess >= IRCConnection.ChannelAccessModes.Op
    End Function
    Public Function isadmin(Connection As IRCConnection, Channel As String, Nickname As String) As Boolean
        Return Connection.Channels.ContainsKey(Channel) AndAlso Connection.Channels(Channel).Users.ContainsKey(Nickname) AndAlso Connection.Channels(Channel).Users(Nickname).ChannelAccess >= IRCConnection.ChannelAccessModes.Admin
    End Function
    Public Function isowner(Connection As IRCConnection, Channel As String, Nickname As String) As Boolean
        Return Connection.Channels.ContainsKey(Channel) AndAlso Connection.Channels(Channel).Users.ContainsKey(Nickname) AndAlso Connection.Channels(Channel).Users(Nickname).ChannelAccess >= IRCConnection.ChannelAccessModes.Owner
    End Function

#End Region

#Region "Math"

    Public Function abs(Value As Decimal) As Decimal
        Return Math.Abs(Value)
    End Function
    Public Function ceil(Value As Decimal) As Decimal
        Return Math.Ceiling(Value)
    End Function
    Public Function cos(Value As Decimal) As Decimal
        Return Math.Cos(Value)
    End Function
    Public Function acos(Value As Decimal) As Decimal
        Return Math.Acos(Value)
    End Function
    Public Function exp(Value As Decimal) As Decimal
        Return Math.Exp(Value)
    End Function
    Public Function floor(Value As Decimal) As Decimal
        Return Math.Floor(Value)
    End Function
    Public Function int(Value As Decimal) As Decimal
        Return Fix(Value)
    End Function
    Public Function log(Value As Decimal) As Decimal
        Return Math.Log(Value, 10)
    End Function
    Public Function log(Value As Decimal, Base As Decimal) As Decimal
        Return Math.Log(Value, Base)
    End Function
    Public Function ln(Value As Decimal) As Decimal
        Return Math.Log(Value)
    End Function
    Public Function max(ParamArray Values() As Decimal) As Decimal
        If Values Is Nothing OrElse Values.Count = 0 Then Return 0
        max = Decimal.MinValue
        For Each Value In Values
            If Value > max Then max = Value
        Next
    End Function
    Public Function min(ParamArray Values() As Decimal) As Decimal
        If Values Is Nothing OrElse Values.Count = 0 Then Return 0
        min = Decimal.MaxValue
        For Each Value In Values
            If Value < min Then min = Value
        Next
    End Function
    Public Function round(Value As Decimal) As Decimal
        Return Math.Round(Value)
    End Function
    Public Function round(Value As Decimal, Places As Integer) As Decimal
        Return Math.Round(Value, Places)
    End Function
    Public Function sin(Value As Decimal) As Decimal
        Return Math.Sin(Value)
    End Function
    Public Function asin(Value As Decimal) As Decimal
        Return Math.Asin(Value)
    End Function
    Public Function sqrt(Value As Decimal) As Decimal
        Return Math.Sqrt(Value)
    End Function
    Public Function root(Value As Decimal, Index As Decimal) As Decimal
        Return Value ^ (1 / Index)
    End Function
    Public Function tan(Value As Decimal) As Decimal
        Return Math.Tan(Value)
    End Function
    Public Function atan(Value As Decimal) As Decimal
        Return Math.Atan(Value)
    End Function
    Public Function atan(y As Decimal, x As Decimal) As Decimal
        Return Math.Atan2(y, x)
    End Function

    ' Constants
    Public Function pi() As Decimal
        Return Math.PI
    End Function
    Public Function e() As Decimal
        Return Math.E
    End Function
#End Region

#Region "Date math"
    Public Function [date]() As Date
        Return Today
    End Function
    Public Function time() As Date
        Return TimeOfDay
    End Function
    Public Function now() As Date
        Return Date.Now
    End Function
    Public Function [date](Year As Integer, Month As Integer, Day As Integer)
        Return New Date(Year, Month, Day)
    End Function
    Public Function [date](Year As Integer, Month As Integer, Day As Integer, Hour As Integer, Minute As Integer, Second As Integer)
        Return New Date(Year, Month, Day, Hour, Minute, Second)
    End Function
#End Region

#Region "Strings"
    Public Function cr() As String
        Return ChrW(13)
    End Function
    Public Function lf() As String
        Return ChrW(10)
    End Function
    Public Function crlf() As String
        Return ChrW(13) & ChrW(10)
    End Function

    Public Function ord(Number As Integer) As String
        If Number < 0 Then Throw New ArgumentException("Number cannot be negative.")
        Dim Suffix As String
        Dim Modulus As Integer = Number Mod 100
        Dim Modulus2 As Integer = Modulus Mod 10

        If Modulus >= 10 And Modulus <= 19 Then
            Suffix = "th"
        ElseIf Modulus2 = 1 Then
            Suffix = "st"
        ElseIf Modulus2 = 2 Then
            Suffix = "nd"
        ElseIf Modulus2 = 3 Then
            Suffix = "rd"
        Else
            Suffix = "th"
        End If

        Return Number.ToString & Suffix
    End Function

    Public Function asc(Character As String) As Integer
        If Character Is Nothing OrElse Character.Length = 0 Then Throw New ArgumentException("An empty string is not valid here.")
        Return AscW(Character(0))
    End Function
    Public Function chr(Index As Integer) As String
        Return ChrW(Index)
    End Function
    Public Function countstr(Text As String, Substring As String) As Integer
        If Substring Is Nothing OrElse Text Is Nothing OrElse Substring.Length = 0 Then Return 0
        countstr = 0
        Dim x As Integer
        Do
            x = Text.IndexOf(Substring, x)
            If x < 1 Then Exit Do
            countstr += 1
            x += Substring.Length
        Loop
    End Function
    Public Function left(Text As String, Count As Integer) As String
        If Text Is Nothing Then Return Nothing
        Return Text.Substring(0, If(Count > Text.Length, Text.Length, Count))
    End Function
    Public Function len(Text As String) As Integer
        If Text Is Nothing Then Return Nothing
        Return Text.Length
    End Function
    Public Function lower(Text As String) As String
        If Text Is Nothing Then Return Nothing
        Return Text.ToLower
    End Function
    Public Function mid(Text As String, Start As Integer) As String
        If Text Is Nothing Then Return Nothing
        If Start >= Text.Length Then Return ""
        Return Text.Substring(Start)
    End Function
    Public Function mid(Text As String, Start As Integer, Length As Integer) As String
        If Text Is Nothing Then Return Nothing
        If Start >= Text.Length Then Return ""
        If Start + Length > Text.Length Then Return Text.Substring(Start)
        Return Text.Substring(Start, Length)
    End Function
    Public Function remove(Text As String, Start As Integer, Length As Integer) As String
        Return Text.Remove(Start, Length)
    End Function
    Public Function removestr(Text As String, Substring As String) As String
        Return Text.Replace(Substring, "")
    End Function
    Public Function replace(Text As String, Substring As String, Replacement As String) As String
        Return Text.Replace(Substring, Replacement)
    End Function
    Public Function replacew(Text As String, Pattern As String, Replacement As String) As String
        Dim RegexBuilder As New StringBuilder, x As Integer
        For x = 0 To Pattern.Length - 1
            Dim c = Pattern(x)
            Select Case c
                Case "*"c
                    RegexBuilder.Append(".*")
                Case "?"c
                    RegexBuilder.Append(".")
                Case "\"c, "+"c, "|"c, "{"c, "["c, "("c, ")"c, "^"c, "$"c, "."c, "#"c
                    RegexBuilder.Append("\"c)
                    RegexBuilder.Append(c)
                Case Else
                    RegexBuilder.Append(c)
            End Select
        Next
        Return System.Text.RegularExpressions.Regex.Replace(Text, RegexBuilder.ToString,
                                                            System.Text.RegularExpressions.Regex.Escape(Replacement))
    End Function
    Public Function replacer(Text As String, Pattern As String, Replacement As String) As String
        Return System.Text.RegularExpressions.Regex.Replace(Text, Pattern, Replacement)
    End Function
    Public Function right(Text As String, Count As Integer) As String
        If Text Is Nothing Then Return Nothing
        Return Strings.Right(Text, Count)
    End Function
    Public Function strip(Text As String) As String
        Return IRCConnection.RemoveCodes(Text)
    End Function
    Public Function upper(Text As String) As String
        If Text Is Nothing Then Return Nothing
        Return Text.ToUpper
    End Function

#End Region

#Region "Filing"
    Public Function filename(Path As String) As String
        Return System.IO.Path.GetFileName(Path)
    End Function

    Public Function filenamenoext(Path As String) As String
        Return System.IO.Path.GetFileNameWithoutExtension(Path)
    End Function

    Public Function filedir(Path As String) As String
        Return System.IO.Path.GetDirectoryName(Path)
    End Function

    Public Function fileext(Path As String) As String
        Return System.IO.Path.GetExtension(Path)
    End Function

    Public Function exists(Path As String) As Boolean
        Return System.IO.File.Exists(Path) OrElse System.IO.Directory.Exists(Path)
    End Function

    Public Function isfile(Path As String) As Boolean
        Return System.IO.File.Exists(Path)
    End Function

    Public Function isdir(Path As String) As Boolean
        Return System.IO.Directory.Exists(Path)
    End Function
#End Region

#Region "Miscellany"
    Public Function [true]() As Boolean
        Return True
    End Function
    Public Function [false]() As Boolean
        Return False
    End Function
    Public Function null() As Object
        Return Nothing
    End Function

    Public Function iif(Expression As Boolean, TrueValue As Object, FalseValue As Object) As Object
        Return If(Expression, TrueValue, FalseValue)
    End Function

    Public Function environ(Name As String) As String
        Return Microsoft.VisualBasic.Interaction.Environ(Name)
    End Function
    Public Function environ(Index As Integer) As String
        Return Microsoft.VisualBasic.Interaction.Environ(Index)
    End Function
#End Region

End Class
