'Advanced Console Output
'by Andrio Celos
'for Microsoft Visual Basic .NET

'For use with console applications only.

'This module contains more advanced functions for console output, including control codes and ASCII tables.

Public Module AdvancedConsoleOutput

    ''' <summary>
    ''' Writes text to the console window. This function accepts special codes that are described under Remarks.
    ''' </summary>
    ''' <param name="Text">The text to write.</param>
    ''' <remarks>Accepts the following codes:
    ''' \c* Changes the colour of following text to *.
    ''' \b* Changes the background colour of following text to *.
    ''' \n  Continues text on the next line.
    ''' \bo Returns the background to the original colour.
    ''' \co Returns the text to the original colour.
    ''' \r  Returns the text and background to the original colour.
    ''' \\  Writes the \ character.
    ''' \c and \b accept the following codes for *: BLACK, DKBLUE, DKGREEN, DKCYAN, DKRED, DKMAGENTA, DKYELLOW, GRAY, DKGRAY, BLUE, GREEN, CYAN, RED, MAGENTA, YELLOW, WHITE.
    ''' All control codes are case-insensitive.
    ''' </remarks>
    Public Sub Output(ByVal Text As String)
        SyncLock Console.Out
            If Text = Nothing Then Exit Sub
            Dim OriginalBackground = Console.BackgroundColor, OriginalForeground = Console.ForegroundColor
            Dim Pos As Integer, NextSwitch As Integer
            Do
                NextSwitch = Text.IndexOf("\"c, Pos)
                If NextSwitch = -1 Then
                    Console.Write(Text.Substring(Pos))
                    Exit Do
                End If
                Console.Write(Text.Substring(Pos, NextSwitch - Pos))
                If Text.Length - NextSwitch >= 3 AndAlso Text.Substring(NextSwitch, 3).ToLower = "\bo" Then
                    Console.BackgroundColor = OriginalBackground
                    Pos = NextSwitch + 3
                ElseIf Text.Length - NextSwitch >= 3 AndAlso Text.Substring(NextSwitch, 3).ToLower = "\co" Then
                    Console.ForegroundColor = OriginalForeground
                    Pos = NextSwitch + 3
                ElseIf Text.Length - NextSwitch >= 2 AndAlso Text.Substring(NextSwitch, 2).ToLower = "\b" Then
                    If Text.Length - NextSwitch >= 7 AndAlso Text.Substring(NextSwitch, 7).ToLower = "\bblack" Then : Console.BackgroundColor = ConsoleColor.Black : Pos = NextSwitch + 7
                    ElseIf Text.Length - NextSwitch >= 8 AndAlso Text.Substring(NextSwitch, 8).ToLower = "\bdkblue" Then : Console.BackgroundColor = ConsoleColor.DarkBlue : Pos = NextSwitch + 8
                    ElseIf Text.Length - NextSwitch >= 9 AndAlso Text.Substring(NextSwitch, 9).ToLower = "\bdkgreen" Then : Console.BackgroundColor = ConsoleColor.DarkGreen : Pos = NextSwitch + 9
                    ElseIf Text.Length - NextSwitch >= 8 AndAlso Text.Substring(NextSwitch, 8).ToLower = "\bdkcyan" Then : Console.BackgroundColor = ConsoleColor.DarkCyan : Pos = NextSwitch + 8
                    ElseIf Text.Length - NextSwitch >= 7 AndAlso Text.Substring(NextSwitch, 7).ToLower = "\bdkred" Then : Console.BackgroundColor = ConsoleColor.DarkRed : Pos = NextSwitch + 7
                    ElseIf Text.Length - NextSwitch >= 11 AndAlso Text.Substring(NextSwitch, 11).ToLower = "\bdkmagenta" Then : Console.BackgroundColor = ConsoleColor.DarkMagenta : Pos = NextSwitch + 11
                    ElseIf Text.Length - NextSwitch >= 10 AndAlso Text.Substring(NextSwitch, 10).ToLower = "\bdkyellow" Then : Console.BackgroundColor = ConsoleColor.DarkYellow : Pos = NextSwitch + 10
                    ElseIf Text.Length - NextSwitch >= 6 AndAlso Text.Substring(NextSwitch, 6).ToLower = "\bgray" Then : Console.BackgroundColor = ConsoleColor.Gray : Pos = NextSwitch + 6
                    ElseIf Text.Length - NextSwitch >= 8 AndAlso Text.Substring(NextSwitch, 8).ToLower = "\bdkgray" Then : Console.BackgroundColor = ConsoleColor.DarkGray : Pos = NextSwitch + 8
                    ElseIf Text.Length - NextSwitch >= 6 AndAlso Text.Substring(NextSwitch, 6).ToLower = "\bblue" Then : Console.BackgroundColor = ConsoleColor.Blue : Pos = NextSwitch + 6
                    ElseIf Text.Length - NextSwitch >= 7 AndAlso Text.Substring(NextSwitch, 7).ToLower = "\bgreen" Then : Console.BackgroundColor = ConsoleColor.Green : Pos = NextSwitch + 7
                    ElseIf Text.Length - NextSwitch >= 6 AndAlso Text.Substring(NextSwitch, 6).ToLower = "\bcyan" Then : Console.BackgroundColor = ConsoleColor.Cyan : Pos = NextSwitch + 6
                    ElseIf Text.Length - NextSwitch >= 5 AndAlso Text.Substring(NextSwitch, 5).ToLower = "\bred" Then : Console.BackgroundColor = ConsoleColor.Red : Pos = NextSwitch + 5
                    ElseIf Text.Length - NextSwitch >= 9 AndAlso Text.Substring(NextSwitch, 9).ToLower = "\bmagenta" Then : Console.BackgroundColor = ConsoleColor.Magenta : Pos = NextSwitch + 9
                    ElseIf Text.Length - NextSwitch >= 8 AndAlso Text.Substring(NextSwitch, 8).ToLower = "\byellow" Then : Console.BackgroundColor = ConsoleColor.Yellow : Pos = NextSwitch + 8
                    ElseIf Text.Length - NextSwitch >= 7 AndAlso Text.Substring(NextSwitch, 7).ToLower = "\bwhite" Then : Console.BackgroundColor = ConsoleColor.White : Pos = NextSwitch + 7
                    Else : Console.Write("\b") : Pos = NextSwitch + 2
                    End If
                ElseIf Text.Length - NextSwitch >= 2 AndAlso Text.Substring(NextSwitch, 2).ToLower = "\c" Then
                    If Text.Length - NextSwitch >= 7 AndAlso Text.Substring(NextSwitch, 7).ToLower = "\cblack" Then : Console.ForegroundColor = ConsoleColor.Black : Pos = NextSwitch + 7
                    ElseIf Text.Length - NextSwitch >= 8 AndAlso Text.Substring(NextSwitch, 8).ToLower = "\cdkblue" Then : Console.ForegroundColor = ConsoleColor.DarkBlue : Pos = NextSwitch + 8
                    ElseIf Text.Length - NextSwitch >= 9 AndAlso Text.Substring(NextSwitch, 9).ToLower = "\cdkgreen" Then : Console.ForegroundColor = ConsoleColor.DarkGreen : Pos = NextSwitch + 9
                    ElseIf Text.Length - NextSwitch >= 8 AndAlso Text.Substring(NextSwitch, 8).ToLower = "\cdkcyan" Then : Console.ForegroundColor = ConsoleColor.DarkCyan : Pos = NextSwitch + 8
                    ElseIf Text.Length - NextSwitch >= 7 AndAlso Text.Substring(NextSwitch, 7).ToLower = "\cdkred" Then : Console.ForegroundColor = ConsoleColor.DarkRed : Pos = NextSwitch + 7
                    ElseIf Text.Length - NextSwitch >= 11 AndAlso Text.Substring(NextSwitch, 11).ToLower = "\cdkmagenta" Then : Console.ForegroundColor = ConsoleColor.DarkMagenta : Pos = NextSwitch + 11
                    ElseIf Text.Length - NextSwitch >= 10 AndAlso Text.Substring(NextSwitch, 10).ToLower = "\cdkyellow" Then : Console.ForegroundColor = ConsoleColor.DarkYellow : Pos = NextSwitch + 10
                    ElseIf Text.Length - NextSwitch >= 6 AndAlso Text.Substring(NextSwitch, 6).ToLower = "\cgray" Then : Console.ForegroundColor = ConsoleColor.Gray : Pos = NextSwitch + 6
                    ElseIf Text.Length - NextSwitch >= 8 AndAlso Text.Substring(NextSwitch, 8).ToLower = "\cdkgray" Then : Console.ForegroundColor = ConsoleColor.DarkGray : Pos = NextSwitch + 8
                    ElseIf Text.Length - NextSwitch >= 6 AndAlso Text.Substring(NextSwitch, 6).ToLower = "\cblue" Then : Console.ForegroundColor = ConsoleColor.Blue : Pos = NextSwitch + 6
                    ElseIf Text.Length - NextSwitch >= 7 AndAlso Text.Substring(NextSwitch, 7).ToLower = "\cgreen" Then : Console.ForegroundColor = ConsoleColor.Green : Pos = NextSwitch + 7
                    ElseIf Text.Length - NextSwitch >= 6 AndAlso Text.Substring(NextSwitch, 6).ToLower = "\ccyan" Then : Console.ForegroundColor = ConsoleColor.Cyan : Pos = NextSwitch + 6
                    ElseIf Text.Length - NextSwitch >= 5 AndAlso Text.Substring(NextSwitch, 5).ToLower = "\cred" Then : Console.ForegroundColor = ConsoleColor.Red : Pos = NextSwitch + 5
                    ElseIf Text.Length - NextSwitch >= 9 AndAlso Text.Substring(NextSwitch, 9).ToLower = "\cmagenta" Then : Console.ForegroundColor = ConsoleColor.Magenta : Pos = NextSwitch + 9
                    ElseIf Text.Length - NextSwitch >= 8 AndAlso Text.Substring(NextSwitch, 8).ToLower = "\cyellow" Then : Console.ForegroundColor = ConsoleColor.Yellow : Pos = NextSwitch + 8
                    ElseIf Text.Length - NextSwitch >= 7 AndAlso Text.Substring(NextSwitch, 7).ToLower = "\cwhite" Then : Console.ForegroundColor = ConsoleColor.White : Pos = NextSwitch + 7
                    Else : Console.Write("\c") : Pos = NextSwitch + 2
                    End If
                ElseIf Text.Length - NextSwitch >= 2 AndAlso Text.Substring(NextSwitch, 2).ToLower = "\r" Then
                    Console.BackgroundColor = OriginalBackground
                    Console.ForegroundColor = OriginalForeground
                    Pos = NextSwitch + 2
                ElseIf Text.Length - NextSwitch >= 2 AndAlso Text.Substring(NextSwitch, 2).ToLower = "\n" Then
                    Console.WriteLine()
                    Pos = NextSwitch + 2
                ElseIf Text.Length - NextSwitch >= 2 AndAlso Text.Substring(NextSwitch, 2).ToLower = "\\" Then
                    Console.Write("\")
                    Pos = NextSwitch + 2
                Else
                    Console.Write("\")
                    Pos = NextSwitch + 1
                End If
            Loop
        End SyncLock
    End Sub
    ''' <summary>
    ''' Writes the text representation of the specified objects to the console window. 
    ''' This function accepts special codes that are described under Remarks.
    ''' </summary>
    ''' <param name="Format">A composite format string. See System.String.Format in Help for details.</param>
    ''' <param name="args">The objects to format.</param>
    Public Sub Output(ByVal Format As String, ByVal ParamArray args() As Object)
        Output(String.Format(Format, args))
    End Sub
    ''' <summary>
    ''' Writes text to the console window and continues output on the next line. 
    ''' This function accepts special codes that are described under Remarks.
    ''' </summary>
    ''' <param name="Text">The text to write.</param>
    Public Sub OutputLine(ByVal Text As String)
        SyncLock Console.Out
            Output(Text)
            Console.WriteLine()
        End SyncLock
    End Sub
    ''' <summary>
    ''' Continues output on the next line. 
    ''' </summary>
    Public Sub OutputLine()
        Console.WriteLine()
    End Sub
    ''' <summary>
    ''' Writes text to the console window and continues output on a lower line. 
    ''' This function accepts special codes that are described under Remarks.
    ''' </summary>
    ''' <param name="Text">The text to write.</param>
    ''' <param name="LineBreaks">The number of line breaks to insert after the output.</param>
    Public Sub OutputLine(ByVal Text As String, ByVal LineBreaks As Integer)
        SyncLock Console.Out
            Output(Text)
            For i = 1 To LineBreaks : Console.WriteLine() : Next
        End SyncLock
    End Sub
    ''' <summary>
    ''' Writes the text representation of the specified objects to the console window and continues output on the next line. 
    ''' This function accepts special codes that are described under Remarks.
    ''' </summary>
    ''' <param name="Format">A composite format string. See System.String.Format in Help for details.</param>
    ''' <param name="args">The objects to format.</param>
    Public Sub OutputLine(ByVal Format As String, ByVal ParamArray args() As Object)
        SyncLock Console.Out
            Output(Format, args)
            Console.WriteLine()
        End SyncLock
    End Sub
    ''' <summary>
    ''' Writes the text representation of the specified objects to the console window and continues output on a lower line. 
    ''' This function accepts special codes that are described under Remarks.
    ''' </summary>
    ''' <param name="Format">A composite format string. See System.String.Format in Help for details.</param>
    ''' <param name="LineBreaks">The number of line breaks to insert after the output.</param>
    ''' <param name="args">The objects to format.</param>
    Public Sub OutputLine(ByVal Format As String, ByVal LineBreaks As Integer, ByVal ParamArray args() As Object)
        SyncLock Console.Out
            Output(Format, args)
            For i = 1 To LineBreaks : Console.WriteLine() : Next
        End SyncLock
    End Sub
    ''' <summary>
    ''' Continues output on a lower line. 
    ''' </summary>
    ''' <param name="LineBreaks">The number of line breaks to insert.</param>
    Public Sub OutputLine(ByVal LineBreaks As Integer)
        SyncLock Console.Out
            For i = 1 To LineBreaks : Console.WriteLine() : Next
        End SyncLock
    End Sub
    ''' <summary>
    ''' Writes text to the console window and continues output on the next line. 
    ''' This function accepts special codes that are described under Remarks.
    ''' </summary>
    ''' <param name="Text">The text to write. Each element of the array is written on its own line.</param>
    Public Sub OutputLine(ByVal Text() As String)
        SyncLock Console.Out
            For Each s In Text
                OutputLine(s)
            Next
        End SyncLock
    End Sub
    ''' <summary>
    ''' Writes text to the console window and continues output on a lower line. 
    ''' This function accepts special codes that are described under Remarks.
    ''' </summary>
    ''' <param name="Text">The text to write. Each element of the array is written on its own line.</param>
    ''' <param name="LineBreaks">The number of line breaks to insert after each output line.</param>
    Public Sub OutputLine(ByVal Text() As String, ByVal LineBreaks As Integer)
        SyncLock Console.Out
            For Each s In Text
                OutputLine(s, LineBreaks)
            Next
        End SyncLock
    End Sub

    ''' <summary>
    ''' Returns the length of the output if the Output function is to be called.
    ''' </summary>
    ''' <param name="Text">The text to analyse.</param>
    ''' <returns>The length of the output, excluding special \ codes.</returns>
    ''' <remarks>Accepts the following codes:
    ''' \c* Changes the colour of following text to *.
    ''' \b* Changes the background colour of following text to *.
    ''' \n  Continues text on the next line.
    ''' \bo Returns the background to the original colour.
    ''' \co Returns the text to the original colour.
    ''' \r  Returns the text and background to the original colour.
    ''' \\  Writes the \ character.
    ''' \c and \b accept the following codes for *: BLACK, DKBLUE, DKGREEN, DKCYAN, DKRED, DKMAGENTA, DKYELLOW, GRAY, DKGRAY, BLUE, GREEN, CYAN, RED, MAGENTA, YELLOW, WHITE.</remarks>
    Public Function OutputLength(ByVal Text As String) As Integer
        If Text = Nothing Then Return 0
        Dim Pos As Integer, NextSwitch As Integer
        OutputLength = 0 'Note that I did not use a Return statement here.
        Do
            NextSwitch = Text.IndexOf("\"c, Pos)
            If NextSwitch = -1 Then
                OutputLength += Text.Substring(Pos).Length
                Exit Do
            End If
            OutputLength += Text.Substring(Pos, NextSwitch - Pos).Length
            If Text.Length - NextSwitch >= 2 AndAlso Text.Substring(NextSwitch, 2).ToLower = "\b" Then
                For Each s In {"black", "dkblue", "dkgreen", "dkcyan", "dkred", "dkmagenta", "dkyellow", "gray", "dkgray", "blue", "green", "cyan", "red", "magenta", "yellow", "white"}
                    If Text.Length - NextSwitch >= s.Length + 2 AndAlso Text.Substring(NextSwitch, s.Length + 2).ToLower = "\b" & s Then
                        Pos = NextSwitch + s.Length + 2
                        Continue Do
                    End If
                Next
                OutputLength += 2
                Pos = NextSwitch + 2
            ElseIf Text.Length - NextSwitch >= 2 AndAlso Text.Substring(NextSwitch, 2).ToLower = "\c" Then
                For Each s In {"black", "dkblue", "dkgreen", "dkcyan", "dkred", "dkmagenta", "dkyellow", "gray", "dkgray", "blue", "green", "cyan", "red", "magenta", "yellow", "white"}
                    If Text.Length - NextSwitch >= s.Length + 2 AndAlso Text.Substring(NextSwitch, s.Length + 2).ToLower = "\c" & s Then
                        Pos = NextSwitch + s.Length + 2
                        Continue Do
                    End If
                Next
                OutputLength += 2
                Pos = NextSwitch + 2
            ElseIf Text.Length - NextSwitch >= 3 AndAlso Text.Substring(NextSwitch, 2).ToLower = "\bo" Then
                Pos = NextSwitch + 3
            ElseIf Text.Length - NextSwitch >= 3 AndAlso Text.Substring(NextSwitch, 2).ToLower = "\co" Then
                Pos = NextSwitch + 3
            ElseIf Text.Length - NextSwitch >= 2 AndAlso Text.Substring(NextSwitch, 2).ToLower = "\r" Then
                Pos = NextSwitch + 2
            ElseIf Text.Length - NextSwitch >= 2 AndAlso Text.Substring(NextSwitch, 2).ToLower = "\n" Then
                Console.WriteLine()
                OutputLength += vbCrLf.Length
                Pos = NextSwitch + 2
            ElseIf Text.Length - NextSwitch >= 2 AndAlso Text.Substring(NextSwitch, 2).ToLower = "\\" Then
                OutputLength += 1
                Pos = NextSwitch + 2
            Else
                OutputLength += 1
                Pos = NextSwitch + 1
            End If
        Loop
    End Function

    ''' <summary>
    ''' Writes text to the console window in the format of a table, using the Output function.
    ''' </summary>
    ''' <param name="Text">The text to write.</param>
    ''' <remarks></remarks>
    Public Sub DrawTable(ByVal Text As String)
        DrawTable({{{Text}}})
    End Sub
    ''' <summary>
    ''' Writes text to the console window in the format of a table, using the Output function.
    ''' </summary>
    ''' <param name="Text">The text to write. Indexing: (Column)</param>
    ''' <remarks></remarks>
    Public Sub DrawTable(ByVal Text() As String)
        SyncLock Console.Out
            Dim s(0, 0 To UBound(Text))() As String
            For i = 0 To UBound(Text)
                s(0, i) = {Text(i)}
            Next
            DrawTable(s)
        End SyncLock
    End Sub
    ''' <summary>
    ''' Writes text to the console window in the format of a table, using the Output function.
    ''' </summary>
    ''' <param name="Text">The text to write. Indexing: (Row, Column)</param>
    ''' <remarks></remarks>
    Public Sub DrawTable(ByVal Text(,) As String)
        SyncLock Console.Out
            Dim s(UBound(Text, 1), UBound(Text, 2))() As String
            For i = 0 To UBound(Text, 1)
                For j = 0 To UBound(Text, 2)
                    s(i, j) = {Text(i, j)}
                Next
            Next
            DrawTable(s)
        End SyncLock
    End Sub
    ''' <summary>
    ''' Writes text to the console window in the format of a table, using the Output function.
    ''' </summary>
    ''' <param name="Text">The text to write. Indexing: (Row, Column, Line)</param>
    ''' <remarks></remarks>
    Public Sub DrawTable(ByVal Text(,,) As String)
        SyncLock Console.Out
            Dim s(UBound(Text, 1), UBound(Text, 2))() As String
            Dim z() As String
            For i = 0 To UBound(Text, 1)
                For j = 0 To UBound(Text, 2)
                    ReDim z(UBound(Text, 3))
                    For k = 0 To UBound(Text, 3)
                        z(k) = Text(i, j, k)
                    Next
                    s(i, j) = z
                Next
            Next
            DrawTable(s)
        End SyncLock
    End Sub
    ''' <summary>
    ''' Writes text to the console window in the format of a table, using the Output function.
    ''' </summary>
    ''' <param name="Text">The text to write. Indexing: (Column)(Line)</param>
    ''' <remarks></remarks>
    Public Sub DrawTable(ByVal Text()() As String)
        SyncLock Console.Out
            Dim s(0, 0 To UBound(Text))() As String
            For i = 0 To UBound(Text)
                s(0, i) = Text(i)
            Next
            DrawTable(s)
        End SyncLock
    End Sub
    ''' <summary>
    ''' Writes text to the console window in the format of a table, using the Output function.
    ''' </summary>
    ''' <param name="Text">The text to write. Indexing: (Row, Column)(Line)</param>
    ''' <remarks></remarks>
    Public Sub DrawTable(ByVal Text(,)() As String)
        SyncLock Console.Out
            Dim RowHeight(0 To UBound(Text, 1)) As Integer, ColumnWidth(0 To UBound(Text, 2)) As Integer
            For y = 0 To UBound(Text, 1)
                Dim Max As Integer = 0
                For x = 0 To UBound(Text, 2)
                    Dim i As Integer = 0
                    For z = 0 To UBound(Text(y, x))
                        If Text(y, x)(z) = Nothing Then Text(y, x)(z) = ""
                        Dim Pos = Text(y, x)(z).IndexOf(vbCrLf)
                        i += 1
                        Do Until Pos = -1
                            i += 1
                            Pos = Text(y, x)(z).IndexOf(vbCrLf, Pos + 1)
                        Loop
                        Pos = Text(y, x)(z).IndexOf("\n")
                        Do Until Pos = -1
                            i += 1
                            Pos = Text(y, x)(z).IndexOf("\n", Pos + 2)
                        Loop
                    Next
                    Max = Math.Max(Max, i)
                Next
                RowHeight(y) = Max
            Next
            For x = 0 To UBound(Text, 2)
                Dim Max As Integer = 0
                For y = 0 To UBound(Text, 1)
                    Dim NewZ() As String = Nothing, i As Integer = 0
                    For z = 0 To UBound(Text(y, x))
                        Dim s = Text(y, x)(z).Split({vbCrLf, "\n"}, StringSplitOptions.None)
                        If NewZ Is Nothing Then
                            ReDim Preserve NewZ(UBound(s))
                        Else
                            ReDim Preserve NewZ(UBound(NewZ) + s.Count)
                        End If
                        For Each Line In s
                            Max = Math.Max(Max, OutputLength(Line))
                            NewZ(i) = Line
                            i += 1
                        Next
                    Next
                    Text(y, x) = NewZ
                Next
                ColumnWidth(x) = Max
            Next
            Output("┌")
            For x = 0 To UBound(Text, 2)
                Output(New String("─", ColumnWidth(x)))
                If x = UBound(Text, 2) Then Output("┐") Else Output("┬")
            Next
            OutputLine()
            For y = 0 To UBound(Text, 1)
                Dim zmax As Integer = 0
                For x = 0 To UBound(Text, 2)
                    zmax = Math.Max(zmax, UBound(Text(y, x)))
                Next
                For z = 0 To zmax
                    Output("│")
                    For x = 0 To UBound(Text, 2)
                        Dim Len As Integer
                        If z > UBound(Text(y, x)) Then
                            Len = 0
                        Else
                            Output(Text(y, x)(z))
                            Len = OutputLength(Text(y, x)(z))
                        End If
                        Output(Space(ColumnWidth(x) - Len))
                        Output("│")
                    Next
                    OutputLine()
                Next
                If y = UBound(Text, 1) Then
                    Output("└")
                    For x = 0 To UBound(Text, 2)
                        Output(New String("─", ColumnWidth(x)))
                        If x = UBound(Text, 2) Then Output("┘") Else Output("┴")
                    Next
                Else
                    Output("├")
                    For x = 0 To UBound(Text, 2)
                        Output(New String("─", ColumnWidth(x)))
                        If x = UBound(Text, 2) Then Output("┤") Else Output("┼")
                    Next
                End If
                OutputLine()
            Next
        End SyncLock
    End Sub

    ''' <summary>
    ''' Writes text to the console window with a border around it, using the Output function.
    ''' </summary>
    ''' <param name="Text">The text to write. Indexing: (Line)</param>
    ''' <remarks>This function is an alias for DrawTable({{Text()}})</remarks>
    Public Sub DrawBox(ByVal ParamArray Text() As String)
        SyncLock Console.Out
            Dim MaxLength As Integer
            For Each s In Text : MaxLength = Math.Max(MaxLength, OutputLength(s)) : Next
            OutputLine("┌" & New String("─"c, MaxLength) & "┐")
            For Each s In Text
                Output("│")
                Output(s)
                Output(Space(MaxLength - OutputLength(s)))
                Output("│")
                OutputLine()
            Next
            OutputLine("└" & New String("─"c, MaxLength) & "┘")
        End SyncLock
    End Sub

    ''' <summary>
    ''' Accepts a line of input from the user.
    ''' </summary>
    ''' <param name="DefaultValue">The value that is initially shown to be 'typed in'. This value can be 'backspaced' and replaced by the user.</param>
    ''' <param name="Width">The number of characters that the user is allowed to enter.</param>
    ''' <param name="AllowBackslash">If false, the function will automatically escape backslashes. If true, the user is allowed to enter codes like \c.</param>
    ''' <param name="ReturnKey">A variable that, on return, should contain the key pressed by the user to accept input.</param>
    ''' <param name="HaltOn">An array of key codes that will end this function. Default: {ENTER}. One of these will be stored in ReturnKey.</param>
    ''' <returns>The string entered.</returns>
    ''' <remarks></remarks>
    Public Function InputLine(ByVal DefaultValue As String, ByVal Width As Integer, ByVal AllowBackslash As Boolean, ByRef ReturnKey As ConsoleKeyInfo, ByVal ParamArray HaltOn() As ConsoleKey) As String
        Dim CursorPosition As Integer  'The current character index that the cursor is on.
        Dim X As Integer               'The X of the start of the string.
        Dim Y As Integer               'The Y of the start of the string.
        Dim Input As ConsoleKeyInfo
        Dim s As String

        s = DefaultValue
        If s <> Nothing Then CursorPosition = s.Length Else CursorPosition = 0
        X = Console.CursorLeft
        Y = Console.CursorTop

        Do
            Console.SetCursorPosition(X, Y)
            Output(Space(Width))
            Console.SetCursorPosition(X, Y)
            Output(s & " \r")
            Console.CursorLeft = X + CursorPosition
