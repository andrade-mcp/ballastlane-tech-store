FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY NuGet.config global.json ./
COPY src/BallastlaneTechStore.Domain/         src/BallastlaneTechStore.Domain/
COPY src/BallastlaneTechStore.Application/    src/BallastlaneTechStore.Application/
COPY src/BallastlaneTechStore.Infrastructure/ src/BallastlaneTechStore.Infrastructure/
COPY src/BallastlaneTechStore.Store.Api/      src/BallastlaneTechStore.Store.Api/
RUN dotnet restore src/BallastlaneTechStore.Store.Api/BallastlaneTechStore.Store.Api.csproj
RUN dotnet publish src/BallastlaneTechStore.Store.Api/BallastlaneTechStore.Store.Api.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "BallastlaneTechStore.Store.Api.dll"]
