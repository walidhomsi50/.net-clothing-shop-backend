Imports System.ComponentModel.DataAnnotations
Imports System.ComponentModel.DataAnnotations.Schema

Namespace Models

    <Table("OrderItems")>
    Public Class OrderItem

        <Key>
        <DatabaseGenerated(DatabaseGeneratedOption.Identity)>
        Public Property OrderItemId As Integer

        <Required>
        Public Property OrderId As Integer

        <ForeignKey("OrderId")>
        Public Overridable Property Order As Order

        <Required>
        Public Property ProductVariantId As Integer

        <ForeignKey("ProductVariantId")>
        Public Overridable Property ProductVariant As ProductVariant

        ' Snapshot of the product details at the time of purchase,
        ' so renaming or deleting a product never changes old orders.
        <Required>
        <StringLength(200)>
        Public Property ProductName As String

        <Required>
        <StringLength(100)>
        Public Property SKU As String

        <StringLength(50)>
        Public Property SizeName As String

        <StringLength(50)>
        Public Property ColorName As String

        <Required>
        Public Property Quantity As Integer

        <Column(TypeName:="decimal")>
        Public Property UnitPrice As Decimal

        ' SQL Server calculates this column: Quantity * UnitPrice
        <DatabaseGenerated(DatabaseGeneratedOption.Computed)>
        <Column(TypeName:="decimal")>
        Public Property TotalPrice As Decimal

    End Class

End Namespace
