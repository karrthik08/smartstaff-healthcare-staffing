FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY SmartStaff.sln ./
COPY backend/SmartStaff.API/SmartStaff.API.csproj backend/SmartStaff.API/
COPY backend/SmartStaff.Application/SmartStaff.Application.csproj backend/SmartStaff.Application/
COPY backend/SmartStaff.Domain/SmartStaff.Domain.csproj backend/SmartStaff.Domain/
COPY backend/SmartStaff.Infrastructure/SmartStaff.Infrastructure.csproj backend/SmartStaff.Infrastructure/
RUN dotnet restore backend/SmartStaff.API/SmartStaff.API.csproj

COPY backend/SmartStaff.API/ backend/SmartStaff.API/
COPY backend/SmartStaff.Application/ backend/SmartStaff.Application/
COPY backend/SmartStaff.Domain/ backend/SmartStaff.Domain/
COPY backend/SmartStaff.Infrastructure/ backend/SmartStaff.Infrastructure/
RUN dotnet publish backend/SmartStaff.API/SmartStaff.API.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=40s --retries=3 \
  CMD curl -fsS http://127.0.0.1:8080/api/health || exit 1
ENTRYPOINT ["dotnet", "SmartStaff.API.dll"]
