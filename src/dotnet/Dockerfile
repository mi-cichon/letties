FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["WebGame.Api/WebGame.Api.csproj", "WebGame.Api/"]
RUN dotnet restore "WebGame.Api/WebGame.Api.csproj"

COPY . .
RUN dotnet publish "WebGame.Api/WebGame.Api.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "WebGame.Api.dll"]