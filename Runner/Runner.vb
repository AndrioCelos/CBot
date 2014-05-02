Imports VBot

Public Class Runner
    Inherits Plugin

    <Command("run", 1, 1,
    "run <executable file>",
    "Starts an executable.",
    ".run")>
    Public Sub CommandItem(ByVal Connection As IRCConnection, ByVal Sender As String, ByVal Channel As String, ByVal args() As String)
        Dim FilePath As String, Arguments As String = ""

        If args(0).StartsWith("""") Then
            Dim Close = args(0).IndexOf("""", 1)
            If Close = -1 Then
                FilePath = args(0).Substring(1)
                Arguments = Nothing
            Else
                FilePath = args(0).Substring(1, Close - 1)
                If args(0).Length = Close + 1 Then
                    Arguments = Nothing
                ElseIf args(0)(Close + 1) = " "c Then
                    Arguments = args(0).Substring(Close + 2)
                Else
                    Arguments = args(0).Substring(Close + 1)
                End If
            End If
        Else
            Dim Fields = args(0).Split({" "c}, 2)
            FilePath = Fields(0)
            If Fields.Count = 2 Then Arguments = Fields(1)
        End If

        If Not System.IO.File.Exists(FilePath) Then
            Reply(Connection, Channel, Sender, "No such file exists.")
            Return
        End If

        Dim Process As New Process()
        Process.StartInfo = New ProcessStartInfo(FilePath, Arguments)
        Process.Start()
        Reply(Connection, Channel, Sender, "Started the executable with process ID $k09" & Process.Id & "$o.")
    End Sub
End Class
