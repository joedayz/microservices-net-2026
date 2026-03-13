# ========================
# Build stage
# ========================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copiar csproj y proto compartido, luego restaurar dependencias
COPY OrderService/OrderService.csproj OrderService/
COPY ProductService/Protos/ ProductService/Protos/
RUN dotnet restore OrderService/OrderService.csproj

# Copiar código y compilar
COPY OrderService/ OrderService/
RUN dotnet publish OrderService/OrderService.csproj -c Release -o /app/publish --no-restore

# ========================
# Runtime stage
# ========================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN adduser --disabled-password --gecos "" appuser

COPY --from=build /app/publish .

EXPOSE 5003

ENV ASPNETCORE_ENVIRONMENT=Production

USER appuser

ENTRYPOINT ["dotnet", "OrderService.dll"]
