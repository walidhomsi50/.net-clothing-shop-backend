Imports System.Data.Entity

Namespace Data

    Public Class RetailStoreContext
        Inherits DbContext

        Public Sub New()
            MyBase.New("name=RetailStoreConnection")
        End Sub

        Public Property Categories As DbSet(Of Models.Category)
        Public Property Products As DbSet(Of Models.Product)
        Public Property Colors As DbSet(Of Models.Color)
        Public Property Sizes As DbSet(Of Models.Size)
        Public Property ProductVariants As DbSet(Of Models.ProductVariant)
        Public Property ProductImages As DbSet(Of Models.ProductImage)
        Public Property Customers As DbSet(Of Models.Customer)

        Public Property CustomerAddresses As DbSet(Of Models.CustomerAddress)
        Public Property Orders As DbSet(Of Models.Order)
        Public Property OrderItems As DbSet(Of Models.OrderItem)

        Public Property Carts As DbSet(Of Models.Cart)
        Public Property CartItems As DbSet(Of Models.CartItem)



    End Class

End Namespace