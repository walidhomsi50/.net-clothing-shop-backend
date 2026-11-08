Imports System.Collections.Generic
Imports System.Linq
Imports System.Web.Http
Imports RetailStore.Data
Imports RetailStore.DTOs
Imports RetailStore.Helpers
Imports RetailStore.Models

''' <summary>
''' REPLACES the old CategoriesController. Public storefront categories (no login needed).
''' Only categories that are active, together with all their parents, are returned.
''' The list is flat: use parentCategoryId to build the menu tree.
''' </summary>
<RoutePrefix("api/v1/categories")>
Public Class CategoriesController
    Inherits ApiController

    Private ReadOnly db As New RetailStoreContext()

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            db.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub


    ' =========================================================
    ' GET api/v1/categories
    ' =========================================================
    <HttpGet>
    <Route("")>
    Public Function GetCategories() As IHttpActionResult

        Dim tree As New CategoryTree(db)
        Dim visibleIds As List(Of Integer) = tree.VisibleIds()

        Dim rows = db.Categories.AsNoTracking() _
            .Where(Function(c) visibleIds.Contains(c.CategoryId)) _
            .OrderBy(Function(c) c.Name) _
            .ToList()

        ' Sellable products directly inside each category (one query)
        Dim directCounts = CatalogQueries.Sellable(db, visibleIds) _
            .GroupBy(Function(p) p.CategoryId) _
            .Select(Function(g) New With {.CategoryId = g.Key, .Total = g.Count()}) _
            .ToList() _
            .ToDictionary(Function(x) x.CategoryId, Function(x) x.Total)

        Dim result As New List(Of CatalogCategoryDto)()

        For Each row In rows

            ' A category counts its own products plus those of all its sub-categories
            Dim total As Integer = 0

            For Each id In tree.SubtreeIds(row.CategoryId)

                Dim direct As Integer = 0

                If directCounts.TryGetValue(id, direct) Then
                    total += direct
                End If

            Next

            result.Add(New CatalogCategoryDto With {
                .CategoryId = row.CategoryId,
                .Name = row.Name,
                .Slug = row.Slug,
                .Description = row.Description,
                .ParentCategoryId = row.ParentCategoryId,
                .ProductCount = total
            })

        Next

        Return Ok(result)

    End Function

End Class
