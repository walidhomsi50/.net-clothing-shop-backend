Imports System.Data.Entity.Infrastructure
Imports System.Net
Imports System.Net.Mail
Imports System.Security.Claims
Imports System.Web
Imports System.Web.Http
Imports RetailStore.Data
Imports RetailStore.DTOs
Imports RetailStore.Helpers

<RoutePrefix("api/v1/auth")>
Public Class AuthController
    Inherits ApiController

    Private ReadOnly db As New RetailStoreContext()

    Private Const MinPasswordLength As Integer = 8

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            db.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub


    ' =========================================================
    ' POST api/v1/auth/register   (customer)
    ' =========================================================
    <HttpPost>
    <Route("register")>
    Public Function Register(request As RegisterRequest) As IHttpActionResult

        If request Is Nothing Then
            Return BadRequest("Request is required.")
        End If

        If String.IsNullOrWhiteSpace(request.FirstName) OrElse
           String.IsNullOrWhiteSpace(request.LastName) OrElse
           String.IsNullOrWhiteSpace(request.Email) OrElse
           String.IsNullOrWhiteSpace(request.Password) Then

            Return BadRequest("First name, last name, email and password are required.")
        End If

        Dim email = request.Email.Trim().ToLowerInvariant()

        If Not IsValidEmail(email) Then
            Return BadRequest("Email address is not valid.")
        End If

        If request.Password.Length < MinPasswordLength Then
            Return BadRequest("Password must be at least " & MinPasswordLength & " characters.")
        End If

        ' Column collation is case-insensitive, so a direct compare is enough
        If db.Customers.Any(Function(c) c.Email = email) Then
            Return Content(HttpStatusCode.Conflict, "An account with this email already exists.")
        End If

        ' IMPORTANT: store NULL, not an empty string, when no phone is given.
        ' The unique index on Phone only ignores NULLs, so "" would block the 2nd customer.
        Dim phone As String = If(String.IsNullOrWhiteSpace(request.Phone), Nothing, request.Phone.Trim())

        Dim customer As New Models.Customer With {
            .FirstName = request.FirstName.Trim(),
            .LastName = request.LastName.Trim(),
            .Email = email,
            .Phone = phone,
            .PasswordHash = PasswordHelper.HashPassword(request.Password),
            .IsActive = True,
            .CreatedDate = DateTime.UtcNow
        }

        Try
            db.Customers.Add(customer)
            db.SaveChanges()
        Catch ex As DbUpdateException
            Diagnostics.Trace.TraceError(ex.ToString())
            Return Content(HttpStatusCode.Conflict, "An account with this email or phone number already exists.")
        End Try

        Return Ok(IssueCustomerTokens(customer))

    End Function


    ' =========================================================
    ' POST api/v1/auth/login   (customer)
    ' =========================================================
    <HttpPost>
    <Route("login")>
    Public Function Login(request As LoginRequest) As IHttpActionResult

        If request Is Nothing OrElse
           String.IsNullOrWhiteSpace(request.Email) OrElse
           String.IsNullOrWhiteSpace(request.Password) Then

            Return BadRequest("Email and password are required.")
        End If

        Dim email = request.Email.Trim().ToLowerInvariant()

        Dim customer = db.Customers.FirstOrDefault(
            Function(c) c.Email = email AndAlso c.IsActive)

        ' Same answer for "no such user" and "wrong password"
        If customer Is Nothing OrElse
           String.IsNullOrEmpty(customer.PasswordHash) OrElse
           Not PasswordHelper.VerifyPassword(request.Password, customer.PasswordHash) Then

            Return InvalidCredentials()
        End If

        Return Ok(IssueCustomerTokens(customer))

    End Function


    ' =========================================================
    ' POST api/v1/auth/admin/login
    ' =========================================================
    <HttpPost>
    <Route("admin/login")>
    Public Function AdminLogin(request As LoginRequest) As IHttpActionResult

        If request Is Nothing OrElse
           String.IsNullOrWhiteSpace(request.Email) OrElse
           String.IsNullOrWhiteSpace(request.Password) Then

            Return BadRequest("Email and password are required.")
        End If

        Dim email = request.Email.Trim().ToLowerInvariant()

        Dim admin = db.Admins.FirstOrDefault(
            Function(a) a.Email = email AndAlso a.IsActive)

        If admin Is Nothing OrElse
           Not PasswordHelper.VerifyPassword(request.Password, admin.PasswordHash) Then

            Return InvalidCredentials()
        End If

        admin.LastLoginDate = DateTime.UtcNow

        Dim refresh = JwtHelper.GenerateRefreshToken()
        db.RefreshTokens.Add(NewRefreshTokenRow(refresh, Nothing, admin.AdminId))
        db.SaveChanges()

        Return Ok(BuildAdminResponse(admin, refresh))

    End Function


    ' =========================================================
    ' POST api/v1/auth/refresh   (customers and admins)
    ' Rotates the refresh token: the old one stops working, a new pair is returned.
    ' =========================================================
    <HttpPost>
    <Route("refresh")>
    Public Function Refresh(request As RefreshRequest) As IHttpActionResult

        If request Is Nothing OrElse String.IsNullOrWhiteSpace(request.RefreshToken) Then
            Return BadRequest("Refresh token is required.")
        End If

        Dim hash = JwtHelper.HashToken(request.RefreshToken)
        Dim stored = db.RefreshTokens.FirstOrDefault(Function(t) t.TokenHash = hash)

        If stored Is Nothing Then
            Return InvalidRefreshToken()
        End If

        ' A token that was already used or revoked is being replayed: treat it as
        ' stolen and revoke every active token of that account.
        If stored.RevokedAt.HasValue Then
            RevokeAllForOwner(stored)
            db.SaveChanges()
            Return InvalidRefreshToken()
        End If

        If stored.ExpiresAt <= DateTime.UtcNow Then
            Return InvalidRefreshToken()
        End If

        Dim newRefresh = JwtHelper.GenerateRefreshToken()
        stored.RevokedAt = DateTime.UtcNow
        stored.ReplacedByTokenHash = JwtHelper.HashToken(newRefresh)

        Dim response As AuthResponse

        If stored.CustomerId.HasValue Then

            Dim customerId = stored.CustomerId.Value
            Dim customer = db.Customers.FirstOrDefault(
                Function(c) c.CustomerId = customerId AndAlso c.IsActive)

            If customer Is Nothing Then
                db.SaveChanges()
                Return InvalidRefreshToken()
            End If

            db.RefreshTokens.Add(NewRefreshTokenRow(newRefresh, customer.CustomerId, Nothing))
            response = BuildCustomerResponse(customer, newRefresh)

        Else

            Dim adminId = stored.AdminId.Value
            Dim admin = db.Admins.FirstOrDefault(
                Function(a) a.AdminId = adminId AndAlso a.IsActive)

            If admin Is Nothing Then
                db.SaveChanges()
                Return InvalidRefreshToken()
            End If

            db.RefreshTokens.Add(NewRefreshTokenRow(newRefresh, Nothing, admin.AdminId))
            response = BuildAdminResponse(admin, newRefresh)

        End If

        db.SaveChanges()

        Return Ok(response)

    End Function


    ' =========================================================
    ' POST api/v1/auth/logout   (revokes the given refresh token)
    ' =========================================================
    <HttpPost>
    <Route("logout")>
    Public Function Logout(request As RefreshRequest) As IHttpActionResult

        If request IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(request.RefreshToken) Then

            Dim hash = JwtHelper.HashToken(request.RefreshToken)
            Dim stored = db.RefreshTokens.FirstOrDefault(
                Function(t) t.TokenHash = hash AndAlso Not t.RevokedAt.HasValue)

            If stored IsNot Nothing Then
                stored.RevokedAt = DateTime.UtcNow
                db.SaveChanges()
            End If

        End If

        ' Always succeed: logging out twice is harmless
        Return Ok(New With {.Message = "Logged out."})

    End Function


    ' =========================================================
    ' GET api/v1/auth/me   (customer profile; also a handy token test)
    ' =========================================================
    <HttpGet>
    <Route("me")>
    <Authorize(Roles:="Customer")>
    Public Function GetProfile() As IHttpActionResult

        Dim id = GetCurrentUserId()
        Dim customer = db.Customers.FirstOrDefault(
            Function(c) c.CustomerId = id AndAlso c.IsActive)

        If customer Is Nothing Then
            Return Unauthorized()
        End If

        Return Ok(New With {
            .CustomerId = customer.CustomerId,
            .FirstName = customer.FirstName,
            .LastName = customer.LastName,
            .Email = customer.Email,
            .Phone = customer.Phone
        })

    End Function


    ' =========================================================
    ' GET api/v1/auth/admin/me   (admin profile; also a handy token test)
    ' =========================================================
    <HttpGet>
    <Route("admin/me")>
    <Authorize(Roles:="Admin,Staff")>
    Public Function GetAdminProfile() As IHttpActionResult

        Dim id = GetCurrentUserId()
        Dim admin = db.Admins.FirstOrDefault(
            Function(a) a.AdminId = id AndAlso a.IsActive)

        If admin Is Nothing Then
            Return Unauthorized()
        End If

        Return Ok(New With {
            .AdminId = admin.AdminId,
            .FullName = admin.FullName,
            .Email = admin.Email,
            .Role = admin.Role
        })

    End Function


    ' =========================================================
    ' Helpers
    ' =========================================================

    Private Function IssueCustomerTokens(customer As Models.Customer) As AuthResponse
        Dim refresh = JwtHelper.GenerateRefreshToken()
        db.RefreshTokens.Add(NewRefreshTokenRow(refresh, customer.CustomerId, Nothing))
        db.SaveChanges()
        Return BuildCustomerResponse(customer, refresh)
    End Function

    Private Shared Function BuildCustomerResponse(customer As Models.Customer, refreshToken As String) As AuthResponse
        Return New AuthResponse With {
            .UserId = customer.CustomerId,
            .DisplayName = (customer.FirstName & " " & customer.LastName).Trim(),
            .Email = customer.Email,
            .Role = JwtHelper.RoleCustomer,
            .AccessToken = JwtHelper.CreateAccessToken(customer.CustomerId, customer.Email, JwtHelper.RoleCustomer),
            .refreshToken = refreshToken,
            .ExpiresInSeconds = JwtHelper.AccessTokenMinutes * 60
        }
    End Function

    Private Shared Function BuildAdminResponse(admin As Models.Admin, refreshToken As String) As AuthResponse
        Return New AuthResponse With {
            .UserId = admin.AdminId,
            .DisplayName = admin.FullName,
            .Email = admin.Email,
            .Role = admin.Role,
            .AccessToken = JwtHelper.CreateAccessToken(admin.AdminId, admin.Email, admin.Role),
            .refreshToken = refreshToken,
            .ExpiresInSeconds = JwtHelper.AccessTokenMinutes * 60
        }
    End Function

    Private Shared Function NewRefreshTokenRow(
        rawToken As String,
        customerId As Integer?,
        adminId As Integer?) As Models.RefreshToken

        Return New Models.RefreshToken With {
            .TokenHash = JwtHelper.HashToken(rawToken),
            .customerId = customerId,
            .adminId = adminId,
            .ExpiresAt = DateTime.UtcNow.AddDays(JwtHelper.RefreshTokenDays),
            .CreatedAt = DateTime.UtcNow,
            .CreatedByIp = GetClientIp()
        }
    End Function

    Private Sub RevokeAllForOwner(token As Models.RefreshToken)

        Dim query = db.RefreshTokens.Where(Function(t) Not t.RevokedAt.HasValue)

        If token.CustomerId.HasValue Then
            Dim cid = token.CustomerId.Value
            query = query.Where(Function(t) t.CustomerId.Value = cid)
        Else
            Dim aid = token.AdminId.Value
            query = query.Where(Function(t) t.AdminId.Value = aid)
        End If

        Dim now = DateTime.UtcNow
        For Each t In query.ToList()
            t.RevokedAt = now
        Next

    End Sub

    Private Function GetCurrentUserId() As Integer
        Dim identity = TryCast(User.Identity, ClaimsIdentity)
        If identity Is Nothing Then
            Return 0
        End If

        Dim claim = identity.FindFirst(ClaimTypes.NameIdentifier)
        Dim id As Integer
        If claim IsNot Nothing AndAlso Integer.TryParse(claim.Value, id) Then
            Return id
        End If

        Return 0
    End Function

    Private Shared Function GetClientIp() As String
        If HttpContext.Current IsNot Nothing Then
            Return HttpContext.Current.Request.UserHostAddress
        End If
        Return Nothing
    End Function

    Private Shared Function IsValidEmail(email As String) As Boolean
        Try
            Dim address As New MailAddress(email)
            Return String.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase)
        Catch
            Return False
        End Try
    End Function

    Private Function InvalidCredentials() As IHttpActionResult
        Return Content(HttpStatusCode.Unauthorized, "Invalid email or password.")
    End Function

    Private Function InvalidRefreshToken() As IHttpActionResult
        Return Content(HttpStatusCode.Unauthorized, "Invalid or expired refresh token.")
    End Function

End Class