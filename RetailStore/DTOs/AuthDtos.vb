' This file contains ALL auth DTOs, including the three your original AuthController used
' (RegisterRequest, LoginRequest, LoginResponse). If you restore an old file that also
' declares them, delete the duplicates from one of the two files.
Namespace DTOs

    Public Class RegisterRequest
        Public Property FirstName As String
        Public Property LastName As String
        Public Property Email As String
        Public Property Phone As String
        Public Property Password As String
    End Class

    Public Class LoginRequest
        Public Property Email As String
        Public Property Password As String
    End Class

    ''' <summary>Old login response, kept in case other code still references it.</summary>
    Public Class LoginResponse
        Public Property CustomerId As Integer
        Public Property FirstName As String
        Public Property LastName As String
        Public Property Email As String
        Public Property Token As String
    End Class

    Public Class RefreshRequest
        Public Property RefreshToken As String
    End Class

    ''' <summary>Returned by register, login, admin login and refresh.</summary>
    Public Class AuthResponse
        Public Property UserId As Integer
        Public Property DisplayName As String
        Public Property Email As String
        Public Property Role As String
        Public Property AccessToken As String
        Public Property RefreshToken As String
        Public Property ExpiresInSeconds As Integer
    End Class

End Namespace
