# Token revocation — how a sign-out, lock or role change reaches every service

Security review: [SecurityReview-2026-11.md](SecurityReview-2026-11.md), wave S4a (SEC-M8, and SEC-M9 for the Redis part).

- **Code:**
  - Identity: `TokenRevocationQueue`, `UnitOfWork` (staging), `RevocationOutboxPublisher`, `RevocationOutboxMessage`.
  - Shared: `ITokenRevocationStore` / `RedisTokenRevocationStore`, `TokenRevocationValidator`, `EnsureSharedStoreOutsideDevelopment`.
- **SQL:** `Identity/040_RevocationOutbox.sql`.

## 1. Why it exists

Access tokens are checked by every service on its own, with Identity's public key. Without a shared "revoked" list:
- a signed-out token would keep working until it expires (5 minutes in the back office, Sessions.md);
- so would the token of a locked account, or one whose role was removed.

The **revocation store** (Redis, key prefix `erp:authz:`) holds three things:
- revoked token ids;
- a "changed at" marker per user;
- a "changed at" marker per tenant.

Every service, the Gateway and the hubs refuse a token whose id is revoked, or that was issued before its user's or tenant's marker.

## 2. Actors and triggers

| Trigger (IdentityService) | Revocation |
|---|---|
| Sign-out | The access token (its `jti`) |
| Password change / reset, lock, deactivation, role assigned / removed, sessions revoked | Every token of the user (user marker) |
| A role's permissions edited / role deleted | Every holder of the role |
| Tenant status, licence | Every token of the tenant (tenant marker) |

## 3. Business rules

| Rule | Statement |
|---|---|
| BR-REV-01 | A revocation is written to the **revocation outbox in the same database commit** as the change that caused it. A rolled-back change leaves no revocation; a committed one always has its revocation. |
| BR-REV-02 | Right after the commit, the revocation is published to the store and marked *published*. A failure never fails the user's request: the change is done. |
| BR-REV-03 | A revocation that could not be published is **never dropped**. The publisher retries every 5 s, oldest first, until the store accepts it. The 3rd failed attempt of a row is logged as an error. |
| BR-REV-04 | A marker carries the **time of the change**, not the time it was published. A late retry therefore never revokes tokens issued legitimately after the change, and a marker is never lowered by an older change. |
| BR-REV-05 | A service outside Development does not start without the shared store (`ConnectionStrings:Redis`). Otherwise it would silently accept revoked tokens. A single-process setup may opt in with `TokenRevocation:AllowInMemory=true`. |
| BR-REV-06 | The store requires a password (`redis-server --requirepass`; every connection string carries it). Without one, anything on the network could delete revocations. |
| BR-REV-07 | Reading the store fails open, as before: when Redis cannot be reached, a token is judged by its signature and lifetime only. The short access-token lifetime limits the exposure, and the outbox delivers the revocation when Redis returns. |
| BR-REV-08 | Each service keeps a token's revocation state in memory for `Security:Revocation:LocalCacheSeconds` (default **5 s**), to avoid a Redis call per request. A revocation therefore takes effect **within 5 seconds** on a service that has just seen the token, not instantly (verified live 2026-11-18). |

## 4. Process and state

```
change committed ──► outbox row (Pending) ──► publish now ── ok ──► Published
                                │                  └─ failure ──► Pending (Attempts+1, LastError)
                                └──────────── publisher every 5 s ──► Published
```

Row: **Pending → Published**. There is no failed end state: the row stays Pending until it is published.

## 5. Data

`RevocationOutbox` (AuthDB) columns:
- `Kind`: 1 Token / 2 Users / 3 Tenant;
- `Subject`: the jti, user id or tenant id;
- `ExpiresAt`: for a token;
- `ChangedAtUnix`, `CreatedAt`, `PublishedAt`, `Attempts`, `LastError`.

There is a filtered index on pending rows. Rows are kept as an audit trail; clean-up is an open item.

## 6. Operations

- **Health:** `SELECT COUNT(*) FROM RevocationOutbox WHERE PublishedAt IS NULL` must stay near 0. Rows with a high `Attempts` mean the store is unreachable.
- **Configuration:**
  - compose: `REDIS_PASSWORD` in `.env`;
  - Kubernetes: `erp-secrets` keys `redis-password` / `redis-connection`.

## 7. Open issues

- Old published rows are not purged yet.
- An alert on the pending count (monitoring) is not wired.

## 8. UAT

| # | Scenario | Expected |
|---|---|---|
| UAT-REV-01 | Sign in, sign out, wait 6 s (BR-REV-08), call another service (Sales) with the old access token | 401; the outbox row is Published |
| UAT-REV-02 | Stop Redis, lock a user, start Redis | Within seconds the row is Published; the user's old token is refused |
| UAT-REV-03 | A rolled-back change (an error inside a transaction) | No outbox row, nobody signed out |
| UAT-REV-04 | Start a service in Production without `ConnectionStrings:Redis` | It refuses to start and names the setting |
| UAT-REV-05 | `redis-cli` without a password | `NOAUTH` |
