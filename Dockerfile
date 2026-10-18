# Multi-stage Dockerfile for VSR Sales Engine .NET 10 LTS WebApi
# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files for dependency caching
COPY ["src/VsrSalesEngineWebApi/VsrSalesEngineWebApi.csproj", "src/VsrSalesEngineWebApi/"]
COPY ["src/VsrSalesEngineWebApi.Core/VsrSalesEngineWebApi.Core.csproj", "src/VsrSalesEngineWebApi.Core/"]
COPY ["src/VsrSalesEngineWebApi.Infrastructure/VsrSalesEngineWebApi.Infrastructure.csproj", "src/VsrSalesEngineWebApi.Infrastructure/"]
COPY ["tests/VsrSalesEngineWebApi.Tests/VsrSalesEngineWebApi.Tests.csproj", "tests/VsrSalesEngineWebApi.Tests/"]
COPY ["VsrSalesEngineWebApi.sln", "./"]

RUN dotnet restore "src/VsrSalesEngineWebApi/VsrSalesEngineWebApi.csproj"

# Copy all source files and publish release
COPY . .
WORKDIR "/src/src/VsrSalesEngineWebApi"
RUN dotnet publish "VsrSalesEngineWebApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Port and environment configuration
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "VsrSalesEngineWebApi.dll"]

