Imports System.ComponentModel.DataAnnotations
Imports System.ComponentModel.DataAnnotations.Schema

Namespace Models

    <Table("Colors")>
    Public Class Color

        <Key>
        <DatabaseGenerated(DatabaseGeneratedOption.Identity)>
        Public Property ColorId As Integer

        <Required>
        <StringLength(50)>
        Public Property Name As String

        <StringLength(7)>
        Public Property HexCode As String

        Public Property IsActive As Boolean

    End Class

End Namespace