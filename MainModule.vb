﻿Imports System.Net.Sockets
Imports System.Text.Encoding
Imports System.Threading
Imports System.IO
Imports System.Net
Imports System.Text
Imports System.Text.RegularExpressions

Module MainModule

    Public local As IPAddress = IPAddress.Parse("127.0.0.1")
    Public listener As TcpListener
    Public islisten As Boolean = True
    Private isbusy As Integer = 0
    Public indexname As List(Of String) = New List(Of String)({"", "index.html", "index.htm", "index.blue", "index.bp"})
    Public contents As Dictionary(Of String, String) = New Dictionary(Of String, String)
    Public Const interpreter As String = ".blue"
    Public Const page_interpreter As String = ".bp"
    Public keepedata As Dictionary(Of String, String) = New Dictionary(Of String, String)
    Public ConvertableFiles As HashSet(Of String) = New HashSet(Of String)
    Public IsDebug As String = ""                       ' If do so, here will be --debug
    Public Port As Integer = 80                             ' Default port
    Public AlwaysRunConverts As Boolean = False
    Public Const PostBackEntry As String = "MinServerPostBack"
    Public DisallowLists As List(Of String) = New List(Of String) ' Providing RegEx!
    'Private lock As Threading.SpinLock = New SpinLock()

    Private Sub InitalizeContents()
        contents.Add(".html", "text/html")
        contents.Add(".blue", "text/html")
        contents.Add(".bp", "text/html")
        contents.Add(".txt", "text/plain")
        contents.Add(".jpg", "image/jpeg")
        contents.Add(".wav", "audio/wav")
        contents.Add(".gif", "image/gif")
        contents.Add(".png", "image/png")
        contents.Add(".htm", "text/html")
        contents.Add(".css", "text/css")
        contents.Add(".js", "text/javascript")
        contents.Add(".pdf", "application/pdf")
        ConvertableFiles.Add(".html")
        ConvertableFiles.Add(".htm")
    End Sub

    Private Sub Initalize()
        InitalizeContents()
        For Each i In My.Application.CommandLineArgs
            Dim SplResult() As String = Split(i, ":", 2)
            If SplResult.Count() < 1 Then
                Continue For
            End If
            If i = "--debug" Then
                IsDebug = "--debug"
                Console.ForegroundColor = ConsoleColor.Magenta
                Console.Out.Write("*** Server is in debug mode ***")
                Console.ForegroundColor = ConsoleColor.White
                Continue For
            ElseIf i = "--always-convert" Then
                AlwaysRunConverts = True
            ElseIf i = "--version" Then
                ' Get its version info:
                Console.Out.Write("MinServer 5")
                Console.Out.WriteLine()
                Console.Out.Write("With BlueBetter and BluePage interpreter")
                Console.Out.WriteLine()
                Console.Out.Write("Version 1.14a")
                Console.Out.WriteLine()
                End
            ElseIf SplResult(0) = "--port" Then
                If SplResult.Count() < 2 Then
                    Continue For
                End If
                Port = Val(SplResult(1))
            ElseIf SplResult(0) = "--extension" Then
                If SplResult.Count() < 2 Then
                    Continue For
                End If
                Dim ConAlias() As String = Split(SplResult(1), "=", 2)
                If ConAlias.Count() < 2 Then
                    Continue For
                End If
                contents.Add(ConAlias(0), ConAlias(1))
            ElseIf SplResult(0) = "--converts" Then
                If SplResult.Count() < 2 Then
                    Continue For
                End If
                ConvertableFiles.Add(SplResult(1))
            ElseIf SplResult(0) = "--disallow" Then
                If SplResult.Count() < 2 Then
                    Continue For
                End If
                DisallowLists.Add(SplResult(1))
            ElseIf SplResult(0) = "--500" Then
                InternalServerPath = SplResult(1)
            ElseIf SplResult(0) = "--404" Then
                NotFoundPath = SplResult(1)
            ElseIf SplResult(0) = "--403" Then
                ForbiddenPath = SplResult(1)
            End If

        Next
    End Sub

    Public Structure ReadWrites
        Public Reader As BinaryReader
        Public Writer As BinaryWriter
        Public Client As TcpClient

        Public Sub New(Reader As BinaryReader, Writer As BinaryWriter, Client As TcpClient)
            Me.Reader = Reader
            Me.Writer = Writer
            Me.Client = Client
        End Sub
    End Structure

    Private r As Random = New Random
    ''' <summary>
    ''' Current environmental directory. Has '\' at the end of the string.
    ''' </summary>
    Private pather As String = Environment.CurrentDirectory

    Private Function GenerateRandom(prefix As String)
        Dim num As Integer = 0
        While My.Computer.FileSystem.FileExists(prefix & num) Or My.Computer.FileSystem.DirectoryExists(prefix & num)
            num = r.Next()
        End While
        Return prefix & num
    End Function

    Private ReadOnly Property FileToCheck As List(Of String) = New List(Of String)({"WebHeader.blue", "BlueBetter4.exe", "bmain.blue", "BluePage.exe", "algo.blue", "BluePage.blue"})
    Private ReadOnly Property BlueESS As List(Of String) = FileToCheck

    Public Function SetQuotes(data As String) As String
        Dim s As StringBuilder = New StringBuilder(data)
        s.Replace(vbCr, "")
        s.Replace(vbLf, "\n")
        s.Replace("\", "\\")
        s.Replace("""", "\""")
        ' & """"
        s.Insert(0, """")
        s.Append("""")
        Return s.ToString()
    End Function

    Public Property DefaultServerErrorPath As String = pather & "\500.html"   ' If actives twice call this.
    Public Property InternalServerPath As String = pather & "\500.html"
    Public Property NotFoundPath As String = pather & "\404.html"
    Public Property ForbiddenPath As String = pather & "\403.html"

    Sub AsyncRequestProcessor(Parameter As IAsyncResult)
        Dim tcp As TcpListener = CType(Parameter.AsyncState, TcpListener)
        Dim current As TcpClient = tcp.EndAcceptTcpClient(Parameter)
        Do Until current.Connected
            Thread.Yield()
        Loop
        isbusy = True
        Dim stream As IO.Stream
        Dim sread As BinaryReader
        Dim swrite As BinaryWriter
        stream = current.GetStream()
        stream.ReadTimeout = 10000
        sread = New BinaryReader(stream)
        swrite = New BinaryWriter(stream)
        Try
            RequestProcessor(New ReadWrites(sread, swrite, current))
            stream.Close()
        Catch ex As IO.IOException
            ShowError("Connection reset by server: " & ex.Message)
        End Try
    End Sub

    Public Function GetExtension(ByVal MyScript As String)
        Return MyScript.Substring(MyScript.LastIndexOf("."c))
    End Function

    Public Function GetDocumentKind(Extension As String)
        If Not contents.ContainsKey(Extension) Then
            Return "text/plain"
        Else
            Return contents(Extension)
        End If
    End Function

    Public Function GetRealPath(MyScriptRaw As String) As String
        Dim MyScript As String
        For Each i In indexname
            MyScript = MyScriptRaw & i
            If My.Computer.FileSystem.FileExists(MyScript) Then
                Return MyScript
            End If
        Next
        Return Nothing
    End Function

    Sub RequestProcessor(Parameter As ReadWrites)
        'Dim ExceptionOccured As Boolean = False
        Dim NotFounds As Boolean = False
        Dim MyRW As ReadWrites = Parameter
        Dim MySenderData As New WebString
        ' Process here...
        Dim MyWebInfo As WebInfo = New WebInfo(MyRW.Reader)
        If Trim(MyWebInfo.Path) = "" Then
            MyRW.Client.Close()
            Exit Sub
        End If
        If MyWebInfo.Path(0) = "/" Or MyWebInfo.Path(0) = "\" Then
            MyWebInfo.Path = MyWebInfo.Path.Remove(0, 1)
        End If
        ' Create BlueBetter Environment under temp directory
        Dim t_add As String = Environ("temp") & "\"
        Dim FilePath As String = MyWebInfo.Path
        Dim FilePathSeeker As Integer = FilePath.IndexOf("?")
        If FilePathSeeker >= 0 Then
            ' Mustn't include '?' component:
            FilePath = FilePath.Substring(0, FilePathSeeker)
        End If
        Dim MyScriptRaw As String = pather & "\" & FilePath
        Dim MyScript As String = GetRealPath(MyScriptRaw)
        Dim ExternalOption As String = ""
        Dim IsServerError As Boolean = False
        If IsNothing(MyScript) Then
            ShowWarning("404: " & MyWebInfo.Path)
            'MySenderData = New WebString(NotFoundError)
            'GoTo BeginWriting
NotFoundError: MyScript = NotFoundPath
            ExternalOption = " --const:ERROR=404"
        End If
        ' Check 403
        For Each i In DisallowLists
            If Regex.IsMatch(MyScript, i, RegexOptions.IgnoreCase) Then
                ShowWarning("403: " & MyWebInfo.Path & " (Satisfies: " & i & ")")
                MyScript = ForbiddenPath
                ExternalOption = " --const:ERROR=403"
            End If
        Next
NormalResolver: Dim MyExtension As String = GetExtension(MyScript)
        Dim DocKind As String = GetDocumentKind(MyExtension)
        Dim MyScriptDir As String = ""
        Try
            MyScriptDir = My.Computer.FileSystem.GetParentPath(MyScript) & "\"  ' MyScript: selected
        Catch ex As Exception

        End Try
        If Trim(MyScriptDir) = "" Then
            MyScriptDir = pather
        End If
        Dim DirectoryToBluebetter As String = ""
        Dim ActiveExecuter As String = ""
        Dim ActiveCommander As String = ""
        Dim MySender As String = ""
        Dim UTFTarget As String = ""
        Dim ActiveScript As StreamWriter = Nothing
        Dim MyHeader As StreamReader = Nothing
        Dim Attachments As List(Of String) = New List(Of String)

        DirectoryToBluebetter = GenerateRandom(t_add)
        ActiveCommander = DirectoryToBluebetter & "\orders.txt"

        If MyExtension = interpreter Then
            ActiveExecuter = DirectoryToBluebetter & "\execute.blue"
        ElseIf MyExtension = page_interpreter Then
            ActiveExecuter = DirectoryToBluebetter & "\execute.bp"
        End If

        Dim SelfPost As String = MyWebInfo.Path
        Dim IsPostBack As String = "0"
        If MyWebInfo.Settings.ContainsKey(PostBackEntry) Then
            IsPostBack = MyWebInfo.Settings(PostBackEntry)
        End If

        ' Prepare common environment
        If MyExtension = interpreter OrElse MyExtension = page_interpreter Then
            My.Computer.FileSystem.CreateDirectory(DirectoryToBluebetter)
            For Each i In BlueESS
                FileCopy(pather & "\" & i, DirectoryToBluebetter & "\" & i)
            Next
            ActiveScript = My.Computer.FileSystem.OpenTextFileWriter(ActiveExecuter, False, Text.Encoding.Default)
            MyHeader = My.Computer.FileSystem.OpenTextFileReader(pather & "\WebHeader.blue")
            ' TODO: '<?blue' for page
            If MyExtension = page_interpreter Then
                ActiveScript.Write("<?blue")
                ActiveScript.WriteLine()
            End If
            ActiveScript.Write(MyHeader.ReadToEnd())
            MyHeader.Close()
            ActiveScript.WriteLine()
            ActiveScript.Write("keeper._init " & SetQuotes(ActiveCommander))
            ActiveScript.WriteLine()
            For Each i In keepedata
                ActiveScript.Write("keeper._set " & SetQuotes(i.Key) & "," & SetQuotes(i.Value))
                ActiveScript.WriteLine()
            Next
            ActiveScript.WriteLine()
            ActiveScript.Write("set receiver.path=" & SetQuotes(MyWebInfo.Path))
            ActiveScript.WriteLine()
            ActiveScript.Write("set receiver.http_version=" & SetQuotes(MyWebInfo.HTTPVersion))
            ActiveScript.WriteLine()
            ActiveScript.Write("set receiver.method=" & SetQuotes(MyWebInfo.Method))
            ActiveScript.WriteLine()
            ActiveScript.Write("set receiver.server_dir=" & SetQuotes(pather))
            ActiveScript.WriteLine()
            For Each i In MyWebInfo.Settings
                ActiveScript.Write("set receiver.attributes:" & SetQuotes(i.Key) & "=" & SetQuotes(i.Value))
                ActiveScript.WriteLine()
            Next
            ' Get content data...
            If MyWebInfo.HaveBoundary Then
                Dim MyContent As WebInfo.PostInfo = MyWebInfo.PostData
                Dim MyData = MyContent.Data
                Dim DataCounter As Integer = 0
                For Each i In MyData
                    Dim TempName As String = "__postdata_" & DataCounter
                    ActiveScript.Write("set " & TempName & "=new post_data")
                    ActiveScript.WriteLine()
                    ActiveScript.Write("set " & TempName & ".name=" & SetQuotes(i.FieldName))
                    ActiveScript.WriteLine()
                    ActiveScript.Write("set " & TempName & ".file_name=" & SetQuotes(i.FileName))
                    ActiveScript.WriteLine()
                    ' Get file for it
                    Dim CurrentFilename As String = GenerateRandom(DirectoryToBluebetter)
                    Dim CurrentStream As BinaryWriter = New BinaryWriter(File.Open(CurrentFilename, FileMode.Create), Encoding.Default)
                    Attachments.Add(CurrentFilename)
                    i.SaveTo(CurrentStream)
                    CurrentStream.Close()
                    ActiveScript.Write("set " & TempName & ".myfile=" & SetQuotes(CurrentFilename))
                    ActiveScript.WriteLine()
                    For Each j In i.Settings
                        ActiveScript.Write("set " & TempName & ".attributes:" & SetQuotes(j.Key) & "=" & SetQuotes(j.Value))
                        ActiveScript.WriteLine()
                    Next
                    ActiveScript.Write("run receiver.content.append " & TempName)
                    ActiveScript.WriteLine()
                    DataCounter += 1
                Next
                ActiveScript.Write("set receiver.ispost=true")
                ActiveScript.WriteLine()
            Else
                Dim ReceiverFilename As String = GenerateRandom(DirectoryToBluebetter)
                Dim ReceiverStream As BinaryWriter = New BinaryWriter(File.Open(ReceiverFilename, FileMode.Create), Encoding.Default)
                Attachments.Add(ReceiverFilename)
                ReceiverStream.Write(MyWebInfo.Content.ByteData)
                ReceiverStream.Close()
                ActiveScript.Write("set receiver.content=new post_data")
                ActiveScript.WriteLine()
                ActiveScript.Write("set receiver.content.myfile=" & SetQuotes(ReceiverFilename))
                ActiveScript.WriteLine()
                ActiveScript.Write("set receiver.ispost=false")
                ActiveScript.WriteLine()
            End If
            ' Always have, but whether or not it will be written depends
            ' on whether it's BluePage.
            MySender = GenerateRandom(DirectoryToBluebetter)
            If MyExtension = interpreter Then
                ActiveScript.Write("run sender._setfiles " & SetQuotes(MySender))
                ActiveScript.WriteLine()
            ElseIf MyExtension = page_interpreter Then
                ActiveScript.Write("?>")
                ActiveScript.WriteLine()
            End If
            ' Requires ANSI for BlueBetter 4

            ' Read file to run
            Dim MyCodeContent As StreamReader = Nothing
            Try
                ' Now we use UTF-8 here!
                MyCodeContent = My.Computer.FileSystem.OpenTextFileReader(MyScript, Encoding.UTF8)
            Catch ex As FileNotFoundException
                ShowWarning("404: " & MyScript)
                GoTo NotFoundError
            End Try
            Dim MyCodeData = MyCodeContent.ReadToEnd()
            ActiveScript.Write(MyCodeData)
            MyCodeContent.Close()
            ActiveScript.Close()
        End If

        If MyExtension = interpreter Then
            ' ... Execute BlueBetter ...
            ' Previous work has been completed!
            ' Execute!
            Shell(DirectoryToBluebetter & "\BlueBetter4.exe " & ActiveExecuter & " --include-from:" & MyScriptDir & " --const:page_mode=0 " & IsDebug & ExternalOption, AppWinStyle.Hide, True)
            ' Write response ...
        ElseIf MyExtension = page_interpreter Then
            ' Supports UTF.
            UTFTarget = GenerateRandom(DirectoryToBluebetter)
            Shell(DirectoryToBluebetter & "\BluePage.exe " & ActiveExecuter & " --target:" & MySender & " --include-from:" & MyScriptDir & " --const:page_mode=1 --const:SELF_POST=" & SelfPost & " --const:IS_POSTBACK=" & IsPostBack & " --const:UTF_TARGET=" & UTFTarget & " " & IsDebug & ExternalOption, AppWinStyle.Hide, True)
        Else
            ' Send the whole file
            ' To modify encoding and apply to everywhere!
            Dim WholeReader As BinaryReader = New BinaryReader(File.Open(MyScript, FileMode.Open))
            Dim WLength As Long = WholeReader.BaseStream.Length
            'Dim WholeData As Char() = WholeReader.ReadChars(WLength)
            Dim WholeData As Byte() = WholeReader.ReadBytes(WLength)
            If NotFounds Then
                MySenderData.Append("HTTP/1.1 404 Not Found")
            ElseIf IsServerError Then
                MySenderData.Append("HTTP/1.1 500 Internal Server Error")
            Else
                MySenderData.Append("HTTP/1.1 200 OK")
            End If
            MySenderData.AppendLine()
            MySenderData.Append("Content-Type: " & DocKind)
            MySenderData.AppendLine()
            MySenderData.Append("Content-Length: " & WLength)
            MySenderData.AppendLine()
            MySenderData.AppendLine()
            MySenderData.Append(WholeData)
            ' Promisor
            'Dim Promisor As BinaryWriter = New BinaryWriter(File.Open(pather & "\Promise.bin", FileMode.Create), Encoding.Default)
            'Promisor.Write(MySenderData.ByteData)
            'Promisor.Close()
            ' End of promisor test
            WholeReader.Close()
        End If

        ' Special processor for interpreters
        If MyExtension = interpreter OrElse MyExtension = page_interpreter Then
NoExecuted: ' This is a connection between BluePage and host server.
            Dim DoUTFConvert As Boolean = AlwaysRunConverts
            Try
                If My.Computer.FileSystem.FileExists(ActiveCommander) Then
                    Dim MyCommanderStream As StreamReader = My.Computer.FileSystem.OpenTextFileReader(ActiveCommander, [Default])
                    While Not MyCommanderStream.EndOfStream
                        Dim CurrentCommand As String = MyCommanderStream.ReadLine()
                        Dim CurrentSplit As String() = Split(CurrentCommand, " ", 2)
                        Select Case CurrentSplit(0)
                            Case "keep"
                                ' Keep command
                                ' keep [a]=[b]
                                If CurrentSplit.Count() < 2 Then
                                    ShowWarning("Caution: Incorrect format of 'keep' command: " & CurrentCommand)
                                    Continue While
                                End If
                                Dim Keeper As String() = Split(CurrentSplit(1), "=", 2)
                                keepedata(Keeper(0)) = Keeper(1)
                            Case "do_convert"
                                ' Convert requirement (ANSI -> UTF-8)
                                If MyExtension = page_interpreter Then
                                    DoUTFConvert = True
                                Else
                                    ShowWarning("Caution: Cannot do UTF Convert for BlueBetter interpreter")
                                End If

                        End Select
                    End While
                    MyCommanderStream.Close()
                    My.Computer.FileSystem.DeleteFile(ActiveCommander)
                End If
            Catch ex As Exception
                ShowWarning("Caution: Commander may be not executed correctly: " & ex.ToString())
            End Try
            Try
                Dim MySenderStream As BinaryReader = New BinaryReader(File.Open(MySender, FileMode.Open))
                If MySenderStream.BaseStream.Length = 0 Then
                    Throw New FileNotFoundException
                End If
                MySenderData.Append(MySenderStream.ReadBytes(MySenderStream.BaseStream.Length))
                If DoUTFConvert Then
                    Dim MyContentStream As BinaryReader = New BinaryReader(File.Open(UTFTarget, FileMode.Open))
                    Dim AllContent = MyContentStream.ReadBytes(MyContentStream.BaseStream.Length)
                    Dim OriginChars = Encoding.Default.GetChars(AllContent)
                    Dim UTFContent = Encoding.UTF8.GetBytes(OriginChars)
                    Dim UTFLength = UTFContent.Count()
                    MySenderData.Append(New WebString("Content-Length: " & UTFLength & vbCrLf & vbCrLf))
                    MySenderData.Append(UTFContent)
                    MyContentStream.Close()
                End If
                MySenderStream.Close()
            Catch ex As FileNotFoundException
                ShowWarning("500: The program doesn't return anything to return!")
                If IsServerError Then
                    MyScript = DefaultServerErrorPath
                Else
                    MyScript = InternalServerPath
                End If
                IsServerError = True
                ExternalOption = " --const:ERROR=500"
                GoTo NormalResolver
            End Try
            Try
                My.Computer.FileSystem.DeleteDirectory(DirectoryToBluebetter, FileIO.DeleteDirectoryOption.DeleteAllContents)
                My.Computer.FileSystem.DeleteFile(MySender)
                If DoUTFConvert Then
                    My.Computer.FileSystem.DeleteFile(UTFTarget)
                End If
                For Each i In Attachments
                    My.Computer.FileSystem.DeleteFile(i)
                Next
            Catch ex As Exception
                ShowWarning("Caution: Commander may be not executed correctly: " & ex.ToString())
            End Try
        End If

BeginWriting: ' Always runs
        MyRW.Writer.Write(MySenderData.ByteData)
        MyRW.Writer.Write(vbLf)
        MyRW.Writer.Flush()
        MyRW.Reader.Close()
        MyRW.Writer.Close()
FinishWriting: MyRW.Client.Close()
    End Sub

    Sub DebugStart()
        'Dim s As WebString = New WebString("ab")
        's.RemoveAt(0)
        's.RemoveAt(s.Length - 1)
        'MsgBox(s.ToStringWithEncoding())
        'End
    End Sub

    Sub Main()
        Initalize()
        DebugStart()

        For Each i In FileToCheck
            If Not My.Computer.FileSystem.FileExists(pather & "\" & i) Then
                ShowError("File not found: " & i)
                Console.Out.WriteLine()
                ShowError("Server cannot continue. Halted! . . .")
                Do
                    Thread.Yield()
                Loop
            End If
        Next


        Console.Out.WriteLine()

        listener = New TcpListener(IPAddress.Any, Port)
        Try
            listener.Start()
        Catch ex As SocketException
            ShowError("Server cannot listen. Halted! . . .")
            Do
                Thread.Yield()
            Loop
        End Try
        Dim currentBus As Integer = 0
        Dim output As Boolean = False
        isbusy = False
        ShowStatus("* Server started *")
        Do
            'Dim stream As NetworkStream
            'Dim sread As StreamReader
            'Dim swrite As StreamWriter
            'Dim current As TcpClient
            Dim thr As Thread = New Thread(New ParameterizedThreadStart(AddressOf RequestProcessor))
            Dim taken As Boolean = False
            Dim currentAsync As IAsyncResult = listener.BeginAcceptTcpClient(New AsyncCallback(AddressOf AsyncRequestProcessor), listener)
            ' Wait ...
            Do Until isbusy
                Thread.Yield()
            Loop
            isbusy = False
            output = False
            'stream = current.GetStream()
            'stream.ReadTimeout = 10000
            'stream.WriteTimeout = 10000
            'sread = New StreamReader(stream)
            'swrite = New StreamWriter(stream)
            'thr.Name = "HTTP Thread"
            'thr.Start(New ReadWrites(sread, swrite, current))
            'stream.Close()  '???
        Loop
    End Sub

End Module
