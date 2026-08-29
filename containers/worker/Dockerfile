FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /project

# docker-compose sets build context to the repo root (Catalog.API/).
# Keep the same on-disk layout inside the container so ProjectReference paths resolve.
COPY . .

RUN dotnet build "Catalog.Worker/Catalog.Worker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Catalog.Worker/Catalog.Worker.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Catalog.Worker.dll"]
