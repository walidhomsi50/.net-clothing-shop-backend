Imports System.Web.Http
Imports RetailStore.Data

Public Class ProductImagesController
    Inherits ApiController

    Private ReadOnly db As New RetailStoreContext()

    ' GET: api/categories
    <HttpGet>
    Public Function GetImages() As IHttpActionResult

        Dim Images = db.ProductImages _
                     .ToList()

        Return Ok(Images)

    End Function

End Class