Imports System.Collections.Generic
Imports System.Linq
Imports RetailStore.Data
Imports RetailStore.Models

Namespace Helpers

    ''' <summary>
    ''' The category hierarchy in memory (one small query). A category is "visible" to
    ''' customers only when it AND all of its parents are active.
    ''' </summary>
    Public NotInheritable Class CategoryTree

        Private ReadOnly _parents As New Dictionary(Of Integer, Integer?)()
        Private ReadOnly _active As New Dictionary(Of Integer, Boolean)()
        Private ReadOnly _children As New Dictionary(Of Integer, List(Of Integer))()
        Private ReadOnly _slugs As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)

        Public Sub New(db As RetailStoreContext)

            Dim rows = db.Categories.AsNoTracking() _
                .Select(Function(c) New With {c.CategoryId, c.ParentCategoryId, c.IsActive, c.Slug}) _
                .ToList()

            For Each row In rows
                _parents(row.CategoryId) = row.ParentCategoryId
                _active(row.CategoryId) = row.IsActive
                _slugs(row.Slug) = row.CategoryId
            Next

            For Each row In rows
                If row.ParentCategoryId.HasValue Then

                    Dim siblings As List(Of Integer) = Nothing

                    If Not _children.TryGetValue(row.ParentCategoryId.Value, siblings) Then
                        siblings = New List(Of Integer)()
                        _children(row.ParentCategoryId.Value) = siblings
                    End If

                    siblings.Add(row.CategoryId)

                End If
            Next

        End Sub

        Public Function FindBySlug(slug As String) As Integer?

            Dim id As Integer

            If _slugs.TryGetValue(slug, id) Then
                Return id
            End If

            Return Nothing

        End Function

        ''' <summary>True when the category and every parent above it are active.</summary>
        Public Function IsVisible(categoryId As Integer) As Boolean

            Dim cursor As Integer? = categoryId
            Dim steps As Integer = 0

            While cursor.HasValue AndAlso steps < 20

                Dim isActive As Boolean = False

                If Not _active.TryGetValue(cursor.Value, isActive) OrElse Not isActive Then
                    Return False
                End If

                Dim parent As Integer? = Nothing
                _parents.TryGetValue(cursor.Value, parent)

                cursor = parent
                steps += 1

            End While

            Return True

        End Function

        Public Function VisibleIds() As List(Of Integer)
            Return _active.Keys.Where(Function(id) IsVisible(id)).ToList()
        End Function

        ''' <summary>The category itself plus all of its sub-categories, at any depth.</summary>
        Public Function SubtreeIds(rootId As Integer) As List(Of Integer)

            Dim result As New List(Of Integer) From {rootId}
            Dim index As Integer = 0

            While index < result.Count AndAlso result.Count < 500

                Dim kids As List(Of Integer) = Nothing

                If _children.TryGetValue(result(index), kids) Then
                    For Each kid In kids
                        If Not result.Contains(kid) Then
                            result.Add(kid)
                        End If
                    Next
                End If

                index += 1

            End While

            Return result

        End Function

    End Class


    Public NotInheritable Class CatalogQueries

        Private Sub New()
        End Sub

        ''' <summary>
        ''' Products customers may see: active, inside the given (visible) categories, and with at
        ''' least one active variant. Out-of-stock products stay listed, marked as not in stock.
        ''' </summary>
        Public Shared Function Sellable(db As RetailStoreContext, categoryIds As List(Of Integer)) As IQueryable(Of Product)

            Return db.Products.Where(
                Function(p) p.IsActive AndAlso
                            categoryIds.Contains(p.CategoryId) AndAlso
                            db.ProductVariants.Any(Function(v) v.ProductId = p.ProductId AndAlso v.IsActive))

        End Function

    End Class

End Namespace
