Imports System.Text
Imports VBot.IRCConnection

Public Class Plugin
    Public Channels() As String
    ''' <summary>
    ''' These channels are supposed to be able to hear basic status messages from a plugin, and can send commands to this plugin by using its key as a command: e.g. !ThisPlugin command
    ''' </summary>
    Public MinorChannels() As String = {}
    ''' <summary>
    ''' The prefix that is sent to minor channels before messages.
    ''' </summary>
    Public MinorLabel As String = IRCColours.Gray & "[" & IRCColours.ClearFormat & Name & IRCColours.Gray & "]" & IRCColours.ClearFormat & " "
    ''' <summary>
    ''' Whether Channel Message, Join, Part etc. messages will be raised from minor channels.
    ''' </summary>
    Public Overridable ReadOnly Property ListenInMinorChannels As Boolean
        Get
            Return False
        End Get
    End Property

    ''' <summary>
    ''' Whether this plugin's key can be used as a global command. e.g. !newplugin command arg
    ''' </summary>
    Public Overridable ReadOnly Property UseGlobalKeyCommand As Boolean
        Get
            'Return Not (Channels.Contains("*") Or Channels.Contains("*/*") Or Channels.Contains("!*") Or Channels.Contains("!*/*"))
            Return False
        End Get
    End Property

    Public Overridable ReadOnly Property Name As String
        Get
            Return "New Plugin"
        End Get
    End Property

    Public ReadOnly Property CommandPrefixes As String()
        Get
            Return DefaultCommandPrefixes
        End Get
    End Property
    Public ReadOnly Property CommandPrefixes(ByVal Channel As String) As String()
        Get
            If ChannelCommandPrefixes.ContainsKey(Channel) Then Return ChannelCommandPrefixes(Channel)
            Return DefaultCommandPrefixes
        End Get
    End Property
    Public ReadOnly Property CommandPrefixes(ByVal Connection As IRCConnection, ByVal Channel As String) As String()
        Get
            If Connection Is Nothing Then Return CommandPrefixes(Channel.Split("/"c)(0) & "/" & Channel.Split("/"c)(1))
            If ChannelCommandPrefixes.ContainsKey((Connection.Address & "/" & Channel).ToLower) Then Return ChannelCommandPrefixes((Connection.Address & "/" & Channel).ToLower)
            Return DefaultCommandPrefixes
        End Get
    End Property

    ''' <summary>
    ''' Returns information about this plugin's functionality, for the !help command.
    ''' </summary>
    ''' <param name="Topic">The topic that was queried.</param>
    ''' <param name="IsMajorChannel"></param>
    Public Overridable Function Help(ByVal Topic As String, ByVal IsMajorChannel As Boolean) As String
        Return Nothing
    End Function

    ''' <summary>
    ''' Returns True if the specified channel is a major channel for this plugin.
    ''' </summary>
    ''' <param name="Connection">The connection to check.</param>
    ''' <param name="Channel">The channel to check.</param>
    Public Function IsMajorChannel(ByVal Connection As IRCConnection, ByVal Channel As String) As Boolean
        If Connection Is Nothing Then
            For Each lChannel In Channels
                If Channel = "*" And lChannel.Split("/"c)(0) = "!" & MyKey Then Return True
                If Channel.Contains(">") Then
                    If lChannel = "*" OrElse
                    System.Text.RegularExpressions.Regex.IsMatch(Channel, String.Format("(\*|!(\*|{0}))/(\*|{1})(/(\*|>\*|>{2}))?", System.Text.RegularExpressions.Regex.Escape(MyKey), System.Text.RegularExpressions.Regex.Escape(If(lChannel.Split({">"c}, 2)(0).Split({"/"c}, 3).ElementAtOrDefault(1), "")), System.Text.RegularExpressions.Regex.Escape(If(lChannel.Split({">"c}, 2).ElementAtOrDefault(1), ""))), RegularExpressions.RegexOptions.IgnoreCase) Then Return True
                Else
                    If lChannel = "*" OrElse
                    System.Text.RegularExpressions.Regex.IsMatch(lChannel, String.Format("(\*|!(\*|{0}))/(\*|{1})(/(\*|>\*))?", System.Text.RegularExpressions.Regex.Escape(If(Channel.Split({">"c}, 2)(0).Split({"/"c}, 3).ElementAtOrDefault(0), "").Substring(1)), System.Text.RegularExpressions.Regex.Escape(If(Channel.Split({">"c}, 2)(0).Split({"/"c}, 3).ElementAtOrDefault(1), ""))), RegularExpressions.RegexOptions.IgnoreCase) Then Return True
                End If
            Next
            Return False
        Else
            If Channel = "*" Then
                For Each lChannel In Channels
                    If lChannel = "*" Or lChannel.Split("/"c)(0) = "*" Or
                        lChannel.Split("/"c)(0).ToLower = Connection.Address.ToLower Then Return True
                Next
                Return False
            Else
                Return Channels.Contains(Connection.Address & "/" & Channel, System.StringComparer.OrdinalIgnoreCase) Or
                    Channels.Contains("*/" & Channel, System.StringComparer.OrdinalIgnoreCase) Or
                    Channels.Contains(Connection.Address & "/*", System.StringComparer.OrdinalIgnoreCase) Or
                    Channels.Contains("*/*") Or Channels.Contains("*")
            End If
        End If
    End Function
    Public Function IsMinorChannel(ByVal Connection As IRCConnection, ByVal Channel As String) As Boolean
        If IsMajorChannel(Connection, Channel) Then Return False
        If Connection Is Nothing Then
            For Each lChannel In MinorChannels
                If Channel = "*" And lChannel.Split("/"c)(0) = "!" & MyKey Then Return True
                If Channel.Contains(">") Then
                    If lChannel = "*" OrElse
                    System.Text.RegularExpressions.Regex.IsMatch(Channel, String.Format("(\*|!(\*|{0}))/(\*|{1})(/(\*|>\*|>{2}))?", System.Text.RegularExpressions.Regex.Escape(MyKey), System.Text.RegularExpressions.Regex.Escape(lChannel.Split({">"c}, 2)(0).Split({"/"c}, 3).ElementAtOrDefault(1)), System.Text.RegularExpressions.Regex.Escape(lChannel.Split({">"c}, 2).ElementAtOrDefault(1))), RegularExpressions.RegexOptions.IgnoreCase) Then Return True
                Else
                    If lChannel = "*" OrElse
                    System.Text.RegularExpressions.Regex.IsMatch(Channel, String.Format("(\*|!(\*|{0}))/(\*|{1})(/(\*|>\*))?", System.Text.RegularExpressions.Regex.Escape(MyKey), System.Text.RegularExpressions.Regex.Escape(lChannel.Split({">"c}, 2)(0).Split({"/"c}, 3).ElementAtOrDefault(1))), RegularExpressions.RegexOptions.IgnoreCase) Then Return True
                End If
            Next
            Return False
        Else
            If Channel = "*" Then
                For Each lChannel In MinorChannels
                    If lChannel.Split("/"c)(0).ToLower = Connection.Address.ToLower Then Return True
                Next
                Return False
            Else
                Return MinorChannels.Contains(Connection.Address & "/" & Channel, System.StringComparer.OrdinalIgnoreCase) Or
                    MinorChannels.Contains("*/" & Channel, System.StringComparer.OrdinalIgnoreCase) Or
                    MinorChannels.Contains(Connection.Address & "/*", System.StringComparer.OrdinalIgnoreCase) Or
                    MinorChannels.Contains("*/*") Or MinorChannels.Contains("*")
            End If
        End If
    End Function

    ' ''' <summary>
    ' ''' Checks whether the user with the given nickname has operator status in a given channel.
    ' ''' </summary>
    ' ''' <param name="Connection">When checking an IRC channel, the connection to check.</param>
    ' ''' <param name="Channel">
    ' ''' When checking an IRC channel, the channel name.
    ' ''' When checking a custom module-defined output, the module key and method name.
    ' ''' </param>
    ' ''' <param name="Nickname">The nickname of the user to check.</param>
    ' ''' <returns>True if the user has operator status; False otherwise.</returns>
    ' ''' <remarks>
    ' ''' This can be used to verify that a user has permission to perform a command.
    ' ''' </remarks>
    'Public Overridable Function IsOp(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal Nickname As String)
    '    Dim ID As Identification

    '    If Connection Is Nothing Then
    '        If Not Identifications.ContainsKey(Channel.Split("/"c)(0) & "/" & Nickname) Then Return False ' No ID, no ops.
    '        ID = Identifications(Channel.Split("/"c)(0) & "/" & Nickname)
    '        Return Accounts(ID.AccountName).OpChannels.Contains(Channel.Split("/"c)(0))
    '    Else
    '        If Identifications.ContainsKey(Connection.Address & "/" & Nickname) Then
    '            ID = Identifications(Connection.Address & "/" & Nickname)
    '            ' We'll still allow the user to use channel-operator commands if they have auto-op permissions but were deopped by another IRC user.
    '            If Accounts(ID.AccountName).OpChannels.Contains(Connection.Address & "/" & Channel) Or
    '               Accounts(ID.AccountName).OpChannels.Contains("*/" & Channel) Then Return True
    '        End If
    '    End If
    '    ' We'll still allow IRC users to give each other ops.
    '    If Not Connection.Channels.ContainsKey(Channel) Then Return False
    '    If Not Connection.Channels(Channel).Users.ContainsKey(Nickname) Then Return False
    '    Return Connection.Channels(Channel).Users(Nickname).ChannelAccess >= IRCConnection.ChannelAccessModes.Op
    'End Function

    Delegate Sub CommandDelegate(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)

    <System.AttributeUsage(System.AttributeTargets.Method)>
    Public Class CommandAttribute
        Inherits System.Attribute

        Public Names() As String
        Public Syntax As String
        Public Description As String
        Public MinArgumentCount As Short
        Public MaxArgumentCount As Short
        Public Permission As String
        Public NoPermissionsMessage As String
        Public Scope As CommandScope

        ''' <summary>
        ''' Specifies the context under which a command is valid.
        ''' </summary>
        ''' <remarks>Mote that multiple values can be numerically added together.</remarks>
        Enum CommandScope As Short
            ''' <summary>The command can be used in a channel message.</summary>
            Channel = 1
            ''' <summary>The command can be used in a private message.</summary>
            PM = 2
            ''' <summary>The command can be used in minor channels.</summary>
            MinorChannel = 4
            NoMinorChannel = 3
            NoPM = 5
            ''' <summary>The command can be used in any channel or PM. Other commands will take precedence over it.</summary>
            GlobalCommand = 8
        End Enum

        ' ''' <summary>Specifies the level of authority a user requires to use a command.</summary>
        ' ''' TODO: This will be replaced by proper permissions soon.
        'Enum CommandAccess As Short
        '    ''' <summary>No requirements: anyone whom the bot can hear may use the command.</summary>
        '    General = 0
        '    ''' <summary>Anyone who has identified with the bot may use the command.</summary>
        '    Identified = 1
        '    ''' <summary>Channel operators may use the command.</summary>
        '    ChannelOp = 2
        '    ''' <summary>Identified bot owners may use the command.</summary>
        '    Owner = 4
        'End Enum

        'Sub New(ByVal Name As String, ByVal MinArgumentCount As Short, ByVal MaxArgumentCount As Short, ByVal Syntax As String, ByVal Description As String, Optional ByVal Access As CommandAccess = CommandAccess.General, Optional ByVal Scope As CommandScope = CommandScope.Channel + CommandScope.PM)
        'MyClass.New({Name}, MinArgumentCount, MaxArgumentCount, Syntax, Description, Access, Scope)
        'End Sub
        Sub New(ByVal Name As String, ByVal MinArgumentCount As Short, ByVal MaxArgumentCount As Short, ByVal Syntax As String, ByVal Description As String, Optional ByVal Permission As String = Nothing, Optional ByVal Scope As CommandScope = CommandScope.Channel + CommandScope.PM, Optional ByVal NoPermissionsMessage As String = "You don't have access to that command.")
            MyClass.New({Name}, MinArgumentCount, MaxArgumentCount, Syntax, Description, Permission, Scope, NoPermissionsMessage)
        End Sub
        Sub New(ByVal Aliases() As String, ByVal MinArgumentCount As Short, ByVal MaxArgumentCount As Short, ByVal Syntax As String, ByVal Description As String, Optional ByVal Permission As String = Nothing, Optional ByVal Scope As CommandScope = CommandScope.Channel + CommandScope.PM, Optional ByVal NoPermissionsMessage As String = "You don't have access to that command.")
            Me.Names = Aliases
            Me.MinArgumentCount = MinArgumentCount
            Me.MaxArgumentCount = MaxArgumentCount
            Me.Syntax = Syntax
            Me.Description = Description
            Me.Permission = Permission
            Me.Scope = Scope
            Me.NoPermissionsMessage = NoPermissionsMessage
        End Sub
    End Class

    <System.AttributeUsage(System.AttributeTargets.Method)>
    Public Class RegexAttribute
        Inherits System.Attribute

        Public Expressions() As String
        'Public Access As CommandAccess
        Public Permission As String
        Public NoPermissionsMessage As String
        Public Scope As CommandScope
        Public MustUseNickname As Boolean

        ''' <summary>
        ''' Specifies the context under which a command is valid.
        ''' </summary>
        ''' <remarks>Mote that multiple values can be numerically added together.</remarks>
        Enum CommandScope As Short
            ''' <summary>The command can be used in a channel message.</summary>
            Channel = 1
            ''' <summary>The command can be used in a private message.</summary>
            PM = 2
        End Enum

        ' ''' <summary>Specifies the level of authority a user requires to use a command.</summary>
        'Enum CommandAccess As Short
        '    ''' <summary>No requirements: anyone whom the bot can hear may use the command.</summary>
        '    General = 0
        '    ''' <summary>Anyone who has identified with the bot may use the command.</summary>
        '    Identified = 1
        '    ''' <summary>Channel operators may use the command.</summary>
        '    ChannelOp = 2
        '    ''' <summary>Identified bot owners may use the command.</summary>
        '    Owner = 4
        'End Enum

        'Sub New(ByVal Name As String, ByVal MinArgumentCount As Short, ByVal MaxArgumentCount As Short, ByVal Syntax As String, ByVal Description As String, Optional ByVal Access As CommandAccess = CommandAccess.General, Optional ByVal Scope As CommandScope = CommandScope.Channel + CommandScope.PM)
        'MyClass.New({Name}, MinArgumentCount, MaxArgumentCount, Syntax, Description, Access, Scope)
        'End Sub
        Sub New(ByVal Expression As String, Optional ByVal Permission As String = Nothing, Optional ByVal Scope As CommandScope = CommandScope.Channel + CommandScope.PM, Optional ByVal MustUseNickname As Boolean = False, Optional ByVal NoPermissionsMessage As String = "You don't have access to that command.")
            MyClass.New({Expression}, Permission, Scope, MustUseNickname, NoPermissionsMessage)
        End Sub
        Sub New(ByVal Expressions() As String, Optional ByVal Permission As String = Nothing, Optional ByVal Scope As CommandScope = CommandScope.Channel + CommandScope.PM, Optional ByVal MustUseNickname As Boolean = False, Optional ByVal NoPermissionsMessage As String = "You don't have access to that command.")
            Me.Expressions = Expressions
            Me.Permission = Permission
            Me.Scope = Scope
            Me.MustUseNickname = MustUseNickname
            Me.NoPermissionsMessage = NoPermissionsMessage
        End Sub
    End Class

    <System.AttributeUsage(System.AttributeTargets.Method)>
    Public Class OutputAttribute
        Inherits System.Attribute

        Public MethodName As String
        Public Rank As IRCConnection.ChannelAccessModes

        'Sub New(ByVal Name As String, ByVal MinArgumentCount As Short, ByVal MaxArgumentCount As Short, ByVal Syntax As String, ByVal Description As String, Optional ByVal Access As CommandAccess = CommandAccess.General, Optional ByVal Scope As CommandScope = CommandScope.Channel + CommandScope.PM)
        'MyClass.New({Name}, MinArgumentCount, MaxArgumentCount, Syntax, Description, Access, Scope)
        'End Sub
        Sub New(ByVal MethodName As String)
            Me.MethodName = MethodName
            Me.Rank = ChannelAccessModes.Normal
        End Sub
        Sub New(ByVal MethodName As String, ByVal Rank As ChannelAccessModes)
            Me.MethodName = MethodName
            Me.Rank = Rank
        End Sub
    End Class

    ''' <summary>Checks for a command in a message received by the client, and runs the command if there is.</summary>
    ''' <param name="Connection">The IRC connection the message was received on, or Nothing to redirect to a custom method.</param>
    ''' <param name="Sender">The hostmask of the user sending the message.</param>
    ''' <param name="Channel">The channel or custom method to send responses on.</param>
    ''' <param name="InputLine">The message that was received.</param>
    ''' <returns>True if a command was found; False otherwise.</returns>
    ''' <remarks>The command will only run if the user has the necessary permission.</remarks>
    Public Function RunCommand(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal InputLine As String, Optional ByVal IsMinorChannelCommand As Boolean = False) As Boolean
        Dim Command = InputLine.Split(" "c)(0)
        For Each c In CommandPrefixes(Connection, Channel)
            If Command.StartsWith(c) Then _
                If Command = c Then Command = "" Else Command = Command.Substring(1)
        Next
        'If Not Commands.ContainsKey(Command.ToLower) Then Return False

        'Dim attrs() As System.Attribute = System.Attribute.GetCustomAttributes(Commands(Command.ToLower).Method)

        Dim Names() As String, MaxArgumentCount As Short, MinArgumentCount As Short, Description As String, Syntax As String, Permission As String, NoPermissionMessage As String, Scope As CommandAttribute.CommandScope
        Dim Method As Reflection.MethodInfo, attr As Object

        For Each Method In Me.GetType.GetMethods
            For Each attr In Method.GetCustomAttributes(False)
                If TypeOf attr Is CommandAttribute Then
                    Names = CType(attr, CommandAttribute).Names
                    If Names.Contains(Command, StringComparer.OrdinalIgnoreCase) Then GoTo invoke
                End If
            Next
        Next
        Return False

Invoke:
        MaxArgumentCount = CType(attr, CommandAttribute).MaxArgumentCount
        MinArgumentCount = CType(attr, CommandAttribute).MinArgumentCount
        Syntax = CType(attr, CommandAttribute).Syntax
        Description = CType(attr, CommandAttribute).Description
        Permission = CType(attr, CommandAttribute).Permission
        If If(Permission, "").StartsWith(".") Then Permission = MyKey & Permission
        NoPermissionMessage = CType(attr, CommandAttribute).NoPermissionsMessage
        Scope = CType(attr, CommandAttribute).Scope

        If (((Scope And Plugin.CommandAttribute.CommandScope.PM) = 0 And Not (Connection Is Nothing Or Channel.Contains("#"))) Or
            ((Scope And Plugin.CommandAttribute.CommandScope.Channel) = 0 And (Connection Is Nothing Or Channel.Contains("#")))) And
        Not IsMinorChannelCommand Then
            Return False
        End If

        'If (Access And bModule.CommandAttribute.CommandAccess.Identified) > 0 AndAlso Not VBot.Identifications.ContainsKey(Sender.Split("!"c)(0)) Then
        '    Say(Connection, Channel, Choose("Sorry " & Sender.Split("!"c)(0) & ", ", "") & Choose("that command is only available to identified users.", Choose("you are ", "you're ") & "not identified.", "only identified users may use that command."), SayOptions.Capitalise)
        '    Return True
        'ElseIf (Access And bModule.CommandAttribute.CommandAccess.ChannelOp) > 0 AndAlso Not IsOp(Connection, Channel, Sender.Split("!"c)(0)) Then
        '    Say(Connection, Channel, Choose("Sorry " & Sender.Split("!"c)(0) & ", ", "") & Choose("that command is only available to operators.", Choose("you are ", "you're ") & "not an operator.", "only operators may use that command."), SayOptions.Capitalise)
        '    Return True
        'ElseIf (Access And bModule.CommandAttribute.CommandAccess.Owner) > 0 AndAlso Not VBot.IsOwner(Connection, Channel, Sender.Split("!"c)(0)) Then
        '    Say(Connection, Channel, Choose("Sorry " & Sender.Split("!"c)(0) & ", ", "") & Choose("that command is only available to bot owners.", Choose("you are ", "you're ") & "not a bot owner.", "only bot owners may use that command."), SayOptions.Capitalise)
        '    Return True
        'End If
        If Permission IsNot Nothing AndAlso Not UserHasPermission(Connection, Channel, Sender, Permission) Then
            If NoPermissionMessage <> Nothing Then Reply(Connection, Channel, Sender, NoPermissionMessage)
            Return True
        End If

        Dim fields = InputLine.Split({" "c}, MaxArgumentCount + 1)

        If fields.Count - 1 < MinArgumentCount Then
            Reply(Connection, Channel, Sender, Choose("That's not how you use that command."))
            Reply(Connection, Channel, Sender, "The correct command syntax is $k12" & Syntax)
            Return True
        End If

        If fields.Count = 1 Then
            Try
                Method.Invoke(Me, {Connection, Sender, Channel, New String() {}})
            Catch ex As Exception
                VBot.LogError(MyKey, Method.Name, ex)
            End Try
            Return True
        Else
            Dim args = fields.Skip(1).ToArray
            Try
                Method.Invoke(Me, {Connection, Sender, Channel, args})
            Catch ex As Exception
                VBot.LogError(MyKey, Method.Name, ex)
            End Try
            Return True
        End If
    End Function

    ''' <summary>Checks for a regular expression in a message received by the client, and runs a command if there is.</summary>
    ''' <param name="Connection">The IRC connection the message was received on, or Nothing to redirect to a custom method.</param>
    ''' <param name="Sender">The hostmask of the user sending the message.</param>
    ''' <param name="Channel">The channel or custom method to send responses on.</param>
    ''' <param name="InputLine">The message that was received.</param>
    ''' <returns>True if a command was found; False otherwise.</returns>
    ''' <remarks>The command will only run if the user has the necessary permission.</remarks>
    Public Function RunRegex(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal InputLine As String, ByVal UsedMyNickname As Boolean) As Boolean
        Dim Expressions() As String, Permission As String, NoPermissionMessage As String, Scope As CommandAttribute.CommandScope
        Dim Method As Reflection.MethodInfo, attr As Object, Match As System.Text.RegularExpressions.Match

        RunRegex = False

        For Each Method In Me.GetType.GetMethods
            For Each attr In Method.GetCustomAttributes(False)
                If TypeOf attr Is RegexAttribute Then
                    Expressions = CType(attr, RegexAttribute).Expressions

                    For Each Expression In Expressions
                        Match = System.Text.RegularExpressions.Regex.Match(InputLine, Expression, RegularExpressions.RegexOptions.IgnoreCase)
                        If Match.Success And (UsedMyNickname Or Not CType(attr, RegexAttribute).MustUseNickname) Then
                            Scope = CType(attr, RegexAttribute).Scope
                            If ((Scope And Plugin.CommandAttribute.CommandScope.PM) > 0 And Not Channel.Contains("#")) Or
    ((Scope And Plugin.CommandAttribute.CommandScope.Channel) > 0 And Channel.Contains("#")) Then

                                'Access = CType(attr, RegexAttribute).Access
                                Permission = CType(attr, RegexAttribute).Permission
                                If If(Permission, "").StartsWith(".") Then Permission = MyKey & Permission
                                NoPermissionMessage = CType(attr, RegexAttribute).NoPermissionsMessage

                                'If (Access And bModule.CommandAttribute.CommandAccess.Identified) > 0 AndAlso Not VBot.Identifications.ContainsKey(Sender.Split("!"c)(0)) Then
                                '    Say(Connection, Channel, Choose("Sorry " & Sender.Split("!"c)(0) & ", ", "") & Choose("that command is only available to identified users.", Choose("you are ", "you're ") & "not identified.", "only identified users may use that command."), True)
                                '    RunRegex = True
                                'ElseIf (Access And bModule.CommandAttribute.CommandAccess.ChannelOp) > 0 AndAlso Not IsOp(Connection, Channel, Sender.Split("!"c)(0)) Then
                                '    Say(Connection, Channel, Choose("Sorry " & Sender.Split("!"c)(0) & ", ", "") & Choose("that command is only available to operators.", Choose("you are ", "you're ") & "not an operator.", "only operators may use that command."), True)
                                '    RunRegex = True
                                'ElseIf (Access And bModule.CommandAttribute.CommandAccess.Owner) > 0 AndAlso Not VBot.IsOwner(Connection, Channel, Sender.Split("!"c)(0)) Then
                                '    Say(Connection, Channel, Choose("Sorry " & Sender.Split("!"c)(0) & ", ", "") & Choose("that command is only available to bot owners.", Choose("you are ", "you're ") & "not a bot owner.", "only bot owners may use that command."), True)
                                '    RunRegex = True
                                'End If
                                If Permission IsNot Nothing AndAlso Not UserHasPermission(Connection, Channel, Sender, Permission) Then
                                    If NoPermissionMessage <> Nothing Then Reply(Connection, Channel, Sender, NoPermissionMessage)
                                    RunRegex = True
                                Else

                                    ' We will support the following argument patterns:
                                    ' Connection As IRCConnection, Sender As String, Channel As String, Match As Regex.Ratch
                                    ' Connection As IRCConnection, Sender As String, Channel As String, Match As Regex.Ratch, Handled As Boolean

                                    Dim Args = Method.GetParameters
                                    If Args.Count = 4 Then
                                        Try
                                            Method.Invoke(Me, {Connection, Sender, Channel, Match})
                                        Catch ex As Exception
                                            VBot.LogError(MyKey, Method.Name, ex)
                                        End Try
                                        RunRegex = True
                                    ElseIf Args.Count = 5 Then
                                        Dim Handled As Boolean
                                        Try
                                            Method.Invoke(Me, {Connection, Sender, Channel, Match, Handled})
                                        Catch ex As Exception
                                            VBot.LogError(MyKey, Method.Name, ex)
                                        End Try
                                        RunRegex = RunRegex Or Handled
                                    End If
                                End If
                            End If
                        End If
                        If RunRegex Then Return True
                    Next
                End If
            Next
        Next
    End Function

    ''' <summary>
    ''' Returns the key of the current module in IRCBot.Modules, or an empty string if it cannot be found.
    ''' </summary>
    ''' <returns>If the current module can be found in IRCBot.Modules, its key; Nothing otherwise.</returns>
    Public ReadOnly Property MyKey As String
        Get
            For Each m In VBot.Plugins
                If m.Value.Obj Is Me Then
                    Return m.Key
                End If
            Next
            Return Nothing
        End Get
    End Property

    ''' <summary>
    ''' Sends a message to a custom output method.
    ''' </summary>
    ''' <param name="MethodName">The name of the method.</param>
    ''' <param name="Message">The message to send.</param>
    ''' <param name="Arguments">Any data that the custom method requires, including a target nickname if applicable.</param>
    ''' <param name="Exclude">Any channels or methods that the message should not be sent to.</param>
    Public Overridable Sub RunOutput(ByVal MethodName As String, ByVal Message As String, ByVal Arguments As String, ByVal MinimumRank As IRCConnection.ChannelAccessModes, Optional ByVal Exclude As String() = Nothing)
        Dim Name As String
        Dim Method As Reflection.MethodInfo, attr As Object

        For Each Method In Me.GetType.GetMethods
            For Each attr In Method.GetCustomAttributes(False)
                If TypeOf attr Is OutputAttribute Then
                    Name = CType(attr, OutputAttribute).MethodName
                    If (Name = MethodName Or MethodName = "*") And Not If(Exclude, {}).Contains("!" & MyKey & "/" & Name) And (CType(attr, OutputAttribute).Rank >= MinimumRank) Then
                        Try
                            Method.Invoke(Me, {Message, Arguments})
                        Catch ex As Exception
                            VBot.LogError(MyKey, Method.Name, ex)
                        End Try
                    End If
                End If
            Next
        Next
        Return
    End Sub

    ''' <summary>
    ''' When overridden in a derived class, responds to input to the VBot console window.
    ''' </summary>
    ''' <param name="Command">The command that was input.</param>
    Public Overridable Sub OnConsoleInput(ByVal Command As String)
        If IsMajorChannel(Nothing, "!MainCommands/Console/") Then _
            OnChannelMessage(Nothing, "user!User@Console", "!MainCommands/Console/", Command)
    End Sub

    Public Enum SayOptions As Short
        ''' <summary>Only operators will hear the message. On some IRCds, the bot must have ops to do this.</summary>
        OpsOnly = 9
        ''' <summary>The frst letter of the message will be capitalised automatically.</summary>
        Capitalise = 2
        ''' <summary>The message will be sent as a NOTICE, even if it's to a channel.</summary>
        NoticeAlways = 8
        ''' <summary>The message will be sent as a PRIVMSG, even if it's to a user.</summary>
        NoticeNever = 4
        ''' <summary>The message will be sent to minor channels too.</summary>
        MinorChannels = 16
        ''' <summary>Don't parse \Kxx,xx \B \U \O \R codes.</summary>
        NoParse = 32
    End Enum

    ''' <summary>
    ''' Sends a message to a channel.
    ''' </summary>
    ''' <param name="Connection">The IRC connection to send to, or Nothing to send to a custom method.</param>
    ''' <param name="Channel">The channel or custom method to send to.</param>
    ''' <param name="Message">The message to send.</param>
    ''' <param name="Options">Options for sending the message.</param>
    Public Sub Say(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal Message As String, Optional ByVal Options As SayOptions = 0)
        If Message Is Nothing Then Return
        If (Options And SayOptions.Capitalise) And Message.Length > 0 Then Mid(Message, 1, 1) = Char.ToUpper(Message(0))

        If (Options And SayOptions.NoParse) = 0 Then
            ' Parse control codes.
            Dim Pos As Integer = -1
            Do While Pos < Message.Length - 1
                Pos = Message.IndexOf("$"c, Pos + 1)
                If Pos < 0 Then Exit Do
                If Pos >= Message.Length - 1 Then Exit Do

                Select Case Char.ToLower(Message(Pos + 1))
                    Case "a"c ' CTCP code (\x01)
                        Message = Message.Remove(Pos, 2).Insert(Pos, Chr(1))
                    Case "c"c ' Primary command prefix
                        Message = Message.Remove(Pos, 2).Insert(Pos, CommandPrefixes(Connection, Channel).ElementAtOrDefault(0))
                    Case "k"c ' Colour code (\x03)
                        Message = Message.Remove(Pos, 2).Insert(Pos, Chr(3))
                    Case "b"c ' Boldface (\x02)
                        Message = Message.Remove(Pos, 2).Insert(Pos, Chr(2))
                    Case "u"c ' Underline (\x1F)
                        Message = Message.Remove(Pos, 2).Insert(Pos, Chr(31))
                    Case "r"c ' Reverse (\x16)
                        Message = Message.Remove(Pos, 2).Insert(Pos, Chr(22))
                    Case "n"c ' New line (CR+LF)
                        Message = Message.Remove(Pos, 2).Insert(Pos, vbCrLf)
                    Case "o"c ' Reset formatting (\x0F)
                        Message = Message.Remove(Pos, 2).Insert(Pos, Chr(15))
                    Case "/"c ' Slash (/)
                        Message = Message.Remove(Pos, 2).Insert(Pos, "/")
                End Select
            Loop
        End If

        If Channel.StartsWith("!") Then
            'If (Options And SayOptions.OpsOnly) Then Return
            If Channel.Substring(1).Split({"/"c}, 3).Length = 3 Then
                VBot.Plugins(Channel.Substring(1).Split({"/"c}, 3)(0)).Obj.RunOutput(Channel.Substring(1).Split({"/"c}, 3)(1), Message, Channel.Substring(1).Split({"/"c}, 3)(2), If((Options And SayOptions.OpsOnly), 8, 0))
            Else
                VBot.Plugins(Channel.Substring(1).Split({"/"c}, 3)(0)).Obj.RunOutput(Channel.Substring(1).Split({"/"c}, 3)(1), Message, Nothing, If((Options And SayOptions.OpsOnly), 8, 0))
            End If
            Return
        End If

        If Connection Is Nothing And Channel = "*" Then
            SayToAllChannels(Message, Options)
            Return
        End If

        Dim sChannels As String = ""
        For Each Channel In Channel.Split(","c)
            If Channel.StartsWith("#") Then
                sChannels &= "," & If(Options And 1, "@", "") & Channel
            ElseIf Channel.StartsWith("@#") Then
                sChannels &= "," & Channel
            ElseIf Channel.Length >= 2 AndAlso Channel(1) = "#" Then
                sChannels &= "," & If(Options And 1, "@", "") & Channel
            Else
                sChannels &= "," & Channel
            End If
        Next
        sChannels = sChannels.TrimStart(","c)

        For Each Line In Message.Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries)
            If (Options And SayOptions.NoticeAlways) Or (Not Channel.Contains("#") And (Options And SayOptions.NoticeNever) = 0) Then
                Connection.Send("NOTICE " & sChannels & " :" & Line)
            Else
                Connection.Send("PRIVMSG " & sChannels & " :" & Line)
            End If
            OnChannelMessageSend(Me, Connection, Channel, Message, Options)
        Next
    End Sub

    ''' <summary>
    ''' Sends a private message to a user.
    ''' </summary>
    ''' <param name="Connection">The IRC connection to send to, or Nothing to send to a custom method.</param>
    ''' <param name="Sender">The hostmask of the user who sent the message being replied to.</param>
    ''' <param name="Channel">The channel or custom method to send to.</param>
    ''' <param name="Message">The message to send.</param>
    ''' <param name="Options">Options for sending the message.</param>
    Public Sub Reply(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal Sender As String, ByVal Message As String, Optional ByVal Options As SayOptions = 0)
        If Connection IsNot Nothing Then
            Say(Connection, Sender.Split("!"c)(0), Message, Options)
        Else
            Say(Nothing, Channel.Split({"/"c}, 3)(0) & "/" & Channel.Split({"/"c}, 3)(1) & "/" & Channel.Split({">"c})(0).Split({"/"c}, 3)(2) & ">" & Sender.Split("!"c)(0), Message, Options)
        End If
    End Sub

    ''' <summary>
    ''' Sends a message to all channels that this module is concerned with.
    ''' </summary>
    ''' <param name="Message">The message to send.</param>
    ''' <param name="Options">Options for sending the message.</param>
    Public Sub SayToAllChannels(ByVal Message As String, Optional ByVal Options As SayOptions = 0, Optional ByVal Exclude() As String = Nothing)
        If Channels Is Nothing Or Message Is Nothing Then Return
        If (Options And SayOptions.NoParse) = 0 Then
            ' Parse control codes.
            Dim Pos As Integer = -1
            Do While Pos < Message.Length - 1
                Pos = Message.IndexOf("$"c, Pos + 1)
                If Pos < 0 Then Exit Do
                If Pos >= Message.Length - 1 Then Exit Do

                Select Case Char.ToLower(Message(Pos + 1))
                    Case "a"c ' CTCP code (\x01)
                        Message = Message.Remove(Pos, 2).Insert(Pos, Chr(1))
                    Case "c"c ' Primary command prefix
                        Message = Message.Remove(Pos, 2).Insert(Pos, DefaultCommandPrefixes.ElementAtOrDefault(0))
                    Case "k"c ' Colour code (\x03)
                        Message = Message.Remove(Pos, 2).Insert(Pos, Chr(3))
                    Case "b"c ' Boldface (\x02)
                        Message = Message.Remove(Pos, 2).Insert(Pos, Chr(2))
                    Case "u"c ' Underline (\x1F)
                        Message = Message.Remove(Pos, 2).Insert(Pos, Chr(31))
                    Case "r"c ' Reverse (\x16)
                        Message = Message.Remove(Pos, 2).Insert(Pos, Chr(22))
                    Case "n"c ' New line (CR+LF)
                        Message = Message.Remove(Pos, 2).Insert(Pos, vbCrLf)
                    Case "o"c ' Reset formatting (\x0F)
                        Message = Message.Remove(Pos, 2).Insert(Pos, Chr(15))
                    Case "/"c ' Slash (/)
                        Message = Message.Remove(Pos, 2).Insert(Pos, "/")
                End Select
            Loop
        End If

        For Each Channel In Channels
            If Channel.StartsWith("!") Then
                'If (Options And 1) Then Continue For
                If Channel = "!*" Then
                    For Each m In VBot.Plugins.Values
                        m.Obj.RunOutput("*", Message, Nothing, If((Options And SayOptions.OpsOnly), 8, 0), Exclude)
                    Next
                ElseIf Channel.StartsWith("!*") Then
                    For Each m In VBot.Plugins.Values
                        m.Obj.RunOutput(Channel.Substring(1).Split({"/"c}, 2)(1), Message, Nothing, If((Options And SayOptions.OpsOnly), 8, 0), Exclude)
                    Next
                Else
                    VBot.Plugins(Channel.Substring(1).Split({"/"c}, 2)(0)).Obj.RunOutput(Channel.Substring(1).Split({"/"c}, 2)(1), Message, Nothing, If((Options And SayOptions.OpsOnly), 8, 0), Exclude)
                End If
            Else
                If Channel = "*" Then
                    For Each m In VBot.Plugins.Values
                        m.Obj.RunOutput("*", Message, Nothing, If((Options And SayOptions.OpsOnly), 8, 0), Exclude)
                    Next
                End If
                For Each Connection In VBot.Connections
                    If Channel = "*" OrElse (Connection.Address = Channel.Split({"/"c}, 2)(0) Or Channel.Split({"/"c}, 2)(0) = "*") Then
                        If Channel = "*" OrElse Channel.Split({"/"c}, 2)(1) = "*" Then
                            For Each eChannel In Connection.Channels.Values
                                If Not If(Exclude, {}).Contains(Connection.Address & "/" & eChannel.Name) Then _
                                    Say(Connection, eChannel.Name, Message, Options)
                            Next
                        Else
                            If Not If(Exclude, {}).Contains(Connection.Address & "/" & Channel) Then _
                        Say(Connection, Channel.Split({"/"c}, 2)(1), Message, Options Or SayOptions.NoParse)
                        End If
                    End If
                Next
            End If
        Next
    End Sub

    Public Sub SayToMinorChannels(ByVal Message As String, Optional ByVal Options As SayOptions = 0, Optional ByVal Exclude As String() = Nothing)
        If MinorChannels Is Nothing Then Return
        If (Options And SayOptions.NoParse) = 0 Then
            ' Parse control codes.
            Dim Pos As Integer = -1
            Do While Pos < Message.Length - 1
                Pos = Message.IndexOf("$"c, Pos + 1)
                If Pos < 0 Then Exit Do
                If Pos >= Message.Length - 1 Then Exit Do

                Select Case Char.ToLower(Message(Pos + 1))
                    Case "a"c ' CTCP code (\x01)
                        Message = Message.Remove(Pos, 2).Insert(Pos, Chr(1))
                    Case "c"c ' Primary command prefix
                        Message = Message.Remove(Pos, 2).Insert(Pos, DefaultCommandPrefixes.ElementAtOrDefault(0))
                    Case "k"c ' Colour code (\x03)
                        Message = Message.Remove(Pos, 2).Insert(Pos, Chr(3))
                    Case "b"c ' Boldface (\x02)
                        Message = Message.Remove(Pos, 2).Insert(Pos, Chr(2))
                    Case "u"c ' Underline (\x1F)
                        Message = Message.Remove(Pos, 2).Insert(Pos, Chr(31))
                    Case "r"c ' Reverse (\x16)
                        Message = Message.Remove(Pos, 2).Insert(Pos, Chr(22))
                    Case "n"c ' New line (CR+LF)
                        Message = Message.Remove(Pos, 2).Insert(Pos, vbCrLf)
                    Case "o"c ' Reset formatting (\x0F)
                        Message = Message.Remove(Pos, 2).Insert(Pos, Chr(15))
                    Case "/"c ' Slash (/)
                        Message = Message.Remove(Pos, 2).Insert(Pos, "/")
                End Select
            Loop
        End If

        For Each Channel In MinorChannels
            If Channel.StartsWith("!") Then
                If Channel = "!*" Then
                    For Each m In VBot.Plugins.Values
                        m.Obj.RunOutput("*", Message, Nothing, If((Options And SayOptions.OpsOnly), 8, 0), Exclude)
                    Next
                ElseIf Channel.StartsWith("!*") Then
                    For Each m In VBot.Plugins.Values
                        m.Obj.RunOutput(Channel.Substring(1).Split({"/"c}, 2)(1), Message, Nothing, If((Options And SayOptions.OpsOnly), 8, 0), Exclude)
                    Next
                Else
                    Plugins(Channel.Substring(1).Split({"/"c}, 2)(0)).Obj.RunOutput(Channel.Substring(1).Split({"/"c}, 2)(1), Message, Nothing, If((Options And SayOptions.OpsOnly), 8, 0), Exclude)
                End If
            Else
                If Channel = "*" Or Channel = "*/*" Then
                    For Each m In VBot.Plugins.Values
                        m.Obj.RunOutput(Channel.Substring(1).Split({"/"c}, 2)(1), Message, Nothing, If((Options And SayOptions.OpsOnly), 8, 0), Exclude)
                    Next
                End If
                For Each Connection In VBot.Connections
                    If Channel = "*" OrElse (Connection.Address = Channel.Split({"/"c}, 2)(0) Or Channel.Split({"/"c}, 2)(0) = "*") Then
                        If Channel = "*" OrElse Channel.Split({"/"c}, 2)(1) = "*" Then
                            For Each eChannel In Connection.Channels.Values
                                If Not If(Exclude, {}).Contains(Connection.Address & "/" & eChannel.Name) Then _
                                    Say(Connection, eChannel.Name, Message, Options)
                            Next
                        Else
                            If Not If(Exclude, {}).Contains(Connection.Address & "/" & Channel) Then _
                        Say(Connection, Channel.Split({"/"c}, 2)(1), Message, Options Or SayOptions.NoParse)
                        End If
                    End If
                Next
            End If
        Next
    End Sub

    ''' <summary>
    ''' When overridden in a derived class, responds to any plugin sending a message to a channel.
    ''' </summary>
    Public Overridable Sub OnChannelMessageSend(ByVal sender As Plugin, ByVal Connection As IRCConnection, ByVal Channel As String, ByVal Message As String, ByVal Options As SayOptions)

    End Sub

    ''' <summary>
    ''' When overridden in a derived class, saves data about the module's current state.
    ''' </summary>
    Public Overridable Sub OnSave()
    End Sub

    Public Overridable Sub OnUnload()
        OnSave()
    End Sub

    Public Sub LogError(ByVal Procedure As String, ByVal ex As Exception)
        VBot.LogError(MyKey, Procedure, ex)
    End Sub

    <Obsolete("Please use proper events instead. You can use reflection to find them if need be.")>
    Public Sub EnableEvents(ByVal TargetPluginKey As String, ByVal ParamArray Events As String())
        For Each Plugin In VBot.Plugins
            If Plugin.Value.Obj Is Me Then
                If Plugin.Value.EventsEnabled Is Nothing Then Plugin.Value.EventsEnabled = New List(Of String)
                For Each lEvent In Events
                    Plugin.Value.EventsEnabled.Add(TargetPluginKey & "." & lEvent)
                Next
                'Plugin.Value.EventsEnabled.Add(TargetPluginKey & "/" & String.Join(",", Events))
            End If
        Next
    End Sub

    <Obsolete("Please use proper events instead. You can use reflection to find them if need be.")>
    Public Function CallEvent(ByVal Key As String, ByVal EventName As String, ByVal Args(,) As Object)
        Dim Handled As Boolean
        For Each Plugin In VBot.Plugins
            If Plugin.Value.EventsEnabled Is Nothing Then Continue For
            For Each Entry In Plugin.Value.EventsEnabled
                If Key & "." & EventName Like Entry Then
                    Plugin.Value.Obj.OnPluginEvent(EventName, Args, Handled)
                    If Handled Then Return True
                    Exit For
                End If
            Next
        Next
        Return False
    End Function

    Public Overridable Sub OnAwayCancelled(ByVal Connection As IRCConnection, ByVal Message As String)
    End Sub
    Public Overridable Sub OnAway(ByVal Connection As IRCConnection, ByVal Message As String)
    End Sub
    Public Overridable Sub OnBanList(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal BannedUser As String, ByVal BanningUser As String, ByVal Time As Date)
    End Sub
    Public Overridable Sub OnBanListEnd(ByVal Connection As IRCConnection, ByVal Message As String)
    End Sub
    Public Overridable Sub OnNicknameChange(ByVal sender As IRCConnection, ByVal User As IRCConnection.IRCUser, ByVal NewNick As String)
    End Sub
    Public Overridable Sub OnNicknameChangeSelf(ByVal sender As IRCConnection, ByVal User As IRCConnection.IRCUser, ByVal NewNick As String)
    End Sub
    Public Overridable Sub OnChannelAction(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Message As String)
        RunRegex(Connection, Sender, Channel, Chr(1) & "ACTION " & Message & Chr(1), False)
    End Sub
    Public Overridable Sub OnChannelActionHighlight(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Message As String)
        RunRegex(Connection, Sender, Channel, Chr(1) & "ACTION " & Message & Chr(1), False)
    End Sub
    Public Overridable Sub OnChannelAdmin(ByVal Connection As IRCConnection, ByVal Sender As IRCUser, ByVal Channel As String, ByVal Target As String)
    End Sub
    Public Overridable Sub OnChannelAdminSelf(ByVal Connection As IRCConnection, ByVal Sender As IRCUser, ByVal Channel As String, ByVal Target As String)
    End Sub
    Public Overridable Sub OnChannelBan(ByVal Connection As IRCConnection, ByVal Sender As IRCUser, ByVal Channel As String, ByVal Target As String, ByVal MatchedUsers As String())
    End Sub
    Public Overridable Sub OnChannelBanSelf(ByVal Connection As IRCConnection, ByVal Sender As IRCUser, ByVal Channel As String, ByVal Target As String, ByVal MatchedUsers As String())
    End Sub
    Public Overridable Sub OnChannelTimestamp(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal Timestamp As Date)
    End Sub
    Public Overridable Sub OnChannelCTCP(ByVal Connection As IRCConnection, ByVal Sender As IRCUser, ByVal Channel As String, ByVal Message As String)
    End Sub
    Public Overridable Sub OnChannelDeAdmin(ByVal Connection As IRCConnection, ByVal Sender As IRCUser, ByVal Channel As String, ByVal Target As String)
    End Sub
    Public Overridable Sub OnChannelDeAdminSelf(ByVal Connection As IRCConnection, ByVal Sender As IRCUser, ByVal Channel As String, ByVal Target As String)
    End Sub
    Public Overridable Sub OnChannelDeHalfOp(ByVal Connection As IRCConnection, ByVal Sender As IRCUser, ByVal Channel As String, ByVal Target As String)
    End Sub
    Public Overridable Sub OnChannelDeHalfOpSelf(ByVal Connection As IRCConnection, ByVal Sender As IRCUser, ByVal Channel As String, ByVal Target As String)
    End Sub
    Public Overridable Sub OnChannelDeHalfVoice(ByVal Connection As IRCConnection, ByVal Sender As IRCUser, ByVal Channel As String, ByVal Target As String)
    End Sub
    Public Overridable Sub OnChannelDeHalfVoiceSelf(ByVal Connection As IRCConnection, ByVal Sender As IRCUser, ByVal Channel As String, ByVal Target As String)
    End Sub
    Public Overridable Sub OnChannelDeOp(ByVal Connection As IRCConnection, ByVal Sender As IRCUser, ByVal Channel As String, ByVal Target As String)
    End Sub
    Public Overridable Sub OnChannelDeOpSelf(ByVal Connection As IRCConnection, ByVal Sender As IRCUser, ByVal Channel As String, ByVal Target As String)
    End Sub
    Public Overridable Sub OnChannelDeOwner(ByVal Connection As IRCConnection, ByVal Sender As IRCUser, ByVal Channel As String, ByVal Target As String)
    End Sub
    Public Overridable Sub OnChannelDeOwnerSelf(ByVal Connection As IRCConnection, ByVal Sender As IRCUser, ByVal Channel As String, ByVal Target As String)
    End Sub
    Public Overridable Sub OnChannelDeVoice(ByVal Connection As IRCConnection, ByVal Sender As IRCUser, ByVal Channel As String, ByVal Target As String)
    End Sub
    Public Overridable Sub OnChannelDeVoiceSelf(ByVal Connection As IRCConnection, ByVal Sender As IRCUser, ByVal Channel As String, ByVal Target As String)
    End Sub
    Public Overridable Sub OnChannelExempt(ByVal Connection As IRCConnection, ByVal Sender As IRCUser, ByVal Channel As String, ByVal Target As String, ByVal MatchedUsers As String())
    End Sub
    Public Overridable Sub OnChannelExemptSelf(ByVal Connection As IRCConnection, ByVal Sender As IRCUser, ByVal Channel As String, ByVal Target As String, ByVal MatchedUsers As String())
    End Sub
    Public Overridable Sub OnChannelExit(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Reason As String)
    End Sub
    Public Overridable Sub OnChannelExitSelf(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Reason As String)
    End Sub
    Public Overridable Sub OnChannelHalfOp(ByVal Connection As IRCConnection, ByVal Sender As IRCUser, ByVal Channel As String, ByVal Target As String)
    End Sub
    Public Overridable Sub OnChannelHalfOpSelf(ByVal Connection As IRCConnection, ByVal Sender As IRCUser, ByVal Channel As String, ByVal Target As String)
    End Sub
    Public Overridable Sub OnChannelHalfVoice(ByVal Connection As IRCConnection, ByVal Sender As IRCUser, ByVal Channel As String, ByVal Target As String)
    End Sub
    Public Overridable Sub OnChannelHalfVoiceSelf(ByVal Connection As IRCConnection, ByVal Sender As IRCUser, ByVal Channel As String, ByVal Target As String)
    End Sub
    Public Overridable Sub OnChannelInviteExempt(ByVal Connection As IRCConnection, ByVal Sender As IRCUser, ByVal Channel As String, ByVal Target As String, ByVal MatchedUsers As String())
    End Sub
    Public Overridable Sub OnChannelInviteExemptSelf(ByVal Connection As IRCConnection, ByVal Sender As IRCUser, ByVal Channel As String, ByVal Target As String, ByVal MatchedUsers As String())
    End Sub
    Public Overridable Sub OnChannelJoin(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String)
    End Sub
    Public Overridable Sub OnChannelJoinSelf(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String)
    End Sub
    Public Overridable Sub OnChannelJoinDeniedBanned(ByVal Connection As IRCConnection, ByVal Channel As String)
    End Sub
    Public Overridable Sub OnChannelJoinDeniedFull(ByVal Connection As IRCConnection, ByVal Channel As String)
    End Sub
    Public Overridable Sub OnChannelJoinDeniedInvite(ByVal Connection As IRCConnection, ByVal Channel As String)
    End Sub
    Public Overridable Sub OnChannelJoinDeniedKey(ByVal Connection As IRCConnection, ByVal Channel As String)
    End Sub
    Public Overridable Sub OnChannelKick(ByVal Connection As IRCConnection, ByVal Sender As IRCUser, ByVal Channel As String, ByVal Target As String, ByVal Reason As String)
    End Sub
    Public Overridable Sub OnChannelKickSelf(ByVal Connection As IRCConnection, ByVal Sender As IRCUser, ByVal Channel As String, ByVal Target As String, ByVal Reason As String)
    End Sub
    Public Overridable Sub OnChannelList(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal Users As Integer, ByVal Topic As String)
    End Sub
    Public Overridable Sub OnChannelMessage(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Message As String)
        Dim UsedMyNickname As Boolean
        If Message.ToLower.StartsWith(Nickname(Connection).ToLower & " ") Then
            Message = Message.Substring(Nickname(Connection).Length + 1)
            UsedMyNickname = True
        ElseIf Message.ToLower.StartsWith(Nickname(Connection).ToLower & ", ") Or
                Message.ToLower.StartsWith(Nickname(Connection).ToLower & ": ") Or
                Message.ToLower.StartsWith(Nickname(Connection).ToLower & "- ") Or
                Message.ToLower.StartsWith(Nickname(Connection).ToLower & ". ") Then
            Message = Message.Substring(Nickname(Connection).Length + 2)
            UsedMyNickname = True
        End If
        If Message.Length = 0 Then Return

        Dim Handled As Boolean
        If CommandPrefixes(Connection, Channel).Contains(Message(0)) Then _
            Handled = RunCommand(Connection, Sender, Channel, Message)
        If Handled Then Exit Sub
        Handled = RunRegex(Connection, Sender, Channel, Message, UsedMyNickname)
    End Sub
    Public Overridable Sub OnChannelMessageSendDenied(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal Message As String, Optional ByRef Handled As Boolean = False)
    End Sub
    Public Overridable Sub OnChannelMessageHighlight(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Message As String)
    End Sub
    Public Overridable Sub OnChannelMode(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Direction As Boolean, ByVal Mode As String)
    End Sub
    Public Overridable Sub OnChannelModesGet(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal Modes As String)
    End Sub
    Public Overridable Sub OnChannelOp(ByVal Connection As IRCConnection, ByVal Sender As IRCUser, ByVal Channel As String, ByVal Target As String)
    End Sub
    Public Overridable Sub OnChannelOpSelf(ByVal Connection As IRCConnection, ByVal Sender As IRCUser, ByVal Channel As String, ByVal Target As String)
    End Sub
    Public Overridable Sub OnChannelOwner(ByVal Connection As IRCConnection, ByVal Sender As IRCUser, ByVal Channel As String, ByVal Target As String)
    End Sub
    Public Overridable Sub OnChannelOwnerSelf(ByVal Connection As IRCConnection, ByVal Sender As IRCUser, ByVal Channel As String, ByVal Target As String)
    End Sub
    Public Overridable Sub OnChannelPart(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Reason As String)
    End Sub
    Public Overridable Sub OnChannelPartSelf(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal Reason As String)
    End Sub
    Public Overridable Sub OnChannelQuiet(ByVal Connection As IRCConnection, ByVal Sender As IRCUser, ByVal Channel As String, ByVal Target As String, ByVal MatchedUsers As String())
    End Sub
    Public Overridable Sub OnChannelQuietSelf(ByVal Connection As IRCConnection, ByVal Sender As IRCUser, ByVal Channel As String, ByVal Target As String, ByVal MatchedUsers As String())
    End Sub
    Public Overridable Sub OnChannelRemoveExempt(ByVal Connection As IRCConnection, ByVal Sender As IRCUser, ByVal Channel As String, ByVal Target As String, ByVal MatchedUsers As String())
    End Sub
    Public Overridable Sub OnChannelRemoveExemptSelf(ByVal Connection As IRCConnection, ByVal Sender As IRCUser, ByVal Channel As String, ByVal Target As String, ByVal MatchedUsers As String())
    End Sub
    Public Overridable Sub OnChannelRemoveInviteExempt(ByVal Connection As IRCConnection, ByVal Sender As IRCUser, ByVal Channel As String, ByVal Target As String, ByVal MatchedUsers As String())
    End Sub
    Public Overridable Sub OnChannelRemoveInviteExemptSelf(ByVal Connection As IRCConnection, ByVal Sender As IRCUser, ByVal Channel As String, ByVal Target As String, ByVal MatchedUsers As String())
    End Sub
    Public Overridable Sub OnChannelRemoveKey(ByVal Connection As IRCConnection, ByVal Sender As IRCUser, ByVal Channel As String)
    End Sub
    Public Overridable Sub OnChannelRemoveLimit(ByVal Connection As IRCConnection, ByVal Sender As IRCUser, ByVal Channel As String)
    End Sub
    Public Overridable Sub OnChannelSetKey(ByVal Connection As IRCConnection, ByVal Sender As IRCUser, ByVal Channel As String, ByVal Key As String)
    End Sub
    Public Overridable Sub OnChannelSetLimit(ByVal Connection As IRCConnection, ByVal Sender As IRCUser, ByVal Channel As String, ByVal Limit As Integer)
    End Sub
    Public Overridable Sub OnChannelTopic(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal Topic As String)
    End Sub
    Public Overridable Sub OnChannelTopicChange(ByVal Connection As IRCConnection, ByVal Sender As IRCUser, ByVal Channel As String, ByVal NewTopic As String)
    End Sub
    Public Overridable Sub OnChannelTopicStamp(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal Setter As String, ByVal SetDate As Date)
    End Sub
    Public Overridable Sub OnChannelUsers(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal Names As String)
    End Sub
    Public Overridable Sub OnChannelUnBan(ByVal Connection As IRCConnection, ByVal Sender As IRCUser, ByVal Channel As String, ByVal Target As String, ByVal MatchedUsers As String())
    End Sub
    Public Overridable Sub OnChannelUnBanSelf(ByVal Connection As IRCConnection, ByVal Sender As IRCUser, ByVal Channel As String, ByVal Target As String, ByVal MatchedUsers As String())
    End Sub
    Public Overridable Sub OnChannelUnQuiet(ByVal Connection As IRCConnection, ByVal Sender As IRCUser, ByVal Channel As String, ByVal Target As String, ByVal MatchedUsers As String())
    End Sub
    Public Overridable Sub OnChannelUnQuietSelf(ByVal Connection As IRCConnection, ByVal Sender As IRCUser, ByVal Channel As String, ByVal Target As String, ByVal MatchedUsers As String())
    End Sub
    Public Overridable Sub OnChannelVoice(ByVal Connection As IRCConnection, ByVal Sender As IRCUser, ByVal Channel As String, ByVal Target As String)
    End Sub
    Public Overridable Sub OnChannelVoiceSelf(ByVal Connection As IRCConnection, ByVal Sender As IRCUser, ByVal Channel As String, ByVal Target As String)
    End Sub
    Public Overridable Sub OnPrivateCTCP(ByVal Connection As IRCConnection, ByVal Sender As IRCUser, ByVal Message As String)
    End Sub
    Public Overridable Sub OnExemptList(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal BannedUser As String, ByVal BanningUser As String, ByVal Time As Date)
    End Sub
    Public Overridable Sub OnExemptListEnd(ByVal Connection As IRCConnection, ByVal Message As String)
    End Sub
    Public Overridable Sub OnInvite(ByVal Connection As IRCConnection, ByVal Sender As IRCUser, ByVal Channel As String)
    End Sub
    Public Overridable Sub OnInviteExemptList(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal BannedUser As String, ByVal BanningUser As String, ByVal Time As Date)
    End Sub
    Public Overridable Sub OnInviteExemptListEnd(ByVal Connection As IRCConnection, ByVal Message As String)
    End Sub
    Public Overridable Sub OnKilled(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Reason As String)
    End Sub
    Public Overridable Sub OnNames(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal Message As String)
    End Sub
    Public Overridable Sub OnNamesEnd(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal Message As String)
    End Sub
    Public Overridable Sub OnPrivateMessage(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Message As String)
        Dim UsedMyNickname As Boolean
        If Message.ToLower.StartsWith(Nickname(Connection).ToLower & " ") Then
            Message = Message.Substring(Nickname(Connection).Length + 1)
            UsedMyNickname = True
        ElseIf Message.ToLower.StartsWith(Nickname(Connection).ToLower & ", ") Or
                Message.ToLower.StartsWith(Nickname(Connection).ToLower & ": ") Or
                Message.ToLower.StartsWith(Nickname(Connection).ToLower & "- ") Or
                Message.ToLower.StartsWith(Nickname(Connection).ToLower & ". ") Then
            Message = Message.Substring(Nickname(Connection).Length + 2)
            UsedMyNickname = True
        End If
        Dim Handled As Boolean
        If Message.StartsWith("!") Then _
                    Handled = RunCommand(Connection, Sender, Sender.Split("!"c)(0), Message)
        If Handled Then Exit Sub
        Handled = RunRegex(Connection, Sender, Sender.Split("!"c)(0), Message, True)
    End Sub
    Public Overridable Sub OnPrivateAction(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Message As String)
    End Sub
    Public Overridable Sub OnPrivateNotice(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Message As String)
    End Sub
    Public Overridable Sub OnQuit(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Reason As String)
    End Sub
    Public Overridable Sub OnQuitSelf(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Reason As String)
    End Sub
    Public Overridable Sub OnRawLineReceived(ByVal Connection As IRCConnection, ByVal Message As String)
    End Sub
    Public Overridable Sub OnTimeOut(ByVal Connection As IRCConnection)
    End Sub
    Public Overridable Sub OnUserModesSet(ByVal Connection As IRCConnection, ByVal Sender As IRCUser, ByVal Modes As String)
    End Sub
    Public Overridable Sub OnServerNotice(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Message As String)
    End Sub
    Public Overridable Sub OnServerError(ByVal Connection As IRCConnection, ByVal Message As String)
    End Sub
    Public Overridable Sub OnServerMessage(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Numeric As String, ByVal Message As String)
    End Sub
    Public Overridable Sub OnServerMessageUnhandled(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Numeric As String, ByVal Message As String)
    End Sub
    Public Overridable Sub OnWhoList(ByVal Connection As IRCConnection, ByVal Channel As String, ByVal Username As String, ByVal Address As String, ByVal Server As String, ByVal Nickname As String, ByVal Flags As String, ByVal Hops As Integer, ByVal FullName As String)
    End Sub
    Public Overridable Sub OnPluginEvent(ByVal EventName As String, ByVal args As Object, ByRef Handled As Boolean)
    End Sub
End Class
