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

COPY --from=build /app/publish .

# CRITICAL: Create data directory with proper permissions for SQLite
RUN mkdir -p /app/data && \
    chmod 777 /app/data && \
    touch /app/data/.gitkeep

# Expose port (Render uses 8080 by default)
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "Pubquiz Platform V2.dll"]
