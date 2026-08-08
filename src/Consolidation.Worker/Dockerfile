# Build a partir da raiz do repositório: `docker build -f src/Consolidation.Worker/Dockerfile .`
# Imagem final `runtime`, e não `aspnet`: o worker não expõe HTTP.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props .editorconfig ./
COPY src/Consolidation.Worker/Consolidation.Worker.csproj                 src/Consolidation.Worker/
COPY src/Consolidation.Application/Consolidation.Application.csproj       src/Consolidation.Application/
COPY src/Consolidation.Domain/Consolidation.Domain.csproj                 src/Consolidation.Domain/
COPY src/Consolidation.Infrastructure/Consolidation.Infrastructure.csproj src/Consolidation.Infrastructure/
COPY src/Shared.Contracts/Shared.Contracts.csproj                         src/Shared.Contracts/
RUN dotnet restore src/Consolidation.Worker/Consolidation.Worker.csproj

COPY src/ src/
RUN dotnet publish src/Consolidation.Worker/Consolidation.Worker.csproj \
    --configuration Release --no-restore --output /app

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app .

USER $APP_UID
ENTRYPOINT ["dotnet", "Consolidation.Worker.dll"]
