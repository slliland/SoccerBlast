# TeamProfileSyncService — How it works

## Previous design (multiple sources / external mapping)

- **Teams** could come from different sources (e.g. football-data.org with our own `Id`, or SportsDB).
- **TeamExternalMap** stored `(TeamId, Provider, ExternalId)` so we could map "our" team id to SportsDB id.
- **TeamProfileSyncService** had to:
  1. Load our team and the mapping row.
  2. If mapping existed, call SportsDB `lookup/team/{ExternalId}`; if the result was wrong sport/team, delete the mapping and "re-resolve".
  3. If no mapping or lookup failed, **resolve by name**: search SportsDB by normalized name, take top N, **lookup each** to verify Soccer, then disambiguate (stadium, formed year, name overlap). That added a lot of logic (resolver, IsWrongTeam, WordOverlap, cleanup, etc.).
  4. Ensure mapping row exists/updated, then write profile from SportsDB payload.

So we needed mapping tables and resolver logic because "our" team id and "SportsDB" id could differ.

## New design (single source = SportsDB)

- **Soccer data is only from SportsDB.** Match sync already creates `Team` with `Id` = SportsDB team id (from events). We add `Team.SportsDbId` (string) so the link is explicit; for sync-created teams it is `Id.ToString()`.
- **No TeamExternalMap.** Team is identified by `Id` (int) and optionally `SportsDbId` (string). Profile is fetched by SportsDB id only.
- **TeamProfileSyncService** now:
  1. Load team by id. Resolve SportsDB id = `team.SportsDbId ?? team.Id.ToString()`.
  2. Call `LookupTeamAsync(sportsDbId)`. If null, return "not found".
  3. Map result to `TeamProfile` and save. No resolver, no mapping table, no cleanup.

So we only "sync profile" for teams we already know by SportsDB id; we don’t resolve by name anymore.
