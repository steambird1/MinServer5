﻿Imports System.Net.Sockets
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
    Public indexname As List(Of String) = New List(Of String)({"", "index.html", "index.htm", "index.blue"})
    Public contents As Dictionary(Of String, String) = New Dictionary(Of String, String)
    Public Const interpreter As String = ".blue"
    'Private lock As Threading.SpinLock = New SpinLock()

    Private Sub InitalizeContents()
        contents.Add(".html", "text/html")
        contents.Add(".blue", "text/html")
        contents.Add(".txt", "text/plain")
        contents.Add(".jpg", "image/jpeg")
    End Sub

    Private Sub Initalize()
        InitalizeContents()
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

    Private ReadOnly Property FileToCheck As List(Of String) = New List(Of String)({"WebHeader.blue", "BlueBetter4.exe", "bmain.blue"})
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
    Public ReadOnly Property InternalServerError As String = "HTTP/1.1 500 Internal Server Error" & vbCrLf & "Connection: Close" & vbCrLf & "Content-Type: text/html" & vbCrLf & "Content-Length: 211" & vbCrLf & vbCrLf & "<html><head><title>500 - Internal Server Error</title></head><body><h1>500 Internal Server Error</h1><br /><p>The BlueBetter Program didn't provide any content to send.</p><hr /><p>MinServer 5</p></body></html>"
    Public ReadOnly Property NotFoundError As String = "HTTP/1.1 404 Not Found" & vbCrLf & "Connection: Close" & vbCrLf & "Content-Type: text/html" & vbCrLf & "Content-Length: 159" & vbCrLf & vbCrLf & "<html><head><title>404 - Not Found</title></head><body><h1>404 Not Found</h1><br /><p>Specified path does not exist.</p><hr /><p>MinServer 5</p></body></html>"

    Sub AsyncRequestProcessor(Parameter As IAsyncResult)
        isbusy = True
        Dim tcp As TcpListener = CType(Parameter.AsyncState, TcpListener)
        Dim current As TcpClient = tcp.EndAcceptTcpClient(Parameter)
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
        Dim MyScriptRaw As String = pather & "\" & MyWebInfo.Path
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

        If MyExtension = interpreter Then
            ' ... Execute BlueBetter ...
            Dim DirectoryToBluebetter As String = GenerateRandom(t_add)
            My.Computer.FileSystem.CreateDirectory(DirectoryToBluebetter)
            For Each i In BlueESS
                FileCopy(pather & "\" & i, DirectoryToBluebetter & "\" & i)
            Next
            ' Requires ANSI for BlueBetter 4
            Dim ActiveExecuter As String = DirectoryToBluebetter & "\execute.blue"
            Dim ActiveScript As StreamWriter = My.Computer.FileSystem.OpenTextFileWriter(ActiveExecuter, False, Text.Encoding.Default)
            Dim MyHeader As StreamReader = My.Computer.FileSystem.OpenTextFileReader(pather & "\WebHeader.blue")
            ActiveScript.Write(MyHeader.ReadToEnd())
            MyHeader.Close()
            ActiveScript.WriteLine()
            ActiveScript.Write("set receiver.path=" & SetQuotes(MyWebInfo.Path))
            ActiveScript.WriteLine()
            ActiveScript.Write("set receiver.http_version=" & SetQuotes(MyWebInfo.HTTPVersion))
            ActiveScript.WriteLine()
            ActiveScript.Write("set receiver.method=" & SetQuotes(MyWebInfo.Method))
            ActiveScript.WriteLine()
            For Each i In MyWebInfo.Settings
                ActiveScript.Write("set receiver.attributes#" & SetQuotes(i.Key) & "=" & SetQuotes(i.Value))
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
                ' Get file for it
                Dim CurrentFilename As String = GenerateRandom(DirectoryToBluebetter)
                Dim CurrentStream As BinaryWriter = New BinaryWriter(File.Open(CurrentFilename, FileMode.Create), Encoding.Default)
                i.SaveTo(CurrentStream)
                CurrentStream.Close()
                ActiveScript.Write("set " & TempName & ".myfile=" & SetQuotes(CurrentFilename))
                ActiveScript.WriteLine()
                For Each j In i.Settings
                    ActiveScript.Write("set " & TempName & ".attributes#" & SetQuotes(j.Key) & "=" & SetQuotes(j.Value))
                    ActiveScript.WriteLine()
                Next
                ActiveScript.Write("run receiver.content.append " & TempName)
                ActiveScript.WriteLine()
                DataCounter += 1
            Next
            Dim MySender As String = GenerateRandom(DirectoryToBluebetter)
            ActiveScript.Write("run sender._setfiles " & SetQuotes(MySender))
            ActiveScript.WriteLine()
            ActiveScript.Write("# Normal code -----")
            ActiveScript.WriteLine()
            ActiveScript.WriteLine()
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
            ' Execute!
            Shell(DirectoryToBluebetter & "\BlueBetter4.exe " & ActiveExecuter, AppWinStyle.Hide, True)
            ' Write response
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
            My.Computer.FileSystem.DeleteDirectory(DirectoryToBluebetter, FileIO.DeleteDirectoryOption.DeleteAllContents)
            My.Computer.FileSystem.DeleteFile(MySender)
        Else
            ' Send the whole file
            'Dim WholeReader As StreamReader = New StreamReader(File.OpenRead(MyScript))
            'Dim WRead As String = WholeReader.ReadToEnd()
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

BeginWriting: If Not ExceptionOccured Then
            MyRW.Writer.Write(MySenderData.ByteData)
            MyRW.Writer.Flush()
        End If
        MyRW.Reader.Close()
        MyRW.Writer.Close()
FinishWriting: MyRW.Client.Close()
    End Sub


    Sub Main()
        Initalize()

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

        ShowStatus("* Server started *")
        Console.Out.WriteLine()

        listener = New TcpListener(local, 80)
        listener.Start()
        Dim currentBus As Integer = 0
        Dim output As Boolean = False
        isbusy = False
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
