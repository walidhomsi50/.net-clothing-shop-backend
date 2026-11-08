Namespace DTOs

    Public Class ProductDetailsDto

        Public Property ProductId As Integer
        Public Property Name As String
        Public Property Description As String
        Public Property Brand As String
        Public Property Price As Decimal
        Public Property SalePrice As Decimal?
        Public Property CategoryId As Integer
        Public Property CategoryName As String
        Public Property Images As List(Of ProductImageDto)

        Public Property Variants As List(Of ProductVariantDto)

    End Class

    Public Class ProductImageDto

        Public Property ProductImageId As Integer
        Public Property ImageUrl As String
        Public Property SortOrder As Integer
        Public Property IsMain As Boolean

    End Class

    Public Class ProductVariantDto

        Public Property ProductVariantId As Integer
        Public Property ColorId As Integer?
        Public Property ColorName As String
        Public Property SizeId As Integer?
        Public Property SizeName As String
        Public Property SKU As String
        Public Property Barcode As String
        Public Property StockQuantity As Integer
        Public Property Price As Decimal?

    End Class

End Namespace