Imports System.Collections.Generic
Imports System.Linq
Imports System.Web.Http
Imports RetailStore.Data
Imports RetailStore.DTOs
Imports RetailStore.Helpers
Imports RetailStore.Models

''' <summary>
''' REPLACES the old ProductsController. Public storefront catalog (no login needed).
''' Products are looked up by slug, filtered and sorted, and never expose barcodes or exact stock.
''' </summary>
<RoutePrefix("api/v1/products")>
Public Class ProductsController
    Inherits ApiController

    Private Const MaxPageSize As Integer = 60

    Private ReadOnly db As New RetailStoreContext()

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            db.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub


    ' =========================================================
    ' GET api/v1/products
    '   ?categorySlug=shirts        a category and everything under it
    '   &search=linen               name or brand
    '   &minPrice=10&maxPrice=50    price you pay (sale price when there is one)
    '   &sizes=2,3&colors=1         ids from /api/v1/catalog/filters; only items in stock match
    '   &onSale=true&inStock=true
    '   &sort=newest|price_asc|price_desc|name
    '   &page=1&pageSize=24
    ' =========================================================
    <HttpGet>
    <Route("")>
    Public Function GetProducts(
        Optional categorySlug As String = Nothing,
        Optional search As String = Nothing,
        Optional minPrice As Decimal? = Nothing,
        Optional maxPrice As Decimal? = Nothing,
        Optional sizes As String = Nothing,
        Optional colors As String = Nothing,
        Optional onSale As Boolean = False,
        Optional inStock As Boolean = False,
        Optional sort As String = Nothing,
        Optional page As Integer = 1,
        Optional pageSize As Integer = 24) As IHttpActionResult

        If page < 1 Then
            page = 1
        End If

        If pageSize < 1 Then
            pageSize = 24
        End If

        If pageSize > MaxPageSize Then
            pageSize = MaxPageSize
        End If

        ' Which categories can customers see?
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

        Dim query = CatalogQueries.Sellable(db, scopeIds)

        If Not String.IsNullOrWhiteSpace(search) Then
            Dim term As String = search.Trim()
            query = query.Where(Function(p) p.Name.Contains(term) OrElse p.Brand.Contains(term))
        End If

        If minPrice.HasValue Then
            Dim lowest As Decimal = minPrice.Value
            query = query.Where(Function(p) If(p.SalePrice.HasValue, p.SalePrice.Value, p.Price) >= lowest)
        End If

        If maxPrice.HasValue Then
            Dim highest As Decimal = maxPrice.Value
            query = query.Where(Function(p) If(p.SalePrice.HasValue, p.SalePrice.Value, p.Price) <= highest)
        End If

        If onSale Then
            query = query.Where(Function(p) p.SalePrice.HasValue AndAlso p.SalePrice.Value < p.Price)
        End If

        If inStock Then
            query = query.Where(
                Function(p) db.ProductVariants.Any(
                    Function(v) v.ProductId = p.ProductId AndAlso v.IsActive AndAlso v.StockQuantity > 0))
        End If

        Dim sizeIds As List(Of Integer) = ParseIds(sizes)

        If sizeIds.Count > 0 Then
            query = query.Where(
                Function(p) db.ProductVariants.Any(
                    Function(v) v.ProductId = p.ProductId AndAlso v.IsActive AndAlso v.StockQuantity > 0 AndAlso
                                v.SizeId.HasValue AndAlso sizeIds.Contains(v.SizeId.Value)))
        End If

        Dim colorIds As List(Of Integer) = ParseIds(colors)

        If colorIds.Count > 0 Then
            query = query.Where(
                Function(p) db.ProductVariants.Any(
                    Function(v) v.ProductId = p.ProductId AndAlso v.IsActive AndAlso v.StockQuantity > 0 AndAlso
                                v.ColorId.HasValue AndAlso colorIds.Contains(v.ColorId.Value)))
        End If

        Dim totalItems As Integer = query.Count()

        Dim ordered As IOrderedQueryable(Of Product)

        Select Case If(sort, "").Trim().ToLowerInvariant()

            Case "price_asc"
                ordered = query _
                    .OrderBy(Function(p) If(p.SalePrice.HasValue, p.SalePrice.Value, p.Price)) _
                    .ThenBy(Function(p) p.ProductId)

            Case "price_desc"
                ordered = query _
                    .OrderByDescending(Function(p) If(p.SalePrice.HasValue, p.SalePrice.Value, p.Price)) _
                    .ThenBy(Function(p) p.ProductId)

            Case "name"
                ordered = query _
                    .OrderBy(Function(p) p.Name) _
                    .ThenBy(Function(p) p.ProductId)

            Case Else
                ordered = query _
                    .OrderByDescending(Function(p) p.CreatedDate) _
                    .ThenByDescending(Function(p) p.ProductId)

        End Select

        Dim products = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList()

        Return Ok(New With {
            .Page = page,
            .PageSize = pageSize,
            .TotalItems = totalItems,
            .TotalPages = CInt(Math.Ceiling(totalItems / CDbl(pageSize))),
            .Items = BuildListItems(products)
        })

    End Function


    ' =========================================================
    ' GET api/v1/products/black-linen-shirt
    ' =========================================================
    <HttpGet>
    <Route("{slug}")>
    Public Function GetProduct(slug As String) As IHttpActionResult

        Dim found = FindVisibleProduct(slug)

        If found Is Nothing Then
            Return NotFound()
        End If

        Dim productId As Integer = found.ProductId
        Dim categoryId As Integer = found.CategoryId

        Dim categoryInfo = db.Categories.AsNoTracking() _
            .Where(Function(c) c.CategoryId = categoryId) _
            .Select(Function(c) New With {c.Name, c.Slug}) _
            .FirstOrDefault()

        Dim variantRows = db.ProductVariants.AsNoTracking() _
            .Include("Color") _
            .Include("Size") _
            .Where(Function(v) v.ProductId = productId AndAlso v.IsActive) _
            .ToList() _
            .OrderBy(Function(v) If(v.Size IsNot Nothing, v.Size.SortOrder, Integer.MaxValue)) _
            .ThenBy(Function(v) If(v.Color IsNot Nothing, v.Color.Name, "")) _
            .ThenBy(Function(v) v.ProductVariantId) _
            .ToList()

        Dim imageRows = db.ProductImages.AsNoTracking() _
            .Where(Function(i) i.ProductId = productId) _
            .OrderByDescending(Function(i) i.IsMain) _
            .ThenBy(Function(i) i.SortOrder) _
            .ThenBy(Function(i) i.ProductImageId) _
            .ToList()

        Dim result As New CatalogProductDetailsDto With {
            .ProductId = found.ProductId,
            .Name = found.Name,
            .Slug = found.Slug,
            .Description = found.Description,
            .Brand = found.Brand,
            .CategoryId = found.CategoryId,
            .Price = found.Price,
            .SalePrice = DiscountOrNothing(found),
            .Currency = PricingHelper.Currency,
            .InStock = variantRows.Any(Function(v) v.StockQuantity > 0),
            .Images = New List(Of CatalogImageDto)(),
            .Variants = New List(Of CatalogVariantDto)(),
            .Colors = New List(Of CatalogColorDto)(),
            .Sizes = New List(Of CatalogSizeDto)()
        }

        If categoryInfo IsNot Nothing Then
            result.CategoryName = categoryInfo.Name
            result.CategorySlug = categoryInfo.Slug
        End If

        For Each image In imageRows
            result.Images.Add(New CatalogImageDto With {
                .ImageUrl = image.ImageUrl,
                .SortOrder = image.SortOrder,
                .IsMain = image.IsMain
            })
        Next

        For Each row In variantRows

            Dim price As Decimal = PricingHelper.GetEffectivePrice(row, found)

            Dim dto As New CatalogVariantDto With {
                .ProductVariantId = row.ProductVariantId,
                .ColorId = row.ColorId,
                .SizeId = row.SizeId,
                .SKU = row.SKU,
                .Price = price,
                .OriginalPrice = If(price < found.Price, CType(found.Price, Decimal?), Nothing),
                .InStock = row.StockQuantity > 0,
                .AvailableQuantity = Math.Min(row.StockQuantity, PricingHelper.MaxQuantityPerItem)
            }

            If row.Color IsNot Nothing Then

                dto.ColorName = row.Color.Name

                If Not result.Colors.Any(Function(c) c.ColorId = row.Color.ColorId) Then
                    result.Colors.Add(New CatalogColorDto With {
                        .ColorId = row.Color.ColorId,
                        .Name = row.Color.Name,
                        .HexCode = row.Color.HexCode
                    })
                End If

            End If

            If row.Size IsNot Nothing Then

                dto.SizeName = row.Size.Name

                If Not result.Sizes.Any(Function(s) s.SizeId = row.Size.SizeId) Then
                    result.Sizes.Add(New CatalogSizeDto With {
                        .SizeId = row.Size.SizeId,
                        .Name = row.Size.Name,
                        .SortOrder = row.Size.SortOrder
                    })
                End If

            End If

            result.Variants.Add(dto)

        Next

        Return Ok(result)

    End Function


    ' =========================================================
    ' GET api/v1/products/black-linen-shirt/related     (up to 8 from the same category)
    ' =========================================================
    <HttpGet>
    <Route("{slug}/related")>
    Public Function GetRelated(slug As String) As IHttpActionResult

        Dim found = FindVisibleProduct(slug)

        If found Is Nothing Then
            Return NotFound()
        End If

        Dim productId As Integer = found.ProductId
        Dim sameCategory As New List(Of Integer) From {found.CategoryId}

        Dim products = CatalogQueries.Sellable(db, sameCategory) _
            .Where(Function(p) p.ProductId <> productId) _
            .OrderByDescending(Function(p) p.CreatedDate) _
            .ThenByDescending(Function(p) p.ProductId) _
            .Take(8) _
            .ToList()

        Return Ok(BuildListItems(products))

    End Function


    ' =========================================================
    ' Helpers
    ' =========================================================

    Private Function FindVisibleProduct(slug As String) As Product

        If String.IsNullOrWhiteSpace(slug) Then
            Return Nothing
        End If

        Dim cleanSlug As String = slug.Trim().ToLowerInvariant()

        Dim found = db.Products.FirstOrDefault(Function(p) p.Slug = cleanSlug AndAlso p.IsActive)

        If found Is Nothing Then
            Return Nothing
        End If

        Dim tree As New CategoryTree(db)

        If Not tree.IsVisible(found.CategoryId) Then
            Return Nothing
        End If

        Return found

    End Function

    ''' <summary>The sale price, but only when it really is lower than the normal price.</summary>
    Private Shared Function DiscountOrNothing(row As Product) As Decimal?
        If row.SalePrice.HasValue AndAlso row.SalePrice.Value < row.Price Then
            Return row.SalePrice
        End If
        Return Nothing
    End Function

    Private Shared Function ParseIds(text As String) As List(Of Integer)

        Dim result As New List(Of Integer)()

        If String.IsNullOrWhiteSpace(text) Then
            Return result
        End If

        For Each piece In text.Split(","c)

            Dim id As Integer

            If Integer.TryParse(piece.Trim(), id) AndAlso Not result.Contains(id) Then
                result.Add(id)
            End If

            If result.Count >= 20 Then
                Exit For
            End If

        Next

        Return result

    End Function

    ''' <summary>Product cards for a page of products: three queries however many products there are.</summary>
    Private Function BuildListItems(products As List(Of Product)) As List(Of CatalogProductListDto)

        Dim items As New List(Of CatalogProductListDto)()

        If products.Count = 0 Then
            Return items
        End If

        Dim productIds = products.Select(Function(p) p.ProductId).ToList()

        Dim variants = db.ProductVariants.AsNoTracking() _
            .Include("Color") _
            .Include("Size") _
            .Where(Function(v) v.IsActive AndAlso productIds.Contains(v.ProductId)) _
            .ToList()

        Dim imageUrls = db.ProductImages.AsNoTracking() _
            .Where(Function(i) productIds.Contains(i.ProductId)) _
            .OrderByDescending(Function(i) i.IsMain) _
            .ThenBy(Function(i) i.SortOrder) _
            .ThenBy(Function(i) i.ProductImageId) _
            .ToList() _
            .GroupBy(Function(i) i.ProductId) _
            .ToDictionary(Function(g) g.Key, Function(g) g.Select(Function(i) i.ImageUrl).Take(2).ToList())

        Dim newSince As DateTime = DateTime.UtcNow.AddDays(-14)

        For Each row In products

            Dim currentProductId As Integer = row.ProductId

            Dim ownVariants = variants.Where(Function(v) v.ProductId = currentProductId).ToList()
            Dim buyable = ownVariants.Where(Function(v) v.StockQuantity > 0).ToList()

            Dim dto As New CatalogProductListDto With {
                .ProductId = row.ProductId,
                .Name = row.Name,
                .Slug = row.Slug,
                .Brand = row.Brand,
                .CategoryId = row.CategoryId,
                .Price = row.Price,
                .SalePrice = DiscountOrNothing(row),
                .Currency = PricingHelper.Currency,
                .InStock = buyable.Count > 0,
                .IsNew = row.CreatedDate >= newSince,
                .Colors = New List(Of CatalogColorDto)(),
                .Sizes = New List(Of CatalogSizeDto)()
            }

            Dim urls As List(Of String) = Nothing

            If imageUrls.TryGetValue(currentProductId, urls) Then

                dto.MainImageUrl = urls(0)

                If urls.Count > 1 Then
                    dto.SecondImageUrl = urls(1)
                End If

            End If

            For Each variantRow In buyable

                If variantRow.Color IsNot Nothing Then

                    Dim colorId As Integer = variantRow.Color.ColorId

                    If Not dto.Colors.Any(Function(c) c.ColorId = colorId) Then
                        dto.Colors.Add(New CatalogColorDto With {
                            .ColorId = variantRow.Color.ColorId,
                            .Name = variantRow.Color.Name,
                            .HexCode = variantRow.Color.HexCode
                        })
                    End If

                End If

                If variantRow.Size IsNot Nothing Then

                    Dim sizeId As Integer = variantRow.Size.SizeId

                    If Not dto.Sizes.Any(Function(s) s.SizeId = sizeId) Then
                        dto.Sizes.Add(New CatalogSizeDto With {
                            .SizeId = variantRow.Size.SizeId,
                            .Name = variantRow.Size.Name,
                            .SortOrder = variantRow.Size.SortOrder
                        })
                    End If

                End If

            Next

            dto.Sizes = dto.Sizes.OrderBy(Function(s) s.SortOrder).ToList()

            items.Add(dto)

        Next

        Return items

    End Function

End Class
