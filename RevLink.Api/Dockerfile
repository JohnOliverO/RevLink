FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY RevLink.Api.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /out --no-restore
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out .
ENV ASPNETCORE_HTTP_PORTS=8080
ENV DatabasePath=/data/revlink.db
EXPOSE 8080
VOLUME ["/data"]
ENTRYPOINT ["dotnet", "RevLink.Api.dll"]
