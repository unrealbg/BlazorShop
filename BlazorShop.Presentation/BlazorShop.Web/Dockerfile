FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

RUN apt-get update \
    && apt-get install -y --no-install-recommends nodejs npm \
    && rm -rf /var/lib/apt/lists/*

COPY BlazorShop.Presentation/BlazorShop.Web/BlazorShop.Web.csproj BlazorShop.Presentation/BlazorShop.Web/
COPY BlazorShop.Presentation/BlazorShop.Web.Shared/BlazorShop.Web.Shared.csproj BlazorShop.Presentation/BlazorShop.Web.Shared/

RUN dotnet restore BlazorShop.Presentation/BlazorShop.Web/BlazorShop.Web.csproj

COPY BlazorShop.Presentation/BlazorShop.Web/package.json BlazorShop.Presentation/BlazorShop.Web/package.json
COPY BlazorShop.Presentation/BlazorShop.Web/package-lock.json BlazorShop.Presentation/BlazorShop.Web/package-lock.json

RUN npm ci --prefix BlazorShop.Presentation/BlazorShop.Web

COPY BlazorShop.Presentation/BlazorShop.Web.Shared/ BlazorShop.Presentation/BlazorShop.Web.Shared/
COPY BlazorShop.Presentation/BlazorShop.Web/ BlazorShop.Presentation/BlazorShop.Web/

RUN dotnet publish BlazorShop.Presentation/BlazorShop.Web/BlazorShop.Web.csproj \
    --configuration Release \
    --output /app/publish \
    /p:UseAppHost=false

FROM nginx:1.27-alpine AS final

COPY BlazorShop.Presentation/BlazorShop.Web/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html

EXPOSE 80