' Load WEB Data.

Imports System.Text

Module WebInterpreter

    Public Class BadWebFormatException
        Inherits Exception
        Public Overrides ReadOnly Property Message As String = "Incorrect Web Request Format!"
    End Class

    Public Class WebInfo
        Public ReadOnly Property Method As String
        Public ReadOnly Property Path As String
        Public ReadOnly Property HTTPVersion As String
        Public ReadOnly Property Settings As Dictionary(Of String, String)
        Public ReadOnly Property Content As String

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
                Public Property Content As String

                Public Sub New(Settings As Dictionary(Of String, String), Content As String)
                    Me.Settings = Settings
                    Me.Content = Content
                End Sub
            End Structure

            Public ReadOnly Property Data As List(Of SingleData)

        End Structure

        Public ReadOnly Property PostData As PostInfo
            Get
                ' 1. Get post data from requested data
                Dim bd As String = Me.MyBoundary
                Dim temp As PostInfo = New PostInfo
                Dim spl As String() = Split(Me.Content, vbLf)
                Dim current As PostInfo.SingleData = Nothing
                Dim content As Boolean = False
                Dim mycontent As StringBuilder = New StringBuilder
                For Each i In spl
                    If i.IndexOf(bd) > 0 Then
                        If Not IsNothing(current) Then
                            current.Content = mycontent.ToString()
                            mycontent.Clear()
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
        Public Sub New(RequestData As String)
            Dim i As Integer
            Dim spl As String() = Split(RequestData, vbLf)
            Dim firstline As String = spl(0).Replace(vbCr, "")
            Dim farg As String() = Split(firstline)
            Dim mycontent As StringBuilder
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
                    mycontent = New StringBuilder(slen)
                End If
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
