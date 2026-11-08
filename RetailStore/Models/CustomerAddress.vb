Imports System.ComponentModel.DataAnnotations
Imports System.ComponentModel.DataAnnotations.Schema

Namespace Models

    <Table("CustomerAddresses")>
    Public Class CustomerAddress

        <Key>
        <DatabaseGenerated(DatabaseGeneratedOption.Identity)>
        Public Property CustomerAddressId As Integer

        <Required>
        Public Property CustomerId As Integer

        <ForeignKey("CustomerId")>
        Public Overridable Property Customer As Customer


        <Required>
        <StringLength(50)>
        Public Property Title As String


        <Required>
        <StringLength(150)>
        Public Property FullName As String


        <Required>
        <StringLength(30)>
        Public Property Phone As String


        <Required>
        <StringLength(300)>
        Public Property AddressLine As String


        <Required>
        <StringLength(100)>
        Public Property City As String


        <StringLength(100)>
        Public Property Area As String


        <StringLength(50)>
        Public Property Building As String


        <StringLength(50)>
        Public Property Floor As String


        <StringLength(50)>
        Public Property Apartment As String


        <StringLength(500)>
        Public Property Notes As String


        Public Property IsDefault As Boolean


        Public Property CreatedDate As DateTime

        Public Property UpdatedDate As DateTime?

    End Class

End Namespace