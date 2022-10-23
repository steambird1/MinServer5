Imports System.Net.Sockets
Imports System.Text.Encoding
Imports System.Threading
Imports System.IO
Imports System.Net
Imports System.Text

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
    Public IsDebug As String = ""                       ' If do so, here will be --debug
    Public Port As Integer = 80                             ' Default port
    'Private lock As Threading.SpinLock = New SpinLock()

    Private Sub InitalizeContents()
        contents.Add(".html", "text/html")
        contents.Add(".blue", "text/html")
        contents.Add(".txt", "text/plain")
        contents.Add(".jpg", "image/jpeg")
        contents.Add(".png", "image/png")
        contents.Add(".htm", "text/html")
    End Sub

    Private Sub Initalize()
        InitalizeContents()
        For Each i In My.Application.CommandLineArgs
            If i = "--debug" Then
                IsDebug = "--debug"
                Continue For
            End If
            If i = "--version" Then
                ' Get its version info:
                Console.Out.Write("MinServer 5")
                Console.Out.WriteLine()
                Console.Out.Write("With BlueBetter and BluePage interpreter")
                Console.Out.WriteLine()
            End If
            Dim PortPosition As Integer = i.IndexOf("--port:")
            If PortPosition >= 0 Then
                PortPosition = Val(i.Substring(PortPosition + "--port:".Length))
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
    ' Requires '\'
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

    ' Note: data is fixed !!!
    Public ReadOnly Property InternalServerError As String = "HTTP/1.1 500 Internal Server Error" & vbCrLf & "Connection: Keep-Alive" & vbCrLf & "Content-Type: text/html" & vbCrLf & "Content-Length: 211" & vbCrLf & vbCrLf & "<html><head><title>500 - Internal Server Error</title></head><body><h1>500 Internal Server Error</h1><br /><p>The BlueBetter Program didn't provide any content to send.</p><hr /><p>MinServer 5</p></body></html>"
    Public ReadOnly Property NotFoundError As String = "HTTP/1.1 404 Not Found" & vbCrLf & "Connection: Keep-Alive" & vbCrLf & "Content-Type: text/html" & vbCrLf & "Content-Length: 159" & vbCrLf & vbCrLf & "<html><head><title>404 - Not Found</title></head><body><h1>404 Not Found</h1><br /><p>Specified path does not exist.</p><hr /><p>MinServer 5</p></body></html>"

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

    Sub RequestProcessor(Parameter As ReadWrites)
        Dim ExceptionOccured As Boolean = False
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
        Dim MyScript As String = MyScriptRaw
        ' Detect if directory
        For Each i In indexname
            MyScript = MyScriptRaw & i
            If My.Computer.FileSystem.FileExists(MyScript) Then
                Exit For
            End If
        Next
        If Not My.Computer.FileSystem.FileExists(MyScript) Then
            ShowWarning("404: " & MyScript)
            MySenderData = New WebString(NotFoundError)
            GoTo BeginWriting
        End If
        Dim MyExtension As String = GetExtension(MyScript)
        Dim DocKind As String = GetDocumentKind(MyExtension)

        Dim DirectoryToBluebetter As String = ""
        Dim ActiveExecuter As String = ""
        Dim ActiveCommander As String = ""
        Dim MySender As String = ""
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
            For Each i In MyWebInfo.Settings
                ActiveScript.Write("set receiver.attributes:" & SetQuotes(i.Key) & "=" & SetQuotes(i.Value))
                ActiveScript.WriteLine()
            Next
            ' Get content data...
            Dim MyContent As WebInfo.PostInfo = MyWebInfo.PostData
            Dim MyData = MyContent.Data
            Dim DataCounter As Integer = 0
            For Each i In MyData
                Dim TempName As String = "__postdata_" & DataCounter
                ActiveScript.Write("set " & TempName & "=new post_data")
                ActiveScript.WriteLine()
                ActiveScript.Write("set " & TempName & ".name=" & SetQuotes(i.FieldName))
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
                MyCodeContent = My.Computer.FileSystem.OpenTextFileReader(MyScript)
            Catch ex As FileNotFoundException
                ShowWarning("404: " & MyScript)
                MySenderData = NotFoundError
                GoTo BeginWriting
            End Try
            ActiveScript.Write(MyCodeContent.ReadToEnd())
            MyCodeContent.Close()
            ActiveScript.Close()
        End If

        If MyExtension = interpreter Then
            ' ... Execute BlueBetter ...
            ' Previous work has been completed!
            ' Execute!
            Shell(DirectoryToBluebetter & "\BlueBetter4.exe " & ActiveExecuter & " --const:page_mode=0 " & IsDebug, AppWinStyle.Hide, True)
            ' Write response ...
        ElseIf MyExtension = page_interpreter Then
            Shell(DirectoryToBluebetter & "\BluePage.exe " & ActiveExecuter & " --target:" & MySender & " --const:page_mode=1 " & IsDebug, AppWinStyle.Hide, True)
        Else
            ' Send the whole file
            ' To modify encoding and apply to everywhere!
            Dim WholeReader As BinaryReader = New BinaryReader(File.Open(MyScript, FileMode.Open))
            Dim WLength As Long = WholeReader.BaseStream.Length
            'Dim WholeData As Char() = WholeReader.ReadChars(WLength)
            Dim WholeData As Byte() = WholeReader.ReadBytes(WLength)
            MySenderData.Append("HTTP/1.1 200 OK")
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
NoExecuted:
            Try
                Dim MySenderStream As BinaryReader = New BinaryReader(File.Open(MySender, FileMode.Open))
                If MySenderStream.BaseStream.Length = 0 Then
                    Throw New FileNotFoundException
                End If
                MySenderData.Append(MySenderStream.ReadBytes(MySenderStream.BaseStream.Length))
                MySenderStream.Close()
            Catch ex As FileNotFoundException
                ShowWarning("500: The program doesn't return anything to return!")
                MyRW.Writer.Write(InternalServerError)
                MyRW.Writer.Write(vbLf)
                ExceptionOccured = True
            End Try
            Try
                If My.Computer.FileSystem.FileExists(ActiveCommander) Then
                    Dim MyCommanderStream As StreamReader = My.Computer.FileSystem.OpenTextFileReader(ActiveCommander)
                    While Not MyCommanderStream.EndOfStream
                        Dim CurrentCommand As String = MyCommanderStream.ReadLine()
                        Dim CurrentSplit As String() = Split(CurrentCommand, " ", 2)
                        If CurrentSplit.Count() < 2 Then
                            Continue While
                        End If
                        Select Case CurrentSplit(0)
                            Case "keep"
                                ' Keep command
                                ' keep [a]=[b]
                                Dim Keeper As String() = Split(CurrentSplit(1), "=", 2)
                                keepedata(Keeper(0)) = Keeper(1)
                        End Select
                    End While
                    MyCommanderStream.Close()
                    My.Computer.FileSystem.DeleteFile(ActiveCommander)
                End If
            Catch ex As Exception
                ShowWarning("Caution: Commander may be not executed correctly: " & ex.ToString())
            End Try
            Try
                My.Computer.FileSystem.DeleteDirectory(DirectoryToBluebetter, FileIO.DeleteDirectoryOption.DeleteAllContents)
                My.Computer.FileSystem.DeleteFile(MySender)
                For Each i In Attachments
                    My.Computer.FileSystem.DeleteFile(i)
                Next
            Catch ex As Exception
                ShowWarning("Caution: Commander may be not executed correctly: " & ex.ToString())
            End Try
        End If

BeginWriting: If Not ExceptionOccured Then
            MyRW.Writer.Write(MySenderData.ByteData)
            MyRW.Writer.Write(vbLf)
            MyRW.Writer.Flush()
        End If
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

        listener = New TcpListener(local, Port)
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
