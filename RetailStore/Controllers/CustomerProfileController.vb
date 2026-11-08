Imports System.Security.Claims
Imports System.Web.Http
Imports RetailStore.Data
Imports RetailStore.DTOs
Imports RetailStore.Helpers

Public Class CustomerProfileController
    Inherits ApiController

    Private ReadOnly db As New RetailStoreContext()


    '===========================================================
    ' GET CURRENT CUSTOMER
    ' GET api/customers/me
    '===========================================================
    <Authorize>
    <HttpGet>
    <Route("api/customers/me")>
    Public Function GetMe() As IHttpActionResult

        Dim customerId As Integer = GetCustomerIdFromToken()

        If customerId <= 0 Then
            Return Unauthorized()
        End If

        Dim customer = db.Customers _
            .Where(Function(c) c.CustomerId = customerId AndAlso c.IsActive) _
            .FirstOrDefault()

        If customer Is Nothing Then
            Return NotFound()
        End If

        Dim result As New CustomerDto With {
            .CustomerId = customer.CustomerId,
            .FirstName = customer.FirstName,
            .LastName = customer.LastName,
            .Email = customer.Email,
            .Phone = customer.Phone,
            .IsActive = customer.IsActive,
            .CreatedDate = customer.CreatedDate
        }

        Return Ok(result)

    End Function


    '===========================================================
    ' UPDATE CURRENT CUSTOMER PROFILE
    ' PUT api/customers/me
    '===========================================================
    <Authorize>
    <HttpPut>
    <Route("api/customers/me")>
    Public Function UpdateProfile(
        <FromBody> request As UpdateCustomerProfileRequest) As IHttpActionResult

        If request Is Nothing Then
            Return BadRequest("Request body is required.")
        End If

        Dim customerId As Integer = GetCustomerIdFromToken()

        If customerId <= 0 Then
            Return Unauthorized()
        End If

        Dim customer = db.Customers _
            .Where(Function(c) c.CustomerId = customerId AndAlso c.IsActive) _
            .FirstOrDefault()

        If customer Is Nothing Then
            Return NotFound()
        End If


        '-------------------------------------------------------
        ' Validate first name
        '-------------------------------------------------------
        If String.IsNullOrWhiteSpace(request.FirstName) Then
            Return BadRequest("First name is required.")
        End If


        '-------------------------------------------------------
        ' Validate last name
        '-------------------------------------------------------
        If String.IsNullOrWhiteSpace(request.LastName) Then
            Return BadRequest("Last name is required.")
        End If


        '-------------------------------------------------------
        ' Validate email
        '-------------------------------------------------------
        If String.IsNullOrWhiteSpace(request.Email) Then
            Return BadRequest("Email is required.")
        End If

        Dim email As String = request.Email.Trim().ToLowerInvariant()


        '-------------------------------------------------------
        ' Check if another customer already uses this email
        '-------------------------------------------------------
        Dim customers = db.Customers.ToList()

        For Each existingCustomer In customers

            If existingCustomer.CustomerId <> customerId Then

                If Not String.IsNullOrWhiteSpace(existingCustomer.Email) Then

                    If existingCustomer.Email.Trim().ToLowerInvariant() = email Then
                        Return BadRequest("Email is already being used by another customer.")
                    End If

                End If

            End If

        Next


        '-------------------------------------------------------
        ' Update customer
        '-------------------------------------------------------
        customer.FirstName = request.FirstName.Trim()
        customer.LastName = request.LastName.Trim()
        customer.Email = email

        If String.IsNullOrWhiteSpace(request.Phone) Then
            customer.Phone = Nothing
        Else
            customer.Phone = request.Phone.Trim()
        End If

        customer.UpdatedDate = DateTime.Now

        db.SaveChanges()


        '-------------------------------------------------------
        ' Return updated customer
        '-------------------------------------------------------
        Dim result As New CustomerDto With {
            .CustomerId = customer.CustomerId,
            .FirstName = customer.FirstName,
            .LastName = customer.LastName,
            .Email = customer.Email,
            .Phone = customer.Phone,
            .IsActive = customer.IsActive,
            .CreatedDate = customer.CreatedDate
        }

        Return Ok(result)

    End Function


    '===========================================================
    ' CHANGE PASSWORD
    ' POST api/customers/change-password
    '===========================================================
    <Authorize>
    <HttpPost>
    <Route("api/customers/change-password")>
    Public Function ChangePassword(
        <FromBody> request As ChangePasswordRequest) As IHttpActionResult

        If request Is Nothing Then
            Return BadRequest("Request body is required.")
        End If


        '-------------------------------------------------------
        ' Validate current password
        '-------------------------------------------------------
        If String.IsNullOrWhiteSpace(request.CurrentPassword) Then
            Return BadRequest("Current password is required.")
        End If


        '-------------------------------------------------------
        ' Validate new password
        '-------------------------------------------------------
        If String.IsNullOrWhiteSpace(request.NewPassword) Then
            Return BadRequest("New password is required.")
        End If

        If request.NewPassword.Length < 8 Then
            Return BadRequest("New password must contain at least 8 characters.")
        End If


        '-------------------------------------------------------
        ' Get customer ID from JWT
        '-------------------------------------------------------
        Dim customerId As Integer = GetCustomerIdFromToken()

        If customerId <= 0 Then
            Return Unauthorized()
        End If


        '-------------------------------------------------------
        ' Get customer
        '-------------------------------------------------------
        Dim customer = db.Customers _
            .Where(Function(c) c.CustomerId = customerId AndAlso c.IsActive) _
            .FirstOrDefault()

        If customer Is Nothing Then
            Return NotFound()
        End If


        '-------------------------------------------------------
        ' Verify current password
        '-------------------------------------------------------
        If Not PasswordHelper.VerifyPassword(
            request.CurrentPassword,
            customer.PasswordHash) Then

            Return BadRequest("Current password is incorrect.")

        End If


        '-------------------------------------------------------
        ' Prevent using the same password
        '-------------------------------------------------------
        If PasswordHelper.VerifyPassword(
            request.NewPassword,
            customer.PasswordHash) Then

            Return BadRequest("New password must be different from the current password.")

        End If


        '-------------------------------------------------------
        ' Hash new password
        '-------------------------------------------------------
        customer.PasswordHash =
            PasswordHelper.HashPassword(request.NewPassword)

        customer.UpdatedDate = DateTime.Now

        db.SaveChanges()


        Return Ok(
            New With {
                .Message = "Password changed successfully."
            })

    End Function


    '===========================================================
    ' GET CUSTOMER ID FROM JWT
    '===========================================================
    Private Function GetCustomerIdFromToken() As Integer

        Dim identity = TryCast(User.Identity, ClaimsIdentity)

        If identity Is Nothing OrElse Not identity.IsAuthenticated Then
            Return 0
        End If


        Dim customerIdClaim =
            identity.FindFirst(ClaimTypes.NameIdentifier)

        If customerIdClaim Is Nothing Then
            Return 0
        End If


        Dim customerId As Integer

        If Not Integer.TryParse(
            customerIdClaim.Value,
            customerId) Then

            Return 0
        End If


        Return customerId

    End Function


    Protected Overrides Sub Dispose(disposing As Boolean)

        If disposing Then
            db.Dispose()
        End If

        MyBase.Dispose(disposing)

    End Sub

End Class