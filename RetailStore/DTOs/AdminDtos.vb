Namespace DTOs

    Public Class UpdateOrderStatusRequest

        ' Pending, Confirmed, Processing, Shipped, Delivered or Cancelled
        Public Property Status As String

    End Class


    Public Class SetActiveRequest

        Public Property IsActive As Boolean

    End Class


    Public Class AdminOrderListDto

        Public Property OrderId As Integer

        Public Property OrderNumber As String

        Public Property OrderDate As DateTime

        Public Property Status As String

        Public Property PaymentMethod As String

        Public Property PaymentStatus As String

        Public Property TotalAmount As Decimal

        Public Property Currency As String

        ' Nothing for guest orders
        Public Property CustomerId As Integer?

        Public Property IsGuest As Boolean

        Public Property ShippingFullName As String

        Public Property ShippingPhone As String

        ' Total number of units in the order
        Public Property ItemCount As Integer

    End Class


    Public Class AdminCustomerListDto

        Public Property CustomerId As Integer

        Public Property FirstName As String

        Public Property LastName As String

        Public Property Email As String

        Public Property Phone As String

        Public Property IsActive As Boolean

        Public Property CreatedDate As DateTime

        ' Orders that were not cancelled
        Public Property OrderCount As Integer

        Public Property TotalSpent As Decimal

    End Class


    Public Class AdminCustomerOrderDto

        Public Property OrderId As Integer

        Public Property OrderNumber As String

        Public Property OrderDate As DateTime

        Public Property Status As String

        Public Property PaymentStatus As String

        Public Property TotalAmount As Decimal

    End Class


    Public Class LowStockDto

        Public Property ProductVariantId As Integer

        Public Property ProductName As String

        Public Property SKU As String

        Public Property SizeName As String

        Public Property ColorName As String

        Public Property StockQuantity As Integer

    End Class

End Namespace
