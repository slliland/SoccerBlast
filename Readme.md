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

### Home Page
<img src="https://amethyst-hollow-whale-949.mypinata.cloud/ipfs/bafybeifgfjruzja4r43jgwl4yobp6apxgfznrtkj6y64jsku52x3tempym" alt="SoccerBlast Home Page" width="900" />

### Search Feature
<img src="https://amethyst-hollow-whale-949.mypinata.cloud/ipfs/bafybeid45txeep7oqs7xhrtyuvvty3xq2pvjdkwwtoxlbsdigs7pqt6bay" alt="SoccerBlast Search Feature" width="900" />


## Getting Started

### Prerequisites
- .NET SDK 8+

### Run the API
```bash
cd src/SoccerBlast.Api
dotnet restore
dotnet run
```

### Inspired by [CricBlast](https://github.com/Raofin/CricBlast)

