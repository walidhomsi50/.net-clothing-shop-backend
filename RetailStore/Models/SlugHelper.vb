Imports System.Globalization
Imports System.Linq
Imports System.Text
Imports RetailStore.Data

Namespace Helpers

    ''' <summary>Turns names into URL-friendly slugs and keeps them unique.</summary>
    Public NotInheritable Class SlugHelper

        Private Sub New()
        End Sub

        ''' <summary>"Linen Shirt - Black!" becomes "linen-shirt-black". Keeps letters of any
        ''' language (Arabic included), drops accents, and returns "" if nothing is left.</summary>
        Public Shared Function Slugify(text As String) As String

            If String.IsNullOrWhiteSpace(text) Then
                Return ""
            End If

            Dim normalized As String = text.Trim().Normalize(NormalizationForm.FormD)
            Dim builder As New StringBuilder()
            Dim lastWasDash As Boolean = False

            For Each ch As Char In normalized

                If CharUnicodeInfo.GetUnicodeCategory(ch) = UnicodeCategory.NonSpacingMark Then
                    Continue For
                End If

                If Char.IsLetterOrDigit(ch) Then
                    builder.Append(Char.ToLowerInvariant(ch))
                    lastWasDash = False
                ElseIf Not lastWasDash AndAlso builder.Length > 0 Then
                    builder.Append("-"c)
                    lastWasDash = True
                End If

            Next

            Return builder.ToString().Trim("-"c)

        End Function


        ''' <summary>Slug for a new product; adds -2, -3 ... if the slug is already taken.</summary>
        Public Shared Function UniqueProductSlug(db As RetailStoreContext, name As String, ignoreProductId As Integer) As String

            Dim baseSlug As String = Truncate(Slugify(name), 230)

            If baseSlug.Length = 0 Then
                baseSlug = "product"
            End If

            Dim candidate As String = baseSlug
            Dim counter As Integer = 2

            While db.Products.Any(Function(p) p.Slug = candidate AndAlso p.ProductId <> ignoreProductId)
                candidate = baseSlug & "-" & counter
                counter += 1
            End While

            Return candidate

        End Function


        ''' <summary>Slug for a new category; adds -2, -3 ... if the slug is already taken.</summary>
        Public Shared Function UniqueCategorySlug(db As RetailStoreContext, name As String, ignoreCategoryId As Integer) As String

            Dim baseSlug As String = Truncate(Slugify(name), 130)

            If baseSlug.Length = 0 Then
                baseSlug = "category"
            End If

            Dim candidate As String = baseSlug
            Dim counter As Integer = 2

            While db.Categories.Any(Function(c) c.Slug = candidate AndAlso c.CategoryId <> ignoreCategoryId)
                candidate = baseSlug & "-" & counter
                counter += 1
            End While

            Return candidate

        End Function


        Private Shared Function Truncate(value As String, maxLength As Integer) As String
            If value.Length <= maxLength Then
                Return value
            End If
            Return value.Substring(0, maxLength).Trim("-"c)
        End Function

    End Class

End Namespace
