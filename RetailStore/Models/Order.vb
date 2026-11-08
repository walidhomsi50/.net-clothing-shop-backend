Imports System.ComponentModel.DataAnnotations
Imports System.ComponentModel.DataAnnotations.Schema

Namespace Models

    <Table("Orders")>
    Public Class Order

        <Key>
        <DatabaseGenerated(DatabaseGeneratedOption.Identity)>
        Public Property OrderId As Integer

        ' Nothing = guest order
        Public Property CustomerId As Integer?

        <ForeignKey("CustomerId")>
        Public Overridable Property Customer As Customer

        ' Calculated by SQL Server from OrderId, e.g. ORD-000057
        <DatabaseGenerated(DatabaseGeneratedOption.Computed)>
        <StringLength(20)>
        Public Property OrderNumber As String

        <Required>
        Public Property OrderDate As DateTime

        <Required>
        <StringLength(20)>
        Public Property Status As String

        <Column(TypeName:="decimal")>
        Public Property Subtotal As Decimal

        <Column(TypeName:="decimal")>
        Public Property ShippingFee As Decimal

        <Column(TypeName:="decimal")>
        Public Property DiscountAmount As Decimal

        <Column(TypeName:="decimal")>
        Public Property TotalAmount As Decimal

        <StringLength(3)>
        Public Property Currency As String

        <Required>
        <StringLength(20)>
        Public Property PaymentMethod As String

        <Required>
        <StringLength(20)>
        Public Property PaymentStatus As String

        ' Filled by the database default (NEWID()); used for guest order tracking links
        <DatabaseGenerated(DatabaseGeneratedOption.Identity)>
        Public Property TrackingToken As Guid

        <StringLength(150)>
        Public Property ShippingFullName As String

        <StringLength(30)>
        Public Property ShippingPhone As String

        <StringLength(255)>
        Public Property ContactEmail As String

        <Required>
        Public Property ShippingAddress As String

        Public Property Notes As String

        Public Property CreatedDate As DateTime

        Public Overridable Property Items As ICollection(Of OrderItem)

    End Class

End Namespace
