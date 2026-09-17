# Construction puis exécution, en deux étages : l'image finale ne contient que
# le runtime ASP.NET et l'application publiée, pas le SDK.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS construction
WORKDIR /src
COPY Api/Api.csproj Api/
RUN dotnet restore Api/Api.csproj
COPY Api/ Api/
RUN dotnet publish Api/Api.csproj -c Release -o /publie --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=construction /publie .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
# L'image fournit un utilisateur sans privilèges : l'API ne tourne pas en root.
USER app
ENTRYPOINT ["dotnet", "Api.dll"]
