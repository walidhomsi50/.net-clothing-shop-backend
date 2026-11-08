Imports System.Web.Http
Imports RetailStore.Data

Public Class ColorsController
    Inherits ApiController

    Private ReadOnly db As New RetailStoreContext()

    ' GET: api/categories
    <HttpGet>
    Public Function GetColors() As IHttpActionResult

        Dim colors = db.Colors _
            .Where(Function(c) c.IsActive) _
            .OrderBy(Function(c) c.Name) _
            .ToList()

        Return Ok(colors)

    End Function

End Class