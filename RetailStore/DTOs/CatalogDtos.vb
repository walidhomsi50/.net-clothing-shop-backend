Namespace DTOs

    ' Public (storefront) catalog responses. They never include barcodes or exact stock counts.

    Public Class CatalogColorDto

        Public Property ColorId As Integer

        Public Property Name As String

        Public Property HexCode As String

    End Class


    Public Class CatalogSizeDto

        Public Property SizeId As Integer

        Public Property Name As String

        Public Property SortOrder As Integer

    End Class


    Public Class CatalogImageDto

        ' Relative URL, e.g. /uploads/products/p5-....jpg (put the API address in front of it)
        Public Property ImageUrl As String

        Public Property SortOrder As Integer

        Public Property IsMain As Boolean

    End Class


    Public Class CatalogCategoryDto

        Public Property CategoryId As Integer

        Public Property Name As String

        Public Property Slug As String

        Public Property Description As String

        ' Nothing for top-level categories
        Public Property ParentCategoryId As Integer?

        ' Sellable products in this category and all its sub-categories
        Public Property ProductCount As Integer

    End Class


    Public Class CatalogProductListDto

        Public Property ProductId As Integer

        Public Property Name As String

        Public Property Slug As String

        Public Property Brand As String

        Public Property CategoryId As Integer

        Public Property Price As Decimal

        ' Only set when it is lower than Price (show Price crossed out)
        Public Property SalePrice As Decimal?

        Public Property Currency As String

        Public Property MainImageUrl As String

        ' Second image, handy for a hover effect on product cards
        Public Property SecondImageUrl As String

        Public Property InStock As Boolean

        ' Added in the last 14 days
        Public Property IsNew As Boolean

        ' Color swatches and sizes that can be bought right now
        Public Property Colors As List(Of CatalogColorDto)

        Public Property Sizes As List(Of CatalogSizeDto)

    End Class


    Public Class CatalogVariantDto

        Public Property ProductVariantId As Integer

        Public Property ColorId As Integer?

        Public Property ColorName As String

        Public Property SizeId As Integer?

        Public Property SizeName As String

        Public Property SKU As String

        ' The price the customer pays for this variant
        Public Property Price As Decimal

        ' Only set when Price is a discount: show this crossed out
        Public Property OriginalPrice As Decimal?

        Public Property InStock As Boolean

        ' Most that can be ordered right now (capped, the exact stock is not exposed)
        Public Property AvailableQuantity As Integer

    End Class


    Public Class CatalogProductDetailsDto

        Public Property ProductId As Integer

        Public Property Name As String

        Public Property Slug As String

        Public Property Description As String

        Public Property Brand As String

        Public Property CategoryId As Integer

        Public Property CategoryName As String

        Public Property CategorySlug As String

        Public Property Price As Decimal

        Public Property SalePrice As Decimal?

        Public Property Currency As String

        Public Property InStock As Boolean

        Public Property Images As List(Of CatalogImageDto)

        Public Property Variants As List(Of CatalogVariantDto)

        ' Every color and size offered, for building the pickers
        Public Property Colors As List(Of CatalogColorDto)

        Public Property Sizes As List(Of CatalogSizeDto)

    End Class

End Namespace
