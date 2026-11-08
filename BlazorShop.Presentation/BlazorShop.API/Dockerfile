ARG DOTNET_SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0.403-noble@sha256:212fe4cb74627d5adf9817a4088906ae3edff1bd853bb27565d4b46731efe61e
ARG DOTNET_ASPNET_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0.14-noble@sha256:1a48394b6f29d3a32c79d9edecd4abda0ecbe8776d567dbfc4590ed6979c740c

FROM ${DOTNET_SDK_IMAGE} AS build
WORKDIR /src

COPY BlazorShop.Presentation/BlazorShop.API/BlazorShop.API.csproj BlazorShop.Presentation/BlazorShop.API/
COPY BlazorShop.Infrastructure/BlazorShop.Infrastructure.csproj BlazorShop.Infrastructure/
COPY BlazorShop.Application/BlazorShop.Application.csproj BlazorShop.Application/
COPY BlazorShop.Domain/BlazorShop.Domain.csproj BlazorShop.Domain/
COPY BlazorShop.ServiceDefaults/BlazorShop.ServiceDefaults.csproj BlazorShop.ServiceDefaults/

RUN dotnet restore BlazorShop.Presentation/BlazorShop.API/BlazorShop.API.csproj

COPY BlazorShop.Presentation/BlazorShop.API/ BlazorShop.Presentation/BlazorShop.API/
COPY BlazorShop.Infrastructure/ BlazorShop.Infrastructure/
COPY BlazorShop.Application/ BlazorShop.Application/
COPY BlazorShop.Domain/ BlazorShop.Domain/
COPY BlazorShop.ServiceDefaults/ BlazorShop.ServiceDefaults/

RUN dotnet publish BlazorShop.Presentation/BlazorShop.API/BlazorShop.API.csproj \
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

RUN mkdir -p /app/uploads

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "BlazorShop.API.dll"]
