-- Run this ONCE against your existing database if __EFMigrationsHistory was empty
-- and "Competitions" (and other tables) already exist. It marks all migrations
-- before the two FixSearch* ones as applied so "dotnet ef database update"
-- only runs the remaining migrations.
-- Execute with: psql <connection> -f baseline-existing-database.sql
-- Or paste into your SQL client.

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES
  ('20260212214731_InitialCreate', '10.0.3'),
  ('20260212215844_MakeCompetitionCountryNullable', '10.0.3'),
  ('20260212223123_AddSyncLogs', '10.0.3'),
  ('20260213051155_AddMatchIndexes', '10.0.3'),
  ('20260213192014_AddTeamCrestUrl', '10.0.3'),
  ('20260213221011_AddNewsItems', '10.0.3'),
  ('20260213223300_AddNewsItems_v2', '10.0.3'),
  ('20260213224719_AddNewsIndexes', '10.0.3'),
  ('20260217012305_AddNewsItemTeam', '10.0.3'),
  ('20260217013023_AddNewsItemTeam2', '10.0.3'),
  ('20260217040301_AddNewsContent', '10.0.3'),
  ('20260217152020_AddSportsDbMappingAndTeamProfile', '10.0.3'),
  ('20260217170934_AddSportsDbMappingTables', '10.0.3'),
  ('20260217182347_EnrichTeamProfile', '10.0.3'),
  ('20260218033243_AddCompetitionBadgeUrl', '10.0.3'),
  ('20260218155645_AddPlayersAndVenues', '10.0.3'),
  ('20260219012959_MatchProviderExternalId', '10.0.3'),
  ('20260219091901_DropTeamExternalMapAddSportsDbId', '10.0.3'),
  ('20260219141418_AddMatchDaySyncState', '10.0.3'),
  ('20260219221505_AddMatchDaySyncStates', '10.0.3'),
  ('20260220024850_AddSearchAliases', '10.0.3'),
  ('20260220043231_AddSearchAliasNorm', '10.0.3'),
  ('20260222000000_AddHonoursTables', '10.0.3'),
  ('20260222074126_AddLeagueSeasonStandings', '10.0.3'),
  ('20260223061719_AddLeagueHonourMap', '10.0.3')
ON CONFLICT ("MigrationId") DO NOTHING;

-- If "dotnet ef database update" then fails on 20260226040427_FixSearchAliasesIdIdentity
-- (e.g. column type already correct), you can mark it applied and only run the last one:
-- INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
-- VALUES ('20260226040427_FixSearchAliasesIdIdentity', '10.0.3')
-- ON CONFLICT ("MigrationId") DO NOTHING;
