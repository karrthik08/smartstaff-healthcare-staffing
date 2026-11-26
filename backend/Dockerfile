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
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "SmartStaff.API.dll"]
