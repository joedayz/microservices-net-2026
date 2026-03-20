# ========================
# Build stage
# ========================
# BUILDPLATFORM = arquitectura nativa del host (evita emulación QEMU en Apple Silicon)
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copiar csproj y restaurar dependencias
# Nota: SkipGrpcCodeGen=true desactiva Grpc.Tools (incompatible con .NET 10 SDK en Docker/ARM64)
# Se usan archivos C# pre-generados desde Grpc/Generated/ en su lugar.
COPY OrderService/OrderService.csproj OrderService/
RUN dotnet restore OrderService/OrderService.csproj /p:SkipGrpcCodeGen=true

# Copiar código (incluye Grpc/Generated/ con protobuf pre-generado) y compilar
COPY OrderService/ OrderService/
RUN dotnet publish OrderService/OrderService.csproj -c Release -o /app/publish /p:SkipGrpcCodeGen=true

# ========================
# Runtime stage
# ========================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 5003

ENV ASPNETCORE_ENVIRONMENT=Production

# Usuario no-root incluido en imágenes .NET 8+
USER app

ENTRYPOINT ["dotnet", "OrderService.dll"]
