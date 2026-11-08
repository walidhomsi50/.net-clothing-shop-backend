Namespace DTOs

    ' ---------- Categories ----------

    Public Class SaveCategoryRequest

        Public Property Name As String

        Public Property Description As String

        ' Nothing = top-level category
        Public Property ParentCategoryId As Integer?

        Public Property IsActive As Boolean

    End Class


    Public Class AdminCategoryDto

        Public Property CategoryId As Integer

        Public Property Name As String

        Public Property Slug As String

        Public Property Description As String

        Public Property ParentCategoryId As Integer?

        Public Property IsActive As Boolean

        Public Property ProductCount As Integer

    End Class


    ' ---------- Products ----------

    Public Class SaveProductRequest

        Public Property CategoryId As Integer

        Public Property Name As String

        Public Property Description As String

        Public Property Brand As String

        Public Property Price As Decimal

        ' Nothing = no sale
        Public Property SalePrice As Decimal?

        Public Property IsActive As Boolean

    End Class


    Public Class AdminProductListDto

        Public Property ProductId As Integer

        Public Property Name As String

        Public Property Slug As String

        Public Property Brand As String

        Public Property CategoryId As Integer

        Public Property CategoryName As String

        Public Property Price As Decimal

        Public Property SalePrice As Decimal?

        Public Property IsActive As Boolean

        Public Property VariantCount As Integer

        Public Property TotalStock As Integer

        Public Property MainImageUrl As String

    End Class


    Public Class AdminImageDto

        Public Property ProductImageId As Integer

        Public Property ImageUrl As String

        Public Property SortOrder As Integer

        Public Property IsMain As Boolean

    End Class


    Public Class AdminProductDetailsDto

        Public Property ProductId As Integer

        Public Property Name As String

        Public Property Slug As String

        Public Property Description As String

        Public Property Brand As String

        Public Property CategoryId As Integer

        Public Property CategoryName As String

        Public Property Price As Decimal

        Public Property SalePrice As Decimal?

        Public Property IsActive As Boolean

        Public Property CreatedDate As DateTime

        Public Property UpdatedDate As DateTime?

        Public Property Variants As List(Of AdminVariantDto)

        Public Property Images As List(Of AdminImageDto)

    End Class


    ' ---------- Variants and stock ----------

    Public Class SaveVariantRequest

        Public Property ColorId As Integer?

        Public Property SizeId As Integer?

        Public Property SKU As String

        Public Property Barcode As String

        ' Used when CREATING a variant. Later changes go through the stock endpoint.
        Public Property StockQuantity As Integer

        ' Nothing = use the product price
        Public Property Price As Decimal?

        Public Property IsActive As Boolean

    End Class


    Public Class AdjustStockRequest

        ' Positive adds stock (new delivery), negative removes it (damaged, lost, counted)
        Public Property Delta As Integer

    End Class


    Public Class AdminVariantDto

        Public Property ProductVariantId As Integer

        Public Property ProductId As Integer

        Public Property ColorId As Integer?

        Public Property ColorName As String

        Public Property SizeId As Integer?

        Public Property SizeName As String

        Public Property SKU As String

        Public Property Barcode As String

        Public Property StockQuantity As Integer

        Public Property Price As Decimal?

        Public Property IsActive As Boolean

    End Class


    ' ---------- Colors and sizes ----------

    Public Class SaveColorRequest

        Public Property Name As String

        ' Like #FF8800, or empty
        Public Property HexCode As String

        Public Property IsActive As Boolean

    End Class


    Public Class SaveSizeRequest

        Public Property Name As String

        Public Property SortOrder As Integer

        Public Property IsActive As Boolean

    End Class

End Namespace
