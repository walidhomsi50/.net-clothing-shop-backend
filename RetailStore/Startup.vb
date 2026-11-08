Imports Microsoft.Owin
Imports Microsoft.Owin.Security.Jwt
Imports Owin
Imports System.Text

<Assembly: OwinStartup(GetType(RetailStore.Startup))>

Namespace RetailStore

    Public Class Startup

        Public Sub Configuration(app As IAppBuilder)

            ConfigureAuth(app)

        End Sub

        Private Sub ConfigureAuth(app As IAppBuilder)

            Dim issuer As String =
                System.Configuration.ConfigurationManager.AppSettings("JwtIssuer")

            Dim audience As String =
                System.Configuration.ConfigurationManager.AppSettings("JwtAudience")

            Dim secret As String =
                System.Configuration.ConfigurationManager.AppSettings("JwtSecret")

            Dim key As Byte() = Encoding.UTF8.GetBytes(secret)

            app.UseJwtBearerAuthentication(
                New JwtBearerAuthenticationOptions With {
                    .AuthenticationMode = Microsoft.Owin.Security.AuthenticationMode.Active,
                                             .IssuerSecurityKeyProviders =
                                New IIssuerSecurityKeyProvider() {
                                    New SymmetricKeyIssuerSecurityKeyProvider(
                                        issuer,
                                        secret
                                    )
                                },
                      .AllowedAudiences = New String() {audience}
                                    })

        End Sub

    End Class

End Namespace