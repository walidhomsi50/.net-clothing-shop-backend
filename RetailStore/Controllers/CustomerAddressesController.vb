Imports System.Linq
Imports System.Security.Claims
Imports System.Web.Http

Imports RetailStore.Data
Imports RetailStore.DTOs
Imports RetailStore.Models

Public Class CustomerAddressesController
    Inherits ApiController

    Private ReadOnly db As New RetailStoreContext()


    ' ============================================================
    ' GET CUSTOMER ID FROM JWT
    ' ============================================================
    Private Function GetCustomerId() As Integer?

        Dim identity As ClaimsIdentity =
            TryCast(User.Identity, ClaimsIdentity)

        If identity Is Nothing Then
            Return Nothing
        End If

        If Not identity.IsAuthenticated Then
            Return Nothing
        End If

        Dim claim As Claim =
            identity.FindFirst(ClaimTypes.NameIdentifier)

        If claim Is Nothing Then
            Return Nothing
        End If

        Dim customerId As Integer

        If Not Integer.TryParse(claim.Value, customerId) Then
            Return Nothing
        End If

        Return customerId

    End Function


    ' ============================================================
    ' MODEL -> DTO
    ' ============================================================
    Private Function ToDto(address As CustomerAddress) As CustomerAddressDto

        Dim result As New CustomerAddressDto()

        result.CustomerAddressId = address.CustomerAddressId
        result.Title = address.Title
        result.FullName = address.FullName
        result.Phone = address.Phone
        result.AddressLine = address.AddressLine
        result.City = address.City
        result.Area = address.Area
        result.Building = address.Building
        result.Floor = address.Floor
        result.Apartment = address.Apartment
        result.Notes = address.Notes
        result.IsDefault = address.IsDefault
        result.CreatedDate = address.CreatedDate
        result.UpdatedDate = address.UpdatedDate

        Return result

    End Function


    ' ============================================================
    ' GET ALL ADDRESSES
    '
    ' GET api/customer-addresses
    ' ============================================================
    <Authorize>
    <HttpGet>
    <Route("api/customer-addresses")>
    Public Function GetAddresses() As IHttpActionResult

        Dim customerId As Integer? = GetCustomerId()

        If Not customerId.HasValue Then
            Return Unauthorized()
        End If


        Dim addresses = db.CustomerAddresses _
            .Where(Function(a) a.CustomerId = customerId.Value) _
            .OrderByDescending(Function(a) a.IsDefault) _
            .ThenByDescending(Function(a) a.CreatedDate) _
            .ToList()


        Dim result As New List(Of CustomerAddressDto)

        For Each address As CustomerAddress In addresses
            result.Add(ToDto(address))
        Next


        Return Ok(result)

    End Function


    ' ============================================================
    ' GET ONE ADDRESS
    '
    ' GET api/customer-addresses/{id}
    ' ============================================================
    <Authorize>
    <HttpGet>
    <Route("api/customer-addresses/{id:int}")>
    Public Function GetAddress(id As Integer) As IHttpActionResult

        Dim customerId As Integer? = GetCustomerId()

        If Not customerId.HasValue Then
            Return Unauthorized()
        End If


        Dim address = db.CustomerAddresses _
            .FirstOrDefault(Function(a) a.CustomerAddressId = id AndAlso a.CustomerId = customerId.Value)


        If address Is Nothing Then
            Return NotFound()
        End If


        Return Ok(ToDto(address))

    End Function


    ' ============================================================
    ' CREATE ADDRESS
    '
    ' POST api/customer-addresses
    ' ============================================================
    <Authorize>
    <HttpPost>
    <Route("api/customer-addresses")>
    Public Function CreateAddress(request As CreateCustomerAddressRequest) As IHttpActionResult

        Dim customerId As Integer? = GetCustomerId()

        If Not customerId.HasValue Then
            Return Unauthorized()
        End If


        If request Is Nothing Then
            Return BadRequest("Request is required.")
        End If


        ' --------------------------------------------------------
        ' VALIDATION
        ' --------------------------------------------------------

        If String.IsNullOrWhiteSpace(request.Title) Then
            Return BadRequest("Title is required.")
        End If

        If String.IsNullOrWhiteSpace(request.FullName) Then
            Return BadRequest("Full name is required.")
        End If

        If String.IsNullOrWhiteSpace(request.Phone) Then
            Return BadRequest("Phone is required.")
        End If

        If String.IsNullOrWhiteSpace(request.AddressLine) Then
            Return BadRequest("Address is required.")
        End If

        If String.IsNullOrWhiteSpace(request.City) Then
            Return BadRequest("City is required.")
        End If


        ' --------------------------------------------------------
        ' CHECK EXISTING ADDRESSES
        ' --------------------------------------------------------

        Dim hasExistingAddress As Boolean =
            db.CustomerAddresses.Any(Function(a) a.CustomerId = customerId.Value)


        ' --------------------------------------------------------
        ' IF NEW ADDRESS IS DEFAULT
        ' REMOVE DEFAULT FROM EXISTING ADDRESSES
        ' --------------------------------------------------------

        If request.IsDefault Then

            Dim existingDefaultAddresses = db.CustomerAddresses _
                .Where(Function(a) a.CustomerId = customerId.Value AndAlso a.IsDefault = True) _
                .ToList()


            For Each existingAddress As CustomerAddress In existingDefaultAddresses
                existingAddress.IsDefault = False
            Next

        End If


        ' --------------------------------------------------------
        ' CREATE ADDRESS
        ' --------------------------------------------------------

        Dim newAddress As New CustomerAddress()

        newAddress.CustomerId = customerId.Value
        newAddress.Title = request.Title.Trim()
        newAddress.FullName = request.FullName.Trim()
        newAddress.Phone = request.Phone.Trim()
        newAddress.AddressLine = request.AddressLine.Trim()
        newAddress.City = request.City.Trim()
        newAddress.Area = request.Area
        newAddress.Building = request.Building
        newAddress.Floor = request.Floor
        newAddress.Apartment = request.Apartment
        newAddress.Notes = request.Notes


        ' First address is automatically default
        If Not hasExistingAddress Then
            newAddress.IsDefault = True
        Else
            newAddress.IsDefault = request.IsDefault
        End If


        newAddress.CreatedDate = DateTime.Now


        db.CustomerAddresses.Add(newAddress)

        db.SaveChanges()


        Return Ok(ToDto(newAddress))

    End Function


    ' ============================================================
    ' UPDATE ADDRESS
    '
    ' PUT api/customer-addresses/{id}
    ' ============================================================
    <Authorize>
    <HttpPut>
    <Route("api/customer-addresses/{id:int}")>
    Public Function UpdateAddress(
        id As Integer,
        request As UpdateCustomerAddressRequest) As IHttpActionResult

        Dim customerId As Integer? = GetCustomerId()

        If Not customerId.HasValue Then
            Return Unauthorized()
        End If


        If request Is Nothing Then
            Return BadRequest("Request is required.")
        End If


        Dim address = db.CustomerAddresses _
            .FirstOrDefault(Function(a) a.CustomerAddressId = id AndAlso a.CustomerId = customerId.Value)


        If address Is Nothing Then
            Return NotFound()
        End If


        ' --------------------------------------------------------
        ' VALIDATION
        ' --------------------------------------------------------

        If String.IsNullOrWhiteSpace(request.Title) Then
            Return BadRequest("Title is required.")
        End If

        If String.IsNullOrWhiteSpace(request.FullName) Then
            Return BadRequest("Full name is required.")
        End If

        If String.IsNullOrWhiteSpace(request.Phone) Then
            Return BadRequest("Phone is required.")
        End If

        If String.IsNullOrWhiteSpace(request.AddressLine) Then
            Return BadRequest("Address is required.")
        End If

        If String.IsNullOrWhiteSpace(request.City) Then
            Return BadRequest("City is required.")
        End If


        ' --------------------------------------------------------
        ' MAKE THIS ADDRESS DEFAULT
        ' --------------------------------------------------------

        If request.IsDefault Then

            Dim otherDefaultAddresses = db.CustomerAddresses _
                .Where(Function(a) a.CustomerId = customerId.Value AndAlso a.CustomerAddressId <> id AndAlso a.IsDefault = True) _
                .ToList()


            For Each otherAddress As CustomerAddress In otherDefaultAddresses
                otherAddress.IsDefault = False
            Next

        End If


        ' --------------------------------------------------------
        ' UPDATE ADDRESS
        ' --------------------------------------------------------

        address.Title = request.Title.Trim()
        address.FullName = request.FullName.Trim()
        address.Phone = request.Phone.Trim()
        address.AddressLine = request.AddressLine.Trim()
        address.City = request.City.Trim()
        address.Area = request.Area
        address.Building = request.Building
        address.Floor = request.Floor
        address.Apartment = request.Apartment
        address.Notes = request.Notes
        address.IsDefault = request.IsDefault
        address.UpdatedDate = DateTime.Now


        db.SaveChanges()


        Return Ok(ToDto(address))

    End Function


    ' ============================================================
    ' DELETE ADDRESS
    '
    ' DELETE api/customer-addresses/{id}
    ' ============================================================
    <Authorize>
    <HttpDelete>
    <Route("api/customer-addresses/{id:int}")>
    Public Function DeleteAddress(id As Integer) As IHttpActionResult

        Dim customerId As Integer? = GetCustomerId()

        If Not customerId.HasValue Then
            Return Unauthorized()
        End If


        Dim address = db.CustomerAddresses _
            .FirstOrDefault(Function(a) a.CustomerAddressId = id AndAlso a.CustomerId = customerId.Value)


        If address Is Nothing Then
            Return NotFound()
        End If


        Dim wasDefault As Boolean = address.IsDefault


        db.CustomerAddresses.Remove(address)

        db.SaveChanges()


        ' --------------------------------------------------------
        ' IF DEFAULT ADDRESS WAS DELETED
        ' MAKE ANOTHER ADDRESS DEFAULT
        ' --------------------------------------------------------

        If wasDefault Then

            Dim nextAddress = db.CustomerAddresses _
                .Where(Function(a) a.CustomerId = customerId.Value) _
                .OrderByDescending(Function(a) a.CreatedDate) _
                .FirstOrDefault()


            If nextAddress IsNot Nothing Then

                nextAddress.IsDefault = True
                nextAddress.UpdatedDate = DateTime.Now

                db.SaveChanges()

            End If

        End If


        Return Ok(
            New With {
                .Message = "Address deleted successfully."
            })


    End Function


    ' ============================================================
    ' DISPOSE
    ' ============================================================
    Protected Overrides Sub Dispose(disposing As Boolean)

        If disposing Then
            db.Dispose()
        End If

        MyBase.Dispose(disposing)

    End Sub

End Class