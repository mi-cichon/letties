FROM node:20 AS angular-build
WORKDIR /app/angular
COPY src/angular/package*.json ./
RUN npm install

RUN chmod -R +x node_modules/.bin

COPY src/angular/ .
RUN chmod -R +x node_modules/.bin

RUN npm run build -- --configuration production

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dotnet-build
WORKDIR /src

COPY ["src/dotnet/WebGame.Api/WebGame.Api.csproj", "WebGame.Api/"]
COPY ["src/dotnet/WebGame.Application/WebGame.Application.csproj", "WebGame.Application/"]
COPY ["src/dotnet/WebGame.Domain/WebGame.Domain.csproj", "WebGame.Domain/"]
RUN dotnet restore "WebGame.Api/WebGame.Api.csproj"

COPY src/dotnet/ .
RUN dotnet publish "WebGame.Api/WebGame.Api.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=dotnet-build /app/publish .

COPY --from=angular-build /app/angular/dist/game/browser ./wwwroot

EXPOSE 8080
ENTRYPOINT ["dotnet", "WebGame.Api.dll"]