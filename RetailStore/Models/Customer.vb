Imports System.ComponentModel.DataAnnotations
Imports System.ComponentModel.DataAnnotations.Schema

Namespace Models

    <Table("Customers")>
    Public Class Customer

        <Key>
        <DatabaseGenerated(DatabaseGeneratedOption.Identity)>
        Public Property CustomerId As Integer

        <Required>
        <StringLength(100)>
        Public Property FirstName As String

        <Required>
        <StringLength(100)>
        Public Property LastName As String

        <Required>
        <StringLength(200)>
        Public Property Email As String

        <StringLength(50)>
        Public Property Phone As String

        Public Property PasswordHash As String

        Public Property IsActive As Boolean

        Public Property CreatedDate As DateTime

        Public Property UpdatedDate As DateTime?

    End Class

End Namespace