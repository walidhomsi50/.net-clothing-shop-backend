Namespace DTOs

    Public Class CreateCustomerAddressRequest

        Public Property Title As String

        Public Property FullName As String

        Public Property Phone As String

        Public Property AddressLine As String

        Public Property City As String

        Public Property Area As String

        Public Property Building As String

        Public Property Floor As String

        Public Property Apartment As String

        Public Property Notes As String

        Public Property IsDefault As Boolean

    End Class


    Public Class UpdateCustomerAddressRequest

        Public Property Title As String

        Public Property FullName As String

        Public Property Phone As String

        Public Property AddressLine As String

        Public Property City As String

        Public Property Area As String

        Public Property Building As String

        Public Property Floor As String

        Public Property Apartment As String

        Public Property Notes As String

        Public Property IsDefault As Boolean

    End Class


    Public Class CustomerAddressDto

        Public Property CustomerAddressId As Integer

        Public Property Title As String

        Public Property FullName As String

        Public Property Phone As String

        Public Property AddressLine As String

        Public Property City As String

        Public Property Area As String

        Public Property Building As String

        Public Property Floor As String

        Public Property Apartment As String

        Public Property Notes As String

        Public Property IsDefault As Boolean

        Public Property CreatedDate As DateTime

        Public Property UpdatedDate As DateTime?

    End Class

End Namespace