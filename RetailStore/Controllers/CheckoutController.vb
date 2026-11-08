Imports System.Collections.Generic
Imports System.Configuration
Imports System.Web.Http
Imports RetailStore.DTOs
Imports RetailStore.Helpers

Namespace DTOs

    Public Class PaymentMethodDto

        ' The value to send as "paymentMethod" when placing an order
        Public Property Code As String

        Public Property Name As String

        Public Property Enabled As Boolean

        ' Shown next to a disabled method, e.g. "Coming soon"
        Public Property Message As String

    End Class

End Namespace


''' <summary>
''' Tells the storefront which payment methods it can offer. Public (no login needed).
''' Card stays "coming soon" until the gateway is integrated: the switch is the
''' CardPaymentsEnabled key in web.config, which must stay off (or missing) until then.
''' </summary>
<RoutePrefix("api/v1/checkout")>
Public Class CheckoutController
    Inherits ApiController

    ' =========================================================
    ' GET api/v1/checkout/payment-methods
    ' =========================================================
    <HttpGet>
    <Route("payment-methods")>
    Public Function GetPaymentMethods() As IHttpActionResult

        Dim cardEnabled As Boolean = String.Equals(
            ConfigurationManager.AppSettings("CardPaymentsEnabled"),
            "true",
            StringComparison.OrdinalIgnoreCase)

        Dim methods As New List(Of PaymentMethodDto)()

        methods.Add(New PaymentMethodDto With {
            .Code = PaymentMethods.COD,
            .Name = "Cash on delivery",
            .Enabled = True
        })

        Dim card As New PaymentMethodDto With {
            .Code = PaymentMethods.Card,
            .Name = "Card payment",
            .Enabled = cardEnabled
        }

        If Not cardEnabled Then
            card.Message = "Coming soon"
        End If

        methods.Add(card)

        Return Ok(methods)

    End Function

End Class
