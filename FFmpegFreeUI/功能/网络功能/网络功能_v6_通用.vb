Imports System.IO
Imports System.Reflection
Imports System.Security.Cryptography
Imports System.Threading

Friend NotInheritable Class 网络功能_v6_通用

    Friend Shared ReadOnly 联网请求超时时间 As TimeSpan = TimeSpan.FromSeconds(45)

    Private Sub New()
    End Sub

    Friend Shared Function 创建联网请求取消源() As CancellationTokenSource
        Return New CancellationTokenSource(联网请求超时时间)
    End Function

    Friend Shared Function 获取异常消息(ex As Exception) As String
        If TypeOf ex Is OperationCanceledException Then
            Return $"请求超过 {联网请求超时时间.TotalSeconds:F0} 秒未完成"
        End If

        Return ex.Message
    End Function

    Friend Shared Function 计算文件SHA256(filePath As String) As String
        Using stream As FileStream = File.OpenRead(filePath)
            Using sha = SHA256.Create()
                Return Convert.ToHexString(sha.ComputeHash(stream))
            End Using
        End Using
    End Function

    Friend Shared Function 安全获取程序集类型(程序集 As Assembly) As IEnumerable(Of Type)
        Try
            Return 程序集.GetTypes()
        Catch ex As ReflectionTypeLoadException
            Return ex.Types.Where(Function(x) x IsNot Nothing)
        Catch
            Return Array.Empty(Of Type)()
        End Try
    End Function

End Class
