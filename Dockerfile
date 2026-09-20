# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release

WORKDIR /src

COPY ["Host/Host.csproj", "Host/"]
COPY ["Application/Application.csproj", "Application/"]
COPY ["Business/Business.csproj", "Business/"]
COPY ["Domain/Domain.csproj", "Domain/"]
COPY ["Infrastructure/Infrastructure.csproj", "Infrastructure/"]
COPY ["Infrastructure.Database/Infrastructure.Database.csproj", "Infrastructure.Database/"]
RUN dotnet restore "Host/Host.csproj"

COPY . .
RUN dotnet publish "Host/Host.csproj" \
    --configuration "$BUILD_CONFIGURATION" \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

# The container uses root so existing bind-mounted media libraries remain writable.
RUN apt-get update \
    && apt-get install --no-install-recommends --yes curl \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_HTTP_PORTS=18081
EXPOSE 18081

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Host.dll"]
