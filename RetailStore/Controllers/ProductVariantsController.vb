Imports System.Web.Http
Imports RetailStore.Data
Imports RetailStore.Models

Public Class ProductVariantsController
    Inherits ApiController

    Private ReadOnly db As New RetailStoreContext()

    ' GET: api/productvariants
    <HttpGet>
    Public Function GetProductVariants(
        Optional productId As Integer? = Nothing
    ) As IHttpActionResult

        Dim query = db.ProductVariants _
            .Where(Function(v) v.IsActive)

        If productId.HasValue Then
            query = query.Where(
                Function(v) v.ProductId = productId.Value
            )
        End If

        Dim variants = query _
            .OrderBy(Function(v) v.ProductId) _
            .ThenBy(Function(v) v.ColorId) _
            .ThenBy(Function(v) v.SizeId) _
            .ToList()

        Return Ok(variants)

    End Function

    ' GET: api/productvariants/5
    <HttpGet>
    Public Function GetProductVariant(id As Integer) As IHttpActionResult

        Dim productVariant = db.ProductVariants _
     .FirstOrDefault(
         Function(v) v.ProductVariantId = id AndAlso v.IsActive
     )

        If productVariant Is Nothing Then
            Return NotFound()
        End If

        Return Ok(productVariant)
    End Function

End Class