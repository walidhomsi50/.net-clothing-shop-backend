Imports System.Collections.Generic
Imports System.Data.Entity
Imports System.Data.Entity.Infrastructure
Imports System.Linq
Imports System.Net
Imports System.Web.Http
Imports RetailStore.Data
Imports RetailStore.DTOs
Imports RetailStore.Helpers
Imports RetailStore.Models

''' <summary>
''' Works for BOTH logged-in customers and guests:
'''   - customer: send the Bearer token
'''   - guest:    send a header  X-Guest-Token: &lt;a GUID your frontend generates once and keeps&gt;
''' </summary>
<RoutePrefix("api/v1/cart")>
Public Class CartController
    Inherits ApiController

    Private ReadOnly db As New RetailStoreContext()

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            db.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub


    ' =========================================================
    ' GET api/v1/cart
    ' =========================================================
    <HttpGet>
    <Route("")>
    Public Function GetCart() As IHttpActionResult

        Dim owner = GetOwner()

        If Not owner.IsValid Then
            Return MissingOwner()
        End If

        Dim cart = FindCart(owner)

        If cart Is Nothing Then
            Return Ok(EmptyCart(owner))
        End If

        Return Ok(BuildCartDto(cart))

    End Function


    ' =========================================================
    ' POST api/v1/cart/items   { "productVariantId": 5, "quantity": 1 }
    ' =========================================================
    <HttpPost>
    <Route("items")>
    Public Function AddItem(body As AddCartItemRequest) As IHttpActionResult

        Dim owner = GetOwner()

        If Not owner.IsValid Then
            Return MissingOwner()
        End If

        If body Is Nothing Then
            Return BadRequest("Request is required.")
        End If

        If body.ProductVariantId <= 0 Then
            Return BadRequest("ProductVariantId is required.")
        End If

        If body.Quantity <= 0 Then
            Return BadRequest("Quantity must be greater than zero.")
        End If

        Dim variantId As Integer = body.ProductVariantId

        Dim selectedVariant = db.ProductVariants _
            .Include("Product") _
            .FirstOrDefault(Function(v) v.ProductVariantId = variantId AndAlso v.IsActive)

        If selectedVariant Is Nothing OrElse selectedVariant.Product Is Nothing OrElse Not selectedVariant.Product.IsActive Then
            Return NotFound()
        End If

        Dim cart = GetOrCreateCart(owner)
        Dim cartId As Integer = cart.CartId

        Dim existing = db.CartItems.FirstOrDefault(
            Function(i) i.CartId = cartId AndAlso i.ProductVariantId = variantId)

        Dim newQuantity As Integer = body.Quantity

        If existing IsNot Nothing Then
            newQuantity = existing.Quantity + body.Quantity
        End If

        Dim problem = CheckQuantity(newQuantity, selectedVariant)

        If problem IsNot Nothing Then
            Return BadRequest(problem)
        End If

        Dim utcNow As DateTime = DateTime.UtcNow

        If existing IsNot Nothing Then
            existing.Quantity = newQuantity
            existing.UpdatedDate = utcNow
        Else
            db.CartItems.Add(New CartItem With {
                .CartId = cartId,
                .ProductVariantId = variantId,
                .Quantity = newQuantity,
                .CreatedDate = utcNow
            })
        End If

        cart.UpdatedDate = utcNow

        Try
            db.SaveChanges()
        Catch ex As DbUpdateException
            Diagnostics.Trace.TraceError(ex.ToString())
            Return Content(HttpStatusCode.Conflict, "The cart was changed by another request. Please try again.")
        End Try

        Return Ok(BuildCartDto(cart))

    End Function


    ' =========================================================
    ' PUT api/v1/cart/items/12   { "quantity": 2 }
    ' =========================================================
    <HttpPut>
    <Route("items/{id:int}")>
    Public Function UpdateItem(id As Integer, body As UpdateCartItemRequest) As IHttpActionResult

        Dim owner = GetOwner()

        If Not owner.IsValid Then
            Return MissingOwner()
        End If

        If body Is Nothing Then
            Return BadRequest("Request is required.")
        End If

        If body.Quantity <= 0 Then
            Return BadRequest("Quantity must be greater than zero.")
        End If

        Dim cart = FindCart(owner)

        If cart Is Nothing Then
            Return NotFound()
        End If

        Dim cartId As Integer = cart.CartId

        Dim item = db.CartItems _
            .Include("ProductVariant.Product") _
            .FirstOrDefault(Function(i) i.CartItemId = id AndAlso i.CartId = cartId)

        If item Is Nothing Then
            Return NotFound()
        End If

        Dim selectedVariant = item.ProductVariant

        If selectedVariant Is Nothing OrElse Not selectedVariant.IsActive OrElse
           selectedVariant.Product Is Nothing OrElse Not selectedVariant.Product.IsActive Then

            Return BadRequest("This item is no longer available. Please remove it from your cart.")
        End If

        Dim problem = CheckQuantity(body.Quantity, selectedVariant)

        If problem IsNot Nothing Then
            Return BadRequest(problem)
        End If

        item.Quantity = body.Quantity
        item.UpdatedDate = DateTime.UtcNow
        cart.UpdatedDate = DateTime.UtcNow

        db.SaveChanges()

        Return Ok(BuildCartDto(cart))

    End Function


    ' =========================================================
    ' DELETE api/v1/cart/items/12
    ' =========================================================
    <HttpDelete>
    <Route("items/{id:int}")>
    Public Function RemoveItem(id As Integer) As IHttpActionResult

        Dim owner = GetOwner()

        If Not owner.IsValid Then
            Return MissingOwner()
        End If

        Dim cart = FindCart(owner)

        If cart Is Nothing Then
            Return NotFound()
        End If

        Dim cartId As Integer = cart.CartId

        Dim item = db.CartItems.FirstOrDefault(Function(i) i.CartItemId = id AndAlso i.CartId = cartId)

        If item Is Nothing Then
            Return NotFound()
        End If

        db.CartItems.Remove(item)
        cart.UpdatedDate = DateTime.UtcNow
        db.SaveChanges()

        Return Ok(BuildCartDto(cart))

    End Function


    ' =========================================================
    ' DELETE api/v1/cart   (empties the cart)
    ' =========================================================
    <HttpDelete>
    <Route("")>
    Public Function ClearCart() As IHttpActionResult

        Dim owner = GetOwner()

        If Not owner.IsValid Then
            Return MissingOwner()
        End If

        Dim cart = FindCart(owner)

        If cart Is Nothing Then
            Return Ok(EmptyCart(owner))
        End If

        Dim cartId As Integer = cart.CartId

        db.CartItems.RemoveRange(db.CartItems.Where(Function(i) i.CartId = cartId).ToList())
        cart.UpdatedDate = DateTime.UtcNow
        db.SaveChanges()

        Return Ok(BuildCartDto(cart))

    End Function


    ' =========================================================
    ' POST api/v1/cart/merge
    ' Call right after a customer logs in, sending BOTH the Bearer token and the
    ' X-Guest-Token header. The guest cart is moved into the customer's cart.
    ' =========================================================
    <HttpPost>
    <Route("merge")>
    <Authorize(Roles:="Customer")>
    Public Function MergeGuestCart() As IHttpActionResult

        Dim owner = GetOwner()

        If Not owner.CustomerId.HasValue Then
            Return MissingOwner()
        End If

        Dim guestToken = CartOwner.ReadGuestToken(Me.Request)

        If Not guestToken.HasValue Then
            Dim current = FindCart(owner)
            If current Is Nothing Then
                Return Ok(EmptyCart(owner))
            End If
            Return Ok(BuildCartDto(current))
        End If

        Dim token As Guid = guestToken.Value
        Dim guestCart = db.Carts.FirstOrDefault(Function(c) c.GuestToken.Value = token)

        Dim customerCart = GetOrCreateCart(owner)

        If guestCart Is Nothing Then
            Return Ok(BuildCartDto(customerCart))
        End If

        Dim guestCartId As Integer = guestCart.CartId
        Dim customerCartId As Integer = customerCart.CartId

        Dim guestItems = db.CartItems _
            .Include("ProductVariant") _
            .Where(Function(i) i.CartId = guestCartId) _
            .ToList()

        Dim customerItems = db.CartItems.Where(Function(i) i.CartId = customerCartId).ToList()

        Dim utcNow As DateTime = DateTime.UtcNow

        For Each guestItem In guestItems

            Dim selectedVariant = guestItem.ProductVariant

            If selectedVariant Is Nothing OrElse Not selectedVariant.IsActive Then
                Continue For
            End If

            Dim limit As Integer = Math.Min(PricingHelper.MaxQuantityPerItem, selectedVariant.StockQuantity)

            If limit <= 0 Then
                Continue For
            End If

            Dim variantId As Integer = guestItem.ProductVariantId
            Dim existing = customerItems.FirstOrDefault(Function(ci) ci.ProductVariantId = variantId)

            If existing IsNot Nothing Then

                existing.Quantity = Math.Min(existing.Quantity + guestItem.Quantity, limit)
                existing.UpdatedDate = utcNow

            Else

                Dim moved As New CartItem With {
                    .CartId = customerCartId,
                    .ProductVariantId = variantId,
                    .Quantity = Math.Min(guestItem.Quantity, limit),
                    .CreatedDate = utcNow
                }

                db.CartItems.Add(moved)
                customerItems.Add(moved)

            End If

        Next

        db.CartItems.RemoveRange(guestItems)
        db.Carts.Remove(guestCart)
        customerCart.UpdatedDate = utcNow

        db.SaveChanges()

        Return Ok(BuildCartDto(customerCart))

    End Function


    ' =========================================================
    ' Helpers
    ' =========================================================

    Private Function GetOwner() As CartOwner
        Return CartOwner.Resolve(Me.Request, Me.User)
    End Function

    Private Function MissingOwner() As IHttpActionResult
        Return Content(HttpStatusCode.Unauthorized,
            "Sign in as a customer, or send a valid X-Guest-Token header.")
    End Function

    Private Shared Function CheckQuantity(quantity As Integer, selectedVariant As ProductVariant) As String

        If quantity > PricingHelper.MaxQuantityPerItem Then
            Return "You can order at most " & PricingHelper.MaxQuantityPerItem & " of the same item."
        End If

        If quantity > selectedVariant.StockQuantity Then
            Return "Requested quantity is greater than available stock."
        End If

        Return Nothing

    End Function

    Private Function FindCart(owner As CartOwner) As Cart

        If owner.CustomerId.HasValue Then
            Dim customerId As Integer = owner.CustomerId.Value
            Return db.Carts.FirstOrDefault(Function(c) c.CustomerId.Value = customerId)
        End If

        Dim token As Guid = owner.GuestToken.Value
        Return db.Carts.FirstOrDefault(Function(c) c.GuestToken.Value = token)

    End Function

    Private Function GetOrCreateCart(owner As CartOwner) As Cart

        Dim cart = FindCart(owner)

        If cart IsNot Nothing Then
            Return cart
        End If

        cart = New Cart With {
            .CustomerId = owner.CustomerId,
            .GuestToken = owner.GuestToken,
            .CreatedDate = DateTime.UtcNow
        }

        db.Carts.Add(cart)

        Try
            db.SaveChanges()
        Catch ex As DbUpdateException
            ' Two requests created the cart at the same moment: use the one that won
            db.Entry(cart).State = EntityState.Detached
            Dim existing = FindCart(owner)
            If existing Is Nothing Then
                Throw
            End If
            Return existing
        End Try

        Return cart

    End Function

    Private Function EmptyCart(owner As CartOwner) As CartDto
        Return New CartDto With {
            .CartId = 0,
            .CustomerId = owner.CustomerId,
            .Items = New List(Of CartItemDto)(),
            .TotalItems = 0,
            .TotalAmount = 0D
        }
    End Function

    ' Three queries in total, however many items the cart has
    Private Function BuildCartDto(cart As Cart) As CartDto

        Dim cartId As Integer = cart.CartId

        Dim items = db.CartItems _
            .Include("ProductVariant.Product") _
            .Include("ProductVariant.Color") _
            .Include("ProductVariant.Size") _
            .Where(Function(i) i.CartId = cartId) _
            .OrderBy(Function(i) i.CartItemId) _
            .ToList()

        Dim productIds = items _
            .Where(Function(i) i.ProductVariant IsNot Nothing) _
            .Select(Function(i) i.ProductVariant.ProductId) _
            .Distinct() _
            .ToList()

        Dim images = ImageLookup.MainImageUrls(db, productIds)

        Dim result As New CartDto With {
            .CartId = cart.CartId,
            .CustomerId = cart.CustomerId,
            .Items = New List(Of CartItemDto)(),
            .TotalItems = 0,
            .TotalAmount = 0D
        }

        For Each item In items

            Dim selectedVariant = item.ProductVariant

            If selectedVariant Is Nothing OrElse selectedVariant.Product Is Nothing Then
                Continue For
            End If

            Dim parentProduct = selectedVariant.Product

            Dim isActive As Boolean = selectedVariant.IsActive AndAlso parentProduct.IsActive
            Dim isAvailable As Boolean = isActive AndAlso selectedVariant.StockQuantity >= item.Quantity

            Dim unitPrice As Decimal = PricingHelper.GetEffectivePrice(selectedVariant, parentProduct)

            Dim dto As New CartItemDto With {
                .CartItemId = item.CartItemId,
                .ProductId = parentProduct.ProductId,
                .ProductName = parentProduct.Name,
                .ProductVariantId = selectedVariant.ProductVariantId,
                .ColorId = selectedVariant.ColorId,
                .SizeId = selectedVariant.SizeId,
                .SKU = selectedVariant.SKU,
                .UnitPrice = unitPrice,
                .Quantity = item.Quantity,
                .TotalPrice = unitPrice * item.Quantity,
                .IsAvailable = isAvailable,
                .AvailableQuantity = If(isActive, Math.Min(selectedVariant.StockQuantity, PricingHelper.MaxQuantityPerItem), 0)
            }

            If selectedVariant.Color IsNot Nothing Then
                dto.ColorName = selectedVariant.Color.Name
            End If

            If selectedVariant.Size IsNot Nothing Then
                dto.SizeName = selectedVariant.Size.Name
            End If

            Dim imageUrl As String = Nothing
            If images.TryGetValue(parentProduct.ProductId, imageUrl) Then
                dto.ImageUrl = imageUrl
            End If

            result.Items.Add(dto)

            If isAvailable Then
                result.TotalItems += item.Quantity
                result.TotalAmount += dto.TotalPrice
            End If

        Next

        Return result

    End Function

End Class
