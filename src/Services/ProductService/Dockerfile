# ========================
# Build stage
# ========================
# BUILDPLATFORM = arquitectura nativa del host (evita emulación QEMU en Apple Silicon)
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copiar csproj y restaurar dependencias
# Nota: SkipGrpcCodeGen=true desactiva Grpc.Tools (incompatible con .NET 10 SDK en Docker/ARM64)
# Se usan archivos C# pre-generados desde Grpc/Generated/ en su lugar.
COPY ProductService/ProductService.csproj ProductService/
RUN dotnet restore ProductService/ProductService.csproj /p:SkipGrpcCodeGen=true

# Copiar todo el código (incluye Grpc/Generated/ con protobuf pre-generado) y compilar
COPY ProductService/ ProductService/
RUN dotnet publish ProductService/ProductService.csproj -c Release -o /app/publish /p:SkipGrpcCodeGen=true

# ========================
# Runtime stage
# ========================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Puertos: 5001 (REST), 5002 (gRPC)
EXPOSE 5001 5002

ENV ASPNETCORE_ENVIRONMENT=Production

# Usuario no-root incluido en imágenes .NET 8+
USER app

ENTRYPOINT ["dotnet", "ProductService.dll"]

ENTRYPOINT ["dotnet", "ProductService.dll"]
