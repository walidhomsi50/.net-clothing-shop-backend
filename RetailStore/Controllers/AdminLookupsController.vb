Imports System.Collections.Generic
Imports System.Linq
Imports System.Text.RegularExpressions
Imports System.Web.Http
Imports RetailStore.Data
Imports RetailStore.DTOs
Imports RetailStore.Helpers
Imports RetailStore.Models

''' <summary>
''' Colors and sizes management for the admin panel. Admin and Staff accounts only.
''' Never deleted: switch IsActive off to stop offering one on new variants.
''' </summary>
<RoutePrefix("api/v1/admin")>
<Authorize(Roles:="Admin,Staff")>
Public Class AdminLookupsController
    Inherits ApiController

    Private Shared ReadOnly HexPattern As New Regex("^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled)

    Private ReadOnly db As New RetailStoreContext()

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            db.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub


    ' =========================================================
    ' COLORS
    ' =========================================================

    ' GET api/v1/admin/colors
    <HttpGet>
    <Route("colors")>
    Public Function GetColors() As IHttpActionResult

        Dim rows = db.Colors.OrderBy(Function(c) c.Name).ToList()

        Return Ok(rows.Select(Function(c) ToColorResult(c)).ToList())

    End Function


    ' POST api/v1/admin/colors      { "name": "Black", "hexCode": "#000000", "isActive": true }
    <HttpPost>
    <Route("colors")>
    Public Function CreateColor(body As SaveColorRequest) As IHttpActionResult

        Dim problem As String = ValidateColor(body, 0)

        If problem IsNot Nothing Then
            Return BadRequest(problem)
        End If

        Dim created As New Color With {
            .Name = body.Name.Trim(),
            .HexCode = NullIfBlank(body.HexCode),
            .IsActive = body.IsActive
        }

        db.Colors.Add(created)
        db.SaveChanges()

        Return Ok(ToColorResult(created))

    End Function


    ' PUT api/v1/admin/colors/3
    <HttpPut>
    <Route("colors/{id:int}")>
    Public Function UpdateColor(id As Integer, body As SaveColorRequest) As IHttpActionResult

        Dim found = db.Colors.FirstOrDefault(Function(c) c.ColorId = id)

        If found Is Nothing Then
            Return NotFound()
        End If

        Dim problem As String = ValidateColor(body, id)

        If problem IsNot Nothing Then
            Return BadRequest(problem)
        End If

        found.Name = body.Name.Trim()
        found.HexCode = NullIfBlank(body.HexCode)
        found.IsActive = body.IsActive

        db.SaveChanges()

        Return Ok(ToColorResult(found))

    End Function


    ' =========================================================
    ' SIZES
    ' =========================================================

    ' GET api/v1/admin/sizes
    <HttpGet>
    <Route("sizes")>
    Public Function GetSizes() As IHttpActionResult

        Dim rows = db.Sizes.OrderBy(Function(s) s.SortOrder).ThenBy(Function(s) s.Name).ToList()

        Return Ok(rows.Select(Function(s) ToSizeResult(s)).ToList())

    End Function


    ' POST api/v1/admin/sizes      { "name": "M", "sortOrder": 3, "isActive": true }
    <HttpPost>
    <Route("sizes")>
    Public Function CreateSize(body As SaveSizeRequest) As IHttpActionResult

        Dim problem As String = ValidateSize(body, 0)

        If problem IsNot Nothing Then
            Return BadRequest(problem)
        End If

        Dim created As New Size With {
            .Name = body.Name.Trim(),
            .SortOrder = body.SortOrder,
            .IsActive = body.IsActive
        }

        db.Sizes.Add(created)
        db.SaveChanges()

        Return Ok(ToSizeResult(created))

    End Function


    ' PUT api/v1/admin/sizes/3
    <HttpPut>
    <Route("sizes/{id:int}")>
    Public Function UpdateSize(id As Integer, body As SaveSizeRequest) As IHttpActionResult

        Dim found = db.Sizes.FirstOrDefault(Function(s) s.SizeId = id)

        If found Is Nothing Then
            Return NotFound()
        End If

        Dim problem As String = ValidateSize(body, id)

        If problem IsNot Nothing Then
            Return BadRequest(problem)
        End If

        found.Name = body.Name.Trim()
        found.SortOrder = body.SortOrder
        found.IsActive = body.IsActive

        db.SaveChanges()

        Return Ok(ToSizeResult(found))

    End Function


    ' =========================================================
    ' Helpers
    ' =========================================================

    Private Function ValidateColor(body As SaveColorRequest, ownId As Integer) As String

        If body Is Nothing Then
            Return "Request is required."
        End If

        If String.IsNullOrWhiteSpace(body.Name) Then
            Return "Name is required."
        End If

        Dim name As String = body.Name.Trim()

        If name.Length > 50 Then
            Return "Name is too long (maximum 50 characters)."
        End If

        Dim hexCode As String = NullIfBlank(body.HexCode)

        If hexCode IsNot Nothing AndAlso Not HexPattern.IsMatch(hexCode) Then
            Return "HexCode must look like #FF8800."
        End If

        If db.Colors.Any(Function(c) c.Name = name AndAlso c.ColorId <> ownId) Then
            Return "A color with this name already exists."
        End If

        Return Nothing

    End Function

    Private Function ValidateSize(body As SaveSizeRequest, ownId As Integer) As String

        If body Is Nothing Then
            Return "Request is required."
        End If

        If String.IsNullOrWhiteSpace(body.Name) Then
            Return "Name is required."
        End If

        Dim name As String = body.Name.Trim()

        If name.Length > 50 Then
            Return "Name is too long (maximum 50 characters)."
        End If

        If db.Sizes.Any(Function(s) s.Name = name AndAlso s.SizeId <> ownId) Then
            Return "A size with this name already exists."
        End If

        Return Nothing

    End Function

    Private Shared Function NullIfBlank(value As String) As String
        If String.IsNullOrWhiteSpace(value) Then
            Return Nothing
        End If
        Return value.Trim()
    End Function

    Private Shared Function ToColorResult(row As Color) As Object
        Return New With {
            .ColorId = row.ColorId,
            .Name = row.Name,
            .HexCode = row.HexCode,
            .IsActive = row.IsActive
        }
    End Function

    Private Shared Function ToSizeResult(row As Size) As Object
        Return New With {
            .SizeId = row.SizeId,
            .Name = row.Name,
            .SortOrder = row.SortOrder,
            .IsActive = row.IsActive
        }
    End Function

End Class
