Imports System.Net
Imports System.Net.Http
Imports System.Web.Http
Imports System.Web.Http.ExceptionHandling
Imports System.Web.Http.Results

''' <summary>
''' Throw this from services for expected failures; the handler turns it into a clean JSON response.
'''   Throw New ApiException(HttpStatusCode.BadRequest, "Not enough stock for size M.")
''' </summary>
Public Class ApiException
    Inherits Exception

    Public ReadOnly Property StatusCode As HttpStatusCode

    Public Sub New(statusCode As HttpStatusCode, message As String)
        MyBase.New(message)
        Me.StatusCode = statusCode
    End Sub
End Class

''' <summary>
''' Catches anything unhandled. Known ApiExceptions return their own message;
''' everything else returns a generic 500 so internal details never leak to clients.
''' </summary>
Public Class ApiExceptionHandler
    Inherits ExceptionHandler

    Public Overrides Sub Handle(context As ExceptionHandlerContext)
        Dim status As HttpStatusCode = HttpStatusCode.InternalServerError
        Dim message As String = "An unexpected error occurred."

        Dim apiEx = TryCast(context.Exception, ApiException)
        If apiEx IsNot Nothing Then
            status = apiEx.StatusCode
            message = apiEx.Message
        Else
            ' TODO: log context.Exception (file, Serilog, Application Insights...)
            Diagnostics.Trace.TraceError(context.Exception.ToString())
        End If

        Dim response = context.Request.CreateResponse(status, New With {.message = message})
        context.Result = New ResponseMessageResult(response)
    End Sub

End Class
