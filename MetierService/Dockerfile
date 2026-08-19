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
# Dossier des documents déposés. Il est créé et attribué à l'utilisateur
# applicatif AVANT le passage en non-root : Docker recopie ces droits dans le
# volume nommé lors de sa première initialisation. Sans cela, le dossier
# appartiendrait à root et l'application — qui ne tourne pas en root — ne
# pourrait rien y écrire.
RUN mkdir -p /donnees/documents && chown -R $APP_UID:$APP_UID /donnees

USER $APP_UID

ENTRYPOINT ["dotnet", "MetierService.dll"]
