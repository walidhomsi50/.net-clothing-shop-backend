Imports System.Data.Entity
Imports System.Data.Entity.Infrastructure
Imports System.Threading
Imports System.Threading.Tasks

' Replaces the previous RetailStoreContext.Audit.vb (same location: the Data folder).
'
' New in this version: a static constructor that switches EF's database initializer OFF.
' Without it, if the connection string ever points at a database that doesn't exist, EF
' silently creates a new empty one from your classes instead of failing loudly.
'
' On every save it also stamps dates:
'   - new rows: CreatedDate / OrderDate left at 0001-01-01 become the current UTC time
'   - changed rows: UpdatedDate (where the table has one) becomes the current UTC time
'
' Variables are not called "entry" because DbContext has a method named Entry and VB is
' case-insensitive.
Namespace Data

    Partial Public Class RetailStoreContext

        Shared Sub New()
            System.Data.Entity.Database.SetInitializer(Of RetailStoreContext)(Nothing)
        End Sub

        Public Overrides Function SaveChanges() As Integer
            StampAuditDates()
            Return MyBase.SaveChanges()
        End Function

        Public Overrides Function SaveChangesAsync(cancellationToken As CancellationToken) As Task(Of Integer)
            StampAuditDates()
            Return MyBase.SaveChangesAsync(cancellationToken)
        End Function

        Private Sub StampAuditDates()

            Dim utcNow As DateTime = DateTime.UtcNow

            For Each tracked As DbEntityEntry In ChangeTracker.Entries()

                If tracked.State = EntityState.Added Then

                    SetIfEmpty(tracked, "CreatedDate", utcNow)
                    SetIfEmpty(tracked, "OrderDate", utcNow)

                ElseIf tracked.State = EntityState.Modified Then

                    If tracked.CurrentValues.PropertyNames.Contains("UpdatedDate") Then
                        tracked.Property("UpdatedDate").CurrentValue = utcNow
                    End If

                End If

            Next

        End Sub

        Private Shared Sub SetIfEmpty(target As DbEntityEntry, propertyName As String, value As DateTime)

            If Not target.CurrentValues.PropertyNames.Contains(propertyName) Then
                Return
            End If

            Dim current As Object = target.Property(propertyName).CurrentValue

            If TypeOf current Is DateTime AndAlso CType(current, DateTime) = DateTime.MinValue Then
                target.Property(propertyName).CurrentValue = value
            End If

        End Sub

    End Class

End Namespace
