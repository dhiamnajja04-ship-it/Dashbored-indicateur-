# Build depuis le dossier MetierService/ :
#   docker build -t indicateurs/metier:1.0 ./MetierService
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY MetierService.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# La chaîne de connexion PostgreSQL n'est PAS dans l'image : elle est injectée
# à l'exécution via la variable ConnectionStrings__DefaultConnection,
# alimentée par le Secret Kubernetes « postgres-secret ».
USER $APP_UID

ENTRYPOINT ["dotnet", "MetierService.dll"]
