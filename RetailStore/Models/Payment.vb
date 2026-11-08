Imports System.ComponentModel.DataAnnotations
Imports System.ComponentModel.DataAnnotations.Schema
Imports System.Data.Entity

Namespace Models

    ' One row per payment attempt (COD, or a card payment through a gateway).
    <Table("Payments")>
    Public Class Payment

        <Key>
        <DatabaseGenerated(DatabaseGeneratedOption.Identity)>
        Public Property PaymentId As Integer

        <Required>
        Public Property OrderId As Integer

        <ForeignKey("OrderId")>
        Public Overridable Property Order As Order

        <Required>
        <StringLength(20)>
        Public Property Method As String

        <StringLength(50)>
        Public Property Provider As String

        <StringLength(200)>
        Public Property ProviderReference As String

        <Column(TypeName:="decimal")>
        Public Property Amount As Decimal

        <Required>
        <StringLength(3)>
        Public Property Currency As String

        <Required>
        <StringLength(20)>
        Public Property Status As String

        Public Property CreatedDate As DateTime

        Public Property UpdatedDate As DateTime?

    End Class

End Namespace


Namespace Data

    ' Adds the Payments table to your context (the context must be Partial, as before).
    Partial Public Class RetailStoreContext

        Public Property Payments As DbSet(Of Models.Payment)

    End Class

End Namespace
