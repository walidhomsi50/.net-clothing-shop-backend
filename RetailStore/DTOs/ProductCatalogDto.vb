Namespace DTOs

    Public Class ProductCatalogDto

        Public Property ProductId As Integer
        Public Property Name As String
        Public Property Description As String
        Public Property Brand As String

        Public Property Price As Decimal
        Public Property SalePrice As Decimal?

        Public Property CategoryId As Integer
        Public Property CategoryName As String

        Public Property MainImageUrl As String

    End Class

End Namespace