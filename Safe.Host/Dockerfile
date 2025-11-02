# ===== build =====
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Safe.Service/nuget.config ./nuget.config
COPY Safe.Service/Safe.Host/Safe.Host.csproj Safe.Host/
COPY Safe.Service/Safe.Application/Safe.Application.csproj Safe.Application/
COPY Safe.Service/Safe.Domain/Safe.Domain.csproj Safe.Domain/
COPY Safe.Service/Safe.EntityFramework/Safe.EntityFramework.csproj Safe.EntityFramework/

RUN dotnet restore Safe.Host/Safe.Host.csproj

COPY Safe.Service/Safe.Host/ Safe.Host/
COPY Safe.Service/Safe.Application/ Safe.Application/
COPY Safe.Service/Safe.Domain/ Safe.Domain/
COPY Safe.Service/Safe.EntityFramework/ Safe.EntityFramework/

WORKDIR /src/Safe.Host
RUN dotnet publish Safe.Host.csproj -c Release -o /app/publish /p:UseAppHost=false

# ===== runtime =====
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

EXPOSE 5001

ENTRYPOINT ["dotnet", "Safe.Host.dll"]
