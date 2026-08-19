# Build depuis le dossier GatewayService/ :
#   docker build -t indicateurs/gateway:1.0 ./GatewayService
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restauration séparée : le cache Docker n'est invalidé que si le .csproj change.
COPY GatewayService.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

# Port interne exposé dans le cluster (Service k8s « gateway »).
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Ne jamais tourner en root dans le cluster.
USER $APP_UID

ENTRYPOINT ["dotnet", "GatewayService.dll"]
