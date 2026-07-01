# Geography (Country → Province → District → Ward)

**Service:** SharedMasterDataService (Port 5015, Code: MST)
**Database:** MasterDataDB

## Purpose

Four-level administrative geography hierarchy for address entry/validation across any
service (CRM contacts, sales/shipping addresses, employee addresses, etc.).

## Hierarchy

```
Country (Code, Name, PhoneCode?)
  └─ Province (CountryId, Code, Name)
       └─ District (ProvinceId, Code, Name)
            └─ Ward (DistrictId, Code, Name)
```

Each level is translatable via its own `{Level}Translation` table (Name per language
code). Low-churn reference data — no domain events are published for CRUD on these
entities; consumers read via
`GET /api/v1/masterdata/geography/{countries|provinces|districts|wards}`.

## Seeding

This schema ships empty (structure only) — populating full country/province/district/
ward data (e.g. Vietnam's 63 provinces) is an operational/data-seeding task, not part
of the service's code.
