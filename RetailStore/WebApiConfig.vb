Imports System.Web.Http
Imports System.Web.Http.Cors
Imports System.Web.Http.ExceptionHandling
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Serialization

' Replaces the default App_Start\WebApiConfig.vb.
' Global.asax.vb should call:  GlobalConfiguration.Configure(AddressOf WebApiConfig.Register)
Public Module WebApiConfig

    Public Sub Register(config As HttpConfiguration)

        ' --- CORS: list only the sites that are allowed to call the API ---
        ' Next.js dev server for now; add your real storefront/admin domains later,
        ' comma separated, e.g. "https://shop.example.com,https://admin.example.com"
        Dim cors As New EnableCorsAttribute(
            origins:="http://localhost:3000",
            headers:="*",
            methods:="*")
        config.EnableCors(cors)

        ' --- Routing: attribute routes only, e.g. <RoutePrefix("api/v1/products")> ---
        config.MapHttpAttributeRoutes()

        ' --- JSON only, camelCase (what React / React Native expect) ---
        Dim json = config.Formatters.JsonFormatter
        json.SerializerSettings.ContractResolver = New CamelCasePropertyNamesContractResolver()
        json.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        json.SerializerSettings.DateTimeZoneHandling = DateTimeZoneHandling.Utc
        config.Formatters.Remove(config.Formatters.XmlFormatter)

        ' --- Consistent error responses ---
        config.Services.Replace(GetType(IExceptionHandler), New ApiExceptionHandler())

    End Sub

End Module
