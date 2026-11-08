Imports System.ComponentModel.DataAnnotations
Imports System.ComponentModel.DataAnnotations.Schema

Namespace Models

    <Table("Carts")>
    Public Class Cart

        <Key>
        <DatabaseGenerated(DatabaseGeneratedOption.Identity)>
        Public Property CartId As Integer

        ' Exactly one of CustomerId / GuestToken is set (the database enforces this with a CHECK)
        Public Property CustomerId As Integer?

        <ForeignKey("CustomerId")>
        Public Overridable Property Customer As Customer

        Public Property GuestToken As Guid?

        Public Property CreatedDate As DateTime

        Public Property UpdatedDate As DateTime?

        Public Overridable Property Items As ICollection(Of CartItem)

    End Class

End Namespace
