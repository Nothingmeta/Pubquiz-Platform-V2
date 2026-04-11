# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY ["Pubquiz Platform V2/Pubquiz Platform V2.csproj", "Pubquiz Platform V2/"]
RUN dotnet restore "Pubquiz Platform V2/Pubquiz Platform V2.csproj"

# Copy source code
COPY . .
WORKDIR "/src/Pubquiz Platform V2"
RUN dotnet build "Pubquiz Platform V2.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "Pubquiz Platform V2.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Install required packages
RUN apt-get update && apt-get install -y \
    curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=publish /app/publish .

# Expose port
EXPOSE 8080

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

# Run application
ENTRYPOINT ["dotnet", "Pubquiz Platform V2.dll"]