ARG DOTNET_SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0.403-noble@sha256:212fe4cb74627d5adf9817a4088906ae3edff1bd853bb27565d4b46731efe61e
ARG DOTNET_ASPNET_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0.14-noble@sha256:1a48394b6f29d3a32c79d9edecd4abda0ecbe8776d567dbfc4590ed6979c740c

FROM ${DOTNET_SDK_IMAGE} AS build
WORKDIR /src

RUN apt-get update \
    && apt-get -o Acquire::Retries=0 install -y --no-install-recommends nodejs npm \
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

# Service the Noble APT packages for CVE-2026-109518.
RUN apt-get update \
    && apt-get -o Acquire::Retries=0 install -y --only-upgrade --no-install-recommends apt libapt-pkg6.0t64 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "BlazorShop.Storefront.dll"]
