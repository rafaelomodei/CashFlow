# Build a partir da raiz do repositório: `docker build -f src/Consolidation.Api/Dockerfile .`
# Multi-stage e usuário não-root, conforme ADR-009.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props .editorconfig ./
COPY src/Consolidation.Api/Consolidation.Api.csproj                       src/Consolidation.Api/
COPY src/Consolidation.Application/Consolidation.Application.csproj       src/Consolidation.Application/
COPY src/Consolidation.Domain/Consolidation.Domain.csproj                 src/Consolidation.Domain/
COPY src/Consolidation.Infrastructure/Consolidation.Infrastructure.csproj src/Consolidation.Infrastructure/
COPY src/Shared.Contracts/Shared.Contracts.csproj                         src/Shared.Contracts/
RUN dotnet restore src/Consolidation.Api/Consolidation.Api.csproj

COPY src/ src/
RUN dotnet publish src/Consolidation.Api/Consolidation.Api.csproj \
    --configuration Release --no-restore --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .

USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Consolidation.Api.dll"]
