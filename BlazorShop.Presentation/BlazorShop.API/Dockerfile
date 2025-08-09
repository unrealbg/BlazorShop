# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build-env
WORKDIR /app

# Copy solution and project files
COPY *.sln .
COPY Directory.Build.props .
COPY Directory.Packages.props .

# Copy all project files
COPY BlazorShop.Domain/*.csproj BlazorShop.Domain/
COPY BlazorShop.Application/*.csproj BlazorShop.Application/
COPY BlazorShop.Infrastructure/*.csproj BlazorShop.Infrastructure/
COPY BlazorShop.ServiceDefaults/*.csproj BlazorShop.ServiceDefaults/
COPY BlazorShop.Presentation/BlazorShop.API/*.csproj BlazorShop.Presentation/BlazorShop.API/
COPY BlazorShop.Presentation/BlazorShop.Web.Shared/*.csproj BlazorShop.Presentation/BlazorShop.Web.Shared/
COPY BlazorShop.Tests/*.csproj BlazorShop.Tests/

# Restore dependencies
RUN dotnet restore BlazorShop.Presentation/BlazorShop.API/BlazorShop.API.csproj

# Copy all source code
COPY . .

# Build the application
WORKDIR /app/BlazorShop.Presentation/BlazorShop.API
RUN dotnet publish -c Release -o out --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Create non-root user for security
RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

# Copy the published app
COPY --from=build-env /app/BlazorShop.Presentation/BlazorShop.API/out .

# Create logs directory
RUN mkdir -p logs

# Expose port
EXPOSE 8080

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:8080/health/live || exit 1

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

# Entry point
ENTRYPOINT ["dotnet", "BlazorShop.API.dll"]