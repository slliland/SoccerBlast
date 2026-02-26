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
  - TheSportsDB v2 (live fixtures/scores)
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
<img src="https://amethyst-hollow-whale-949.mypinata.cloud/ipfs/bafybeialwbqtqgpwk23c43qa2ioetuccoxorczhdpn2er5uzbu7wkairo4" alt="Home" width="900" />

### Search
<img src="https://amethyst-hollow-whale-949.mypinata.cloud/ipfs/bafybeihg35u55teqp4sgo4l7mrtpgqmcoypgxtcyj3nysjr37yybj3tkzq" alt="Search" width="900" />

### All Matches
<img src="https://amethyst-hollow-whale-949.mypinata.cloud/ipfs/bafybeibfayqhchqf7vu42jz3xfydjiqh4ze4kml35un33ukkijjfcd2nla" alt="All Matches" width="900" />

### Following
<img src="https://amethyst-hollow-whale-949.mypinata.cloud/ipfs/bafybeigzpipvdufmyz47rfvay2632v2lrspqehhvtud6w6wzvmiwwpjr2a" alt="Following" width="900" />

### Video
<img src="https://amethyst-hollow-whale-949.mypinata.cloud/ipfs/bafybeiagq2323rxssr2hjwzrrh237xsoiwiid5byujlg5rgfk4gc7ovabu" alt="Video" width="900" />

### Competition
<img src="https://amethyst-hollow-whale-949.mypinata.cloud/ipfs/bafybeib4w263ihda5kpghjel7wbffn55756gsyhfs57yiseclsx6d2ysee" alt="Competition Seasons" width="900" />

<img src="https://amethyst-hollow-whale-949.mypinata.cloud/ipfs/bafybeietfolzxgrmbieirtkfio7si2j5565kc4ob4d25iovi5km3p3pb34" alt="Competition Details" width="900" />

### Player
<img src="https://amethyst-hollow-whale-949.mypinata.cloud/ipfs/bafybeibo66ljrw7snzmiboq2exywufwjyktheg7amzkop6vuqeqifaxhni" alt="Player Profile" width="900" />

<img src="https://amethyst-hollow-whale-949.mypinata.cloud/ipfs/bafybeibe2dw24lzj6wjv4r3gtn7yejxiov4iskv2lgiatj3oebsooukr6e" alt="Player Career" width="900" />

<img src="https://amethyst-hollow-whale-949.mypinata.cloud/ipfs/bafybeifio4qg7m46avy2cegw7mj64atpan2bcwsjxbuqx5ky27isfk6lsi" alt="Player Honors" width="900" />

<img src="https://amethyst-hollow-whale-949.mypinata.cloud/ipfs/bafybeieclc5nsgyghl2mavdjf533ipaard4mlixewsr3z26slyrbjsg5gi" alt="Player Milestones" width="900" />

### Stadium
<img src="https://amethyst-hollow-whale-949.mypinata.cloud/ipfs/bafybeicszqml2q3ocsfekgx2t2rjkdwpjze2ldessifyvknyg7fl2jmpcy" alt="Stadium Overview" width="900" />

<img src="https://amethyst-hollow-whale-949.mypinata.cloud/ipfs/bafybeich3sb366k46ruxn2wrckdml7kdgx3oi7zcu5v23omunrdtl5ntbe" alt="Stadium Gallery" width="900" />


## Getting Started

### Prerequisites
- .NET SDK 8+

### Run the API
```bash
./run-api.sh
```
---
#### Inspired by [CricBlast](https://github.com/Raofin/CricBlast)

