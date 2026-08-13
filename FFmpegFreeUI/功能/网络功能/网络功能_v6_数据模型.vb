Imports System.Text.Json.Serialization

Public Partial Class 网络功能

    Public Class AgentSpEndpointInfo
        <JsonPropertyName("display_name")>
        Public Property DisplayName As String = ""

        <JsonPropertyName("address")>
        Public Property Address As String = ""

        <JsonPropertyName("api_key")>
        Public Property ApiKey As String = ""

        <JsonPropertyName("extra_headers")>
        Public Property ExtraHeaders As String = ""

        <JsonPropertyName("extra_body")>
        Public Property ExtraBody As String = ""

        Public Function Clone() As AgentSpEndpointInfo
            Return New AgentSpEndpointInfo With {
                .DisplayName = If(DisplayName, ""),
                .Address = If(Address, ""),
                .ApiKey = If(ApiKey, ""),
                .ExtraHeaders = If(ExtraHeaders, ""),
                .ExtraBody = If(ExtraBody, "")
            }
        End Function
    End Class

    Public Class 新闻单片数据类
        Public Property Title As String
        Public Property TitleColor As String
        Public Property SubTitle As String
        Public Property Type As String
        Public Property Body As String
    End Class

End Class
