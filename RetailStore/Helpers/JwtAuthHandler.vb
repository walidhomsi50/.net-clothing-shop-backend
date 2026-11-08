Imports System.Net.Http
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Web

Namespace Helpers

    ''' <summary>
    ''' Reads "Authorization: Bearer xxx", validates it, and sets the current user so that
    ''' [Authorize] and [Authorize(Roles:="Admin")] work. Invalid or missing tokens simply
    ''' leave the request anonymous; [Authorize] then answers 401.
    ''' Register once in WebApiConfig:  config.MessageHandlers.Add(New JwtAuthHandler())
    ''' </summary>
    Public Class JwtAuthHandler
        Inherits DelegatingHandler

        Protected Overrides Async Function SendAsync(
            request As HttpRequestMessage,
            cancellationToken As CancellationToken) As Task(Of HttpResponseMessage)

            Dim header = request.Headers.Authorization

            If header IsNot Nothing AndAlso
               String.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) AndAlso
               Not String.IsNullOrWhiteSpace(header.Parameter) Then

                Dim principal = JwtHelper.ValidateToken(header.Parameter)

                If principal IsNot Nothing Then
                    request.GetRequestContext().Principal = principal
                    Thread.CurrentPrincipal = principal
                    If HttpContext.Current IsNot Nothing Then
                        HttpContext.Current.User = principal
                    End If
                End If

            End If

            Return Await MyBase.SendAsync(request, cancellationToken)

        End Function

    End Class

End Namespace
