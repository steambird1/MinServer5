Imports System.Net.Sockets
Imports System.Threading
Imports System.IO
Imports System.Net

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
        Dim ActiveScript As StreamWriter = My.Computer.FileSystem.OpenTextFileWriter(DirectoryToBluebetter & "\execute.blue", False, Text.Encoding.Default)
        Dim MyHeader As StreamReader = My.Computer.FileSystem.OpenTextFileReader(pather & "\WebHeader.blue")
        ActiveScript.Write(MyHeader.ReadToEnd())
        MyHeader.Close()
        ActiveScript.WriteLine()
        ActiveScript.Write("set receiver.path=" & MyWebInfo.Path)
        ActiveScript.WriteLine()
        ActiveScript.Write("set receiver.http_version=" & MyWebInfo.HTTPVersion)
        ActiveScript.WriteLine()
        ActiveScript.Write("set receiver.method=" & MyWebInfo.Method)
        ActiveScript.WriteLine()
        For Each i In MyWebInfo.Settings
            ActiveScript.Write("set receiver.attributes." & i.Key & "=" & i.Value)
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
            ActiveScript.Write("set " & TempName & ".myfile=" & CurrentFilename)
            ActiveScript.WriteLine()
            For Each j In i.Settings
                ActiveScript.Write("set " & TempName & ".attributes." & j.Key & "=" & j.Value)
                ActiveScript.WriteLine()
            Next
            DataCounter += 1
        Next
        Dim MySender As String = GenerateRandom(DirectoryToBluebetter)

        ' ... Load for sender ...

        Shell(DirectoryToBluebetter & MyWebInfo.Path, AppWinStyle.Hide, True)
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
