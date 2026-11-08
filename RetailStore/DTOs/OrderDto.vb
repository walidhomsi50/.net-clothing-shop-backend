Namespace DTOs

    ' Logged-in customer checkout (uses one of the customer's saved addresses)
    Public Class CreateOrderRequest

        Public Property CustomerAddressId As Integer

        ' "COD" (default if empty) or "Card"
        Public Property PaymentMethod As String

        Public Property Notes As String

    End Class


    ' Guest checkout (no account). The cart is identified by the X-Guest-Token header.
    Public Class GuestCheckoutRequest

        Public Property FullName As String
        Public Property Phone As String
        Public Property Email As String

        Public Property AddressLine As String
        Public Property City As String
        Public Property Area As String
        Public Property Building As String
        Public Property Floor As String
        Public Property Apartment As String
        Public Property AddressNotes As String

        ' "COD" (default if empty) or "Card"
        Public Property PaymentMethod As String

        Public Property Notes As String

    End Class


    Public Class OrderListDto

        Public Property OrderId As Integer

        Public Property OrderNumber As String

        Public Property OrderDate As DateTime

        Public Property Status As String

        Public Property PaymentMethod As String

        Public Property PaymentStatus As String

        Public Property TotalAmount As Decimal

        Public Property ShippingAddress As String

        ' Total number of units in the order
        Public Property ItemCount As Integer

    End Class


    Public Class OrderDetailsDto

        Public Property OrderId As Integer

        Public Property OrderNumber As String

        Public Property OrderDate As DateTime

        Public Property Status As String

        Public Property PaymentMethod As String

        Public Property PaymentStatus As String

        ' True for card orders that still need to be paid
        Public Property RequiresPayment As Boolean

        Public Property Currency As String

        Public Property Subtotal As Decimal

        Public Property ShippingFee As Decimal

        Public Property DiscountAmount As Decimal

        Public Property TotalAmount As Decimal

        Public Property ShippingFullName As String

        Public Property ShippingPhone As String

        Public Property ContactEmail As String

        Public Property ShippingAddress As String

        Public Property Notes As String

        ' Secret token for guest order tracking: GET api/v1/orders/track/{trackingToken}
        Public Property TrackingToken As Guid

        Public Property Items As List(Of OrderItemDto)

    End Class


    Public Class OrderItemDto

        Public Property OrderItemId As Integer

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

    End Class

End Namespace
