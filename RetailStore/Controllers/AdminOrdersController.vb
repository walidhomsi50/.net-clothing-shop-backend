Imports System.Collections.Generic
Imports System.Linq
Imports System.Web.Http
Imports RetailStore.Data
Imports RetailStore.DTOs
Imports RetailStore.Helpers
Imports RetailStore.Models
Imports RetailStore.Services

''' <summary>Order management for the admin panel. Admin and Staff accounts only.</summary>
<RoutePrefix("api/v1/admin/orders")>
<Authorize(Roles:="Admin,Staff")>
Public Class AdminOrdersController
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
    ' GET api/v1/admin/orders?status=Pending&paymentStatus=Pending&search=ahmad&page=1&pageSize=20
    ' search matches order number, recipient name, phone or email
    ' =========================================================
    <HttpGet>
    <Route("")>
    Public Function GetOrders(
        Optional status As String = Nothing,
        Optional paymentStatus As String = Nothing,
        Optional search As String = Nothing,
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

        Dim query = db.Orders.AsQueryable()

        If Not String.IsNullOrWhiteSpace(status) Then
            Dim statusFilter As String = status.Trim()
            query = query.Where(Function(o) o.Status = statusFilter)
        End If

        If Not String.IsNullOrWhiteSpace(paymentStatus) Then
            Dim paymentFilter As String = paymentStatus.Trim()
            query = query.Where(Function(o) o.PaymentStatus = paymentFilter)
        End If

        If Not String.IsNullOrWhiteSpace(search) Then

            Dim term As String = search.Trim()

            query = query.Where(
                Function(o) o.OrderNumber.Contains(term) OrElse
                            o.ShippingFullName.Contains(term) OrElse
                            o.ShippingPhone.Contains(term) OrElse
                            o.ContactEmail.Contains(term))

        End If

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

        Dim items As New List(Of AdminOrderListDto)()

        For Each row In orders

            Dim units As Integer = 0
            quantities.TryGetValue(row.OrderId, units)

            items.Add(New AdminOrderListDto With {
                .OrderId = row.OrderId,
                .OrderNumber = row.OrderNumber,
                .OrderDate = row.OrderDate,
                .Status = row.Status,
                .PaymentMethod = row.PaymentMethod,
                .PaymentStatus = row.PaymentStatus,
                .TotalAmount = row.TotalAmount,
                .Currency = row.Currency,
                .CustomerId = row.CustomerId,
                .IsGuest = Not row.CustomerId.HasValue,
                .ShippingFullName = row.ShippingFullName,
                .ShippingPhone = row.ShippingPhone,
                .ItemCount = units
            })

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
    ' GET api/v1/admin/orders/5
    ' =========================================================
    <HttpGet>
    <Route("{id:int}")>
    Public Function GetOrder(id As Integer) As IHttpActionResult

        Dim found = db.Orders.FirstOrDefault(Function(o) o.OrderId = id)

        If found Is Nothing Then
            Return NotFound()
        End If

        Return Ok(BuildAdminDetails(found))

    End Function


    ' =========================================================
    ' PUT api/v1/admin/orders/5/status      { "status": "Confirmed" }
    ' =========================================================
    <HttpPut>
    <Route("{id:int}/status")>
    Public Function UpdateStatus(id As Integer, body As UpdateOrderStatusRequest) As IHttpActionResult

        If body Is Nothing OrElse String.IsNullOrWhiteSpace(body.Status) Then
            Return BadRequest("Status is required.")
        End If

        ' Rule violations come back as clean 400/404/409 messages (ApiException)
        _orders.ChangeStatus(id, body.Status)

        ' The context has not loaded this order yet, so this reads the fresh values
        Dim found = db.Orders.FirstOrDefault(Function(o) o.OrderId = id)

        If found Is Nothing Then
            Return NotFound()
        End If

        Return Ok(BuildAdminDetails(found))

    End Function


    Private Function BuildAdminDetails(found As Order) As Object

        Dim orderId As Integer = found.OrderId

        Dim payments = db.Payments _
            .Where(Function(p) p.OrderId = orderId) _
            .OrderBy(Function(p) p.PaymentId) _
            .Select(Function(p) New With {
                p.PaymentId,
                p.Method,
                p.Provider,
                p.Amount,
                p.Currency,
                p.Status,
                p.CreatedDate
            }) _
            .ToList()

        Return New With {
            .Order = _orders.BuildDetails(found),
            .CustomerId = found.CustomerId,
            .IsGuest = Not found.CustomerId.HasValue,
            .Payments = payments
        }

    End Function

End Class
