START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261003025842_AddStatutoryLibrary') THEN
    CREATE TABLE "IssuingAuthorities" (
        "Id" uuid NOT NULL,
        "Name" character varying(255) NOT NULL,
        CONSTRAINT "PK_IssuingAuthorities" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261003025842_AddStatutoryLibrary') THEN
    CREATE TABLE "LegalFields" (
        "Id" uuid NOT NULL,
        "Name" character varying(255) NOT NULL,
        CONSTRAINT "PK_LegalFields" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261003025842_AddStatutoryLibrary') THEN
    CREATE TABLE "StatutoryDocuments" (
        "Id" uuid NOT NULL,
        "Title" character varying(1000) NOT NULL,
        "Number" character varying(100) NOT NULL,
        "Type" smallint NOT NULL,
        "AuthorityId" uuid NOT NULL,
        "IssuedOn" date NOT NULL,
        "EffectiveFrom" date NOT NULL,
        "ExpiresOn" date,
        "PartiallyExpired" boolean NOT NULL,
        "Summary" character varying(10000),
        "SourceUrl" character varying(2048),
        "FileUrl" character varying(2048),
        "IsPublished" boolean NOT NULL,
        "PublishedAt" timestamp with time zone,
        "CreatedBy" uuid NOT NULL,
        "UpdatedBy" uuid NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_StatutoryDocuments" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_StatutoryDocuments_IssuingAuthorities_AuthorityId" FOREIGN KEY ("AuthorityId") REFERENCES "IssuingAuthorities" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261003025842_AddStatutoryLibrary') THEN
    CREATE TABLE "LegalSections" (
        "Id" uuid NOT NULL,
        "DocumentId" uuid NOT NULL,
        "ParentId" uuid,
        "Type" smallint NOT NULL,
        "Number" character varying(100) NOT NULL,
        "Title" character varying(1000) NOT NULL,
        "Order" integer NOT NULL,
        "Path" character varying(1000) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_LegalSections" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_LegalSections_LegalSections_ParentId" FOREIGN KEY ("ParentId") REFERENCES "LegalSections" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_LegalSections_StatutoryDocuments_DocumentId" FOREIGN KEY ("DocumentId") REFERENCES "StatutoryDocuments" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261003025842_AddStatutoryLibrary') THEN
    CREATE TABLE "StatutoryDocumentFields" (
        "DocumentId" uuid NOT NULL,
        "FieldId" uuid NOT NULL,
        CONSTRAINT "PK_StatutoryDocumentFields" PRIMARY KEY ("DocumentId", "FieldId"),
        CONSTRAINT "FK_StatutoryDocumentFields_LegalFields_FieldId" FOREIGN KEY ("FieldId") REFERENCES "LegalFields" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_StatutoryDocumentFields_StatutoryDocuments_DocumentId" FOREIGN KEY ("DocumentId") REFERENCES "StatutoryDocuments" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261003025842_AddStatutoryLibrary') THEN
    CREATE TABLE "StatutoryDocumentRelations" (
        "Id" uuid NOT NULL,
        "DocumentId" uuid NOT NULL,
        "RelatedDocumentId" uuid NOT NULL,
        "Type" smallint NOT NULL,
        CONSTRAINT "PK_StatutoryDocumentRelations" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_StatutoryDocumentRelations_StatutoryDocuments_DocumentId" FOREIGN KEY ("DocumentId") REFERENCES "StatutoryDocuments" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_StatutoryDocumentRelations_StatutoryDocuments_RelatedDocume~" FOREIGN KEY ("RelatedDocumentId") REFERENCES "StatutoryDocuments" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261003025842_AddStatutoryLibrary') THEN
    CREATE TABLE "LegalSectionRelations" (
        "Id" uuid NOT NULL,
        "SectionId" uuid NOT NULL,
        "RelatedSectionId" uuid NOT NULL,
        "Type" smallint NOT NULL,
        CONSTRAINT "PK_LegalSectionRelations" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_LegalSectionRelations_LegalSections_RelatedSectionId" FOREIGN KEY ("RelatedSectionId") REFERENCES "LegalSections" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_LegalSectionRelations_LegalSections_SectionId" FOREIGN KEY ("SectionId") REFERENCES "LegalSections" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261003025842_AddStatutoryLibrary') THEN
    CREATE TABLE "LegalSectionVersions" (
        "Id" uuid NOT NULL,
        "SectionId" uuid NOT NULL,
        "VersionNumber" integer NOT NULL,
        "Content" text NOT NULL,
        "ValidFrom" date NOT NULL,
        "ValidTo" date,
        "IsPublished" boolean NOT NULL,
        "ChangeType" smallint NOT NULL,
        "ChangedByDocumentId" uuid,
        "Note" character varying(4000),
        "CreatedBy" uuid NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_LegalSectionVersions" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_LegalSectionVersions_LegalSections_SectionId" FOREIGN KEY ("SectionId") REFERENCES "LegalSections" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_LegalSectionVersions_StatutoryDocuments_ChangedByDocumentId" FOREIGN KEY ("ChangedByDocumentId") REFERENCES "StatutoryDocuments" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261003025842_AddStatutoryLibrary') THEN
    CREATE UNIQUE INDEX "IX_IssuingAuthorities_Name" ON "IssuingAuthorities" ("Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261003025842_AddStatutoryLibrary') THEN
    CREATE UNIQUE INDEX "IX_LegalFields_Name" ON "LegalFields" ("Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261003025842_AddStatutoryLibrary') THEN
    CREATE INDEX "IX_LegalSectionRelations_RelatedSectionId" ON "LegalSectionRelations" ("RelatedSectionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261003025842_AddStatutoryLibrary') THEN
    CREATE UNIQUE INDEX "IX_LegalSectionRelations_SectionId_RelatedSectionId_Type" ON "LegalSectionRelations" ("SectionId", "RelatedSectionId", "Type");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261003025842_AddStatutoryLibrary') THEN
    CREATE INDEX "IX_LegalSections_DocumentId_ParentId_Order" ON "LegalSections" ("DocumentId", "ParentId", "Order");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261003025842_AddStatutoryLibrary') THEN
    CREATE INDEX "IX_LegalSections_ParentId" ON "LegalSections" ("ParentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261003025842_AddStatutoryLibrary') THEN
    CREATE INDEX "IX_LegalSections_Path" ON "LegalSections" ("Path");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261003025842_AddStatutoryLibrary') THEN
    CREATE INDEX "IX_LegalSectionVersions_ChangedByDocumentId" ON "LegalSectionVersions" ("ChangedByDocumentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261003025842_AddStatutoryLibrary') THEN
    CREATE INDEX "IX_LegalSectionVersions_SectionId_IsPublished_ValidFrom_ValidTo" ON "LegalSectionVersions" ("SectionId", "IsPublished", "ValidFrom", "ValidTo");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261003025842_AddStatutoryLibrary') THEN
    CREATE UNIQUE INDEX "IX_LegalSectionVersions_SectionId_ValidFrom" ON "LegalSectionVersions" ("SectionId", "ValidFrom");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261003025842_AddStatutoryLibrary') THEN
    CREATE UNIQUE INDEX "IX_LegalSectionVersions_SectionId_VersionNumber" ON "LegalSectionVersions" ("SectionId", "VersionNumber");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261003025842_AddStatutoryLibrary') THEN
    CREATE INDEX "IX_StatutoryDocumentFields_FieldId" ON "StatutoryDocumentFields" ("FieldId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261003025842_AddStatutoryLibrary') THEN
    CREATE UNIQUE INDEX "IX_StatutoryDocumentRelations_DocumentId_RelatedDocumentId_Type" ON "StatutoryDocumentRelations" ("DocumentId", "RelatedDocumentId", "Type");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261003025842_AddStatutoryLibrary') THEN
    CREATE INDEX "IX_StatutoryDocumentRelations_RelatedDocumentId" ON "StatutoryDocumentRelations" ("RelatedDocumentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261003025842_AddStatutoryLibrary') THEN
    CREATE INDEX "IX_StatutoryDocuments_AuthorityId" ON "StatutoryDocuments" ("AuthorityId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261003025842_AddStatutoryLibrary') THEN
    CREATE INDEX "IX_StatutoryDocuments_EffectiveFrom" ON "StatutoryDocuments" ("EffectiveFrom");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261003025842_AddStatutoryLibrary') THEN
    CREATE INDEX "IX_StatutoryDocuments_ExpiresOn" ON "StatutoryDocuments" ("ExpiresOn");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261003025842_AddStatutoryLibrary') THEN
    CREATE INDEX "IX_StatutoryDocuments_IsPublished_IssuedOn" ON "StatutoryDocuments" ("IsPublished", "IssuedOn");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261003025842_AddStatutoryLibrary') THEN
    CREATE UNIQUE INDEX "IX_StatutoryDocuments_Number_AuthorityId" ON "StatutoryDocuments" ("Number", "AuthorityId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261003025842_AddStatutoryLibrary') THEN
    CREATE INDEX "IX_StatutoryDocuments_Type_IssuedOn" ON "StatutoryDocuments" ("Type", "IssuedOn");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261003025842_AddStatutoryLibrary') THEN
    CREATE INDEX "IX_StatutoryDocuments_Search" ON "StatutoryDocuments" USING GIN (to_tsvector('simple', "Title" || ' ' || "Number"));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261003025842_AddStatutoryLibrary') THEN
    CREATE INDEX "IX_LegalSectionVersions_Search" ON "LegalSectionVersions" USING GIN (to_tsvector('simple', "Content"));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261003025842_AddStatutoryLibrary') THEN
    ALTER TABLE "StatutoryDocuments" ADD CONSTRAINT "CK_StatutoryDates" CHECK ("EffectiveFrom" >= "IssuedOn" AND ("ExpiresOn" IS NULL OR "ExpiresOn" >= "EffectiveFrom"));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261003025842_AddStatutoryLibrary') THEN
    ALTER TABLE "LegalSectionVersions" ADD CONSTRAINT "CK_SectionVersionDates" CHECK ("ValidTo" IS NULL OR "ValidTo" >= "ValidFrom");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261003025842_AddStatutoryLibrary') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261003025842_AddStatutoryLibrary', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261003033801_AddStatutorySectionPaths') THEN
    DROP INDEX "IX_LegalSections_Path";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261003033801_AddStatutorySectionPaths') THEN
    CREATE UNIQUE INDEX "IX_LegalSections_DocumentId_Path" ON "LegalSections" ("DocumentId", "Path");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261003033801_AddStatutorySectionPaths') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261003033801_AddStatutorySectionPaths', '8.0.11');
    END IF;
END $EF$;
COMMIT;
