Namespace DTOs

    Public Class AddCartItemRequest

        Public Property ProductVariantId As Integer

        Public Property Quantity As Integer

    End Class


    Public Class UpdateCartItemRequest

        Public Property Quantity As Integer

    End Class


    Public Class CartItemDto

        Public Property CartItemId As Integer

        Public Property ProductId As Integer

        Public Property ProductName As String

        Public Property ProductVariantId As Integer

        Public Property ColorId As Integer?
        Public Property ColorName As String

        Public Property SizeId As Integer?
        Public Property SizeName As String

        Public Property SKU As String

        Public Property ImageUrl As String

        Public Property UnitPrice As Decimal

        Public Property Quantity As Integer

        Public Property TotalPrice As Decimal

        ' Most the customer can order right now (capped, exact stock is not exposed)
        Public Property AvailableQuantity As Integer

        ' False if the product/variant was deactivated or stock dropped below the quantity.
        ' Unavailable lines are not counted in the cart totals and block checkout.
        Public Property IsAvailable As Boolean

    End Class


    Public Class CartDto

        Public Property CartId As Integer

        ' Nothing for guest carts
        Public Property CustomerId As Integer?

        Public Property Items As List(Of CartItemDto)

        Public Property TotalItems As Integer

        Public Property TotalAmount As Decimal

    End Class

End Namespace
