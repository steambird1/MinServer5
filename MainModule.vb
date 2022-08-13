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

    Sub RequestProcessor(Parameter As Object)
        Dim MyRW As ReadWrites = Parameter
        ' Process here...
        Dim re As String = MyRW.Reader.ReadToEnd()
        Console.Out.Write(re)
        Console.Out.WriteLine()
        ' End
        MyRW.Client.Close()
    End Sub

    Sub Main()

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