0:          Input = Console.ReadKey(True)
            Select Case Input.Key
                Case ConsoleKey.Delete
                    If CursorPosition < s.Length Then
                        Output(" ")
                        s = s.Remove(CursorPosition, 1)
                    End If
                Case ConsoleKey.Backspace
                    If CursorPosition > 0 Then
                        Console.CursorLeft -= 1
                        Output(" ")
                        s = s.Remove(CursorPosition - 1, 1)
                        CursorPosition -= 1
                    End If
                Case ConsoleKey.LeftArrow
                    If CursorPosition > 0 Then
                        CursorPosition -= 1
                        Console.CursorLeft -= 1
                    End If
                    GoTo 0
                Case ConsoleKey.RightArrow
                    If CursorPosition < s.Length Then
                        CursorPosition += 1
                        Console.CursorLeft += 1
                    End If
                    GoTo 0
                Case ConsoleKey.Home
                    CursorPosition = 0
                    Console.CursorLeft = X
                    GoTo 0
                Case ConsoleKey.End
                    CursorPosition = s.Length
                    Console.CursorLeft = X + s.Length
                    GoTo 0
                Case Else
                    If s = Nothing Then s = ""
                    If HaltOn.Contains(Input.Key) Then
                        ReturnKey = Input
                        Return s
                    End If
                    If Input.KeyChar <> Chr(0) And s.Length < Width Then
                        If CursorPosition = s.Length Then
                            s &= Input.KeyChar
                        Else
                            s = s.Insert(CursorPosition, Input.KeyChar)
                        End If
                        CursorPosition += 1
                    Else
                        GoTo 0
                    End If
            End Select
            If HaltOn.Contains(Input.Key) Then
                ReturnKey = Input
                Return s
            End If
        Loop
    End Function
End Module
