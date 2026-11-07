-- ============================================================
-- ManufacturingDB (PostgreSQL) - Migration 003: shifts, work calendars, work centers, machines, standard operations, reason codes, crews.
-- BA spec 01 §5.4-5.7, §7.1. Append-only and idempotent. Every table has the base columns of BaseEntity (see 001/002); optimistic locking uses xmin.
-- Codes are unique per (TenantId, PlantId, Code); reason codes per (TenantId, Type, Code).
-- ============================================================

CREATE TABLE IF NOT EXISTS "Shifts" (
    "Id" uuid NOT NULL PRIMARY KEY, "TenantId" uuid NOT NULL, "Description" varchar(2000) NULL, "IsDeleted" boolean NOT NULL DEFAULT false,
    "CreatedBy" varchar(200) NULL, "CreatedAt" timestamptz NOT NULL DEFAULT now(), "UpdatedBy" varchar(200) NULL, "UpdatedAt" timestamptz NOT NULL DEFAULT now(),
    "PlantId"      uuid         NOT NULL REFERENCES "Plants" ("Id"),
    "Code"         varchar(20)  NOT NULL,
    "Name"         varchar(100) NOT NULL,
    "StartTime"    time         NOT NULL,
    "EndTime"      time         NOT NULL,            -- not after StartTime = overnight shift
    "BreakMinutes" integer      NOT NULL DEFAULT 0,
    "IsActive"     boolean      NOT NULL DEFAULT true
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Shifts_TenantId_PlantId_Code" ON "Shifts" ("TenantId", "PlantId", "Code");

CREATE TABLE IF NOT EXISTS "WorkCalendars" (
    "Id" uuid NOT NULL PRIMARY KEY, "TenantId" uuid NOT NULL, "Description" varchar(2000) NULL, "IsDeleted" boolean NOT NULL DEFAULT false,
    "CreatedBy" varchar(200) NULL, "CreatedAt" timestamptz NOT NULL DEFAULT now(), "UpdatedBy" varchar(200) NULL, "UpdatedAt" timestamptz NOT NULL DEFAULT now(),
    "PlantId"  uuid         NOT NULL REFERENCES "Plants" ("Id"),
    "Code"     varchar(20)  NOT NULL,
    "Name"     varchar(100) NOT NULL,
    "IsActive" boolean      NOT NULL DEFAULT true
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_WorkCalendars_TenantId_PlantId_Code" ON "WorkCalendars" ("TenantId", "PlantId", "Code");

CREATE TABLE IF NOT EXISTS "WorkCalendarPatterns" (
    "Id" uuid NOT NULL PRIMARY KEY, "TenantId" uuid NOT NULL, "Description" varchar(2000) NULL, "IsDeleted" boolean NOT NULL DEFAULT false,
    "CreatedBy" varchar(200) NULL, "CreatedAt" timestamptz NOT NULL DEFAULT now(), "UpdatedBy" varchar(200) NULL, "UpdatedAt" timestamptz NOT NULL DEFAULT now(),
    "CalendarId" uuid     NOT NULL REFERENCES "WorkCalendars" ("Id"),
    "DayOfWeek"  smallint NOT NULL,                   -- 0 Sunday .. 6 Saturday (System.DayOfWeek)
    "ShiftId"    uuid     NOT NULL REFERENCES "Shifts" ("Id")
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_WorkCalendarPatterns_Calendar_Day_Shift" ON "WorkCalendarPatterns" ("TenantId", "CalendarId", "DayOfWeek", "ShiftId");

CREATE TABLE IF NOT EXISTS "WorkCalendarExceptions" (
    "Id" uuid NOT NULL PRIMARY KEY, "TenantId" uuid NOT NULL, "Description" varchar(2000) NULL, "IsDeleted" boolean NOT NULL DEFAULT false,
    "CreatedBy" varchar(200) NULL, "CreatedAt" timestamptz NOT NULL DEFAULT now(), "UpdatedBy" varchar(200) NULL, "UpdatedAt" timestamptz NOT NULL DEFAULT now(),
    "CalendarId" uuid     NOT NULL REFERENCES "WorkCalendars" ("Id"),
    "Date"       date     NOT NULL,
    "Type"       smallint NOT NULL                    -- 1 Holiday, 2 ExtraWorkingDay, 3 ModifiedShifts
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_WorkCalendarExceptions_Calendar_Date" ON "WorkCalendarExceptions" ("TenantId", "CalendarId", "Date");

CREATE TABLE IF NOT EXISTS "WorkCalendarExceptionShifts" (
    "Id" uuid NOT NULL PRIMARY KEY, "TenantId" uuid NOT NULL, "Description" varchar(2000) NULL, "IsDeleted" boolean NOT NULL DEFAULT false,
    "CreatedBy" varchar(200) NULL, "CreatedAt" timestamptz NOT NULL DEFAULT now(), "UpdatedBy" varchar(200) NULL, "UpdatedAt" timestamptz NOT NULL DEFAULT now(),
    "ExceptionId" uuid NOT NULL REFERENCES "WorkCalendarExceptions" ("Id"),
    "ShiftId"     uuid NOT NULL REFERENCES "Shifts" ("Id")
);
CREATE INDEX IF NOT EXISTS "IX_WorkCalendarExceptionShifts_Exception" ON "WorkCalendarExceptionShifts" ("TenantId", "ExceptionId");

-- The plant's default calendar (added to Plants in 002 without a key) now points at a real calendar.
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Plants_DefaultCalendar') THEN
        ALTER TABLE "Plants" ADD CONSTRAINT "FK_Plants_DefaultCalendar" FOREIGN KEY ("DefaultCalendarId") REFERENCES "WorkCalendars" ("Id");
    END IF;
END $$;

CREATE TABLE IF NOT EXISTS "WorkCenters" (
    "Id" uuid NOT NULL PRIMARY KEY, "TenantId" uuid NOT NULL, "Description" varchar(2000) NULL, "IsDeleted" boolean NOT NULL DEFAULT false,
    "CreatedBy" varchar(200) NULL, "CreatedAt" timestamptz NOT NULL DEFAULT now(), "UpdatedBy" varchar(200) NULL, "UpdatedAt" timestamptz NOT NULL DEFAULT now(),
    "PlantId"            uuid          NOT NULL REFERENCES "Plants" ("Id"),
    "Code"               varchar(20)   NOT NULL,
    "Name"               varchar(200)  NOT NULL,
    "DepartmentId"       uuid          NULL,          -- MasterData department; no cross-service key, not checked (R163)
    "CalendarId"         uuid          NOT NULL REFERENCES "WorkCalendars" ("Id"),
    "Type"               smallint      NOT NULL,      -- 1 Machine, 2 Labor, 3 Subcontract
    "NumberOfResources"  integer       NOT NULL DEFAULT 1,
    "EfficiencyPct"      numeric(5,2)  NOT NULL DEFAULT 100,
    "UtilizationPct"     numeric(5,2)  NOT NULL DEFAULT 100,
    "LaborRatePerHour"   numeric(18,4) NOT NULL DEFAULT 0,
    "MachineRatePerHour" numeric(18,4) NOT NULL DEFAULT 0,
    "QueueTimeMinutes"   integer       NOT NULL DEFAULT 0,
    "MoveTimeMinutes"    integer       NOT NULL DEFAULT 0,
    "VendorId"           uuid          NULL,          -- Purchasing supplier of a Subcontract center; not checked (R163)
    "IsActive"           boolean       NOT NULL DEFAULT true
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_WorkCenters_TenantId_PlantId_Code" ON "WorkCenters" ("TenantId", "PlantId", "Code");

CREATE TABLE IF NOT EXISTS "Machines" (
    "Id" uuid NOT NULL PRIMARY KEY, "TenantId" uuid NOT NULL, "Description" varchar(2000) NULL, "IsDeleted" boolean NOT NULL DEFAULT false,
    "CreatedBy" varchar(200) NULL, "CreatedAt" timestamptz NOT NULL DEFAULT now(), "UpdatedBy" varchar(200) NULL, "UpdatedAt" timestamptz NOT NULL DEFAULT now(),
    "PlantId"               uuid          NOT NULL REFERENCES "Plants" ("Id"),
    "Code"                  varchar(20)   NOT NULL,
    "Name"                  varchar(200)  NOT NULL,
    "WorkCenterId"          uuid          NOT NULL REFERENCES "WorkCenters" ("Id"),
    "Status"                smallint      NOT NULL DEFAULT 1,   -- 1 Active, 2 UnderMaintenance, 3 Retired
    "IdealCycleTimeSeconds" numeric(10,2) NOT NULL,
    "SerialNo"              varchar(100)  NULL,
    "IsActive"              boolean       NOT NULL DEFAULT true
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Machines_TenantId_PlantId_Code" ON "Machines" ("TenantId", "PlantId", "Code");
CREATE INDEX IF NOT EXISTS "IX_Machines_WorkCenter" ON "Machines" ("TenantId", "WorkCenterId");

CREATE TABLE IF NOT EXISTS "StandardOperations" (
    "Id" uuid NOT NULL PRIMARY KEY, "TenantId" uuid NOT NULL, "Description" varchar(2000) NULL, "IsDeleted" boolean NOT NULL DEFAULT false,
    "CreatedBy" varchar(200) NULL, "CreatedAt" timestamptz NOT NULL DEFAULT now(), "UpdatedBy" varchar(200) NULL, "UpdatedAt" timestamptz NOT NULL DEFAULT now(),
    "PlantId"             uuid          NOT NULL REFERENCES "Plants" ("Id"),
    "Code"                varchar(20)   NOT NULL,
    "Name"                varchar(200)  NOT NULL,
    "DefaultWorkCenterId" uuid          NOT NULL REFERENCES "WorkCenters" ("Id"),
    "SetupTimeMinutes"    numeric(10,2) NOT NULL DEFAULT 0,
    "RunTimeMinutes"      numeric(10,4) NOT NULL,
    "RunTimeBasis"        smallint      NOT NULL DEFAULT 1,     -- 1 PerUnit, 2 PerBatch
    "BatchSize"           numeric(18,6) NULL,
    "Instructions"        text          NULL,
    "IsActive"            boolean       NOT NULL DEFAULT true
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_StandardOperations_TenantId_PlantId_Code" ON "StandardOperations" ("TenantId", "PlantId", "Code");

CREATE TABLE IF NOT EXISTS "ReasonCodes" (
    "Id" uuid NOT NULL PRIMARY KEY, "TenantId" uuid NOT NULL, "Description" varchar(2000) NULL, "IsDeleted" boolean NOT NULL DEFAULT false,
    "CreatedBy" varchar(200) NULL, "CreatedAt" timestamptz NOT NULL DEFAULT now(), "UpdatedBy" varchar(200) NULL, "UpdatedAt" timestamptz NOT NULL DEFAULT now(),
    "Type"     smallint     NOT NULL,   -- 1 Scrap, 2 Rework, 3 Downtime, 4 Cancel, 5 Hold, 6 ShortClose, 7 UnplannedMaterial, 8 OverIssue
    "Code"     varchar(20)  NOT NULL,
    "Name"     varchar(200) NOT NULL,
    "Category" smallint     NULL,       -- downtime only: 1 Planned, 2 Unplanned
    "IsActive" boolean      NOT NULL DEFAULT true
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_ReasonCodes_TenantId_Type_Code" ON "ReasonCodes" ("TenantId", "Type", "Code");

CREATE TABLE IF NOT EXISTS "Crews" (
    "Id" uuid NOT NULL PRIMARY KEY, "TenantId" uuid NOT NULL, "Description" varchar(2000) NULL, "IsDeleted" boolean NOT NULL DEFAULT false,
    "CreatedBy" varchar(200) NULL, "CreatedAt" timestamptz NOT NULL DEFAULT now(), "UpdatedBy" varchar(200) NULL, "UpdatedAt" timestamptz NOT NULL DEFAULT now(),
    "PlantId"          uuid         NOT NULL REFERENCES "Plants" ("Id"),
    "Code"             varchar(20)  NOT NULL,
    "Name"             varchar(200) NOT NULL,
    "WorkCenterId"     uuid         NULL REFERENCES "WorkCenters" ("Id"),
    "LeaderEmployeeId" uuid         NULL,                 -- MasterData employee, checked through the internal lookup
    "IsActive"         boolean      NOT NULL DEFAULT true
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Crews_TenantId_PlantId_Code" ON "Crews" ("TenantId", "PlantId", "Code");

CREATE TABLE IF NOT EXISTS "CrewMembers" (
    "Id" uuid NOT NULL PRIMARY KEY, "TenantId" uuid NOT NULL, "Description" varchar(2000) NULL, "IsDeleted" boolean NOT NULL DEFAULT false,
    "CreatedBy" varchar(200) NULL, "CreatedAt" timestamptz NOT NULL DEFAULT now(), "UpdatedBy" varchar(200) NULL, "UpdatedAt" timestamptz NOT NULL DEFAULT now(),
    "CrewId"           uuid          NOT NULL REFERENCES "Crews" ("Id"),
    "EmployeeId"       uuid          NOT NULL,
    "ValidFrom"        date          NOT NULL,
    "ValidTo"          date          NULL,
    "LaborRatePerHour" numeric(18,4) NULL
);
CREATE INDEX IF NOT EXISTS "IX_CrewMembers_TenantId_EmployeeId" ON "CrewMembers" ("TenantId", "EmployeeId");
