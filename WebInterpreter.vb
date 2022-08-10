' Load WEB Data.

Module WebInterpreter

    Public Class BadWebFormatException
        Inherits Exception
        Public Overrides ReadOnly Property Message As String = "Incorrect Web Request Format!"
    End Class

    Public Class WebInfo
        Public Property Method As String
        Public Property Path As String
        Public Property HTTPVersion As String
        Public Property Settings As Dictionary(Of String, String)

        ' Only for GET method now.
        Public Sub New(RequestData As String)
            Dim spl As String() = Split(RequestData, vbLf)
            Dim firstline As String = spl(0).Replace(vbCr, "")
            Dim farg As String() = Split(firstline)
            If farg.Count() < 3 Then
                Throw New BadWebFormatException()
            End If
            Me.Method = farg(0)
            Me.Path = farg(1)
            Me.HTTPVersion = farg(2)
            For i = 1 To spl.Count - 1
                Dim myline As String = spl(i).Replace(vbCr, "")
                Dim argspl As String() = Split(myline, ":", 2)
                If argspl.Count() < 2 Then
                    Continue For
                End If
                argspl(1) = Trim(argspl(1))
                Me.Settings(argspl(0)) = argspl(1)
            Next
        End Sub
    End Class
End Module
