FROM mcr.microsoft.com/dotnet/sdk:10.0 AS database-build

WORKDIR /src

COPY Database/MediaNormalizer.Database.sqlproj Database/
COPY Database/ Database/

RUN dotnet build Database/MediaNormalizer.Database.sqlproj --configuration Release

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS deployer

WORKDIR /app

RUN dotnet tool install --tool-path /tools Microsoft.SqlPackage --version 170.5.96

COPY --from=database-build /src/Database/bin/Release/MediaNormalizer.Database.dacpac /app/MediaNormalizer.Database.dacpac
COPY Database/Deploy/entrypoint.sh /app/entrypoint.sh

RUN chmod +x /app/entrypoint.sh

ENTRYPOINT ["/bin/bash", "/app/entrypoint.sh"]
