ARG DOTNET_SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0.202-noble
ARG DOTNET_ASPNET_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0.6-noble

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

COPY --from=build /app/publish .

RUN mkdir -p /app/uploads

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "BlazorShop.API.dll"]