Imports System.Collections.Generic
Imports System.Data.Entity
Imports System.Data.Entity.Infrastructure
Imports System.IO
Imports System.Linq
Imports System.Net
Imports System.Net.Http
Imports System.Threading.Tasks
Imports System.Web.Hosting
Imports System.Web.Http
Imports RetailStore.Data
Imports RetailStore.DTOs
Imports RetailStore.Helpers
Imports RetailStore.Models

''' <summary>
''' Product images for the admin panel. Admin and Staff accounts only.
''' Files are saved in the "uploads/products" folder of the site and served as static files.
''' The database stores a relative URL such as /uploads/products/p5-3f2b...c1.jpg
''' (your frontend puts the API address in front of it).
''' </summary>
<RoutePrefix("api/v1/admin")>
<Authorize(Roles:="Admin,Staff")>
Public Class AdminProductImagesController
    Inherits ApiController

    Private Const UploadFolder As String = "~/uploads/products"
    Private Const UrlPrefix As String = "/uploads/products/"

    Private Const MaxFilesPerRequest As Integer = 8
    Private Const MaxFileBytes As Integer = 5 * 1024 * 1024        ' 5 MB per image
    Private Const MaxRequestBytes As Integer = 8 * 1024 * 1024     ' 8 MB per request

    Private ReadOnly db As New RetailStoreContext()

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            db.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub


    ' =========================================================
    ' POST api/v1/admin/products/5/images
    ' Body: multipart/form-data with one or more file fields (any field name).
    ' JPEG, PNG and WebP only, 5 MB each. The first image of a product becomes the main one.
    ' =========================================================
    <HttpPost>
    <Route("products/{productId:int}/images")>
    Public Async Function UploadImages(productId As Integer) As Task(Of IHttpActionResult)

        If Not db.Products.Any(Function(p) p.ProductId = productId) Then
            Return NotFound()
        End If

        If Not Me.Request.Content.IsMimeMultipartContent() Then
            Return Content(HttpStatusCode.UnsupportedMediaType, "Send the files as multipart/form-data.")
        End If

        Dim declaredLength As Long? = Me.Request.Content.Headers.ContentLength

        If declaredLength.HasValue AndAlso declaredLength.Value > MaxRequestBytes Then
            Return Content(HttpStatusCode.RequestEntityTooLarge, "The upload is too large (maximum 8 MB per request).")
        End If

        Dim provider As New MultipartMemoryStreamProvider()
        Await Me.Request.Content.ReadAsMultipartAsync(provider)

        Dim parts As New List(Of HttpContent)()

        For Each part In provider.Contents
            Dim disposition = part.Headers.ContentDisposition
            If disposition IsNot Nothing AndAlso Not String.IsNullOrEmpty(disposition.FileName) Then
                parts.Add(part)
            End If
        Next

        If parts.Count = 0 Then
            Return BadRequest("No files were uploaded.")
        End If

        If parts.Count > MaxFilesPerRequest Then
            Return BadRequest("You can upload at most " & MaxFilesPerRequest & " images at a time.")
        End If

        ' Read and check EVERY file before saving anything
        Dim validFiles As New List(Of Byte())()
        Dim extensions As New List(Of String)()

        For index As Integer = 0 To parts.Count - 1

            Dim bytes As Byte() = Await parts(index).ReadAsByteArrayAsync()

            If bytes.Length = 0 Then
                Return BadRequest("File " & (index + 1) & " is empty.")
            End If

            If bytes.Length > MaxFileBytes Then
                Return BadRequest("File " & (index + 1) & " is too large (maximum 5 MB per image).")
            End If

            ' The file name and content type sent by the client are never trusted:
            ' the real type is read from the first bytes of the file.
            Dim extension As String = DetectImageExtension(bytes)

            If extension Is Nothing Then
                Return BadRequest("File " & (index + 1) & " is not a JPEG, PNG or WebP image.")
            End If

            validFiles.Add(bytes)
            extensions.Add(extension)

        Next

        Dim folder As String = HostingEnvironment.MapPath(UploadFolder)
        Directory.CreateDirectory(folder)

        Dim maxSort As Integer? = db.ProductImages _
            .Where(Function(i) i.ProductId = productId) _
            .Max(Function(i) CType(i.SortOrder, Integer?))

        Dim nextSort As Integer = If(maxSort.HasValue, maxSort.Value + 1, 0)

        Dim hasMain As Boolean = db.ProductImages.Any(Function(i) i.ProductId = productId AndAlso i.IsMain)

        Dim writtenFiles As New List(Of String)()
        Dim created As New List(Of ProductImage)()

        Try

            For index As Integer = 0 To validFiles.Count - 1

                Dim fileName As String = "p" & productId & "-" & Guid.NewGuid().ToString("N") & extensions(index)
                Dim fullPath As String = Path.Combine(folder, fileName)

                File.WriteAllBytes(fullPath, validFiles(index))
                writtenFiles.Add(fullPath)

                Dim image As New ProductImage With {
                    .ProductId = productId,
                    .ImageUrl = UrlPrefix & fileName,
                    .SortOrder = nextSort + index,
                    .IsMain = (Not hasMain AndAlso index = 0),
                    .CreatedDate = DateTime.UtcNow
                }

                db.ProductImages.Add(image)
                created.Add(image)

            Next

            db.SaveChanges()

        Catch ex As Exception

            ' Do not leave orphan files behind when the database save fails
            For Each writtenPath In writtenFiles
                DeleteFileQuietly(writtenPath)
            Next

            Throw

        End Try

        Return Ok(created.Select(Function(i) ToDto(i)).ToList())

    End Function


    ' =========================================================
    ' PUT api/v1/admin/images/12/main      (make this the product's main image)
    ' =========================================================
    <HttpPut>
    <Route("images/{imageId:int}/main")>
    Public Function SetMainImage(imageId As Integer) As IHttpActionResult

        Dim productId As Integer = db.ProductImages _
            .Where(Function(i) i.ProductImageId = imageId) _
            .Select(Function(i) i.ProductId) _
            .FirstOrDefault()

        If productId = 0 Then
            Return NotFound()
        End If

        Using tx = db.Database.BeginTransaction()

            ' Two steps, because only one image per product may be the main one
            db.Database.ExecuteSqlCommand(
                "UPDATE dbo.ProductImages SET IsMain = 0 WHERE ProductId = {0} AND IsMain = 1",
                productId)

            db.Database.ExecuteSqlCommand(
                "UPDATE dbo.ProductImages SET IsMain = 1 WHERE ProductImageId = {0}",
                imageId)

            tx.Commit()

        End Using

        Return Ok(ListImages(productId))

    End Function


    ' =========================================================
    ' PUT api/v1/admin/products/5/images/order      { "imageIds": [14, 12, 13] }
    ' =========================================================
    <HttpPut>
    <Route("products/{productId:int}/images/order")>
    Public Function ReorderImages(productId As Integer, body As ReorderImagesRequest) As IHttpActionResult

        If body Is Nothing OrElse body.ImageIds Is Nothing Then
            Return BadRequest("ImageIds is required.")
        End If

        Dim images = db.ProductImages.Where(Function(i) i.ProductId = productId).ToList()

        If images.Count = 0 Then
            Return NotFound()
        End If

        Dim requested As List(Of Integer) = body.ImageIds

        If requested.Count <> images.Count OrElse
           requested.Distinct().Count() <> images.Count OrElse
           images.Any(Function(i) Not requested.Contains(i.ProductImageId)) Then

            Return BadRequest("ImageIds must list every image of the product exactly once.")
        End If

        For position As Integer = 0 To requested.Count - 1
            Dim imageId As Integer = requested(position)
            images.First(Function(i) i.ProductImageId = imageId).SortOrder = position
        Next

        db.SaveChanges()

        Return Ok(ListImages(productId))

    End Function


    ' =========================================================
    ' DELETE api/v1/admin/images/12
    ' If it was the main image, the next one in order becomes the main image.
    ' =========================================================
    <HttpDelete>
    <Route("images/{imageId:int}")>
    Public Function DeleteImage(imageId As Integer) As IHttpActionResult

        Dim image = db.ProductImages.FirstOrDefault(Function(i) i.ProductImageId = imageId)

        If image Is Nothing Then
            Return NotFound()
        End If

        Dim productId As Integer = image.ProductId
        Dim wasMain As Boolean = image.IsMain
        Dim imageUrl As String = image.ImageUrl

        db.ProductImages.Remove(image)
        db.SaveChanges()

        If wasMain Then
            db.Database.ExecuteSqlCommand(
                "UPDATE dbo.ProductImages SET IsMain = 1 WHERE ProductImageId = " &
                "(SELECT TOP 1 ProductImageId FROM dbo.ProductImages WHERE ProductId = {0} ORDER BY SortOrder, ProductImageId)",
                productId)
        End If

        DeleteUploadedFile(imageUrl)

        Return Ok(ListImages(productId))

    End Function


    ' =========================================================
    ' Helpers
    ' =========================================================

    ''' <summary>Current images of a product, read fresh from the database.</summary>
    Private Function ListImages(productId As Integer) As List(Of AdminImageDto)

        Dim rows = db.ProductImages.AsNoTracking() _
            .Where(Function(i) i.ProductId = productId) _
            .OrderByDescending(Function(i) i.IsMain) _
            .ThenBy(Function(i) i.SortOrder) _
            .ThenBy(Function(i) i.ProductImageId) _
            .ToList()

        Return rows.Select(Function(i) ToDto(i)).ToList()

    End Function

    Private Shared Function ToDto(row As ProductImage) As AdminImageDto
        Return New AdminImageDto With {
            .ProductImageId = row.ProductImageId,
            .ImageUrl = row.ImageUrl,
            .SortOrder = row.SortOrder,
            .IsMain = row.IsMain
        }
    End Function

    ''' <summary>".jpg", ".png" or ".webp" based on the file's first bytes, or Nothing.</summary>
    Private Shared Function DetectImageExtension(bytes As Byte()) As String

        ' JPEG: FF D8 FF
        If bytes.Length >= 3 AndAlso bytes(0) = &HFF AndAlso bytes(1) = &HD8 AndAlso bytes(2) = &HFF Then
            Return ".jpg"
        End If

        ' PNG: 89 50 4E 47 0D 0A 1A 0A
        If bytes.Length >= 8 AndAlso
           bytes(0) = &H89 AndAlso bytes(1) = &H50 AndAlso bytes(2) = &H4E AndAlso bytes(3) = &H47 AndAlso
           bytes(4) = &HD AndAlso bytes(5) = &HA AndAlso bytes(6) = &H1A AndAlso bytes(7) = &HA Then
            Return ".png"
        End If

        ' WebP: "RIFF" .... "WEBP"
        If bytes.Length >= 12 AndAlso
           bytes(0) = &H52 AndAlso bytes(1) = &H49 AndAlso bytes(2) = &H46 AndAlso bytes(3) = &H46 AndAlso
           bytes(8) = &H57 AndAlso bytes(9) = &H45 AndAlso bytes(10) = &H42 AndAlso bytes(11) = &H50 Then
            Return ".webp"
        End If

        Return Nothing

    End Function

    ''' <summary>Deletes the file behind a stored image URL, only inside the uploads folder.</summary>
    Private Shared Sub DeleteUploadedFile(imageUrl As String)

        If String.IsNullOrEmpty(imageUrl) OrElse
           Not imageUrl.StartsWith(UrlPrefix, StringComparison.OrdinalIgnoreCase) Then
            Return
        End If

        ' Only the file name is used, so a stored value can never point outside the folder
        Dim fileName As String = Path.GetFileName(imageUrl)

        If String.IsNullOrEmpty(fileName) Then
            Return
        End If

        Dim folder As String = HostingEnvironment.MapPath(UploadFolder)

        DeleteFileQuietly(Path.Combine(folder, fileName))

    End Sub

    Private Shared Sub DeleteFileQuietly(fullPath As String)
        Try
            If File.Exists(fullPath) Then
                File.Delete(fullPath)
            End If
        Catch ex As Exception
            Diagnostics.Trace.TraceError(ex.ToString())
        End Try
    End Sub

End Class
