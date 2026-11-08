Imports System.ComponentModel.DataAnnotations
Imports System.ComponentModel.DataAnnotations.Schema

Namespace Models

    <Table("ProductImages")>
    Public Class ProductImage

        <Key>
        <DatabaseGenerated(DatabaseGeneratedOption.Identity)>
        Public Property ProductImageId As Integer

        <Required>
        Public Property ProductId As Integer

        <ForeignKey("ProductId")>
        Public Overridable Property Product As Product

        <Required>
        Public Property ImageUrl As String

        Public Property SortOrder As Integer

        Public Property IsMain As Boolean

        Public Property CreatedDate As DateTime

    End Class

End Namespace