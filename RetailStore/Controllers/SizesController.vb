Imports System.Web.Http
Imports RetailStore.Data

Public Class SizesController
    Inherits ApiController

    Private ReadOnly db As New RetailStoreContext()

    ' GET: api/categories
    <HttpGet>
    Public Function GetSizes() As IHttpActionResult

        Dim sizes = db.Sizes _
            .Where(Function(c) c.IsActive) _
            .OrderBy(Function(c) c.Name) _
            .ToList()

        Return Ok(sizes)

    End Function

End Class