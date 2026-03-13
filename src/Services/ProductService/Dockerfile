# ========================
# Build stage
# ========================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copiar csproj y restaurar dependencias
COPY ProductService/ProductService.csproj ProductService/
COPY ProductService/Protos/ ProductService/Protos/
RUN dotnet restore ProductService/ProductService.csproj

# Copiar todo el código y compilar
COPY ProductService/ ProductService/
RUN dotnet publish ProductService/ProductService.csproj -c Release -o /app/publish --no-restore

# ========================
# Runtime stage
# ========================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Crear usuario no-root para seguridad
RUN adduser --disabled-password --gecos "" appuser

COPY --from=build /app/publish .

# Puertos: 5001 (REST), 5002 (gRPC)
EXPOSE 5001 5002

ENV ASPNETCORE_ENVIRONMENT=Production

USER appuser

ENTRYPOINT ["dotnet", "ProductService.dll"]
