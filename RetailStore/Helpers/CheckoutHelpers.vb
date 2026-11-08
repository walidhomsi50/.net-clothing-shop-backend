Imports System.Collections.Generic
Imports System.Linq
Imports System.Net.Http
Imports System.Security.Claims
Imports System.Security.Principal
Imports System.Text.RegularExpressions
Imports RetailStore.Data

Namespace Helpers

    ' Values allowed by the CHECK constraints in the database
    Public NotInheritable Class OrderStatuses
        Private Sub New()
        End Sub
        Public Const Pending As String = "Pending"
        Public Const Confirmed As String = "Confirmed"
        Public Const Processing As String = "Processing"
        Public Const Shipped As String = "Shipped"
        Public Const Delivered As String = "Delivered"
        Public Const Cancelled As String = "Cancelled"
    End Class

    Public NotInheritable Class PaymentStatuses
        Private Sub New()
        End Sub
        Public Const Pending As String = "Pending"
        Public Const Paid As String = "Paid"
        Public Const Failed As String = "Failed"
        Public Const Refunded As String = "Refunded"
    End Class

    Public NotInheritable Class PaymentMethods
        Private Sub New()
        End Sub
        Public Const COD As String = "COD"
        Public Const Card As String = "Card"
    End Class


    ''' <summary>The single place where prices and limits are decided.</summary>
    Public NotInheritable Class PricingHelper

        Private Sub New()
        End Sub

        ' Change to your store currency (stored on every order)
        Public Const Currency As String = "USD"

        Public Const MaxQuantityPerItem As Integer = 10

        ''' <summary>Variant price if set, else the product sale price if set, else the product price.</summary>
        Public Shared Function GetEffectivePrice(selectedVariant As Models.ProductVariant, parentProduct As Models.Product) As Decimal
            If selectedVariant.Price.HasValue Then
                Return selectedVariant.Price.Value
            End If
            If parentProduct.SalePrice.HasValue Then
                Return parentProduct.SalePrice.Value
            End If
            Return parentProduct.Price
        End Function

        ''' <summary>Flat/free shipping rules go here (for example free above a threshold).</summary>
        Public Shared Function CalculateShippingFee(subtotal As Decimal) As Decimal
            Return 0D
        End Function

    End Class


    ''' <summary>Works out who a cart belongs to: a logged-in customer, or a guest (X-Guest-Token header).</summary>
    Public NotInheritable Class CartOwner

        Public Const GuestHeaderName As String = "X-Guest-Token"

        Public Property CustomerId As Integer?

        Public Property GuestToken As Guid?

        Public ReadOnly Property IsValid As Boolean
            Get
                Return CustomerId.HasValue OrElse GuestToken.HasValue
            End Get
        End Property

        Public Shared Function Resolve(httpRequest As HttpRequestMessage, user As IPrincipal) As CartOwner

            Dim owner As New CartOwner()

            ' A signed-in CUSTOMER always wins. Admin tokens never count as customers.
            If user IsNot Nothing AndAlso
               user.Identity IsNot Nothing AndAlso
               user.Identity.IsAuthenticated AndAlso
               user.IsInRole(JwtHelper.RoleCustomer) Then

                Dim identity = TryCast(user.Identity, ClaimsIdentity)

                If identity IsNot Nothing Then
                    Dim claim = identity.FindFirst(ClaimTypes.NameIdentifier)
                    Dim id As Integer
                    If claim IsNot Nothing AndAlso Integer.TryParse(claim.Value, id) Then
                        owner.CustomerId = id
                    End If
                End If

                Return owner

            End If

            owner.GuestToken = ReadGuestToken(httpRequest)
            Return owner

        End Function

        Public Shared Function ReadGuestToken(httpRequest As HttpRequestMessage) As Guid?

            If httpRequest Is Nothing Then
                Return Nothing
            End If

            Dim values As IEnumerable(Of String) = Nothing

            If Not httpRequest.Headers.TryGetValues(GuestHeaderName, values) Then
                Return Nothing
            End If

            Dim parsed As Guid

            If Guid.TryParse(values.FirstOrDefault(), parsed) AndAlso parsed <> Guid.Empty Then
                Return parsed
            End If

            Return Nothing

        End Function

    End Class


    Public NotInheritable Class ImageLookup

        Private Sub New()
        End Sub

        ''' <summary>Main image URL for each product, using ONE query for all products.</summary>
        Public Shared Function MainImageUrls(db As RetailStoreContext, productIds As List(Of Integer)) As Dictionary(Of Integer, String)

            Dim result As New Dictionary(Of Integer, String)()

            If productIds Is Nothing OrElse productIds.Count = 0 Then
                Return result
            End If

            Dim images = db.ProductImages _
                .Where(Function(i) productIds.Contains(i.ProductId)) _
                .OrderByDescending(Function(i) i.IsMain) _
                .ThenBy(Function(i) i.SortOrder) _
                .ToList()

            For Each image In images
                If Not result.ContainsKey(image.ProductId) Then
                    result(image.ProductId) = image.ImageUrl
                End If
            Next

            Return result

        End Function

    End Class


    Public NotInheritable Class AddressFormatter

        Private Sub New()
        End Sub

        ''' <summary>Multi-line text snapshot stored on the order.</summary>
        Public Shared Function Build(
            fullName As String,
            phone As String,
            addressLine As String,
            city As String,
            area As String,
            building As String,
            floorText As String,
            apartment As String,
            notes As String) As String

            Dim text As String =
                fullName & Environment.NewLine &
                phone & Environment.NewLine &
                addressLine & Environment.NewLine &
                city

            If Not String.IsNullOrWhiteSpace(area) Then
                text &= Environment.NewLine & "Area: " & area
            End If

            If Not String.IsNullOrWhiteSpace(building) Then
                text &= Environment.NewLine & "Building: " & building
            End If

            If Not String.IsNullOrWhiteSpace(floorText) Then
                text &= Environment.NewLine & "Floor: " & floorText
            End If

            If Not String.IsNullOrWhiteSpace(apartment) Then
                text &= Environment.NewLine & "Apartment: " & apartment
            End If

            If Not String.IsNullOrWhiteSpace(notes) Then
                text &= Environment.NewLine & "Address Notes: " & notes
            End If

            Return text

        End Function

    End Class


    Public NotInheritable Class InputValidator

        Private Sub New()
        End Sub

        Private Shared ReadOnly PhonePattern As New Regex("^[0-9+\-\s()]{6,30}$", RegexOptions.Compiled)

        Public Shared Function IsValidPhone(value As String) As Boolean
            Return Not String.IsNullOrWhiteSpace(value) AndAlso PhonePattern.IsMatch(value.Trim())
        End Function

        Public Shared Function IsValidEmail(value As String) As Boolean

            If String.IsNullOrWhiteSpace(value) OrElse value.Length > 255 Then
                Return False
            End If

            Try
                Dim address As New System.Net.Mail.MailAddress(value.Trim())
                Return String.Equals(address.Address, value.Trim(), StringComparison.OrdinalIgnoreCase)
            Catch
                Return False
            End Try

        End Function

        Public Shared Function TooLong(value As String, maxLength As Integer) As Boolean
            Return value IsNot Nothing AndAlso value.Length > maxLength
        End Function

    End Class

End Namespace
