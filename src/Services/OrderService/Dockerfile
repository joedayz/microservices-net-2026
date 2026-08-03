FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY OrderService/ OrderService/
# Necesita el .proto de ProductService para generar el cliente gRPC
COPY ProductService/Protos/ ProductService/Protos/
RUN dotnet publish OrderService/OrderService.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN adduser --disabled-password --gecos "" appuser
COPY --from=build /app/publish .
EXPOSE 5003
ENV ASPNETCORE_ENVIRONMENT=Production
USER appuser
ENTRYPOINT ["dotnet", "OrderService.dll"]
