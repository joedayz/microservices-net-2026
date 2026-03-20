# ========================
# Build stage
# ========================
# BUILDPLATFORM = arquitectura nativa del host (evita emulación QEMU en Apple Silicon)
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Gateway.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /app/publish

# ========================
# Runtime stage
# ========================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 5010

ENV ASPNETCORE_ENVIRONMENT=Production

# Usuario no-root incluido en imágenes .NET 8+
USER app

ENTRYPOINT ["dotnet", "Gateway.dll"]
