Imports System.Configuration
Imports RetailStore.Data

Namespace Helpers

    ''' <summary>
    ''' Creates the first admin account when the Admins table is empty, using two web.config
    ''' settings (SeedAdminEmail, SeedAdminPassword). After the first run, DELETE both settings.
    ''' Call once from Application_Start in Global.asax.vb:  AdminSeeder.EnsureFirstAdmin()
    ''' </summary>
    Public NotInheritable Class AdminSeeder

        Private Sub New()
        End Sub

        Public Shared Sub EnsureFirstAdmin()

            Dim email = ConfigurationManager.AppSettings("SeedAdminEmail")
            Dim password = ConfigurationManager.AppSettings("SeedAdminPassword")

            If String.IsNullOrWhiteSpace(email) OrElse String.IsNullOrWhiteSpace(password) Then
                Return
            End If

            Using db As New RetailStoreContext()

                If db.Admins.Any() Then
                    Return
                End If

                db.Admins.Add(New Models.Admin With {
                    .FullName = "Store Admin",
                    .Email = email.Trim().ToLowerInvariant(),
                    .PasswordHash = PasswordHelper.HashPassword(password),
                    .Role = JwtHelper.RoleAdmin,
                    .IsActive = True,
                    .CreatedDate = DateTime.UtcNow
                })

                db.SaveChanges()

            End Using

        End Sub

    End Class

End Namespace
