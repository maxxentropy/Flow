# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files
COPY ["src/McpServer.Domain/McpServer.Domain.csproj", "src/McpServer.Domain/"]
COPY ["src/McpServer.Application/McpServer.Application.csproj", "src/McpServer.Application/"]
COPY ["src/McpServer.Infrastructure/McpServer.Infrastructure.csproj", "src/McpServer.Infrastructure/"]
COPY ["src/McpServer.Abstractions/McpServer.Abstractions.csproj", "src/McpServer.Abstractions/"]
COPY ["src/McpServer.Web/McpServer.Web.csproj", "src/McpServer.Web/"]
COPY ["src/McpServer.Console/McpServer.Console.csproj", "src/McpServer.Console/"]

# Restore dependencies
RUN dotnet restore "src/McpServer.Web/McpServer.Web.csproj"
RUN dotnet restore "src/McpServer.Console/McpServer.Console.csproj"

# Copy everything else
COPY . .

# Build projects
RUN dotnet publish "src/McpServer.Web/McpServer.Web.csproj" -c Release -o /app/web --no-restore
RUN dotnet publish "src/McpServer.Console/McpServer.Console.csproj" -c Release -o /app/console --no-restore

# Runtime stage - Web
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS web
WORKDIR /app
COPY --from=build /app/web .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "McpServer.Web.dll"]

# Runtime stage - Console
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS console
WORKDIR /app
COPY --from=build /app/console .
ENTRYPOINT ["dotnet", "McpServer.Console.dll"]