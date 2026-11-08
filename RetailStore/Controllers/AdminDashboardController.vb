Imports System.Collections.Generic
Imports System.Linq
Imports System.Web.Http
Imports RetailStore.Data
Imports RetailStore.DTOs
Imports RetailStore.Helpers
Imports RetailStore.Models

''' <summary>Numbers for the admin home screen. Admin and Staff accounts only.</summary>
<RoutePrefix("api/v1/admin/dashboard")>
<Authorize(Roles:="Admin,Staff")>
Public Class AdminDashboardController
    Inherits ApiController

    Private Const LowStockThreshold As Integer = 5
    Private Const LowStockListSize As Integer = 20

    Private ReadOnly db As New RetailStoreContext()

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            db.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub


    ' =========================================================
    ' GET api/v1/admin/dashboard
    ' Dates use UTC, so "today" starts at midnight UTC.
    ' =========================================================
    <HttpGet>
    <Route("")>
    Public Function GetDashboard() As IHttpActionResult

        Dim utcNow As DateTime = DateTime.UtcNow
        Dim todayStart As DateTime = utcNow.Date
        Dim last30Days As DateTime = utcNow.AddDays(-30)
        Dim last7Days As DateTime = utcNow.AddDays(-7)
        Dim threshold As Integer = LowStockThreshold

        Dim pendingOrders As Integer = db.Orders.Count(Function(o) o.Status = OrderStatuses.Pending)

        Dim ordersToday As Integer = db.Orders.Count(Function(o) o.OrderDate >= todayStart)

        ' Casting to a nullable decimal lets Sum return Nothing (instead of failing) when there are no orders
        Dim revenue As Decimal? = db.Orders _
            .Where(Function(o) o.OrderDate >= last30Days AndAlso o.Status <> OrderStatuses.Cancelled) _
            .Sum(Function(o) CType(o.TotalAmount, Decimal?))

        Dim newCustomers As Integer = db.Customers.Count(Function(c) c.CreatedDate >= last7Days)

        Dim lowStockRows = db.ProductVariants _
            .Include("Product") _
            .Include("Size") _
            .Include("Color") _
            .Where(Function(v) v.IsActive AndAlso v.Product.IsActive AndAlso v.StockQuantity <= threshold) _
            .OrderBy(Function(v) v.StockQuantity) _
            .ThenBy(Function(v) v.ProductVariantId) _
            .Take(LowStockListSize) _
            .ToList()

        Dim lowStock As New List(Of LowStockDto)()

        For Each row In lowStockRows

            Dim dto As New LowStockDto With {
                .ProductVariantId = row.ProductVariantId,
                .ProductName = row.Product.Name,
                .SKU = row.SKU,
                .StockQuantity = row.StockQuantity
            }

            If row.Size IsNot Nothing Then
                dto.SizeName = row.Size.Name
            End If

            If row.Color IsNot Nothing Then
                dto.ColorName = row.Color.Name
            End If

            lowStock.Add(dto)

        Next

        Return Ok(New With {
            .PendingOrders = pendingOrders,
            .OrdersToday = ordersToday,
            .RevenueLast30Days = revenue.GetValueOrDefault(),
            .Currency = PricingHelper.Currency,
            .NewCustomersLast7Days = newCustomers,
            .LowStockThreshold = LowStockThreshold,
            .LowStock = lowStock
        })

    End Function

End Class
