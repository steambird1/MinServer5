' Load WEB Data.

Imports System.Text

Module WebInterpreter

    Public Sub ShowError(Info As String)
        Console.ForegroundColor = ConsoleColor.Red
        Console.Out.Write(Info)
        Console.Out.WriteLine()
        Console.ForegroundColor = ConsoleColor.White
    End Sub

    Public Sub ShowWarning(Info As String)
        Console.ForegroundColor = ConsoleColor.Yellow
        Console.Out.Write(Info)
        Console.Out.WriteLine()
        Console.ForegroundColor = ConsoleColor.White
    End Sub

    Public Sub ShowStatus(Info As String)
        Console.ForegroundColor = ConsoleColor.Cyan
        Console.Out.Write(Info)
        Console.Out.WriteLine()
        Console.ForegroundColor = ConsoleColor.White
    End Sub

    Public Class BadWebFormatException
        Inherits Exception
        Public Overrides ReadOnly Property Message As String = "Incorrect Web Request Format!"
    End Class

    Public Class NoByteStringException
        Inherits Exception

        Public Sub New(message As String)
            MyBase.New(message)
        End Sub
    End Class

    ' Must use ToStringWithEncoding() when regard it as string.
    <Serializable>
    Public Class WebString

        Private _byteData As Byte() = {}
        'Private Referencer As StringBuilder = New StringBuilder
        Private ReferenceLine As Byte

        Public Const ByteCR As Byte = 13
        Public Const ByteLF As Byte = 10

        Public ReadOnly Property Length As Long
            Get
                Return _byteData.Count()
            End Get
        End Property

        Public Property ByteData As Byte()
            Get
                Return _byteData
            End Get
            Private Set(value As Byte())
                _byteData = value
            End Set
        End Property

        Public Property Capacity As Long
            Get
                Return _byteData.Count()
            End Get
            Set(value As Long)
                ReDim Preserve _byteData(value)
            End Set
        End Property

        Public Sub New()
            ReferenceLine = AscW(vbLf) 'Referencer.AppendLine().ToString()(0)
            'ReDim Me._byteData(0)
        End Sub

        Public Sub New(Str As String)
            Me.Append(Str.ToCharArray())
        End Sub

        Public Sub New(Str As WebString)
            Me._byteData = Str._byteData
        End Sub

        Public Sub New(Str As Byte())
            Me._byteData = Str
        End Sub

        Public Sub New(str As Char())
            Me.Append(str)
        End Sub

        Public Sub New(caps As Long)
            Me.Capacity = caps
        End Sub

        Public Sub Append(Str As WebString)
            Dim Origin As Long = Me._byteData.LongCount()
            ReDim Preserve Me._byteData(Origin + Str._byteData.LongCount() - 1)
            Array.Copy(Str._byteData, 0, Me._byteData, Origin, Str._byteData.Count())
        End Sub

        Public Sub Append(Str As Char(), Optional ByVal Encoding As Encoding = Nothing)
            If IsNothing(Encoding) Then
                Encoding = Encoding.Default
            End If
            Me.Append(New WebString(Encoding.GetBytes(Str)))
        End Sub

        Public Sub Append(Str As Byte())
            Me.Append(New WebString(Str))
        End Sub

        Public Shared Widening Operator CType(ByVal Str As String) As WebString
            Return New WebString(Str)
        End Operator

        Public Shared Narrowing Operator CType(ByVal Str As WebString) As String
            Throw New NoByteStringException("Please use ToStringWithEncoding() instead !")
        End Operator

        Public Sub AppendLine()
            Me.Append({ReferenceLine})
        End Sub

        Public Shared Operator +(ByVal Str1 As WebString, ByVal Str2 As WebString) As WebString
            Dim Str As WebString = Str1
            Str.Append(Str2)
            Return Str
        End Operator

        Public Function ToStringWithEncoding(Optional ByVal Encoding As Encoding = Nothing) As String
            If IsNothing(Encoding) Then
                Encoding = Encoding.Default
            End If
            Return Encoding.GetChars(Me._byteData)
        End Function

        ' Please modify everywhere.

        Public Overrides Function ToString() As String
            Throw New NoByteStringException("Please use ToStringWithEncoding() instead !")
        End Function

        ' -1 If not found. as same as String.IndexOf

        Public Sub RemoveAt(pos As Integer)
            If (pos >= 0) AndAlso (pos < Me.Length) Then
                Dim mlen As Integer = Me.Length - 2
                For i = pos To mlen
                    _byteData(i) = _byteData(i + 1)
                Next
                ReDim Preserve _byteData(mlen)
            End If
        End Sub

        Public Sub RemoveCrLf()
            For i = 0 To 1
                If Me(0) = ByteCR Or Me(0) = ByteLF Then
                    Me.RemoveAt(0)
                End If
            Next
            For i = 0 To 1
                If Me(Me.Length - 1) = ByteCR Or Me(Me.Length - 1) = ByteLF Then
                    Me.RemoveAt(Me.Length - 1)
                End If
            Next
        End Sub

        Public Function Promise(target As String) As Boolean
            Dim s As IO.BinaryWriter = New IO.BinaryWriter(IO.File.Open(target, IO.FileMode.OpenOrCreate))
            s.Write(Me._byteData)
            s.Close()
            Return True
        End Function

        Default Public Property ByteDataFromIndex(Index As Integer) As Byte
            Get
                If Index < 0 Or Index >= Me.Length Then
                    Return 0
                End If
                Return Me._byteData(Index)
            End Get
            Set(ByVal value As Byte)
                If Index >= 0 And Index < Me.Length Then
                    Me._byteData(Index) = value
                End If
            End Set
        End Property

    End Class

    Public Function ReadBinaryLine(ByRef Reader As IO.BinaryReader) As WebString
        Dim ws As WebString = New WebString
        Dim ch As Byte
        Do Until ch = AscW(vbLf)
            ch = Reader.ReadByte()
            ws.Append({ch})
        Loop
        Return ws
    End Function

    Public Function STrim(ByVal Str As String) As String
        Dim t As StringBuilder = New StringBuilder(Trim(Str))
        While t.Length > 0 AndAlso (t(0) = vbCr Or t(0) = vbLf)
            t = t.Remove(0, 1)
        End While
        While t.Length > 0 AndAlso (t(t.Length - 1) = vbCr Or t(t.Length - 1) = vbLf)
            t = t.Remove(t.Length - 1, 1)
        End While
        Return t.ToString()
    End Function

    Public Class WebInfo
        Public Property Method As String = ""
        Public Property Path As String = ""
        Public Property HTTPVersion As String = ""
        Public Property Settings As Dictionary(Of String, String) = New Dictionary(Of String, String)
        Public Property Content As WebString = New WebString()

        Public ReadOnly Property MyBoundary As String
            Get
                If Not Settings.ContainsKey("Content-Type") Then
                    Return ""
                End If
                Dim spl As String() = Split(Settings("Content-Type"), ";")
                ' Is like [normal]; boundary=[altn]
                If spl.Count() < 2 Then
                    Return ""
                End If
                Try
                    Dim nspl As String() = Split(spl(1), "=", 2)
                    If Trim(StrConv(nspl(0), VbStrConv.Lowercase)) <> "boundary" Then
                        Return ""
                    End If
                    spl(1) = nspl(1)
                    Return Trim(spl(1))
                Catch ex As IndexOutOfRangeException
                    Return ""
                End Try
            End Get
        End Property

        Public ReadOnly Property HaveBoundary As Boolean
            Get
                Return Trim(MyBoundary) <> ""
            End Get
        End Property

        Public Structure PostInfo

            Public Structure SingleData

                Public Property Settings As Dictionary(Of String, String)
                Public Property Content As WebString

                Public ReadOnly Property FieldName As String
                    Get
                        If Not Me.Settings.ContainsKey("Content-Disposition") Then
                            Return ""
                        End If
                        Dim cd As String = Me.Settings("Content-Disposition")
                        Dim spl As String() = Split(cd, ";")
                        ' Is like [normal]; name=[name]
                        If spl.Count() < 2 Then
                            Return ""
                        End If
                        Try
                            spl(1) = Split(spl(1), "=", 2)(1)
                            Dim t As String = Trim(spl(1))
                            While t.Length > 0 AndAlso (t(0) = """" OrElse t(0) = vbCr OrElse t(0) = vbLf)
                                t = t.Remove(0, 1)
                            End While
                            While t.Length > 0 AndAlso (t(t.Length - 1) = """" OrElse t(t.Length - 1) = vbCr OrElse t(t.Length - 1) = vbLf)
                                t = t.Remove(t.Length - 1)
                            End While
                            Return t
                        Catch ex As IndexOutOfRangeException
                            Return ""
                        End Try
                    End Get
                End Property

                Public Sub New(Settings As Dictionary(Of String, String), Content As WebString)
                    Me.Settings = Settings
                    Me.Content = Content
                End Sub

                Public Sub SaveTo(Stream As IO.BinaryWriter)
                    'Stream.Write(Me.Content)
                    Stream.Write(Me.Content.ByteData)
                End Sub

            End Structure

            Public Property Data As List(Of SingleData)

        End Structure

        Public ReadOnly Property PostData As PostInfo
            Get
                ' 1. Get post data from requested data
                Dim bd As String = Me.MyBoundary
                Dim temp As PostInfo = New PostInfo
                temp.Data = New List(Of PostInfo.SingleData)
                'Dim spl As String() = Split(Me.Content.ToStringWithEncoding(), vbLf)
                Dim spl As List(Of WebString) = New List(Of WebString)
                Dim cur As WebString = New WebString
                Dim delimitor As Byte = AscW(vbLf)
                For i = 0 To Me.Content.Length - 1
                    Dim nowone As Byte = Me.Content.ByteData(i)
                    If nowone = delimitor Then
                        spl.Add(cur)
                        cur = New WebString
                    Else
                        cur.Append({nowone})
                    End If
                Next
                If cur.Length > 0 Then
                    spl.Add(cur)
                End If
                Dim current As PostInfo.SingleData = New PostInfo.SingleData
                Dim content As Boolean = False
                Dim mycontent As New WebString
                Dim debug_counter As Integer = 0
                For Each i In spl
                    debug_counter += 1
                    If i.ToStringWithEncoding().IndexOf(bd) >= 0 Then
                        'If Not IsNothing(current) Then
                        mycontent.RemoveCrLf()
                        current.Content = mycontent
                        temp.Data.Add(current)
                        mycontent = New WebString
                        'End If
                        If i(i.Length - 1) = AscW("-"c) Then
                            Exit For
                        End If
                        current = New PostInfo.SingleData
                        current.Settings = New Dictionary(Of String, String)
                        current.Content = New WebString
                        content = False
                    ElseIf STrim(i.ToStringWithEncoding()) = "" Then
                        content = True
                        'If i.Length > 0 Then
                        If content Then
                                mycontent.Append(i)
                                mycontent.Append(vbLf)
                            End If
                        'End If
                    Else
                        If Not IsNothing(current) Then
                            If content Then
                                mycontent.Append(i)
                                mycontent.Append(vbLf)
                            Else
                                Dim args As String() = Split(i.ToStringWithEncoding(), ":", 2)
                                current.Settings(args(0)) = Trim(args(1))
                            End If
                        End If
                    End If
                Next
                If temp.Data.Count > 0 Then
                    temp.Data.RemoveAt(0)
                End If
                Return temp
            End Get
        End Property

        ' Will not read content !
        Protected Sub GenerateFrom(RequestData As WebString)
            If Trim(RequestData.ToStringWithEncoding()).Length <= 0 Then
                Exit Sub
            End If
            Dim i As Integer
            Dim spl As String() = Split(RequestData.ToStringWithEncoding(), vbLf)
            Dim firstline As String = spl(0).Replace(vbCr, "")
            Dim farg As String() = Split(firstline)
            Dim mycontent As WebString = Nothing
            If farg.Count() < 3 Then
                Throw New BadWebFormatException()
            End If
            Me.Method = farg(0)
            Me.Path = farg(1)
            Me.HTTPVersion = farg(2)
            Dim content As Boolean = False
            For i = 1 To spl.Count - 1
                Dim myline As String = Trim(spl(i).Replace(vbCr, ""))
                ' Remove CR, LF
                While myline.Length > 0 AndAlso (myline(0) = vbCr Or myline(0) = vbLf)
                    myline = myline.Remove(0, 1)
                End While
                If myline.Length <= 0 Then
                    ' Main content begin!
                    Exit For
                End If
                Dim argspl As String() = Split(myline, ":", 2)
                If argspl.Count() < 2 Then
                    Continue For
                End If
                argspl(1) = Trim(argspl(1))
                Me.Settings(argspl(0)) = argspl(1)
            Next
        End Sub

        Public Sub New(RequestData As String)
            GenerateFrom(RequestData)
        End Sub

        Public EndOfLines As List(Of Char) = New List(Of Char)({vbCr, vbLf})

        ' To test if POST.
        Public Sub New(RequestStream As IO.BinaryReader)
            Try
                Dim tmp As WebString = New WebString
                Dim myLength As Long = 0
                ' Read first line for 'tmp'.
                Do
                    Try
                        Dim CurrentLine As String = ReadBinaryLine(RequestStream).ToStringWithEncoding()
                        tmp.Append(CurrentLine)
                        Dim LastChar, FirstChar As Char
                        If CurrentLine.Length > 0 Then
                            LastChar = CurrentLine(CurrentLine.Length - 1)
                            FirstChar = CurrentLine(0)
                        Else
                            LastChar = vbLf
                            FirstChar = vbLf
                        End If

                        If EndOfLines.Contains(FirstChar) And EndOfLines.Contains(LastChar) Then
                            ' An empty line!
                            ' Content must be began
                            If myLength > 0 Then
                                Me.Content = New WebString(RequestStream.ReadBytes(myLength))
                            End If
                            Exit Do
                        Else
                            ' Must be attribute or beginning.
                            Dim spl As String() = Split(CurrentLine, " ", 2)
                            If spl.Count() >= 2 And spl(0) = "Content-Length:" Then
                                myLength = Val(spl(1))
                            End If
                        End If
                    Catch ex As IO.EndOfStreamException
                        Exit Do
                    End Try
                Loop
                GenerateFrom(tmp)
            Catch ex As IO.IOException
                ShowError("Timeout! " & ex.Message)
            End Try
        End Sub

        Public Sub DebugOutput()
            Console.ForegroundColor = ConsoleColor.Magenta
            Console.Out.Write("Method: " & Me.Method)
            Console.Out.WriteLine()
            Console.Out.Write("Path: " & Me.Path)
            Console.Out.WriteLine()
            Console.Out.Write("HTTP Version: " & Me.HTTPVersion)
            Console.Out.WriteLine()
            Console.Out.Write("Parameters:")
            Console.Out.WriteLine()
            For Each i In Me.Settings
                Console.Out.Write(i.Key & " = " & i.Value)
                Console.Out.WriteLine()
            Next
            Console.Out.Write("Content:")
            Console.Out.WriteLine()
            Console.Out.Write(Me.Content)
            Console.ForegroundColor = ConsoleColor.White
        End Sub
    End Class
End Module
