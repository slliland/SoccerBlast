-- One-time script: creates Honours, TeamHonours, HonourWinners and records the migration.
-- Run from repo root: sqlite3 SoccerBlast.Api/soccerblast.db < SoccerBlast.Api/Migrations/ApplyHonoursTables.sql
-- Safe to run multiple times (IF NOT EXISTS / INSERT OR IGNORE).

CREATE TABLE IF NOT EXISTS "Honours" (
    "Id" INTEGER NOT NULL,
    "Slug" TEXT NOT NULL,
    "Title" TEXT NULL,
    "TrophyImageUrl" TEXT NULL,
    "HonourUrl" TEXT NOT NULL,
    "TypeGuess" TEXT NULL,
    CONSTRAINT "PK_Honours" PRIMARY KEY ("Id")
);

CREATE TABLE IF NOT EXISTS "TeamHonours" (
    "TeamId" TEXT NOT NULL,
    "HonourId" INTEGER NOT NULL,
    CONSTRAINT "PK_TeamHonours" PRIMARY KEY ("TeamId", "HonourId"),
    CONSTRAINT "FK_TeamHonours_Honours_HonourId" FOREIGN KEY ("HonourId") REFERENCES "Honours" ("Id") ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS "HonourWinners" (
    "HonourId" INTEGER NOT NULL,
    "YearLabel" TEXT NOT NULL,
    "TeamId" TEXT NOT NULL,
    "TeamName" TEXT NULL,
    "TeamBadgeUrl" TEXT NULL,
    CONSTRAINT "PK_HonourWinners" PRIMARY KEY ("HonourId", "YearLabel"),
    CONSTRAINT "FK_HonourWinners_Honours_HonourId" FOREIGN KEY ("HonourId") REFERENCES "Honours" ("Id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_TeamHonours_HonourId" ON "TeamHonours" ("HonourId");
CREATE INDEX IF NOT EXISTS "IX_TeamHonours_TeamId" ON "TeamHonours" ("TeamId");
CREATE INDEX IF NOT EXISTS "IX_HonourWinners_TeamId" ON "HonourWinners" ("TeamId");

INSERT OR IGNORE INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260222000000_AddHonoursTables', '10.0.3');
