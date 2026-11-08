Imports System.Collections.Generic
Imports System.Data.Entity
Imports System.Linq
Imports System.Net
Imports System.Web.Http
Imports RetailStore.Data
Imports RetailStore.DTOs
Imports RetailStore.Helpers
Imports RetailStore.Models
Imports RetailStore.Services

''' <summary>
''' Customer orders (Bearer token, role Customer) and guest checkout (X-Guest-Token header).
''' Admin order management comes in a separate controller.
''' </summary>
<RoutePrefix("api/v1/orders")>
Public Class OrdersController
    Inherits ApiController

    Private ReadOnly db As New RetailStoreContext()
    Private ReadOnly _orders As OrderService

    Public Sub New()
        MyBase.New()
        _orders = New OrderService(db)
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            db.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub


    ' =========================================================
    ' POST api/v1/orders       (logged-in customer, saved address)
    ' { "customerAddressId": 3, "paymentMethod": "COD", "notes": "..." }
    ' =========================================================
    <HttpPost>
    <Route("")>
    <Authorize(Roles:="Customer")>
    Public Function CreateOrder(body As CreateOrderRequest) As IHttpActionResult

        Dim owner = CartOwner.Resolve(Me.Request, Me.User)

        If Not owner.CustomerId.HasValue Then
            Return Unauthorized()
        End If

        If body Is Nothing Then
            Return BadRequest("Request is required.")
        End If

        If body.CustomerAddressId <= 0 Then
            Return BadRequest("CustomerAddressId is required.")
        End If

        If InputValidator.TooLong(body.Notes, 1000) Then
            Return BadRequest("Notes are too long (maximum 1000 characters).")
        End If

        Dim paymentMethod As String = OrderService.NormalizePaymentMethod(body.PaymentMethod)

        Dim customerId As Integer = owner.CustomerId.Value
        Dim addressId As Integer = body.CustomerAddressId

        Dim customer = db.Customers.FirstOrDefault(Function(c) c.CustomerId = customerId AndAlso c.IsActive)

        If customer Is Nothing Then
            Return Unauthorized()
        End If

        Dim address = db.CustomerAddresses.FirstOrDefault(
            Function(a) a.CustomerAddressId = addressId AndAlso a.CustomerId = customerId)

        If address Is Nothing Then
            Return BadRequest("Shipping address was not found.")
        End If

        Dim cart = db.Carts.FirstOrDefault(Function(c) c.CustomerId.Value = customerId)

        If cart Is Nothing Then
            Return BadRequest("Cart is empty.")
        End If

        Dim shipping As New OrderShippingInfo With {
            .FullName = address.FullName,
            .Phone = address.Phone,
            .Email = customer.Email,
            .Notes = body.Notes,
            .AddressText = AddressFormatter.Build(
                address.FullName,
                address.Phone,
                address.AddressLine,
                address.City,
                address.Area,
                address.Building,
                address.Floor,
                address.Apartment,
                address.Notes)
        }

        Dim placed = _orders.PlaceOrder(cart, customerId, shipping, paymentMethod)

        Return Ok(_orders.BuildDetails(placed))

    End Function


    ' =========================================================
    ' POST api/v1/orders/guest     (no account)
    ' Header: X-Guest-Token (the same GUID used for the guest cart)
    ' =========================================================
    <HttpPost>
    <Route("guest")>
    <AllowAnonymous>
    Public Function GuestCheckout(body As GuestCheckoutRequest) As IHttpActionResult

        Dim guestToken = CartOwner.ReadGuestToken(Me.Request)

        If Not guestToken.HasValue Then
            Return BadRequest("The X-Guest-Token header is required.")
        End If

        If body Is Nothing Then
            Return BadRequest("Request is required.")
        End If

        Dim problem As String = ValidateGuest(body)

        If problem IsNot Nothing Then
            Return BadRequest(problem)
        End If

        Dim paymentMethod As String = OrderService.NormalizePaymentMethod(body.PaymentMethod)

        Dim token As Guid = guestToken.Value
        Dim cart = db.Carts.FirstOrDefault(Function(c) c.GuestToken.Value = token)

        If cart Is Nothing Then
            Return BadRequest("Cart is empty.")
        End If

        Dim fullName As String = body.FullName.Trim()
        Dim phone As String = body.Phone.Trim()

        Dim shipping As New OrderShippingInfo With {
            .FullName = fullName,
            .Phone = phone,
            .Email = body.Email.Trim().ToLowerInvariant(),
            .Notes = body.Notes,
            .AddressText = AddressFormatter.Build(
                fullName,
                phone,
                body.AddressLine.Trim(),
                body.City.Trim(),
                body.Area,
                body.Building,
                body.Floor,
                body.Apartment,
                body.AddressNotes)
        }

        Dim placed = _orders.PlaceOrder(cart, Nothing, shipping, paymentMethod)

        Return Ok(_orders.BuildDetails(placed))

    End Function


    ' =========================================================
    ' GET api/v1/orders/track/{trackingToken}    (guest order lookup)
    ' =========================================================
    <HttpGet>
    <Route("track/{token:guid}")>
    <AllowAnonymous>
    Public Function TrackOrder(token As Guid) As IHttpActionResult

        Dim order = db.Orders.FirstOrDefault(Function(o) o.TrackingToken = token)

        If order Is Nothing Then
            Return NotFound()
        End If

        Return Ok(_orders.BuildDetails(order))

    End Function


    ' =========================================================
    ' GET api/v1/orders?page=1&pageSize=20     (my orders)
    ' =========================================================
    <HttpGet>
    <Route("")>
    <Authorize(Roles:="Customer")>
    Public Function GetOrders(Optional page As Integer = 1, Optional pageSize As Integer = 20) As IHttpActionResult

        Dim owner = CartOwner.Resolve(Me.Request, Me.User)

        If Not owner.CustomerId.HasValue Then
            Return Unauthorized()
        End If

        If page < 1 Then
            page = 1
        End If

        If pageSize < 1 Then
            pageSize = 20
        End If

        If pageSize > 100 Then
            pageSize = 100
        End If

        Dim customerId As Integer = owner.CustomerId.Value

        Dim query = db.Orders.Where(Function(o) o.CustomerId.Value = customerId)

        Dim totalItems As Integer = query.Count()

        Dim orders = query _
            .OrderByDescending(Function(o) o.OrderDate) _
            .ThenByDescending(Function(o) o.OrderId) _
            .Skip((page - 1) * pageSize) _
            .Take(pageSize) _
            .ToList()

        Dim orderIds = orders.Select(Function(o) o.OrderId).ToList()

        ' Units per order for the whole page in one query
        Dim quantities = db.OrderItems _
            .Where(Function(i) orderIds.Contains(i.OrderId)) _
            .GroupBy(Function(i) i.OrderId) _
            .Select(Function(g) New With {.OrderId = g.Key, .Units = g.Sum(Function(i) i.Quantity)}) _
            .ToList() _
            .ToDictionary(Function(x) x.OrderId, Function(x) x.Units)

        Dim result As New List(Of OrderListDto)()

        For Each order In orders

            Dim units As Integer = 0
            quantities.TryGetValue(order.OrderId, units)

            result.Add(New OrderListDto With {
                .OrderId = order.OrderId,
                .OrderNumber = order.OrderNumber,
                .OrderDate = order.OrderDate,
                .Status = order.Status,
                .PaymentMethod = order.PaymentMethod,
                .PaymentStatus = order.PaymentStatus,
                .TotalAmount = order.TotalAmount,
                .ShippingAddress = order.ShippingAddress,
                .ItemCount = units
            })

        Next

        Return Ok(New With {
            .Page = page,
            .PageSize = pageSize,
            .TotalItems = totalItems,
            .TotalPages = CInt(Math.Ceiling(totalItems / CDbl(pageSize))),
            .Items = result
        })

    End Function


    ' =========================================================
    ' GET api/v1/orders/5     (one of my orders)
    ' =========================================================
    <HttpGet>
    <Route("{id:int}")>
    <Authorize(Roles:="Customer")>
    Public Function GetOrder(id As Integer) As IHttpActionResult

        Dim owner = CartOwner.Resolve(Me.Request, Me.User)

        If Not owner.CustomerId.HasValue Then
            Return Unauthorized()
        End If

        Dim customerId As Integer = owner.CustomerId.Value

        Dim order = db.Orders.FirstOrDefault(Function(o) o.OrderId = id AndAlso o.CustomerId.Value = customerId)

        If order Is Nothing Then
            Return NotFound()
        End If

        Return Ok(_orders.BuildDetails(order))

    End Function


    ' =========================================================
    ' POST api/v1/orders/5/cancel     (only while the order is still Pending)
    ' =========================================================
    <HttpPost>
    <Route("{id:int}/cancel")>
    <Authorize(Roles:="Customer")>
    Public Function CancelOrder(id As Integer) As IHttpActionResult

        Dim owner = CartOwner.Resolve(Me.Request, Me.User)

        If Not owner.CustomerId.HasValue Then
            Return Unauthorized()
        End If

        Dim customerId As Integer = owner.CustomerId.Value

        Dim order = db.Orders.FirstOrDefault(Function(o) o.OrderId = id AndAlso o.CustomerId.Value = customerId)

        If order Is Nothing Then
            Return NotFound()
        End If

        _orders.CancelOrder(id, New String() {OrderStatuses.Pending})

        ' The status was changed with raw SQL, so refresh the tracked copy
        db.Entry(order).Reload()

        Return Ok(New With {
            .OrderId = order.OrderId,
            .OrderNumber = order.OrderNumber,
            .Status = order.Status,
            .Message = "Order cancelled."
        })

    End Function


    ' =========================================================
    ' Helpers
    ' =========================================================
    Private Shared Function ValidateGuest(body As GuestCheckoutRequest) As String

        If String.IsNullOrWhiteSpace(body.FullName) OrElse
           String.IsNullOrWhiteSpace(body.Phone) OrElse
           String.IsNullOrWhiteSpace(body.Email) OrElse
           String.IsNullOrWhiteSpace(body.AddressLine) OrElse
           String.IsNullOrWhiteSpace(body.City) Then

            Return "Full name, phone, email, address and city are required."
        End If

        If Not InputValidator.IsValidEmail(body.Email) Then
            Return "Email address is not valid."
        End If

        If Not InputValidator.IsValidPhone(body.Phone) Then
            Return "Phone number is not valid."
        End If

        If InputValidator.TooLong(body.FullName, 150) OrElse
           InputValidator.TooLong(body.AddressLine, 300) OrElse
           InputValidator.TooLong(body.City, 100) OrElse
           InputValidator.TooLong(body.Area, 100) OrElse
           InputValidator.TooLong(body.Building, 50) OrElse
           InputValidator.TooLong(body.Floor, 50) OrElse
           InputValidator.TooLong(body.Apartment, 50) OrElse
           InputValidator.TooLong(body.AddressNotes, 500) OrElse
           InputValidator.TooLong(body.Notes, 1000) Then

            Return "One of the fields is too long."
        End If

        Return Nothing

    End Function

End Class
