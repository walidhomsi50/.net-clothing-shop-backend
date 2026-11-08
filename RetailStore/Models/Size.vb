Imports System.ComponentModel.DataAnnotations
Imports System.ComponentModel.DataAnnotations.Schema

Namespace Models

    <Table("Sizes")>
    Public Class Size

        <Key>
        <DatabaseGenerated(DatabaseGeneratedOption.Identity)>
        Public Property SizeId As Integer

        <Required>
        <StringLength(50)>
        Public Property Name As String

        Public Property SortOrder As Integer

        Public Property IsActive As Boolean

    End Class

End Namespace