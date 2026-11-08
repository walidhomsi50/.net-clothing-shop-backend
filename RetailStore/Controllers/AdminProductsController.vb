Imports System.Collections.Generic
Imports System.Linq
Imports System.Net
Imports System.Web.Http
Imports RetailStore.Data
Imports RetailStore.DTOs
Imports RetailStore.Helpers
Imports RetailStore.Models

''' <summary>
''' Product, variant and stock management for the admin panel. Admin and Staff accounts only.
''' Products and variants are never deleted: switch IsActive off to hide them
''' (old orders keep pointing at them).
''' </summary>
<RoutePrefix("api/v1/admin/products")>
<Authorize(Roles:="Admin,Staff")>
Public Class AdminProductsController
    Inherits ApiController

    Private ReadOnly db As New RetailStoreContext()

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            db.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub


    ' =========================================================
    ' GET api/v1/admin/products?search=linen&categoryId=2&isActive=true&page=1&pageSize=20
    ' search matches product name, brand, and variant SKU or barcode
    ' =========================================================
    <HttpGet>
    <Route("")>
    Public Function GetProducts(
        Optional search As String = Nothing,
        Optional categoryId As Integer? = Nothing,
        Optional isActive As Boolean? = Nothing,
        Optional page As Integer = 1,
        Optional pageSize As Integer = 20) As IHttpActionResult

        If page < 1 Then
            page = 1
        End If

        If pageSize < 1 Then
            pageSize = 20
        End If

        If pageSize > 100 Then
            pageSize = 100
        End If

        Dim query = db.Products.AsQueryable()

        If categoryId.HasValue Then
            Dim categoryFilter As Integer = categoryId.Value
            query = query.Where(Function(p) p.CategoryId = categoryFilter)
        End If

        If isActive.HasValue Then
            Dim activeFilter As Boolean = isActive.Value
            query = query.Where(Function(p) p.IsActive = activeFilter)
        End If

        If Not String.IsNullOrWhiteSpace(search) Then

            Dim term As String = search.Trim()

            query = query.Where(
                Function(p) p.Name.Contains(term) OrElse
                            p.Brand.Contains(term) OrElse
                            db.ProductVariants.Any(
                                Function(v) v.ProductId = p.ProductId AndAlso
                                            (v.SKU.Contains(term) OrElse v.Barcode.Contains(term))))

        End If

        Dim totalItems As Integer = query.Count()

        Dim products = query _
            .OrderByDescending(Function(p) p.CreatedDate) _
            .ThenByDescending(Function(p) p.ProductId) _
            .Skip((page - 1) * pageSize) _
            .Take(pageSize) _
            .ToList()

        Dim productIds = products.Select(Function(p) p.ProductId).ToList()
        Dim categoryIds = products.Select(Function(p) p.CategoryId).Distinct().ToList()

        Dim categoryNames = db.Categories _
            .Where(Function(c) categoryIds.Contains(c.CategoryId)) _
            .ToList() _
            .ToDictionary(Function(c) c.CategoryId, Function(c) c.Name)

        Dim stats = db.ProductVariants _
            .Where(Function(v) productIds.Contains(v.ProductId)) _
            .GroupBy(Function(v) v.ProductId) _
            .Select(Function(g) New With {
                .ProductId = g.Key,
                .Variants = g.Count(),
                .Stock = g.Sum(Function(v) v.StockQuantity)
            }) _
            .ToList()

        Dim variantCounts = stats.ToDictionary(Function(x) x.ProductId, Function(x) x.Variants)
        Dim stockTotals = stats.ToDictionary(Function(x) x.ProductId, Function(x) x.Stock)

        Dim images = ImageLookup.MainImageUrls(db, productIds)

        Dim items As New List(Of AdminProductListDto)()

        For Each row In products

            Dim dto As New AdminProductListDto With {
                .ProductId = row.ProductId,
                .Name = row.Name,
                .Slug = row.Slug,
                .Brand = row.Brand,
                .CategoryId = row.CategoryId,
                .Price = row.Price,
                .SalePrice = row.SalePrice,
                .IsActive = row.IsActive
            }

            Dim categoryName As String = Nothing
            If categoryNames.TryGetValue(row.CategoryId, categoryName) Then
                dto.CategoryName = categoryName
            End If

            Dim variantCount As Integer = 0
            If variantCounts.TryGetValue(row.ProductId, variantCount) Then
                dto.VariantCount = variantCount
                dto.TotalStock = stockTotals(row.ProductId)
            End If

            Dim imageUrl As String = Nothing
            If images.TryGetValue(row.ProductId, imageUrl) Then
                dto.MainImageUrl = imageUrl
            End If

            items.Add(dto)

        Next

        Return Ok(New With {
            .Page = page,
            .PageSize = pageSize,
            .TotalItems = totalItems,
            .TotalPages = CInt(Math.Ceiling(totalItems / CDbl(pageSize))),
            .Items = items
        })

    End Function


    ' =========================================================
    ' GET api/v1/admin/products/5
    ' =========================================================
    <HttpGet>
    <Route("{id:int}")>
    Public Function GetProduct(id As Integer) As IHttpActionResult

        Dim found = db.Products.FirstOrDefault(Function(p) p.ProductId = id)

        If found Is Nothing Then
            Return NotFound()
        End If

        Return Ok(BuildDetails(found))

    End Function


    ' =========================================================
    ' POST api/v1/admin/products
    ' { "categoryId": 2, "name": "Linen Shirt", "description": "...", "brand": "Acme",
    '   "price": 39.90, "salePrice": null, "isActive": true }
    ' Add variants (sizes/colors/stock) afterwards with POST .../5/variants
    ' =========================================================
    <HttpPost>
    <Route("")>
    Public Function CreateProduct(body As SaveProductRequest) As IHttpActionResult

        Dim problem As String = ValidateProduct(body)

        If problem IsNot Nothing Then
            Return BadRequest(problem)
        End If

        Dim name As String = body.Name.Trim()

        Dim created As New Product With {
            .CategoryId = body.CategoryId,
            .Name = name,
            .Slug = SlugHelper.UniqueProductSlug(db, name, 0),
            .Description = NullIfBlank(body.Description),
            .Brand = NullIfBlank(body.Brand),
            .Price = body.Price,
            .SalePrice = body.SalePrice,
            .IsActive = body.IsActive,
            .CreatedDate = DateTime.UtcNow
        }

        db.Products.Add(created)
        db.SaveChanges()

        Return Ok(BuildDetails(created))

    End Function


    ' =========================================================
    ' PUT api/v1/admin/products/5
    ' The slug does not change when you rename a product, so existing links keep working.
    ' =========================================================
    <HttpPut>
    <Route("{id:int}")>
    Public Function UpdateProduct(id As Integer, body As SaveProductRequest) As IHttpActionResult

        Dim found = db.Products.FirstOrDefault(Function(p) p.ProductId = id)

        If found Is Nothing Then
            Return NotFound()
        End If

        Dim problem As String = ValidateProduct(body)

        If problem IsNot Nothing Then
            Return BadRequest(problem)
        End If

        found.CategoryId = body.CategoryId
        found.Name = body.Name.Trim()
        found.Description = NullIfBlank(body.Description)
        found.Brand = NullIfBlank(body.Brand)
        found.Price = body.Price
        found.SalePrice = body.SalePrice
        found.IsActive = body.IsActive

        db.SaveChanges()

        Return Ok(BuildDetails(found))

    End Function


    ' =========================================================
    ' POST api/v1/admin/products/5/variants
    ' { "colorId": 1, "sizeId": 3, "sku": "LS-BLK-M", "barcode": "6221234567890",
    '   "stockQuantity": 10, "price": null, "isActive": true }
    ' =========================================================
    <HttpPost>
    <Route("{productId:int}/variants")>
    Public Function CreateVariant(productId As Integer, body As SaveVariantRequest) As IHttpActionResult

        If Not db.Products.Any(Function(p) p.ProductId = productId) Then
            Return NotFound()
        End If

        Dim problem As String = ValidateVariant(body, productId, 0, True)

        If problem IsNot Nothing Then
            Return BadRequest(problem)
        End If

        Dim created As New ProductVariant With {
            .ProductId = productId,
            .ColorId = body.ColorId,
            .SizeId = body.SizeId,
            .SKU = body.SKU.Trim(),
            .Barcode = NullIfBlank(body.Barcode),
            .StockQuantity = body.StockQuantity,
            .Price = body.Price,
            .IsActive = body.IsActive,
            .CreatedDate = DateTime.UtcNow
        }

        db.ProductVariants.Add(created)
        db.SaveChanges()

        Return Ok(LoadVariant(created.ProductVariantId))

    End Function


    ' =========================================================
    ' PUT api/v1/admin/variants/12
    ' Changes color, size, SKU, barcode, price and active flag.
    ' StockQuantity in the body is ignored here: use the stock endpoint, which stays
    ' correct even while customers are ordering.
    ' =========================================================
    <HttpPut>
    <Route("~/api/v1/admin/variants/{variantId:int}")>
    Public Function UpdateVariant(variantId As Integer, body As SaveVariantRequest) As IHttpActionResult

        Dim found = db.ProductVariants.FirstOrDefault(Function(v) v.ProductVariantId = variantId)

        If found Is Nothing Then
            Return NotFound()
        End If

        Dim problem As String = ValidateVariant(body, found.ProductId, variantId, False)

        If problem IsNot Nothing Then
            Return BadRequest(problem)
        End If

        found.ColorId = body.ColorId
        found.SizeId = body.SizeId
        found.SKU = body.SKU.Trim()
        found.Barcode = NullIfBlank(body.Barcode)
        found.Price = body.Price
        found.IsActive = body.IsActive

        db.SaveChanges()

        Return Ok(LoadVariant(variantId))

    End Function


    ' =========================================================
    ' POST api/v1/admin/variants/12/stock      { "delta": 10 }   or   { "delta": -2 }
    ' The change is applied inside the database in one step, so it can never overwrite
    ' stock that a customer order changed a moment earlier. Stock cannot go below zero.
    ' =========================================================
    <HttpPost>
    <Route("~/api/v1/admin/variants/{variantId:int}/stock")>
    Public Function AdjustStock(variantId As Integer, body As AdjustStockRequest) As IHttpActionResult

        If body Is Nothing OrElse body.Delta = 0 Then
            Return BadRequest("Delta must be a positive or negative number.")
        End If

        If Math.Abs(CLng(body.Delta)) > 100000 Then
            Return BadRequest("Delta is too large.")
        End If

        If Not db.ProductVariants.Any(Function(v) v.ProductVariantId = variantId) Then
            Return NotFound()
        End If

        Dim affected As Integer = db.Database.ExecuteSqlCommand(
            "UPDATE dbo.ProductVariants SET StockQuantity = StockQuantity + {0} " &
            "WHERE ProductVariantId = {1} AND StockQuantity + {0} >= 0",
            body.Delta,
            variantId)

        If affected = 0 Then
            Return BadRequest("Stock cannot go below zero.")
        End If

        Dim newStock As Integer = db.Database _
            .SqlQuery(Of Integer)("SELECT StockQuantity FROM dbo.ProductVariants WHERE ProductVariantId = {0}", variantId) _
            .FirstOrDefault()

        Return Ok(New With {
            .ProductVariantId = variantId,
            .StockQuantity = newStock
        })

    End Function


    ' =========================================================
    ' Validation
    ' =========================================================

    ''' <summary>Returns an error message, or Nothing when the request is fine.</summary>
    Private Function ValidateProduct(body As SaveProductRequest) As String

        If body Is Nothing Then
            Return "Request is required."
        End If

        If String.IsNullOrWhiteSpace(body.Name) Then
            Return "Name is required."
        End If

        If body.Name.Trim().Length > 200 Then
            Return "Name is too long (maximum 200 characters)."
        End If

        If InputValidator.TooLong(body.Brand, 100) Then
            Return "Brand is too long (maximum 100 characters)."
        End If

        If body.Price < 0D Then
            Return "Price cannot be negative."
        End If

        If body.SalePrice.HasValue Then

            If body.SalePrice.Value < 0D Then
                Return "Sale price cannot be negative."
            End If

            If body.SalePrice.Value > body.Price Then
                Return "Sale price cannot be higher than the normal price."
            End If

        End If

        Dim categoryId As Integer = body.CategoryId

        If Not db.Categories.Any(Function(c) c.CategoryId = categoryId) Then
            Return "Category was not found."
        End If

        Return Nothing

    End Function


    ''' <summary>Returns an error message, or Nothing when the request is fine.</summary>
    Private Function ValidateVariant(
        body As SaveVariantRequest,
        productId As Integer,
        ownVariantId As Integer,
        isNew As Boolean) As String

        If body Is Nothing Then
            Return "Request is required."
        End If

        If String.IsNullOrWhiteSpace(body.SKU) Then
            Return "SKU is required."
        End If

        Dim sku As String = body.SKU.Trim()

        If sku.Length > 100 Then
            Return "SKU is too long (maximum 100 characters)."
        End If

        If db.ProductVariants.Any(Function(v) v.SKU = sku AndAlso v.ProductVariantId <> ownVariantId) Then
            Return "This SKU is already used by another variant."
        End If

        Dim barcode As String = NullIfBlank(body.Barcode)

        If barcode IsNot Nothing Then

            If barcode.Length > 100 Then
                Return "Barcode is too long (maximum 100 characters)."
            End If

            If db.ProductVariants.Any(Function(v) v.Barcode = barcode AndAlso v.ProductVariantId <> ownVariantId) Then
                Return "This barcode is already used by another variant."
            End If

        End If

        If isNew AndAlso body.StockQuantity < 0 Then
            Return "Stock quantity cannot be negative."
        End If

        If body.Price.HasValue AndAlso body.Price.Value < 0D Then
            Return "Price cannot be negative."
        End If

        If body.ColorId.HasValue Then
            Dim colorId As Integer = body.ColorId.Value
            If Not db.Colors.Any(Function(c) c.ColorId = colorId AndAlso c.IsActive) Then
                Return "Color was not found."
            End If
        End If

        If body.SizeId.HasValue Then
            Dim sizeId As Integer = body.SizeId.Value
            If Not db.Sizes.Any(Function(s) s.SizeId = sizeId AndAlso s.IsActive) Then
                Return "Size was not found."
            End If
        End If

        ' The same color + size combination can only exist once per product
        Dim sameCombination = db.ProductVariants.Where(
            Function(v) v.ProductId = productId AndAlso v.ProductVariantId <> ownVariantId)

        If body.ColorId.HasValue Then
            Dim wantedColor As Integer = body.ColorId.Value
            sameCombination = sameCombination.Where(Function(v) v.ColorId.Value = wantedColor)
        Else
            sameCombination = sameCombination.Where(Function(v) Not v.ColorId.HasValue)
        End If

        If body.SizeId.HasValue Then
            Dim wantedSize As Integer = body.SizeId.Value
            sameCombination = sameCombination.Where(Function(v) v.SizeId.Value = wantedSize)
        Else
            sameCombination = sameCombination.Where(Function(v) Not v.SizeId.HasValue)
        End If

        If sameCombination.Any() Then
            Return "This product already has a variant with the same color and size."
        End If

        Return Nothing

    End Function


    ' =========================================================
    ' Mapping helpers
    ' =========================================================

    Private Function BuildDetails(found As Product) As AdminProductDetailsDto

        Dim productId As Integer = found.ProductId

        Dim categoryName As String = db.Categories _
            .Where(Function(c) c.CategoryId = found.CategoryId) _
            .Select(Function(c) c.Name) _
            .FirstOrDefault()

        Dim variantRows = db.ProductVariants _
            .Include("Color") _
            .Include("Size") _
            .Where(Function(v) v.ProductId = productId) _
            .OrderBy(Function(v) v.ProductVariantId) _
            .ToList()

        Dim imageRows = db.ProductImages _
            .Where(Function(i) i.ProductId = productId) _
            .OrderByDescending(Function(i) i.IsMain) _
            .ThenBy(Function(i) i.SortOrder) _
            .ToList()

        Dim result As New AdminProductDetailsDto With {
            .ProductId = found.ProductId,
            .Name = found.Name,
            .Slug = found.Slug,
            .Description = found.Description,
            .Brand = found.Brand,
            .CategoryId = found.CategoryId,
            .CategoryName = categoryName,
            .Price = found.Price,
            .SalePrice = found.SalePrice,
            .IsActive = found.IsActive,
            .CreatedDate = found.CreatedDate,
            .UpdatedDate = found.UpdatedDate,
            .Variants = New List(Of AdminVariantDto)(),
            .Images = New List(Of AdminImageDto)()
        }

        For Each row In variantRows
            result.Variants.Add(ToVariantDto(row))
        Next

        For Each image In imageRows
            result.Images.Add(New AdminImageDto With {
                .ProductImageId = image.ProductImageId,
                .ImageUrl = image.ImageUrl,
                .SortOrder = image.SortOrder,
                .IsMain = image.IsMain
            })
        Next

        Return result

    End Function

    Private Function LoadVariant(variantId As Integer) As AdminVariantDto

        Dim row = db.ProductVariants _
            .Include("Color") _
            .Include("Size") _
            .FirstOrDefault(Function(v) v.ProductVariantId = variantId)

        Return ToVariantDto(row)

    End Function

    Private Shared Function ToVariantDto(row As ProductVariant) As AdminVariantDto

        Dim dto As New AdminVariantDto With {
            .ProductVariantId = row.ProductVariantId,
            .ProductId = row.ProductId,
            .ColorId = row.ColorId,
            .SizeId = row.SizeId,
            .SKU = row.SKU,
            .Barcode = row.Barcode,
            .StockQuantity = row.StockQuantity,
            .Price = row.Price,
            .IsActive = row.IsActive
        }

        If row.Color IsNot Nothing Then
            dto.ColorName = row.Color.Name
        End If

        If row.Size IsNot Nothing Then
            dto.SizeName = row.Size.Name
        End If

        Return dto

    End Function

    Private Shared Function NullIfBlank(value As String) As String
        If String.IsNullOrWhiteSpace(value) Then
            Return Nothing
        End If
        Return value.Trim()
    End Function

End Class
