FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /app

COPY *.sln .
COPY . .
WORKDIR /app/src/Api
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS runtime
WORKDIR /app
COPY --from=build /app/src/Api/out ./

# Optional: Set this here if not setting it from docker-compose.yml

ENTRYPOINT ["dotnet", "Api.dll"]