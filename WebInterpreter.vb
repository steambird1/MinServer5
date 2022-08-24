' Load WEB Data.

Imports System.Text

Module WebInterpreter

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
                ' Is like [normal]; [altn]
                If spl.Count() < 2 Then
                    Return ""
                End If
                Return Trim(spl(1))
            End Get
        End Property

        Public Structure PostInfo

            Public Structure SingleData
                Public Property Settings As Dictionary(Of String, String)
                Public Property Content As WebString

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
                Dim spl As String() = Split(Me.Content.ToStringWithEncoding(), vbLf)
                Dim current As PostInfo.SingleData = Nothing
                Dim content As Boolean = False
                Dim mycontent As New WebString
                For Each i In spl
                    If i.IndexOf(bd) > 0 Then
                        If Not IsNothing(current) Then
                            current.Content = mycontent.ToString()
                            mycontent = New WebString
                        End If
                        If i(i.Length - 1) = "-" Then
                            Exit For
                        End If
                        current = New PostInfo.SingleData
                        content = False
                    ElseIf Trim(i) = "" Then
                        content = True
                    Else
                        If Not IsNothing(current) Then
                            If content Then
                                mycontent.Append(i)
                                mycontent.Append(vbLf)
                            Else
                                Dim args As String() = Split(i, ":", 2)
                                current.Settings(args(0)) = trim(args(1))
                            End If
                        End If
                    End If
                Next
                Return temp
            End Get
        End Property

        ' Only for GET method now.

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
                If content And (Not IsNothing(mycontent)) Then
                    mycontent.Append(spl(i))
                    mycontent.Append(vbLf)
                End If
                Dim myline As String = Trim(spl(i).Replace(vbCr, ""))
                If myline.Length < 0 Then
                    ' Main content begin!
                    content = True
                    Dim slen As Integer = 1
                    If Me.Settings.ContainsKey("Content-Length") Then
                        slen = Val(Me.Settings("Content-Length"))
                    End If
                    mycontent = New WebString(slen)
                End If
                Dim argspl As String() = Split(myline, ":", 2)
                If argspl.Count() < 2 Then
                    Continue For
                End If
                argspl(1) = Trim(argspl(1))
                Me.Settings(argspl(0)) = argspl(1)
            Next
            Console.Out.Write("End of analyze...")
            Console.Out.WriteLine()
        End Sub

        Public Sub New(RequestData As String)
            GenerateFrom(RequestData)
        End Sub

        Public EndOfLines As List(Of Char) = New List(Of Char)({vbCr, vbLf})

        ' To test if POST.
        Public Sub New(RequestStream As IO.BinaryReader)
            ' Test
            Console.Out.Write("WebInfo.New (IO.StreamReader)")
            Console.Out.WriteLine()
            ' End
            Try
                Dim tmp As WebString = New WebString
                Dim myLength As Long = 0
                ' Read first line for 'tmp'.
                Dim CurrentData As Byte()
                Do
                    Try
                        Dim CurrentLine As String = ReadBinaryLine(RequestStream).ToStringWithEncoding()
                        Console.Out.Write(CurrentLine) '  Console.Out.WriteLine()
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
                                ReDim CurrentData(myLength + 1)
                                CurrentData = RequestStream.ReadBytes(myLength)
                                tmp.Append(CurrentData)
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
                Console.Out.Write("Timeout!")
                Console.Out.WriteLine()
            End Try
        End Sub

        Public Sub DebugOutput()
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
        End Sub
    End Class
End Module
