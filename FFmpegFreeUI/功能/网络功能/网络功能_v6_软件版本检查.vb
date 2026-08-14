Imports System.Diagnostics
Imports System.Threading

Friend NotInheritable Class 网络功能_v6_软件版本检查

    Friend NotInheritable Class 版本检查结果

        Friend ReadOnly Property 检查成功 As Boolean
        Friend ReadOnly Property 有新版本 As Boolean
        Friend ReadOnly Property 当前版本号 As String
        Friend ReadOnly Property 最新版本号 As String
        Friend ReadOnly Property 失败原因 As String

        Friend Sub New(检查成功 As Boolean, 有新版本 As Boolean, 当前版本号 As String, 最新版本号 As String, 失败原因 As String)
            Me.检查成功 = 检查成功
            Me.有新版本 = 有新版本
            Me.当前版本号 = 当前版本号
            Me.最新版本号 = 最新版本号
            Me.失败原因 = 失败原因
        End Sub

    End Class

    Friend Const GitHub仓库拥有者 As String = "SteveYu000"
    Friend Const GitHub仓库名称 As String = "FFmpegFreeUI-API-Extended-Edition"
    Friend Const GitHub仓库地址 As String = "https://github.com/" & GitHub仓库拥有者 & "/" & GitHub仓库名称
    Private Shared ReadOnly 版本检查任务锁 As New Object()
    Private Shared 当前版本检查任务 As Task(Of 版本检查结果)

    Private Sub New()
    End Sub

    Friend Shared Async Sub 启动时检查新版本()
        Try
            Dim 检查结果 As 版本检查结果 = Await 获取或开始版本检查Async()
            If Not 检查结果.检查成功 OrElse Not 检查结果.有新版本 Then Exit Sub
            If FormMain_v6.IsDisposed OrElse FormMain_v6.Disposing OrElse Not FormMain_v6.Visible Then Exit Sub
            显示发现新版本提示(检查结果)
        Catch ex As Exception
            Debug.WriteLine($"检查新版本失败：{网络功能_v6_通用.获取异常消息(ex)}")
        End Try
    End Sub

    Friend Shared Async Function 手动检查新版本Async() As Task(Of 版本检查结果)
        Dim 检查结果 As 版本检查结果 = Await 获取或开始版本检查Async()

        If FormMain_v6.IsDisposed OrElse FormMain_v6.Disposing Then Return 检查结果

        If Not 检查结果.检查成功 Then
            LakeUI.ExOverlayMsgBox(
                FormMain_v6,
                $"{检查结果.失败原因}{vbCrLf}{vbCrLf}当前版本：{检查结果.当前版本号}",
                MsgBoxStyle.Exclamation,
                "检查更新失败")
        ElseIf String.IsNullOrWhiteSpace(检查结果.最新版本号) Then
            LakeUI.ExOverlayMsgBox(
                FormMain_v6,
                $"项目仓库目前还没有发布 Release，暂时没有可用更新。{vbCrLf}{vbCrLf}当前版本：{检查结果.当前版本号}",
                MsgBoxStyle.Information,
                "暂无可用更新")
        ElseIf 检查结果.有新版本 Then
            显示发现新版本提示(检查结果)
        Else
            LakeUI.ExOverlayMsgBox(
                FormMain_v6,
                $"当前已是最新版本。{vbCrLf}{vbCrLf}当前版本：{检查结果.当前版本号}{vbCrLf}最新版本：{检查结果.最新版本号}",
                MsgBoxStyle.Information,
                "没有发现新版本")
        End If

        Return 检查结果
    End Function

    Private Shared Function 获取或开始版本检查Async() As Task(Of 版本检查结果)
        SyncLock 版本检查任务锁
            If 当前版本检查任务 Is Nothing OrElse 当前版本检查任务.IsCompleted Then
                当前版本检查任务 = 执行版本检查Async()
            End If

            Return 当前版本检查任务
        End SyncLock
    End Function

    Private Shared Async Function 执行版本检查Async() As Task(Of 版本检查结果)
        Dim 当前版本号 As String = 版本号.获取自身版本号()

        If Not My.Computer.Network.IsAvailable Then
            Return New 版本检查结果(False, False, 当前版本号, "", "当前网络不可用，请连接网络后重试。")
        End If

        Try
            Dim 发布信息 As LakeUI.GitHub.GitHubReleaseInfo = Await 获取最新发行版信息Async()
            If 发布信息 Is Nothing OrElse Not 发布信息.IsSuccess Then
                Return New 版本检查结果(False, False, 当前版本号, "", "未能从 GitHub 获取最新 Release，请稍后重试。")
            End If

            Dim 云端版本号 As String = If(发布信息.TagName, "").Trim()
            If String.IsNullOrWhiteSpace(云端版本号) Then
                Return New 版本检查结果(True, False, 当前版本号, "", "")
            End If

            Dim 有新版本 As Boolean = 版本号.CompareVersion(云端版本号, 当前版本号) > 0
            Return New 版本检查结果(True, 有新版本, 当前版本号, 云端版本号, "")
        Catch ex As Exception
            Debug.WriteLine($"检查新版本失败：{网络功能_v6_通用.获取异常消息(ex)}")
            Return New 版本检查结果(False, False, 当前版本号, "", "连接 GitHub 时发生错误，请稍后重试。")
        End Try
    End Function

    Private Shared Async Function 获取最新发行版信息Async() As Task(Of LakeUI.GitHub.GitHubReleaseInfo)
        Using cts As CancellationTokenSource = 网络功能_v6_通用.创建联网请求取消源()
            Dim 发布信息 As LakeUI.GitHub.GitHubReleaseInfo = Await LakeUI.GitHub.GetLatestReleaseAssetUrlsAsync(
                GitHub仓库拥有者,
                GitHub仓库名称,
                includePrerelease:=True,
                cancellationToken:=cts.Token)

            Return 发布信息
        End Using
    End Function

    Private Shared Sub 显示发现新版本提示(检查结果 As 版本检查结果)
        Dim 选择结果 As Integer = LakeUI.ExOverlayMsgBox(
            FormMain_v6,
            $"检测到新版本 {检查结果.最新版本号}。{vbCrLf}{vbCrLf}当前版本：{检查结果.当前版本号}{vbCrLf}是否打开项目 Release 页面进行升级？",
            {"前往升级", "暂不升级"},
            "发现新版本",
            MsgBoxStyle.Question,
            0)

        If 选择结果 = 0 Then 打开发行版页面(检查结果.最新版本号)
    End Sub

    Private Shared Sub 打开发行版页面(云端版本号 As String)
        Dim 地址 As String = $"{GitHub仓库地址}/releases/tag/{Uri.EscapeDataString(云端版本号)}"
        Process.Start(New ProcessStartInfo With {
            .FileName = 地址,
            .UseShellExecute = True
        })
    End Sub

End Class
