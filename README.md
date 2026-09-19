# MediaNormalizer

MediaNormalizer is a .NET Core Web API application intended to run alongside a media server i.e. Plex or Jellyfin. It's used to move media files from an intake folder into destination folders (depending on media type), match them to a known entry on OMDB, and use the OMDB entry to rename files and directories into a format recognized by the chosen media server app. It's designed to be deployed via Docker Compose on a Linux container.

## Docker deployment

Install Docker Desktop or Docker Engine with the Compose plugin, then create the local environment file:

```powershell
Copy-Item .env.example .env
```

Edit `.env` and set:

- `MEDIA_LIBRARY_PATH` to the existing media directory on the host.
- `MEDIA_NORMALIZED_PATH` to the writable output directory on the host.
- `OMDB_API_KEY` to the OMDb API key used for TV identification.
- `MEDIA_LIBRARY_TV_ENABLED` to `true` only after the mounted paths are verified.

The container maps those host directories to `/media/library` and `/media/normalized`. It runs as root so the existing bind-mounted media permissions remain usable; restrict access to the Docker host accordingly.

Build and start the API:

```powershell
docker compose config
docker compose build
docker compose up -d
```

The API listens on `http://localhost:18081` by default. Verify the container health endpoint:

```powershell
Invoke-RestMethod http://localhost:18081/health
```

Normalization is API-triggered and is disabled by default through the media-type setting. To trigger it after reviewing the configuration:

```powershell
Invoke-RestMethod -Method Post -Uri http://localhost:18081/normalize
```

`POST /normalize` can move and rename files in the mounted media directories. Do not invoke it until the source and output paths are correct and backed up as needed.

View logs or stop the deployment with:

```powershell
docker compose logs -f media-normalizer
docker compose down
```

The container exposes HTTP on port `18081`. TLS and authentication should be provided by the deployment environment or an external reverse proxy.
