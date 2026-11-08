Imports System.ComponentModel.DataAnnotations
Imports System.ComponentModel.DataAnnotations.Schema

Namespace Models

    <Table("Products")>
    Public Class Product

        <Key>
        <DatabaseGenerated(DatabaseGeneratedOption.Identity)>
        Public Property ProductId As Integer

        <Required>
        Public Property CategoryId As Integer

        <ForeignKey("CategoryId")>
        Public Overridable Property Category As Category

        <Required>
        <StringLength(200)>
        Public Property Name As String

        ' URL-friendly name used in links, e.g. black-linen-shirt (set once when the product is created)
        <Required>
        <StringLength(250)>
        Public Property Slug As String

        Public Property Description As String

        <StringLength(100)>
        Public Property Brand As String

        <Column(TypeName:="decimal")>
        Public Property Price As Decimal

        <Column(TypeName:="decimal")>
        Public Property SalePrice As Decimal?

        Public Property IsActive As Boolean

        Public Property CreatedDate As DateTime

        Public Property UpdatedDate As DateTime?

    End Class

End Namespace
