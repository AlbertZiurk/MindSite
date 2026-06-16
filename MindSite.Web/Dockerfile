# ─── BUILD STAGE ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia apenas o .csproj primeiro para aproveitar o cache de camadas do Docker
COPY *.csproj ./
RUN dotnet restore

# Copia o restante do código fonte (garanta que o seu .dockerignore ignore bin/ e obj/)
COPY . .

# CORREÇÃO: Removeu-se o --no-restore para forçar a geração do MindSite.Views.dll
RUN dotnet publish -c Release -o /app/publish -p:UseAppHost=false

# ─── RUNTIME STAGE ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Cria pasta para uploads locais (usada via WSL em desenvolvimento)
RUN mkdir -p /app/wwwroot/uploads && chmod 777 /app/wwwroot/uploads

COPY --from=build /app/publish .

# Azure App Service e contêiner interno expostos na porta padrão 8080
# EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Development

ENTRYPOINT ["dotnet", "MindSite.dll"]