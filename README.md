# FHIR Terminology Service Liveness Monitor

This project contains a simple C# application, which can be installed as a service, that periodically pings a URL and restarts a Windows Service if the request fails.


# Documentation

Configuration is done via the `appsettings.json` file.

The application can be run manually or installed as a service.
* If running as a console application, the application requires administrative privileges in order to stop or start services.
* To install as a service (named terminology-service-liveness-monitor)
  * `sc create terminology-service-liveness-monitor "binPath=<path to executable>\terminology-service-liveness-monitor.exe" start=demand`

## More Information

### Requirements

- .Net 5.0 or later ([dotnet.microsoft.com](https://dotnet.microsoft.com/download))

### Building

Releases are made available periodically.  If you want to build from source, simply clone the repository and run:
- `dotnet build .\src\terminology-service-liveness-monitor.csproj -c Release`


FHIR&reg; is the registered trademark of HL7 and is used with the permission of HL7. 