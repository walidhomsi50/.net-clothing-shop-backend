Imports System.ComponentModel.DataAnnotations
Imports System.ComponentModel.DataAnnotations.Schema

Namespace Models

    <Table("Categories")>
    Public Class Category

        <Key>
        <DatabaseGenerated(DatabaseGeneratedOption.Identity)>
        Public Property CategoryId As Integer

        <Required>
        <StringLength(100)>
        Public Property Name As String

        ' URL-friendly name used in links, e.g. men-shirts (set once when the category is created)
        <Required>
        <StringLength(150)>
        Public Property Slug As String

        Public Property Description As String

        ' Nothing = top-level category. Only the id is mapped on purpose: a navigation property
        ' would make the public category endpoints load parent categories one by one.
        Public Property ParentCategoryId As Integer?

        Public Property IsActive As Boolean

        Public Property CreatedDate As DateTime

    End Class

End Namespace
