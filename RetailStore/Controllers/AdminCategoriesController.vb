Imports System.Collections.Generic
Imports System.Linq
Imports System.Net
Imports System.Web.Http
Imports RetailStore.Data
Imports RetailStore.DTOs
Imports RetailStore.Helpers
Imports RetailStore.Models

''' <summary>
''' Category management for the admin panel. Admin and Staff accounts only.
''' Categories are never deleted: switch IsActive off to hide one.
''' </summary>
<RoutePrefix("api/v1/admin/categories")>
<Authorize(Roles:="Admin,Staff")>
Public Class AdminCategoriesController
    Inherits ApiController

    Private ReadOnly db As New RetailStoreContext()

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            db.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub


    ' =========================================================
    ' GET api/v1/admin/categories     (all categories, including hidden ones)
    ' =========================================================
    <HttpGet>
    <Route("")>
    Public Function GetCategories() As IHttpActionResult

        Dim categories = db.Categories.OrderBy(Function(c) c.Name).ToList()

        Dim counts = db.Products _
            .GroupBy(Function(p) p.CategoryId) _
            .Select(Function(g) New With {.CategoryId = g.Key, .Total = g.Count()}) _
            .ToList() _
            .ToDictionary(Function(x) x.CategoryId, Function(x) x.Total)

        Dim result As New List(Of AdminCategoryDto)()

        For Each row In categories

            Dim total As Integer = 0
            counts.TryGetValue(row.CategoryId, total)

            result.Add(ToDto(row, total))

        Next

        Return Ok(result)

    End Function


    ' =========================================================
    ' POST api/v1/admin/categories
    ' { "name": "Shirts", "description": "", "parentCategoryId": 1, "isActive": true }
    ' =========================================================
    <HttpPost>
    <Route("")>
    Public Function CreateCategory(body As SaveCategoryRequest) As IHttpActionResult

        Dim problem As String = Validate(body, 0)

        If problem IsNot Nothing Then
            Return BadRequest(problem)
        End If

        Dim name As String = body.Name.Trim()

        Dim created As New Category With {
            .Name = name,
            .Slug = SlugHelper.UniqueCategorySlug(db, name, 0),
            .Description = NullIfBlank(body.Description),
            .ParentCategoryId = body.ParentCategoryId,
            .IsActive = body.IsActive,
            .CreatedDate = DateTime.UtcNow
        }

        db.Categories.Add(created)
        db.SaveChanges()

        Return Ok(ToDto(created, 0))

    End Function


    ' =========================================================
    ' PUT api/v1/admin/categories/3
    ' The slug does not change when you rename a category, so existing links keep working.
    ' =========================================================
    <HttpPut>
    <Route("{id:int}")>
    Public Function UpdateCategory(id As Integer, body As SaveCategoryRequest) As IHttpActionResult

        Dim found = db.Categories.FirstOrDefault(Function(c) c.CategoryId = id)

        If found Is Nothing Then
            Return NotFound()
        End If

        Dim problem As String = Validate(body, id)

        If problem IsNot Nothing Then
            Return BadRequest(problem)
        End If

        found.Name = body.Name.Trim()
        found.Description = NullIfBlank(body.Description)
        found.ParentCategoryId = body.ParentCategoryId
        found.IsActive = body.IsActive

        db.SaveChanges()

        Dim total As Integer = db.Products.Count(Function(p) p.CategoryId = id)

        Return Ok(ToDto(found, total))

    End Function


    ' =========================================================
    ' Helpers
    ' =========================================================

    ''' <summary>Returns an error message, or Nothing when the request is fine.</summary>
    Private Function Validate(body As SaveCategoryRequest, ownId As Integer) As String

        If body Is Nothing Then
            Return "Request is required."
        End If

        If String.IsNullOrWhiteSpace(body.Name) Then
            Return "Name is required."
        End If

        Dim name As String = body.Name.Trim()

        If name.Length > 100 Then
            Return "Name is too long (maximum 100 characters)."
        End If

        If db.Categories.Any(Function(c) c.Name = name AndAlso c.CategoryId <> ownId) Then
            Return "A category with this name already exists."
        End If

        If Not body.ParentCategoryId.HasValue Then
            Return Nothing
        End If

        Dim parentId As Integer = body.ParentCategoryId.Value

        If parentId = ownId Then
            Return "A category cannot be its own parent."
        End If

        If Not db.Categories.Any(Function(c) c.CategoryId = parentId) Then
            Return "Parent category was not found."
        End If

        ' Walk up the parent chain: it must never lead back to this category
        Dim cursor As Integer? = parentId
        Dim steps As Integer = 0

        While cursor.HasValue AndAlso steps < 20

            If ownId <> 0 AndAlso cursor.Value = ownId Then
                Return "A category cannot be moved under one of its own sub-categories."
            End If

            Dim currentId As Integer = cursor.Value

            cursor = db.Categories _
                .Where(Function(c) c.CategoryId = currentId) _
                .Select(Function(c) c.ParentCategoryId) _
                .FirstOrDefault()

            steps += 1

        End While

        Return Nothing

    End Function

    Private Shared Function NullIfBlank(value As String) As String
        If String.IsNullOrWhiteSpace(value) Then
            Return Nothing
        End If
        Return value.Trim()
    End Function

    Private Shared Function ToDto(row As Category, productCount As Integer) As AdminCategoryDto
        Return New AdminCategoryDto With {
            .CategoryId = row.CategoryId,
            .Name = row.Name,
            .Slug = row.Slug,
            .Description = row.Description,
            .ParentCategoryId = row.ParentCategoryId,
            .IsActive = row.IsActive,
            .ProductCount = productCount
        }
    End Function

End Class
