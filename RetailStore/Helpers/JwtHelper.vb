Imports System.Configuration
Imports System.IdentityModel.Tokens.Jwt
Imports System.Security.Claims
Imports System.Security.Cryptography
Imports System.Text
Imports Microsoft.IdentityModel.Tokens

Namespace Helpers

    ''' <summary>
    ''' Uses the same web.config keys you already have: JwtIssuer, JwtAudience, JwtSecret
    ''' (JwtSecret = base64 string of at least 32 random bytes).
    ''' </summary>
    Public NotInheritable Class JwtHelper

        Private Sub New()
        End Sub

        Public Const AccessTokenMinutes As Integer = 15
        Public Const RefreshTokenDays As Integer = 30

        Public Const RoleCustomer As String = "Customer"
        Public Const RoleAdmin As String = "Admin"
        Public Const RoleStaff As String = "Staff"

        Private Shared Function GetSigningKey() As SymmetricSecurityKey
            Dim secret = ConfigurationManager.AppSettings("JwtSecret")
            If String.IsNullOrWhiteSpace(secret) Then
                Throw New InvalidOperationException("JwtSecret is not configured in web.config.")
            End If

            Dim keyBytes = Convert.FromBase64String(secret)
            If keyBytes.Length < 32 Then
                Throw New InvalidOperationException("JwtSecret must decode to at least 32 bytes.")
            End If

            Return New SymmetricSecurityKey(keyBytes)
        End Function

        ''' <summary>Short-lived access token. The role claim separates customers from admins
        ''' (their numeric IDs can overlap), so always protect endpoints with a Roles attribute.</summary>
        Public Shared Function CreateAccessToken(userId As Integer, email As String, role As String) As String

            Dim credentials As New SigningCredentials(GetSigningKey(), SecurityAlgorithms.HmacSha256)

            Dim claims As New List(Of Claim) From {
                New Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                New Claim(ClaimTypes.Name, email),
                New Claim(ClaimTypes.Role, role)
            }

            Dim token As New JwtSecurityToken(
                issuer:=ConfigurationManager.AppSettings("JwtIssuer"),
                audience:=ConfigurationManager.AppSettings("JwtAudience"),
                claims:=claims,
                notBefore:=DateTime.UtcNow,
                expires:=DateTime.UtcNow.AddMinutes(AccessTokenMinutes),
                signingCredentials:=credentials)

            Return New JwtSecurityTokenHandler().WriteToken(token)
        End Function

        ''' <summary>Returns the principal for a valid token, or Nothing if it is invalid/expired.</summary>
        Public Shared Function ValidateToken(token As String) As ClaimsPrincipal

            Dim parameters As New TokenValidationParameters With {
                .ValidateIssuer = True,
                .ValidIssuer = ConfigurationManager.AppSettings("JwtIssuer"),
                .ValidateAudience = True,
                .ValidAudience = ConfigurationManager.AppSettings("JwtAudience"),
                .ValidateIssuerSigningKey = True,
                .IssuerSigningKey = GetSigningKey(),
                .ValidateLifetime = True,
                .ClockSkew = TimeSpan.FromMinutes(1)
            }

            Try
                Dim validatedToken As SecurityToken = Nothing
                Return New JwtSecurityTokenHandler().ValidateToken(token, parameters, validatedToken)
            Catch ex As Exception
                Return Nothing
            End Try

        End Function

        ''' <summary>Random 64-byte token, URL-safe base64. Give this to the client; store only its hash.</summary>
        Public Shared Function GenerateRefreshToken() As String
            Dim bytes(63) As Byte
            Using rng = RandomNumberGenerator.Create()
                rng.GetBytes(bytes)
            End Using
            Return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd("="c)
        End Function

        ''' <summary>SHA-256 hex (64 chars) of a refresh token, for storing/looking up in the database.</summary>
        Public Shared Function HashToken(token As String) As String
            Using sha = SHA256.Create()
                Dim hash = sha.ComputeHash(Encoding.UTF8.GetBytes(token))
                Return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant()
            End Using
        End Function

    End Class

End Namespace