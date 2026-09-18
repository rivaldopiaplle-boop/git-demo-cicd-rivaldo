# Construction puis exécution, en deux étages : l'image finale ne contient que
# le runtime ASP.NET et l'application publiée, pas le SDK.
#
# Le contexte de construction est la racine du dépôt, car l'application embarque
# mysql/schema.sql : c'est lui qui prépare la base au démarrage chez l'hébergeur.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS construction
WORKDIR /src
COPY backend/Api/Api.csproj backend/Api/
RUN dotnet restore backend/Api/Api.csproj
COPY backend/Api/ backend/Api/
COPY mysql/schema.sql mysql/schema.sql
RUN dotnet publish backend/Api/Api.csproj -c Release -o /publie --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=construction /publie .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
# L'image fournit un utilisateur sans privilèges : l'API ne tourne pas en root.
USER app
ENTRYPOINT ["dotnet", "Api.dll"]
