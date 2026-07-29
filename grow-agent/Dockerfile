# syntax=docker/dockerfile:1

# Wie beim Hauptprogramm: uebersetzt wird einmal auf dem Bau-Rechner
# (`UseAppHost=false` liefert CPU-unabhaengiges IL), nur die Laufzeitschicht ist
# je Zielarchitektur. Das haelt den Mehrfach-Bau schnell, ohne QEMU.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS publish
WORKDIR /src

COPY GrowAgent/GrowAgent.csproj GrowAgent/
RUN dotnet restore GrowAgent/GrowAgent.csproj

COPY GrowAgent/ GrowAgent/
RUN dotnet publish GrowAgent/GrowAgent.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=publish /app/publish .

# Auf allen Schnittstellen im Container lauschen. Nach draussen ist trotzdem
# nichts offen: das Add-on veroeffentlicht keinen Port, erreichbar ist es nur
# ueber den Ingress von Home Assistant und aus dem internen Add-on-Netz.
ENV ASPNETCORE_URLS=http://0.0.0.0:5077
ENV DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 5077
ENTRYPOINT ["dotnet", "GrowAgent.dll"]
