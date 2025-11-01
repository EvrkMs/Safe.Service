# ===== build =====
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# копим слой с зависимостями отдельно для кэша
COPY Safe.Host.csproj ./
RUN dotnet restore

# копируем остальное и публикуем
COPY . .
RUN dotnet publish -c Release -o /out /p:UseAppHost=false

# ===== runtime =====
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /out ./

# порт и биндинг
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 5001

# замените MyProject.dll на реальное имя сборки (обычно как .csproj)
ENTRYPOINT ["dotnet", "Safe.Host.dll"]
