Imports VBot

Partial Public Class ScriptEngine

    Public Class Commands
        Public Plugin As ScriptEngine

        Public Sub New(ByVal Plugin As ScriptEngine)
            Me.Plugin = Plugin
        End Sub

#Region "IRC commands"
        Public Sub ame(ByVal Message As String)
            For Each _connection In Connections
                If _connection.IsRegistered Then
                    For Each _Channel In _connection.Channels
                        Plugin.Say(_connection, _Channel.Key, ChrW(1) & "ACTION " & Message & ChrW(1), SayOptions.NoParse)
                    Next
                End If
            Next
        End Sub
        Public Sub amsg(ByVal Message As String)
            For Each _connection In Connections
                If _connection.IsRegistered Then
                    For Each _channel In _connection.Channels
                        Plugin.Say(_connection, _channel.Key, Message, SayOptions.NoParse)
                    Next
                End If
            Next
        End Sub

        Public Sub anick(ByVal Nickname As String)
            ' TODO: Validity check
            For Each _connection In Connections
                _connection.Send("NICK " & Nickname)
            Next
        End Sub

        Public Sub ban(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal User As String)
            If System.Text.RegularExpressions.Regex.IsMatch(User, "^[A-}][A-}0-9]*$") Then
                ' It's a valid nickname.
                ' Are they on the channel?
                If Connection.Channels(Channel).Users.ContainsKey(User) Then
                    Connection.Send("MODE " & Channel & " +b *!*@" & Connection.Channels(Channel).Users(User).Host)
                    Return
                End If
            End If
            Connection.Send("MODE " & Channel & " +b " & User)
        End Sub

        Public Sub away(ByVal Connection As IRCConnection, ByVal Reason As String)
            Connection.Send("AWAY " & Reason)
        End Sub

        Public Sub back(ByVal Connection As IRCConnection)
            Connection.Send("AWAY")
        End Sub

        Public Sub ctcp(ByVal connection As IRCConnection, ByVal Target As String, ByVal Request As String)
            connection.Send("PRIVMSG " & Target & " " & ChrW(1) & Request & ChrW(1))
        End Sub
        Public Sub ctcp(ByVal connection As IRCConnection, ByVal Target As String, ByVal Request As String, ByVal ParamArray Parameters() As String)
            connection.Send("PRIVMSG " & Target & " " & ChrW(1) & Request & " " & String.Join(" ", Parameters) & ChrW(1))
        End Sub

        Public Sub ctcpreply(ByVal connection As IRCConnection, ByVal Target As String, ByVal Message As String)
            connection.Send("NOTICE " & Target & " " & ChrW(1) & Message & ChrW(1))
        End Sub

        Public Sub describe(ByVal connection As IRCConnection, ByVal Target As String, ByVal Action As String)
            connection.Send("PRIVMSG " & Target & " " & ChrW(1) & "ACTION " & Action & ChrW(1))
        End Sub

        Public Sub hop(ByVal connection As IRCConnection, ByVal Channel As String)
            connection.Send("PART " & Channel)
            connection.Send("JOIN " & Channel)
        End Sub
        Public Sub hop(ByVal connection As IRCConnection, ByVal Channel As String, ByVal TargetChannel As String)
            connection.Send("PART " & Channel)
            connection.Send("JOIN " & TargetChannel)
        End Sub
        Public Sub hop(ByVal connection As IRCConnection, ByVal Channel As String, ByVal TargetChannel As String, ByVal Message As String)
            connection.Send("PART " & Channel & " :" & Message)
            connection.Send("JOIN " & TargetChannel)
        End Sub

        Public Sub invite(ByVal connection As IRCConnection, ByVal Channel As String, ByVal Nickname As String)
            connection.Send("INVITE " & Nickname & " " & Channel)
        End Sub

        Public Sub join(ByVal connection As IRCConnection, ByVal rChannel As String)
            connection.Send("JOIN " & rChannel)
        End Sub

        Public Sub kick(ByVal connection As IRCConnection, ByVal Channel As String, ByVal Target As String)
            connection.Send("KICK " & Channel & " " & Target)
        End Sub
        Public Sub kick(ByVal connection As IRCConnection, ByVal Channel As String, ByVal Target As String, ByVal Message As String)
            connection.Send("KICK " & Channel & " " & Target & " :" & Message)
        End Sub

        Public Sub kickban(ByVal connection As IRCConnection, ByVal Channel As String, ByVal Target As String)
            ban(connection, Channel, Target)
            kick(connection, Channel, Target)
        End Sub
        Public Sub kickban(ByVal connection As IRCConnection, ByVal Channel As String, ByVal Target As String, ByVal Message As String)
            ban(connection, Channel, Target)
            kick(connection, Channel, Target, Message)
        End Sub

        Public Sub [me](ByVal Connection As IRCConnection, ByVal Channel As String, ByVal Action As String)
            Connection.Send("PRIVMSG " & Channel & " :" & ChrW(1) & "ACTION " & Action & ChrW(1))
        End Sub

        Public Sub mode(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal Modes As String)
            Connection.Send("MODE " & Channel & " " & Modes)
        End Sub

        Public Sub msg(ByVal connection As IRCConnection, ByVal Target As String, ByVal Message As String)
            connection.Send("PRIVMSG " & Target & " :" & Message)
        End Sub

        Public Sub nick(ByVal connection As IRCConnection, ByVal Nickname As String)
            connection.Send("NICK " & Nickname)
        End Sub

        Public Sub notice(ByVal connection As IRCConnection, ByVal Target As String, ByVal Message As String)
            connection.Send("NOTICE " & Target & " :" & Message)
        End Sub

        Public Sub part(ByVal connection As IRCConnection, ByVal Channel As String)
            connection.Send("PART " & Channel)
        End Sub
        Public Sub part(ByVal connection As IRCConnection, ByVal Channel As String, ByVal Message As String)
            connection.Send("PART " & Channel & " :" & Message)
        End Sub

        Public Sub partall(ByVal connection As IRCConnection)
            For Each _channel In connection.Channels
                connection.Send("PART " & _channel.Key)
            Next
        End Sub
        Public Sub partall(ByVal connection As IRCConnection, ByVal Message As String)
            For Each _channel In connection.Channels
                connection.Send("PART " & _channel.Key & " :" & Message)
            Next
        End Sub

        Public Sub quit(ByVal connection As IRCConnection)
            connection.Send("QUIT")
        End Sub
        Public Sub quit(ByVal connection As IRCConnection, ByVal Message As String)
            connection.Send("QUIT :" & Message)
        End Sub

        Public Sub raw(ByVal connection As IRCConnection, ByVal Command As String)
            connection.Send(Command)
        End Sub

        Public Sub say(ByVal Connection As IRCConnection, ByVal cChannel As String, ByVal Message As String)
            Connection.Send("PRIVMSG " & cChannel & " :" & Message)
        End Sub

        Public Sub topic(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal Topic As String)
            Connection.Send("TOPIC " & Channel & " :" & Topic)
        End Sub

