# ========================
# Build stage
# ========================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Gateway.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

# ========================
# Runtime stage
# ========================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN adduser --disabled-password --gecos "" appuser

COPY --from=build /app/publish .

EXPOSE 5010

ENV ASPNETCORE_ENVIRONMENT=Production

USER appuser

ENTRYPOINT ["dotnet", "Gateway.dll"]
