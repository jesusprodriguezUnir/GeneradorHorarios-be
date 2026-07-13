# ── Build ─────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restaurar primero (mejor caché de capas)
COPY Directory.Build.props ./
COPY apps/api/api.csproj apps/api/
COPY src/HorariosEscolares.Domain/HorariosEscolares.Domain.csproj src/HorariosEscolares.Domain/
COPY src/HorariosEscolares.Application/HorariosEscolares.Application.csproj src/HorariosEscolares.Application/
COPY src/HorariosEscolares.Infrastructure/HorariosEscolares.Infrastructure.csproj src/HorariosEscolares.Infrastructure/
RUN dotnet restore apps/api/api.csproj

COPY apps/ apps/
COPY src/ src/
RUN dotnet publish apps/api/api.csproj -c Release -o /app/publish /p:UseAppHost=false

# ── Runtime ───────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000

ENTRYPOINT ["dotnet", "HorariosEscolares.Api.dll"]
