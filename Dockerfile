# syntax=docker/dockerfile:1.7
# Build multi-stage: Node compila o React, o SDK publica a API e a imagem final só tem o runtime.

# ---------- 1. Front-end ----------
FROM node:22.12.0-alpine AS frontend
WORKDIR /src
COPY src/PortalTarefas.Frontend/package.json src/PortalTarefas.Frontend/package-lock.json ./
RUN npm ci
COPY src/PortalTarefas.Frontend/ ./
RUN npm run build

# ---------- 2. Back-end ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0.100 AS build
WORKDIR /src
COPY src/PortalTarefas.Web/PortalTarefas.Web.csproj src/PortalTarefas.Web/
RUN dotnet restore src/PortalTarefas.Web/PortalTarefas.Web.csproj
COPY src/PortalTarefas.Web/ src/PortalTarefas.Web/
COPY --from=frontend /src/dist/ src/PortalTarefas.Web/wwwroot/app/
RUN dotnet publish src/PortalTarefas.Web/PortalTarefas.Web.csproj -c Release -o /app/publish \
    --no-restore /p:UseAppHost=false /p:OpenApiGenerateDocuments=false

# ---------- 3. Execução ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true
COPY --from=build /app/publish .
# Usuário não privilegiado já existente nas imagens oficiais do .NET
USER $APP_UID
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD ["dotnet", "PortalTarefas.Web.dll", "--healthcheck"]
ENTRYPOINT ["dotnet", "PortalTarefas.Web.dll"]
# Nenhum segredo aqui: connection string e senhas chegam por variável de ambiente na execução.
