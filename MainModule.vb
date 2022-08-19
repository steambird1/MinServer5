Imports System.Net.Sockets
Imports System.Threading
Imports System.IO
Imports System.Net
Imports System.Text

Module MainModule

    Public local As IPAddress = IPAddress.Parse("127.0.0.1")
    Public listener As TcpListener
    Public islisten As Boolean = True

    Public Structure ReadWrites
        Public Reader As StreamReader
        Public Writer As StreamWriter
        Public Client As TcpClient

        Public Sub New(Reader As StreamReader, Writer As StreamWriter, Client As TcpClient)
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
        s.Replace("""", "\""")
        ' & """"
        s.Insert(0, """")
        s.Append("""")
        Return s.ToString()
    End Function

    ' Note: data is fixed !!!
    Public ReadOnly Property InternalServerError As String = "HTTP/1.1 500 Internal Server Error" & vbCrLf & "Connection: Close" & vbCrLf & "Content-Type: text/html" & vbCrLf & "Content-Length: 211" & vbCrLf & vbCrLf & "<html><head><title>500 - Internal Server Error</title></head><body><h1>500 Internal Server Error</h1><br /><p>The BlueBetter Program didn't provide any content to send.</p><hr /><p>MinServer 5</p></body></html>"

    Sub RequestProcessor(Parameter As Object)
        Dim MyRW As ReadWrites = Parameter
        ' Process here...
        Dim MyWebInfo As WebInfo = New WebInfo(MyRW.Reader)
        ' Create BlueBetter Environment under temp directory
        Dim t_add As String = Environ("temp") & "\"
        Dim DirectoryToBluebetter As String = GenerateRandom(t_add)
        For Each i In BlueESS
            FileCopy(pather & "\" & i, DirectoryToBluebetter)
        Next
        ' Requires ANSI for BlueBetter 4
        Dim ActiveExecuter As String = DirectoryToBluebetter & "\execute.blue"
        Dim ActiveScript As StreamWriter = My.Computer.FileSystem.OpenTextFileWriter(ActiveExecuter, False, Text.Encoding.Default)
        Dim MyHeader As StreamReader = My.Computer.FileSystem.OpenTextFileReader(pather & "\WebHeader.blue")
        Dim MyScript As String = pather & "\" & MyWebInfo.Path
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
            Dim CurrentStream As BinaryWriter = New BinaryWriter(File.Open(CurrentFilename, FileMode.Create))
            i.SaveTo(CurrentStream)
            CurrentStream.Close()
            ActiveScript.Write("set " & TempName & ".myfile=" & SetQuotes(CurrentFilename))
            ActiveScript.WriteLine()
            For Each j In i.Settings
                ActiveScript.Write("set " & TempName & ".attributes#" & SetQuotes(j.Key) & "=" & SetQuotes(j.Value))
                ActiveScript.WriteLine()
            Next
            DataCounter += 1
        Next
        Dim MySender As String = GenerateRandom(DirectoryToBluebetter)
        ActiveScript.Write("run sender._setfiles " & SetQuotes(MySender))
        ActiveScript.WriteLine()
        ActiveScript.WriteLine()
        ' Read file to run
        Dim MyCodeContent As StreamReader = My.Computer.FileSystem.OpenTextFileReader(MyScript)
        ActiveScript.Write(MyCodeContent.ReadToEnd())
        MyCodeContent.Close()
        ActiveScript.Close()
        ' Execute!
        Shell(ActiveExecuter, AppWinStyle.Hide, True)
        ' Write response
        Try
            Dim MySenderStream As BinaryReader = New BinaryReader(File.Open(MySender, FileMode.Open))

        Catch ex As FileNotFoundException
            Console.Out.Write("The program doesn't return anything to return!")
            Console.Out.WriteLine()
            MyRW.Writer.Write(InternalServerError)
            MyRW.Writer.WriteLine()
        End Try
        ' End
        MyRW.Client.Close()
    End Sub


    Sub Main()

        For Each i In FileToCheck
            If Not My.Computer.FileSystem.FileExists(pather & "\" & i) Then
                Console.Out.Write("File not found: " & i)
                Console.Out.WriteLine()
                Console.Out.Write("Server cannot continue. Halted! . . .")
                Do
                    Thread.Yield()
                Loop
            End If
        Next

        Console.Out.Write("* Server started *")
        Console.Out.WriteLine()

        listener = New TcpListener(local, 80)
        listener.Start()
        Do
            Dim stream As NetworkStream
            Dim sread As StreamReader
            Dim swrite As StreamWriter
            Dim current As TcpClient
            Dim thr As Thread = New Thread(New ParameterizedThreadStart(AddressOf RequestProcessor))
            current = listener.AcceptTcpClient()
            stream = current.GetStream()
            sread = New StreamReader(stream)
            swrite = New StreamWriter(stream)
            thr.Start(New ReadWrites(sread, swrite, current))
        Loop
    End Sub

End Module