#Region "Nickname modes"
        Public Sub admin(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal ParamArray Targets() As String)
            Connection.Channels(Channel).Admin(Targets)
        End Sub
        Public Sub op(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal ParamArray Targets() As String)
            Connection.Channels(Channel).Op(Targets)
        End Sub
        Public Sub halfop(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal ParamArray Targets() As String)
            Connection.Channels(Channel).HalfOp(Targets)
        End Sub
        Public Sub voice(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal ParamArray Targets() As String)
            Connection.Channels(Channel).Voice(Targets)
        End Sub
        Public Sub halfvoice(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal ParamArray Targets() As String)
            Connection.Channels(Channel).DeHalfVoice(Targets)
        End Sub

        Public Sub deadmin(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal ParamArray Targets() As String)
            Connection.Channels(Channel).DeAdmin(Targets)
        End Sub
        Public Sub deop(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal ParamArray Targets() As String)
            Connection.Channels(Channel).DeOp(Targets)
        End Sub
        Public Sub dehalfop(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal ParamArray Targets() As String)
            Connection.Channels(Channel).DeHalfOp(Targets)
        End Sub
        Public Sub devoice(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal ParamArray Targets() As String)
            Connection.Channels(Channel).DeVoice(Targets)
        End Sub
        Public Sub dehalfvoice(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal ParamArray Targets() As String)
            Connection.Channels(Channel).DeHalfVoice(Targets)
        End Sub
