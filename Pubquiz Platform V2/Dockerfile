# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Pubquiz Platform V2.csproj", "./"]
RUN dotnet restore "Pubquiz Platform V2.csproj"
COPY . .
RUN dotnet build "Pubquiz Platform V2.csproj" -c Release -o /app/build
RUN dotnet publish "Pubquiz Platform V2.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

# CRITICAL: Create data directory for SQLite database
RUN mkdir -p /app/data && chmod 755 /app/data

COPY --from=build /app/publish .

# Expose both 5000 and 8080 (Render uses 8080)
EXPOSE 5000 8080

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health || curl -f http://localhost:5000/health || exit 1

ENTRYPOINT ["dotnet", "Pubquiz Platform V2.dll"]
