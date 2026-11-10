ARG DOTNET_SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0.403-noble@sha256:212fe4cb74627d5adf9817a4088906ae3edff1bd853bb27565d4b46731efe61e
ARG NGINX_IMAGE=nginx:1.30.6-alpine@sha256:583b7f2fce1cdb8da5ad5c39ebd3c33b98b87d493190e548bdee1fa8c619c311

FROM ${DOTNET_SDK_IMAGE} AS build
WORKDIR /src

RUN apt-get update \
    && apt-get -o Acquire::Retries=0 install -y --no-install-recommends nodejs npm \
    && rm -rf /var/lib/apt/lists/*

COPY BlazorShop.Presentation/BlazorShop.Web/BlazorShop.Web.csproj BlazorShop.Presentation/BlazorShop.Web/
COPY BlazorShop.Presentation/BlazorShop.Web.Shared/BlazorShop.Web.Shared.csproj BlazorShop.Presentation/BlazorShop.Web.Shared/
COPY BlazorShop.Domain/BlazorShop.Domain.csproj BlazorShop.Domain/

RUN dotnet restore BlazorShop.Presentation/BlazorShop.Web/BlazorShop.Web.csproj

COPY BlazorShop.Presentation/BlazorShop.Web/package.json BlazorShop.Presentation/BlazorShop.Web/package.json
COPY BlazorShop.Presentation/BlazorShop.Web/package-lock.json BlazorShop.Presentation/BlazorShop.Web/package-lock.json

RUN npm ci --prefix BlazorShop.Presentation/BlazorShop.Web

COPY BlazorShop.Presentation/BlazorShop.Web.Shared/ BlazorShop.Presentation/BlazorShop.Web.Shared/
COPY BlazorShop.Presentation/BlazorShop.Web/ BlazorShop.Presentation/BlazorShop.Web/
COPY BlazorShop.Domain/ BlazorShop.Domain/

RUN dotnet publish BlazorShop.Presentation/BlazorShop.Web/BlazorShop.Web.csproj \
    --configuration Release \
    --output /app/publish \
    /p:UseAppHost=false

FROM ${NGINX_IMAGE} AS final

COPY BlazorShop.Presentation/BlazorShop.Web/nginx.conf /etc/nginx/templates/default.conf.template
COPY BlazorShop.Presentation/BlazorShop.Web/nginx-security-headers.conf /etc/nginx/snippets/blazorshop-security-headers.conf
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html

EXPOSE 80
