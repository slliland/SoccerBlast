# SoccerBlast

A C#/.NET project that combines:
- **Live soccer scores & fixtures** (scoreboard)
- **Match exploration** (competitions → seasons → matches)

Built as a multi-client system:
- **ASP.NET Core Web API** backend
- **Blazor Server** web app (initial UI)
- (Planned) **.NET MAUI** mobile app reusing the same API

## Tech Stack
- .NET 8
- ASP.NET Core Web API
- Blazor Server (web UI)
- EF Core + SQLite (local dev DB)
- External data providers (planned):
  - football-data.org (live fixtures/scores)
  - StatsBomb Open Data (historical + event data)

## Project Structure
- `src/SoccerBlast.Api`    → REST API + database + sync services
- `src/SoccerBlast.Web`    → Web UI that consumes the API
- `src/SoccerBlast.Shared` → Shared DTOs/contracts used by both

## Features (MVP)
- Today’s matches endpoint: `GET /api/matches/today`
- Explore competitions and matches (basic)
- Manual sync endpoint (planned): `POST /api/admin/sync/today`

## Screenshots

### Home
<img src="https://amethyst-hollow-whale-949.mypinata.cloud/ipfs/bafybeicsojm2waf7zc4747urmc3uigmkcfyqdyinwrwh4bo67kpzmf4olu" alt="Home" width="900" />

### Search
<img src="https://amethyst-hollow-whale-949.mypinata.cloud/ipfs/bafybeihg35u55teqp4sgo4l7mrtpgqmcoypgxtcyj3nysjr37yybj3tkzq" alt="Search" width="900" />

### All Matches
<img src="https://amethyst-hollow-whale-949.mypinata.cloud/ipfs/bafybeic22walloz7rzkhj5sof2t3bh3mvsavjpmi4qyfpazdyzkfobihle" alt="All Matches" width="900" />

### Following
<img src="https://amethyst-hollow-whale-949.mypinata.cloud/ipfs/bafybeiauc6jtnbhecs7xl45sdxyljszosh2o4fwwdlllnjmskc3jxdmana" alt="Following" width="900" />

### Video
<img src="https://amethyst-hollow-whale-949.mypinata.cloud/ipfs/bafybeiagq2323rxssr2hjwzrrh237xsoiwiid5byujlg5rgfk4gc7ovabu" alt="Video" width="900" />



## Getting Started

### Prerequisites
- .NET SDK 8+

### Run the API
```bash
cd src/SoccerBlast.Api
dotnet restore
dotnet run
```
---
#### Inspired by [CricBlast](https://github.com/Raofin/CricBlast)

