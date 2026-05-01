ARG DOTNET_SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0.202-noble
ARG DOTNET_ASPNET_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0.6-noble

FROM ${DOTNET_SDK_IMAGE} AS build
WORKDIR /src

RUN apt-get update \
    && apt-get install -y --no-install-recommends nodejs npm \
    && rm -rf /var/lib/apt/lists/*

COPY BlazorShop.Presentation/BlazorShop.Storefront/BlazorShop.Storefront.csproj BlazorShop.Presentation/BlazorShop.Storefront/
COPY BlazorShop.Presentation/BlazorShop.Web.Shared/BlazorShop.Web.Shared.csproj BlazorShop.Presentation/BlazorShop.Web.Shared/
COPY BlazorShop.Application/BlazorShop.Application.csproj BlazorShop.Application/
COPY BlazorShop.Domain/BlazorShop.Domain.csproj BlazorShop.Domain/
COPY BlazorShop.ServiceDefaults/BlazorShop.ServiceDefaults.csproj BlazorShop.ServiceDefaults/

RUN dotnet restore BlazorShop.Presentation/BlazorShop.Storefront/BlazorShop.Storefront.csproj

COPY BlazorShop.Presentation/BlazorShop.Web/package.json BlazorShop.Presentation/BlazorShop.Web/package.json
COPY BlazorShop.Presentation/BlazorShop.Web/package-lock.json BlazorShop.Presentation/BlazorShop.Web/package-lock.json

COPY BlazorShop.Presentation/BlazorShop.Storefront/ BlazorShop.Presentation/BlazorShop.Storefront/
COPY BlazorShop.Presentation/BlazorShop.Web/ BlazorShop.Presentation/BlazorShop.Web/
COPY BlazorShop.Presentation/BlazorShop.Web.Shared/ BlazorShop.Presentation/BlazorShop.Web.Shared/
COPY BlazorShop.Application/ BlazorShop.Application/
COPY BlazorShop.Domain/ BlazorShop.Domain/
COPY BlazorShop.ServiceDefaults/ BlazorShop.ServiceDefaults/

WORKDIR /src/BlazorShop.Presentation/BlazorShop.Web

RUN rm -rf node_modules \
    && npm ci \
    && npx tailwindcss -c tailwind.config.js -i ./wwwroot/css/input.css -o ./wwwroot/css/site.css --minify

WORKDIR /src

RUN dotnet publish BlazorShop.Presentation/BlazorShop.Storefront/BlazorShop.Storefront.csproj \
    --configuration Release \
    --output /app/publish \
    /p:UseAppHost=false

FROM ${DOTNET_ASPNET_IMAGE} AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "BlazorShop.Storefront.dll"]
