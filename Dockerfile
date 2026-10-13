# ---------- 1. Frontend (Vue) ----------
FROM node:24-alpine AS frontend
WORKDIR /app
COPY frontend/package*.json ./
RUN npm ci --no-audit --no-fund
COPY frontend/ ./
RUN npm run build

# ---------- 2. Backend (.NET) ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
WORKDIR /src
COPY backend/src/Financas.Api/Financas.Api.csproj src/Financas.Api/
RUN dotnet restore src/Financas.Api/Financas.Api.csproj
COPY backend/src/ src/
RUN dotnet publish src/Financas.Api/Financas.Api.csproj -c Release -o /out --no-restore

# ---------- 3. Imagem final: um processo serve API + site ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    Banco__Caminho=/data/financas.db \
    Backup__Pasta=/backups \
    TZ=America/Sao_Paulo
COPY --from=backend /out ./
COPY --from=frontend /app/dist ./wwwroot
RUN mkdir -p /data /backups && chown -R $APP_UID /data /backups
USER $APP_UID
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 CMD ["dotnet", "Financas.Api.dll", "healthcheck"]
ENTRYPOINT ["dotnet", "Financas.Api.dll"]