#End Region
#End Region

        Public Sub connect(ByVal Address As String)
            Dim newConnection As New IRCConnection
            newConnection.Address = Address
            newConnection.Connect()
        End Sub
        Public Sub connect(ByVal Address As String, ByVal Port As UShort)
            Dim newConnection As New IRCConnection
            newConnection.Address = Address
            newConnection.Port = Port
            newConnection.Connect()
        End Sub
        Public Sub die()
            For Each _connection In Connections
                _connection.Send("QUIT")
            Next
            For Each lPlugin In Plugins
                lPlugin.Value.Obj.OnSave()
            Next
            VBot.Die()
        End Sub
        Public Sub disconnect(ByVal Connection As IRCConnection)
            Connection.Send("QUIT")
            Connection.Disconnect()
        End Sub

        Public Sub echo(text As String)
            Console.WriteLine(text)
        End Sub

        Public Sub [set](VariableName As String, Value As Object)
            If Plugin.Variables.ContainsKey(VariableName) Then
                Plugin.Variables(VariableName) = New TypedValue(Value)
            Else
                Plugin.Variables.Add(VariableName, New TypedValue(Value))
            End If
        End Sub
        Public Sub unset(VariableName As String)
            If Plugin.Variables.ContainsKey(VariableName) Then
                Plugin.Variables.Remove(VariableName)
            End If
        End Sub

        Public Sub inc(VariableName As String)
            inc(VariableName, 1)
        End Sub
        Public Sub inc(VariableName As String, Value As Decimal)
            If Plugin.Variables.ContainsKey(VariableName) Then
                If Plugin.Variables(VariableName).Type = TypedValue.BasicType.tDecimal Then
                    Plugin.Variables(VariableName) = New TypedValue(CDec(Plugin.Variables(VariableName).Value) + 1)
                Else
                    Throw New InvalidCastException("Cannot increment a non-numeric value.")
                End If
            Else
                Plugin.Variables.Add(VariableName, New TypedValue(1D))
            End If
        End Sub
        Public Sub dec(VariableName As String)
            dec(VariableName, 1)
        End Sub
        Public Sub dec(VariableName As String, Value As Double)
            If Plugin.Variables.ContainsKey(VariableName) Then
                If Plugin.Variables(VariableName).Type = TypedValue.BasicType.tDecimal Then
                    Plugin.Variables(VariableName) = New TypedValue(CDec(Plugin.Variables(VariableName).Value) - 1)
                Else
                    Throw New InvalidCastException("Cannot decrement a non-numeric value.")
                End If
            Else
                Plugin.Variables.Add(VariableName, New TypedValue(-1D))
            End If
        End Sub

#Region "Filing"
        Public Sub copy(Source As String, Destination As String, Overwrite As Boolean)
            My.Computer.FileSystem.CopyFile(Source, Destination, Overwrite)
        End Sub
        Public Sub copy(Source As String, Destination As String)
            copy(Source, Destination, True)
        End Sub

        Public Sub move(Source As String, Destination As String, Overwrite As Boolean)
            My.Computer.FileSystem.MoveFile(Source, Destination, Overwrite)
        End Sub
        Public Sub move(Source As String, Destination As String)
            move(Source, Destination, True)
        End Sub

        Public Sub delete(File As String)
            My.Computer.FileSystem.DeleteFile(File)
        End Sub

        Public Sub rename(File As String, Name As String)
            My.Computer.FileSystem.RenameFile(File, Name)
        End Sub
#End Region
    End Class
End Class