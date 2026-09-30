# Build & publish
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY RepairShop.sln ./
COPY src ./src
COPY tests ./tests
COPY docs ./docs

RUN dotnet restore RepairShop.sln
RUN dotnet publish src/RepairShop.Api/RepairShop.Api.csproj -c Release -o /app/publish --no-restore

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Needed for container healthchecks (curl)
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "RepairShop.Api.dll"]
