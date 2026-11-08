Imports System.Collections.Generic
Imports System.Configuration
Imports System.Linq
Imports System.Net
Imports RetailStore.Data
Imports RetailStore.DTOs
Imports RetailStore.Helpers
Imports RetailStore.Models

Namespace Services

    ''' <summary>Who the order ships to, as text snapshots stored on the order.</summary>
    Public Class OrderShippingInfo
        Public Property FullName As String
        Public Property Phone As String
        Public Property Email As String
        Public Property AddressText As String
        Public Property Notes As String
    End Class


    ''' <summary>
    ''' All checkout rules in one place, shared by customer checkout, guest checkout and
    ''' (later) the admin panel. Expected failures are thrown as ApiException; your global
    ''' exception handler turns them into clean JSON { "message": "..." } responses.
    ''' </summary>
    Public Class OrderService

        Private ReadOnly _db As RetailStoreContext

        Public Sub New(db As RetailStoreContext)
            _db = db
        End Sub


        ' =====================================================
        ' Payment method
        ' =====================================================
        Public Shared Function NormalizePaymentMethod(value As String) As String

            If String.IsNullOrWhiteSpace(value) OrElse
               String.Equals(value.Trim(), PaymentMethods.COD, StringComparison.OrdinalIgnoreCase) Then

                Return PaymentMethods.COD
            End If

            If String.Equals(value.Trim(), PaymentMethods.Card, StringComparison.OrdinalIgnoreCase) Then

                ' Card checkout stays switched off until the payment gateway is integrated.
                ' Enable with <add key="CardPaymentsEnabled" value="true" /> in web.config.
                Dim enabled = ConfigurationManager.AppSettings("CardPaymentsEnabled")

                If Not String.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase) Then
                    Throw New ApiException(HttpStatusCode.BadRequest,
                        "Card payments are not available yet. Please choose cash on delivery.")
                End If

                Return PaymentMethods.Card
            End If

            Throw New ApiException(HttpStatusCode.BadRequest, "PaymentMethod must be 'COD' or 'Card'.")

        End Function


        ' =====================================================
        ' Place an order from a cart
        ' =====================================================
        Public Function PlaceOrder(
            cart As Cart,
            customerId As Integer?,
            shipping As OrderShippingInfo,
            paymentMethod As String) As Order

            If cart Is Nothing Then
                Throw New ApiException(HttpStatusCode.BadRequest, "Cart is empty.")
            End If

            Dim cartId As Integer = cart.CartId
            Dim cartItems = _db.CartItems.Where(Function(i) i.CartId = cartId).ToList()

            If cartItems.Count = 0 Then
                Throw New ApiException(HttpStatusCode.BadRequest, "Cart is empty.")
            End If

            ' Load every variant (with product, size, color) in ONE query
            Dim variantIds = cartItems.Select(Function(i) i.ProductVariantId).Distinct().ToList()

            Dim variants = _db.ProductVariants _
                .Include("Product") _
                .Include("Size") _
                .Include("Color") _
                .Where(Function(v) variantIds.Contains(v.ProductVariantId)) _
                .ToList() _
                .ToDictionary(Function(v) v.ProductVariantId)

            Dim orderItems As New List(Of OrderItem)()
            Dim subtotal As Decimal = 0D
            Dim newOrder As Order = Nothing

            Using tx = _db.Database.BeginTransaction()

                For Each cartItem In cartItems

                    Dim selectedVariant As ProductVariant = Nothing

                    If Not variants.TryGetValue(cartItem.ProductVariantId, selectedVariant) OrElse
                       Not selectedVariant.IsActive OrElse
                       selectedVariant.Product Is Nothing OrElse
                       Not selectedVariant.Product.IsActive Then

                        Throw New ApiException(HttpStatusCode.BadRequest,
                            "An item in your cart is no longer available. Please review your cart.")
                    End If

                    ' ATOMIC stock deduction: the database only subtracts when enough stock is
                    ' left, so two customers can never buy the last item at the same time.
                    Dim affected As Integer = _db.Database.ExecuteSqlCommand(
                        "UPDATE dbo.ProductVariants SET StockQuantity = StockQuantity - {0} " &
                        "WHERE ProductVariantId = {1} AND StockQuantity >= {0}",
                        cartItem.Quantity,
                        selectedVariant.ProductVariantId)

                    If affected = 0 Then
                        Throw New ApiException(HttpStatusCode.BadRequest,
                            "Not enough stock for " & Describe(selectedVariant) & ".")
                    End If

                    Dim unitPrice As Decimal = PricingHelper.GetEffectivePrice(selectedVariant, selectedVariant.Product)

                    subtotal += unitPrice * cartItem.Quantity

                    Dim sizeName As String = Nothing
                    If selectedVariant.Size IsNot Nothing Then
                        sizeName = selectedVariant.Size.Name
                    End If

                    Dim colorName As String = Nothing
                    If selectedVariant.Color IsNot Nothing Then
                        colorName = selectedVariant.Color.Name
                    End If

                    orderItems.Add(New OrderItem With {
                        .ProductVariantId = selectedVariant.ProductVariantId,
                        .Quantity = cartItem.Quantity,
                        .UnitPrice = unitPrice,
                        .ProductName = selectedVariant.Product.Name,
                        .SKU = selectedVariant.SKU,
                        .SizeName = sizeName,
                        .ColorName = colorName
                    })

                Next

                subtotal = Math.Round(subtotal, 2)

                Dim shippingFee As Decimal = PricingHelper.CalculateShippingFee(subtotal)
                Dim utcNow As DateTime = DateTime.UtcNow

                newOrder = New Order With {
                    .CustomerId = customerId,
                    .OrderDate = utcNow,
                    .CreatedDate = utcNow,
                    .Status = OrderStatuses.Pending,
                    .Subtotal = subtotal,
                    .ShippingFee = shippingFee,
                    .DiscountAmount = 0D,
                    .TotalAmount = subtotal + shippingFee,
                    .Currency = PricingHelper.Currency,
                    .PaymentMethod = paymentMethod,
                    .PaymentStatus = PaymentStatuses.Pending,
                    .ShippingFullName = shipping.FullName,
                    .ShippingPhone = shipping.Phone,
                    .ContactEmail = shipping.Email,
                    .ShippingAddress = shipping.AddressText,
                    .Notes = shipping.Notes,
                    .Items = orderItems
                }

                _db.Orders.Add(newOrder)

                _db.Payments.Add(New Payment With {
                    .Order = newOrder,
                    .Method = paymentMethod,
                    .Amount = newOrder.TotalAmount,
                    .Currency = PricingHelper.Currency,
                    .Status = PaymentStatuses.Pending,
                    .CreatedDate = utcNow
                })

                ' Empty the cart in the same transaction
                _db.CartItems.RemoveRange(cartItems)
                cart.UpdatedDate = utcNow

                _db.SaveChanges()

                tx.Commit()

            End Using

            Return newOrder

        End Function


        ' =====================================================
        ' Cancel an order and put the stock back
        ' =====================================================
        ''' <param name="allowedStatuses">Statuses the order may currently have (use OrderStatuses constants only).</param>
        Public Sub CancelOrder(orderId As Integer, allowedStatuses As String())

            ' Built from constants only, never from user input
            Dim statusList As String = String.Join(",", allowedStatuses.Select(Function(s) "'" & s & "'"))

            Using tx = _db.Database.BeginTransaction()

                ' The status change is conditional, so cancelling twice can never restock twice
                Dim affected As Integer = _db.Database.ExecuteSqlCommand(
                    "UPDATE dbo.Orders SET Status = '" & OrderStatuses.Cancelled & "' " &
                    "WHERE OrderId = {0} AND Status IN (" & statusList & ")",
                    orderId)

                If affected = 0 Then
                    Throw New ApiException(HttpStatusCode.BadRequest, "This order can no longer be cancelled.")
                End If

                Dim lines = _db.OrderItems _
                    .Where(Function(i) i.OrderId = orderId) _
                    .Select(Function(i) New With {i.ProductVariantId, i.Quantity}) _
                    .ToList()

                For Each line In lines
                    _db.Database.ExecuteSqlCommand(
                        "UPDATE dbo.ProductVariants SET StockQuantity = StockQuantity + {0} WHERE ProductVariantId = {1}",
                        line.Quantity,
                        line.ProductVariantId)
                Next

                tx.Commit()

            End Using

        End Sub


        ' =====================================================
        ' Admin: move an order to its next status
        ' =====================================================
        ''' <summary>
        ''' Allowed moves: Pending to Confirmed, Confirmed to Processing, Processing to Shipped,
        ''' Shipped to Delivered, and Pending/Confirmed/Processing to Cancelled (stock goes back).
        ''' Delivering a cash-on-delivery order marks it as paid.
        ''' </summary>
        Public Sub ChangeStatus(orderId As Integer, newStatus As String)

            Dim targetStatus As String = NormalizeStatus(newStatus)

            Dim current = _db.Orders _
                .Where(Function(o) o.OrderId = orderId) _
                .Select(Function(o) New With {o.Status, o.PaymentMethod, o.PaymentStatus}) _
                .FirstOrDefault()

            If current Is Nothing Then
                Throw New ApiException(HttpStatusCode.NotFound, "Order not found.")
            End If

            If Not AllowedNext(current.Status).Contains(targetStatus) Then
                Throw New ApiException(HttpStatusCode.BadRequest,
                    "An order cannot be changed from " & current.Status & " to " & targetStatus & ".")
            End If

            If targetStatus = OrderStatuses.Cancelled Then
                CancelOrder(orderId, New String() {OrderStatuses.Pending, OrderStatuses.Confirmed, OrderStatuses.Processing})
                Return
            End If

            If current.PaymentMethod = PaymentMethods.Card AndAlso current.PaymentStatus <> PaymentStatuses.Paid Then
                Throw New ApiException(HttpStatusCode.BadRequest, "This card order has not been paid yet.")
            End If

            Using tx = _db.Database.BeginTransaction()

                ' Only changes the order if nobody else changed its status in the meantime
                Dim affected As Integer = _db.Database.ExecuteSqlCommand(
                    "UPDATE dbo.Orders SET Status = {0} WHERE OrderId = {1} AND Status = {2}",
                    targetStatus,
                    orderId,
                    current.Status)

                If affected = 0 Then
                    Throw New ApiException(HttpStatusCode.Conflict,
                        "The order was changed by someone else. Reload it and try again.")
                End If

                If targetStatus = OrderStatuses.Delivered AndAlso current.PaymentMethod = PaymentMethods.COD Then

                    ' The courier collected the cash
                    _db.Database.ExecuteSqlCommand(
                        "UPDATE dbo.Orders SET PaymentStatus = {0} WHERE OrderId = {1} AND PaymentStatus = {2}",
                        PaymentStatuses.Paid,
                        orderId,
                        PaymentStatuses.Pending)

                    _db.Database.ExecuteSqlCommand(
                        "UPDATE dbo.Payments SET Status = {0}, UpdatedDate = SYSUTCDATETIME() " &
                        "WHERE OrderId = {1} AND Method = {2} AND Status = {3}",
                        PaymentStatuses.Paid,
                        orderId,
                        PaymentMethods.COD,
                        PaymentStatuses.Pending)

                End If

                tx.Commit()

            End Using

        End Sub


        Private Shared Function NormalizeStatus(value As String) As String

            If Not String.IsNullOrWhiteSpace(value) Then

                Dim allStatuses As String() = New String() {
                    OrderStatuses.Pending,
                    OrderStatuses.Confirmed,
                    OrderStatuses.Processing,
                    OrderStatuses.Shipped,
                    OrderStatuses.Delivered,
                    OrderStatuses.Cancelled
                }

                For Each statusName In allStatuses
                    If String.Equals(statusName, value.Trim(), StringComparison.OrdinalIgnoreCase) Then
                        Return statusName
                    End If
                Next

            End If

            Throw New ApiException(HttpStatusCode.BadRequest,
                "Status must be Pending, Confirmed, Processing, Shipped, Delivered or Cancelled.")

        End Function


        Private Shared Function AllowedNext(currentStatus As String) As String()

            Select Case currentStatus

                Case OrderStatuses.Pending
                    Return New String() {OrderStatuses.Confirmed, OrderStatuses.Cancelled}

                Case OrderStatuses.Confirmed
                    Return New String() {OrderStatuses.Processing, OrderStatuses.Cancelled}

                Case OrderStatuses.Processing
                    Return New String() {OrderStatuses.Shipped, OrderStatuses.Cancelled}

                Case OrderStatuses.Shipped
                    Return New String() {OrderStatuses.Delivered}

                Case Else
                    Return New String() {}

            End Select

        End Function


        ' =====================================================
        ' Order -> response DTO
        ' =====================================================
        Public Function BuildDetails(order As Order) As OrderDetailsDto

            Dim orderId As Integer = order.OrderId

            Dim items = _db.OrderItems _
                .Include("ProductVariant") _
                .Where(Function(i) i.OrderId = orderId) _
                .OrderBy(Function(i) i.OrderItemId) _
                .ToList()

            Dim productIds = items _
                .Where(Function(i) i.ProductVariant IsNot Nothing) _
                .Select(Function(i) i.ProductVariant.ProductId) _
                .Distinct() _
                .ToList()

            Dim images = ImageLookup.MainImageUrls(_db, productIds)

            Dim result As New OrderDetailsDto With {
                .OrderId = order.OrderId,
                .OrderNumber = order.OrderNumber,
                .OrderDate = order.OrderDate,
                .Status = order.Status,
                .PaymentMethod = order.PaymentMethod,
                .PaymentStatus = order.PaymentStatus,
                .RequiresPayment =
                    order.PaymentMethod = PaymentMethods.Card AndAlso
                    order.PaymentStatus = PaymentStatuses.Pending AndAlso
                    order.Status <> OrderStatuses.Cancelled,
                .Currency = order.Currency,
                .Subtotal = order.Subtotal,
                .ShippingFee = order.ShippingFee,
                .DiscountAmount = order.DiscountAmount,
                .TotalAmount = order.TotalAmount,
                .ShippingFullName = order.ShippingFullName,
                .ShippingPhone = order.ShippingPhone,
                .ContactEmail = order.ContactEmail,
                .ShippingAddress = order.ShippingAddress,
                .Notes = order.Notes,
                .TrackingToken = order.TrackingToken,
                .Items = New List(Of OrderItemDto)()
            }

            For Each item In items

                Dim dto As New OrderItemDto With {
                    .OrderItemId = item.OrderItemId,
                    .ProductVariantId = item.ProductVariantId,
                    .ProductName = item.ProductName,
                    .SizeName = item.SizeName,
                    .ColorName = item.ColorName,
                    .SKU = item.SKU,
                    .UnitPrice = item.UnitPrice,
                    .Quantity = item.Quantity,
                    .TotalPrice = item.UnitPrice * item.Quantity
                }

                If item.ProductVariant IsNot Nothing Then
                    dto.ProductId = item.ProductVariant.ProductId
                    dto.ColorId = item.ProductVariant.ColorId
                    dto.SizeId = item.ProductVariant.SizeId

                    Dim imageUrl As String = Nothing
                    If images.TryGetValue(item.ProductVariant.ProductId, imageUrl) Then
                        dto.ImageUrl = imageUrl
                    End If
                End If

                result.Items.Add(dto)

            Next

            Return result

        End Function


        ' "Linen Shirt (Black / M)" for error messages
        Private Shared Function Describe(selectedVariant As ProductVariant) As String

            Dim text As String = selectedVariant.Product.Name

            Dim options As New List(Of String)()

            If selectedVariant.Color IsNot Nothing Then
                options.Add(selectedVariant.Color.Name)
            End If

            If selectedVariant.Size IsNot Nothing Then
                options.Add(selectedVariant.Size.Name)
            End If

            If options.Count > 0 Then
                text &= " (" & String.Join(" / ", options) & ")"
            End If

            Return text

        End Function

    End Class

End Namespace
