# Build a partir da raiz do repositório: `docker build -f src/CashFlow.Api/Dockerfile .`
# Multi-stage e usuário não-root, conforme ADR-009.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Os manifestos vêm antes do código: a camada de restore só é invalidada quando
# uma dependência muda, e não a cada alteração de implementação.
COPY global.json Directory.Build.props .editorconfig ./
COPY src/CashFlow.Api/CashFlow.Api.csproj                       src/CashFlow.Api/
COPY src/CashFlow.Application/CashFlow.Application.csproj       src/CashFlow.Application/
COPY src/CashFlow.Domain/CashFlow.Domain.csproj                 src/CashFlow.Domain/
COPY src/CashFlow.Infrastructure/CashFlow.Infrastructure.csproj src/CashFlow.Infrastructure/
COPY src/Shared.Contracts/Shared.Contracts.csproj               src/Shared.Contracts/
RUN dotnet restore src/CashFlow.Api/CashFlow.Api.csproj

COPY src/ src/
RUN dotnet publish src/CashFlow.Api/CashFlow.Api.csproj \
    --configuration Release --no-restore --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .

# A imagem de runtime não traz cliente HTTP algum, e sem um deles o `healthcheck`
# do Compose não tem como perguntar nada ao processo.
RUN apt-get update \
    && apt-get install --no-install-recommends -y curl \
    && rm -rf /var/lib/apt/lists/*

USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "CashFlow.Api.dll"]
