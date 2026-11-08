Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Web.Http

Public Module WebApiConfig
    Public Sub Register(ByVal config As HttpConfiguration)
        ' Web API configuration and services

        Dim json = config.Formatters.JsonFormatter
        json.SerializerSettings.ContractResolver = New Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver()
        json.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore
        config.Formatters.Remove(config.Formatters.XmlFormatter)

        ' Web API routes
        config.MapHttpAttributeRoutes()

        config.Routes.MapHttpRoute(
            name:="DefaultApi",
            routeTemplate:="api/{controller}/{id}",
            defaults:=New With {.id = RouteParameter.Optional}
        )
        ' Return JSON by default
        config.Formatters.Remove(config.Formatters.XmlFormatter)
    End Sub
End Module
