Imports System.Collections.Generic
Imports System.Linq
Imports System.Web.Http
Imports RetailStore.Data
Imports RetailStore.DTOs
Imports RetailStore.Helpers
Imports RetailStore.Models

''' <summary>
''' Options for the storefront filter sidebar. Public (no login needed).
''' </summary>
<RoutePrefix("api/v1/catalog")>
Public Class CatalogController
    Inherits ApiController

    Private ReadOnly db As New RetailStoreContext()

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            db.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub


    ' =========================================================
    ' GET api/v1/catalog/filters?categorySlug=shirts
    ' Colors and sizes that exist on items IN STOCK, plus the price range,
    ' for one category (and everything under it) or for the whole store.
    ' =========================================================
    <HttpGet>
    <Route("filters")>
    Public Function GetFilters(Optional categorySlug As String = Nothing) As IHttpActionResult

        Dim tree As New CategoryTree(db)
        Dim scopeIds As List(Of Integer)

        If Not String.IsNullOrWhiteSpace(categorySlug) Then

            Dim categoryId = tree.FindBySlug(categorySlug.Trim())

            If Not categoryId.HasValue OrElse Not tree.IsVisible(categoryId.Value) Then
                Return NotFound()
            End If

            scopeIds = tree.SubtreeIds(categoryId.Value).Where(Function(id) tree.IsVisible(id)).ToList()

        Else

            scopeIds = tree.VisibleIds()

        End If

        Dim sellable = CatalogQueries.Sellable(db, scopeIds)

        ' Casting to a nullable decimal lets Min/Max return Nothing (instead of failing) when nothing matches
        Dim lowestPrice As Decimal? = sellable.Min(
            Function(p) CType(If(p.SalePrice.HasValue, p.SalePrice.Value, p.Price), Decimal?))

        Dim highestPrice As Decimal? = sellable.Max(
            Function(p) CType(If(p.SalePrice.HasValue, p.SalePrice.Value, p.Price), Decimal?))

        Dim inStockVariants = db.ProductVariants.Where(
            Function(v) v.IsActive AndAlso
                        v.StockQuantity > 0 AndAlso
                        v.Product.IsActive AndAlso
                        scopeIds.Contains(v.Product.CategoryId))

        Dim colorRows = inStockVariants _
            .Where(Function(v) v.ColorId.HasValue) _
            .Select(Function(v) New With {.ColorId = v.Color.ColorId, .Name = v.Color.Name, .HexCode = v.Color.HexCode}) _
            .Distinct() _
            .ToList()

        Dim sizeRows = inStockVariants _
            .Where(Function(v) v.SizeId.HasValue) _
            .Select(Function(v) New With {.SizeId = v.Size.SizeId, .Name = v.Size.Name, .SortOrder = v.Size.SortOrder}) _
            .Distinct() _
            .ToList()

        Dim colorList As New List(Of CatalogColorDto)()

        For Each row In colorRows.OrderBy(Function(c) c.Name)
            colorList.Add(New CatalogColorDto With {
                .ColorId = row.ColorId,
                .Name = row.Name,
                .HexCode = row.HexCode
            })
        Next

        Dim sizeList As New List(Of CatalogSizeDto)()

        For Each row In sizeRows.OrderBy(Function(s) s.SortOrder).ThenBy(Function(s) s.Name)
            sizeList.Add(New CatalogSizeDto With {
                .SizeId = row.SizeId,
                .Name = row.Name,
                .SortOrder = row.SortOrder
            })
        Next

        Return Ok(New With {
            .Currency = PricingHelper.Currency,
            .MinPrice = lowestPrice,
            .MaxPrice = highestPrice,
            .Colors = colorList,
            .Sizes = sizeList
        })

    End Function

End Class
