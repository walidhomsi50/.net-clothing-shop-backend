Imports System.Collections.Generic
Imports System.Linq
Imports System.Web.Http
Imports RetailStore.Data
Imports RetailStore.DTOs
Imports RetailStore.Helpers
Imports RetailStore.Models

''' <summary>
''' REPLACES the old CustomersController (which was open to everyone).
''' Customer management for the admin panel. Admin and Staff accounts only.
''' </summary>
<RoutePrefix("api/v1/admin/customers")>
<Authorize(Roles:="Admin,Staff")>
Public Class CustomersController
    Inherits ApiController

    Private ReadOnly db As New RetailStoreContext()

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            db.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub


    ' =========================================================
    ' GET api/v1/admin/customers?search=ahmad&page=1&pageSize=20
    ' =========================================================
    <HttpGet>
    <Route("")>
    Public Function GetCustomers(
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

        Dim query = db.Customers.AsQueryable()

        If Not String.IsNullOrWhiteSpace(search) Then

            Dim term As String = search.Trim()

            query = query.Where(
                Function(c) c.FirstName.Contains(term) OrElse
                            c.LastName.Contains(term) OrElse
                            c.Email.Contains(term) OrElse
                            c.Phone.Contains(term))

        End If

        Dim totalItems As Integer = query.Count()

        Dim customers = query _
            .OrderByDescending(Function(c) c.CreatedDate) _
            .ThenByDescending(Function(c) c.CustomerId) _
            .Skip((page - 1) * pageSize) _
            .Take(pageSize) _
            .ToList()

        Dim customerIds = customers.Select(Function(c) c.CustomerId).ToList()

        ' Order count and total spent for the whole page in one query (cancelled orders ignored)
        Dim statRows = db.Orders _
            .Where(Function(o) o.CustomerId.HasValue AndAlso
                               customerIds.Contains(o.CustomerId.Value) AndAlso
                               o.Status <> OrderStatuses.Cancelled) _
            .GroupBy(Function(o) o.CustomerId.Value) _
            .Select(Function(g) New With {
                .CustomerId = g.Key,
                .OrderCount = g.Count(),
                .TotalSpent = g.Sum(Function(o) o.TotalAmount)
            }) _
            .ToList()

        Dim orderCounts = statRows.ToDictionary(Function(x) x.CustomerId, Function(x) x.OrderCount)
        Dim totalsSpent = statRows.ToDictionary(Function(x) x.CustomerId, Function(x) x.TotalSpent)

        Dim items As New List(Of AdminCustomerListDto)()

        For Each customer In customers

            Dim dto As New AdminCustomerListDto With {
                .CustomerId = customer.CustomerId,
                .FirstName = customer.FirstName,
                .LastName = customer.LastName,
                .Email = customer.Email,
                .Phone = customer.Phone,
                .IsActive = customer.IsActive,
                .CreatedDate = customer.CreatedDate,
                .OrderCount = 0,
                .TotalSpent = 0D
            }

            Dim ordersPlaced As Integer = 0
            If orderCounts.TryGetValue(customer.CustomerId, ordersPlaced) Then
                dto.OrderCount = ordersPlaced
                dto.TotalSpent = totalsSpent(customer.CustomerId)
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
    ' GET api/v1/admin/customers/7
    ' =========================================================
    <HttpGet>
    <Route("{id:int}")>
    Public Function GetCustomer(id As Integer) As IHttpActionResult

        Dim customer = db.Customers.FirstOrDefault(Function(c) c.CustomerId = id)

        If customer Is Nothing Then
            Return NotFound()
        End If

        Dim addresses = db.CustomerAddresses _
            .Where(Function(a) a.CustomerId = id) _
            .OrderByDescending(Function(a) a.IsDefault) _
            .ThenBy(Function(a) a.CustomerAddressId) _
            .Select(Function(a) New With {
                a.CustomerAddressId,
                a.Title,
                a.FullName,
                a.Phone,
                a.AddressLine,
                a.City,
                a.Area,
                a.Building,
                a.Floor,
                a.Apartment,
                a.Notes,
                a.IsDefault
            }) _
            .ToList()

        Dim recentOrders = db.Orders _
            .Where(Function(o) o.CustomerId.Value = id) _
            .OrderByDescending(Function(o) o.OrderDate) _
            .ThenByDescending(Function(o) o.OrderId) _
            .Take(10) _
            .ToList() _
            .Select(Function(o) New AdminCustomerOrderDto With {
                .OrderId = o.OrderId,
                .OrderNumber = o.OrderNumber,
                .OrderDate = o.OrderDate,
                .Status = o.Status,
                .PaymentStatus = o.PaymentStatus,
                .TotalAmount = o.TotalAmount
            }) _
            .ToList()

        Return Ok(New With {
            .CustomerId = customer.CustomerId,
            .FirstName = customer.FirstName,
            .LastName = customer.LastName,
            .Email = customer.Email,
            .Phone = customer.Phone,
            .IsActive = customer.IsActive,
            .CreatedDate = customer.CreatedDate,
            .Addresses = addresses,
            .RecentOrders = recentOrders
        })

    End Function


    ' =========================================================
    ' PUT api/v1/admin/customers/7/active      { "isActive": false }
    ' Deactivating blocks login and refresh immediately. An access token that was
    ' already issued keeps working until it expires (15 minutes at most).
    ' =========================================================
    <HttpPut>
    <Route("{id:int}/active")>
    Public Function SetActive(id As Integer, body As SetActiveRequest) As IHttpActionResult

        If body Is Nothing Then
            Return BadRequest("Request is required.")
        End If

        Dim customer = db.Customers.FirstOrDefault(Function(c) c.CustomerId = id)

        If customer Is Nothing Then
            Return NotFound()
        End If

        customer.IsActive = body.IsActive
        db.SaveChanges()

        If Not body.IsActive Then
            db.Database.ExecuteSqlCommand(
                "UPDATE dbo.RefreshTokens SET RevokedAt = SYSUTCDATETIME() WHERE CustomerId = {0} AND RevokedAt IS NULL",
                id)
        End If

        Return Ok(New With {
            .CustomerId = customer.CustomerId,
            .IsActive = customer.IsActive
        })

    End Function

End Class
