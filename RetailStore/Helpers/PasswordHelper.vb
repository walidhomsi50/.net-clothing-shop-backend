Imports System.Runtime.CompilerServices
Imports System.Security.Cryptography

Namespace Helpers

    ''' <summary>
    ''' PBKDF2-HMAC-SHA256 password hashing.
    ''' New format:    iterations:salt:hash   (all base64 except the number)
    ''' Legacy format: salt:hash              (100,000 iterations) - still verifies,
    '''                and NeedsRehash() reports True so you can upgrade it at login.
    ''' </summary>
    Public Class PasswordHelper

        Private Const CurrentIterations As Integer = 600000
        Private Const LegacyIterations As Integer = 100000
        Private Const SaltSize As Integer = 16
        Private Const HashSize As Integer = 32

        Public Shared Function HashPassword(password As String) As String

            Dim salt(SaltSize - 1) As Byte

            Using rng = RandomNumberGenerator.Create()
                rng.GetBytes(salt)
            End Using

            Dim hash = Derive(password, salt, CurrentIterations)

            Return CurrentIterations.ToString() & ":" &
                   Convert.ToBase64String(salt) & ":" &
                   Convert.ToBase64String(hash)

        End Function


        Public Shared Function VerifyPassword(password As String, storedHash As String) As Boolean

            If String.IsNullOrEmpty(password) OrElse String.IsNullOrWhiteSpace(storedHash) Then
                Return False
            End If

            Dim iterations As Integer
            Dim salt As Byte() = Nothing
            Dim expected As Byte() = Nothing

            If Not TryParse(storedHash, iterations, salt, expected) Then
                Return False
            End If

            Dim actual = Derive(password, salt, iterations)

            Return ConstantTimeEquals(actual, expected)

        End Function


        ''' <summary>True when the stored hash uses fewer iterations than today's setting.
        ''' After a SUCCESSFUL login, re-hash the password you just verified and save it.</summary>
        Public Shared Function NeedsRehash(storedHash As String) As Boolean

            Dim iterations As Integer
            Dim salt As Byte() = Nothing
            Dim expected As Byte() = Nothing

            If String.IsNullOrWhiteSpace(storedHash) OrElse
               Not TryParse(storedHash, iterations, salt, expected) Then
                Return False
            End If

            Return iterations < CurrentIterations

        End Function


        Private Shared Function Derive(password As String, salt As Byte(), iterations As Integer) As Byte()
            Using pbkdf2 As New Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256)
                Return pbkdf2.GetBytes(HashSize)
            End Using
        End Function


        Private Shared Function TryParse(
            storedHash As String,
            ByRef iterations As Integer,
            ByRef salt As Byte(),
            ByRef hash As Byte()) As Boolean

            Try
                Dim parts = storedHash.Split(":"c)

                If parts.Length = 2 Then
                    iterations = LegacyIterations
                    salt = Convert.FromBase64String(parts(0))
                    hash = Convert.FromBase64String(parts(1))

                ElseIf parts.Length = 3 Then
                    If Not Integer.TryParse(parts(0), iterations) OrElse iterations < 1 Then
                        Return False
                    End If
                    salt = Convert.FromBase64String(parts(1))
                    hash = Convert.FromBase64String(parts(2))

                Else
                    Return False
                End If

                Return salt.Length > 0 AndAlso hash.Length > 0

            Catch ex As FormatException
                Return False
            End Try

        End Function


        ''' <summary>Compares every byte instead of stopping at the first difference,
        ''' so response time doesn't reveal how much of a hash matched.</summary>
        <MethodImpl(MethodImplOptions.NoInlining Or MethodImplOptions.NoOptimization)>
        Private Shared Function ConstantTimeEquals(a As Byte(), b As Byte()) As Boolean

            Dim diff As Integer = a.Length Xor b.Length
            Dim count As Integer = Math.Min(a.Length, b.Length)

            For i As Integer = 0 To count - 1
                diff = diff Or (a(i) Xor b(i))
            Next

            Return diff = 0

        End Function

    End Class

End Namespace