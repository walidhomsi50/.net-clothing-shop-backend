Imports System.ComponentModel.DataAnnotations
Imports System.ComponentModel.DataAnnotations.Schema
Imports System.Data.Entity

' FOR CODE FIRST ONLY (your project has hand-maintained classes in Models and a context in Data).
' If you actually use an .edmx file, DO NOT add this file: run "Update Model from Database" instead.
' Requires the tables created by 002_auth_and_catalog.sql.

Namespace Models

    <Table("Admins")>
    Public Class Admin

        <Key>
        <DatabaseGenerated(DatabaseGeneratedOption.Identity)>
        Public Property AdminId As Integer

        <Required>
        <StringLength(150)>
        Public Property FullName As String

        <Required>
        <StringLength(255)>
        Public Property Email As String

        <Required>
        <StringLength(500)>
        Public Property PasswordHash As String

        <Required>
        <StringLength(30)>
        Public Property Role As String

        Public Property IsActive As Boolean

        Public Property LastLoginDate As DateTime?

        Public Property CreatedDate As DateTime

        Public Property UpdatedDate As DateTime?

    End Class


    <Table("RefreshTokens")>
    Public Class RefreshToken

        <Key>
        <DatabaseGenerated(DatabaseGeneratedOption.Identity)>
        Public Property RefreshTokenId As Integer

        <Required>
        <StringLength(64)>
        Public Property TokenHash As String

        Public Property CustomerId As Integer?

        Public Property AdminId As Integer?

        Public Property ExpiresAt As DateTime

        Public Property CreatedAt As DateTime

        <StringLength(45)>
        Public Property CreatedByIp As String

        Public Property RevokedAt As DateTime?

        <StringLength(64)>
        Public Property ReplacedByTokenHash As String

    End Class

End Namespace


Namespace Data

    ' Adds the two new tables to your existing context (it must be declared Partial).
    Partial Public Class RetailStoreContext

        Public Property Admins As DbSet(Of Models.Admin)

        Public Property RefreshTokens As DbSet(Of Models.RefreshToken)

    End Class

End Namespace
