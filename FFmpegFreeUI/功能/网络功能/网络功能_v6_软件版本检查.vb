Imports System.Diagnostics
Imports System.Threading

Friend NotInheritable Class 网络功能_v6_软件版本检查

    Friend Const GitHub仓库拥有者 As String = "SteveYu000"
    Friend Const GitHub仓库名称 As String = "FFmpegFreeUI-API-Extended-Edition"
    Friend Const GitHub仓库地址 As String = "https://github.com/" & GitHub仓库拥有者 & "/" & GitHub仓库名称
    Private Shared 当前是否正在检查新版本 As Boolean = False

    Private Sub New()
    End Sub

    Friend Shared Async Sub 启动时检查新版本()
        If 当前是否正在检查新版本 OrElse Not My.Computer.Network.IsAvailable Then Exit Sub

        当前是否正在检查新版本 = True
        Try
            Dim 云端版本号 As String = Await 获取最新发行版版本号Async()
            If String.IsNullOrWhiteSpace(云端版本号) Then Exit Sub

            Dim 当前版本号 As String = 版本号.获取自身版本号()
            If 版本号.CompareVersion(云端版本号, 当前版本号) <= 0 Then Exit Sub
            If FormMain_v6.IsDisposed OrElse FormMain_v6.Disposing OrElse Not FormMain_v6.Visible Then Exit Sub

            Dim 选择结果 As Integer = LakeUI.ExOverlayMsgBox(
                FormMain_v6,
                $"检测到新版本 {云端版本号}。{vbCrLf}{vbCrLf}当前版本：{当前版本号}{vbCrLf}是否打开项目 Release 页面进行升级？",
                {"前往升级", "暂不升级"},
                "发现新版本",
                MsgBoxStyle.Question,
                0)

            If 选择结果 = 0 Then 打开发行版页面(云端版本号)
        Catch ex As Exception
            Debug.WriteLine($"检查新版本失败：{网络功能_v6_通用.获取异常消息(ex)}")
        Finally
            当前是否正在检查新版本 = False
        End Try
    End Sub

    Private Shared Async Function 获取最新发行版版本号Async() As Task(Of String)
        Using cts As CancellationTokenSource = 网络功能_v6_通用.创建联网请求取消源()
            Dim 发布信息 As LakeUI.GitHub.GitHubReleaseInfo = Await LakeUI.GitHub.GetLatestReleaseAssetUrlsAsync(
                GitHub仓库拥有者,
                GitHub仓库名称,
                includePrerelease:=True,
                cancellationToken:=cts.Token)

            If 发布信息 Is Nothing OrElse Not 发布信息.IsSuccess Then Return ""
            Return If(发布信息.TagName, "").Trim()
        End Using
    End Function

    Private Shared Sub 打开发行版页面(云端版本号 As String)
        Dim 地址 As String = $"{GitHub仓库地址}/releases/tag/{Uri.EscapeDataString(云端版本号)}"
        Process.Start(New ProcessStartInfo With {
            .FileName = 地址,
            .UseShellExecute = True
        })
    End Sub

End Class
