Imports System.ComponentModel.DataAnnotations
Imports System.ComponentModel.DataAnnotations.Schema

Namespace Models

    <Table("CartItems")>
    Public Class CartItem

        <Key>
        <DatabaseGenerated(DatabaseGeneratedOption.Identity)>
        Public Property CartItemId As Integer

        <Required>
        Public Property CartId As Integer

        <ForeignKey("CartId")>
        Public Overridable Property Cart As Cart

        <Required>
        Public Property ProductVariantId As Integer

        <ForeignKey("ProductVariantId")>
        Public Overridable Property ProductVariant As ProductVariant

        <Required>
        Public Property Quantity As Integer

        Public Property CreatedDate As DateTime

        Public Property UpdatedDate As DateTime?

    End Class

End Namespace