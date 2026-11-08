Imports System.ComponentModel.DataAnnotations
Imports System.ComponentModel.DataAnnotations.Schema

Namespace Models

    <Table("ProductVariants")>
    Public Class ProductVariant

        <Key>
        <DatabaseGenerated(DatabaseGeneratedOption.Identity)>
        Public Property ProductVariantId As Integer

        <Required>
        Public Property ProductId As Integer

        <ForeignKey("ProductId")>
        Public Overridable Property Product As Product

        Public Property ColorId As Integer?

        <ForeignKey("ColorId")>
        Public Overridable Property Color As Color

        Public Property SizeId As Integer?

        <ForeignKey("SizeId")>
        Public Overridable Property Size As Size

        <Required>
        <StringLength(100)>
        Public Property SKU As String

        <StringLength(100)>
        Public Property Barcode As String

        Public Property StockQuantity As Integer

        <Column(TypeName:="decimal")>
        Public Property Price As Decimal?

        Public Property IsActive As Boolean

        Public Property CreatedDate As DateTime

    End Class

End Namespace