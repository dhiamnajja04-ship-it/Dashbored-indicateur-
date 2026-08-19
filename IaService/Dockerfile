# Build depuis le dossier IaService/ :
#   docker build -t indicateurs/ia:1.0 ./IaService
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY IaService.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Le modèle n'est pas embarqué dans cette image : il tourne dans le pod « ollama »
# (image officielle + volume persistant). Ce conteneur reste donc léger.
USER $APP_UID

ENTRYPOINT ["dotnet", "IaService.dll"]
