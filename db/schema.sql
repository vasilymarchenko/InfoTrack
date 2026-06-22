CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260618123314_InitialCreate') THEN
    CREATE TABLE "Firms" (
        "Id" uuid NOT NULL,
        "IdentityKey" text NOT NULL,
        "FirmName" text NOT NULL,
        "Address" text NOT NULL,
        "Town" text,
        "Postcode" text,
        "Phone" text,
        "WebsiteUrl" text,
        "EnquiryUrl" text,
        "ProfileUrl" text,
        "Description" text,
        "LogoUrl" text,
        "FirstSeenAt" timestamp with time zone NOT NULL,
        "LastSeenAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_Firms" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260618123314_InitialCreate') THEN
    CREATE TABLE "SearchRuns" (
        "Id" uuid NOT NULL,
        "RunAtUtc" timestamp with time zone NOT NULL,
        "AreaOfLaw" text NOT NULL,
        "RequestedLocations" text NOT NULL,
        "TotalLocations" integer NOT NULL,
        "TotalUniqueFirms" integer NOT NULL,
        CONSTRAINT "PK_SearchRuns" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260618123314_InitialCreate') THEN
    CREATE TABLE "LocationOutcomes" (
        "Id" uuid NOT NULL,
        "SearchRunId" uuid NOT NULL,
        "Location" text NOT NULL,
        "RequestedUrl" text,
        "Status" text NOT NULL,
        "ErrorMessage" text,
        CONSTRAINT "PK_LocationOutcomes" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_LocationOutcomes_SearchRuns_SearchRunId" FOREIGN KEY ("SearchRunId") REFERENCES "SearchRuns" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260618123314_InitialCreate') THEN
    CREATE TABLE "Sightings" (
        "Id" uuid NOT NULL,
        "LocationOutcomeId" uuid NOT NULL,
        "FirmId" uuid NOT NULL,
        "ReviewCount" integer,
        CONSTRAINT "PK_Sightings" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Sightings_Firms_FirmId" FOREIGN KEY ("FirmId") REFERENCES "Firms" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_Sightings_LocationOutcomes_LocationOutcomeId" FOREIGN KEY ("LocationOutcomeId") REFERENCES "LocationOutcomes" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260618123314_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_Firms_IdentityKey" ON "Firms" ("IdentityKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260618123314_InitialCreate') THEN
    CREATE INDEX "IX_LocationOutcomes_SearchRunId" ON "LocationOutcomes" ("SearchRunId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260618123314_InitialCreate') THEN
    CREATE INDEX "IX_SearchRuns_RunAtUtc" ON "SearchRuns" ("RunAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260618123314_InitialCreate') THEN
    CREATE INDEX "IX_Sightings_FirmId" ON "Sightings" ("FirmId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260618123314_InitialCreate') THEN
    CREATE INDEX "IX_Sightings_LocationOutcomeId" ON "Sightings" ("LocationOutcomeId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260618123314_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260618123314_InitialCreate', '10.0.9');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260622081919_AddSightingTier') THEN
    ALTER TABLE "Sightings" ADD "Tier" text NOT NULL DEFAULT 'Featured';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260622081919_AddSightingTier') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260622081919_AddSightingTier', '10.0.9');
    END IF;
END $EF$;
COMMIT;

