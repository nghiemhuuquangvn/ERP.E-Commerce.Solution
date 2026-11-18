# CI/CD — GitHub Actions → GHCR → Kubernetes

> Áp dụng từ 2026-11-17. Thay thế `.github/workflows/docker-cicd.yml` cũ (chỉ Identity, trỏ nhánh `main`
> không tồn tại, file `Dockerfile.yml` không tồn tại → chưa từng chạy được).
> Liên quan: [per-service-deploy-guide.md](per-service-deploy-guide.md) (deploy tay),
> [deployment-guide.md](deployment-guide.md), `CLAUDE.md` (luật kiến trúc mà pipeline kiểm tra).

## 0. Tóm tắt một trang

| Sự kiện | Chạy gì | Kết quả |
|---|---|---|
| Mở / cập nhật **Pull Request** vào `master` | Chỉ pipeline của service / app có file bị đổi + `ci · platform` | Build, test, build image + quét lỗ hổng. **Không push, không deploy.** Đỏ = không merge được. |
| **Merge vào `master`** | Như trên + push image `ghcr.io/<owner>/erp-<service>:sha-xxxxxxx` | Tự động deploy **staging** (khi `K8S_DEPLOY_ENABLED=true`), tự rollback nếu pod mới không lên. |
| Đổi `src/Database/**` trên `master` | `db · migrations` | Image migration + K8s Job chạy migration trên staging. |
| **Đẩy tag `vX.Y.Z`** | `release` | **Một lần duyệt**, rồi deploy production theo thứ tự: migrations → backend → Warehouse → Manufacturing → web. Image được *promote* (không build lại). |
| Bấm tay (*Run workflow*) | Bất kỳ `svc-*`, `web-*`, `mobile` (EAS build) | Chạy lại pipeline của một service; build APK theo tenant. |

Ba nguyên tắc xuyên suốt:
1. **Một service = một pipeline độc lập** (đúng tinh thần microservice): đổi Payment không build lại Identity.
2. **Image bất biến**: deploy theo *digest* `sha256:…`, không bao giờ theo `latest`. Cái đã chạy trên staging chính là cái lên production.
3. **Cổng chất lượng chặn trước khi đóng gói**: test đỏ → không có image; image có lỗ hổng CRITICAL đã có bản vá → không push.

## 1. Bản đồ file

```
.github/
  services.json                    bảng 19 service (dùng cho release.yml)
  check-services.js                kiểm tra services.json == svc-*.yml == docker-compose.yml == k8s manifests
  dependabot.yml                   PR cập nhật thư viện hàng tuần
  actions/k8s-rollout/action.yml   set image + rollout status + tự rollback (dùng chung)
  workflows/
    _dotnet-service.yml            pipeline TÁI SỬ DỤNG cho 1 service .NET (logic nằm ở đây)
    _frontend-web.yml              pipeline TÁI SỬ DỤNG cho 1 SPA (admin / storefront)
    svc-<service>.yml  (x19)       "caller" ~40 dòng: build CÁI GÌ, KHI NÀO (path filter)
    web-admin.yml, web-storefront.yml
    mobile.yml                     3 app Expo: tokens + Jest; EAS build bằng tay
    ci-platform.yml                cổng chung: actionlint, architecture tests, schema drift SQL/PG
    db-migrations.yml              image + K8s Job migration (gọi được từ release.yml)
    release.yml                    tag v* → production theo đợt
deployment/
  docker/Dockerfile.migrations (+ .dockerignore)   2 target: sqlserver, postgres
  docker/run-migrations.sh         + bỏ qua Manufacturing (PostgreSQL) và (tùy chọn) dữ liệu demo
  k8s/jobs/db-migrate.yaml, pg-migrate.yaml, run-migration-jobs.sh
  k8s/deployments/*.yaml           + imagePullSecrets: ghcr-pull
```

19 pipeline backend: `gateway, identity, salesorder, purchaseorder, warehouse, accounting, crm, hrm, product, payment,
shipping, pos, promotion, notification, reporting, masterdata, logging, chatbot, manufacturing`.

## 2. Luồng tổng thể

```
 feature branch ──PR──► [svc-X: build+test → image build + Trivy]   [ci-platform]   (không push)
                             │ merge
                             ▼
 master ──────────► [svc-X: build+test → image → push sha-xxxx] ──► deploy STAGING (tự động, tự rollback)
                             │ git tag v1.4.0 && git push origin v1.4.0
                             ▼
 release ─► plan ─► APPROVE (1 người duyệt) ─► migrations ─► backend (17) ─► warehouse ─► manufacturing ─► admin + storefront
                                                  (production, mỗi bước chờ bước trước xanh)
```

## 3. Pipeline backend (`_dotnet-service.yml`) — chi tiết quan trọng

### 3.1 Khi nào chạy — *path filter*
Mỗi `svc-<service>.yml` chỉ kích hoạt khi một trong các đường dẫn sau đổi:

| Đường dẫn | Vì sao |
|---|---|
| `src/services/<Service>/**` | code của chính service |
| `src/Shared/**` | **mọi** service tham chiếu `ERP.Shared.*` (BaseEntity, tenant filter, outbox, authorization…) — đổi Shared thì *tất cả* service phải build + test lại. Đây là chủ ý, không phải lãng phí. |
| `tests/unit/ERP.<CODE>.UnitTests/**` | test của service |
| `Dockerfile`, `.dockerignore` | file build dùng chung |
| chính workflow + `_dotnet-service.yml` + action rollout | đổi pipeline thì chạy lại để kiểm chứng |

`src/Database/**` **không** kích hoạt service: image service không chứa script SQL. Migration có pipeline riêng (§7) và được
kiểm tra khớp EF model ở `ci-platform` (§9).

### 3.2 Job `build-test`
- `dotnet build` project API ở Release + `dotnet test` project unit test của service (xuất `.trx` + coverage, tải lên artifact 14 ngày).
- Cache NuGet theo hash của mọi `.csproj` (repo không có `packages.lock.json`).
- ChatBot và Logging **chưa có** unit test project → chỉ build (ghi nhận rủi ro R327).

### 3.3 Job `image`
- Dùng **chính `Dockerfile` gốc** với build-arg giống hệt `docker-compose.yml` (`SERVICE_PATH`, `PROJECT_FILE`, `ASSEMBLY_NAME`) — CI và máy dev build cùng một cách.
- **Tag** (docker/metadata-action): `sha-<7 ký tự commit>` (bất biến), `master`, `latest` (chỉ master, để tiện xem — **không dùng để deploy**), `pr-<n>`, `X.Y.Z` khi release.
- **Build 2 lần có chủ đích**: lần 1 `load` vào Docker local để **Trivy** quét *trước khi* push; lần 2 trúng cache 100% rồi push kèm **SBOM** và **provenance** (chứng thực nguồn gốc build).
- **Trivy**: fail khi có lỗ hổng `CRITICAL` **đã có bản vá** (`ignore-unfixed`) — lỗ hổng chưa có bản vá không chặn (không làm gì được), lỗ hổng có bản vá thì phải cập nhật base image / package.
- **Cache** BuildKit trên GitHub (`type=gha`, scope theo service) → build lại thường chỉ mất vài chục giây.
- PR: build + quét nhưng **không push**. `master`: push lên GHCR bằng `GITHUB_TOKEN` (không cần secret thêm).
- Output `digest` (`sha256:…`) được truyền cho job deploy.

### 3.4 Promote khi release (không build lại)
Trên tag `vX.Y.Z`, job `image` kiểm tra `ghcr.io/…/erp-<svc>:sha-<commit>` đã có chưa (master đã build commit này).
- Có → `docker buildx imagetools create` gắn thêm tag `vX.Y.Z` cho **đúng manifest đó** (cùng digest) → production chạy *byte-for-byte* cái đã chạy staging.
- Chưa có (service không đổi ở commit đó) → build như bình thường.

### 3.5 Concurrency
- Cùng service + cùng nhánh: PR mới **hủy** run cũ (tiết kiệm phút chạy); trên `master` **không hủy** (không bỏ dở một lần deploy).
- Deploy: một hàng đợi theo `deploy-<env>-<service>` — hai lần deploy cùng service không bao giờ chạy song song.

## 4. Pipeline web (`_frontend-web.yml`) — admin, storefront

| Bước | Lệnh | Bắt lỗi gì |
|---|---|---|
| Install | `npm ci` (storefront: `npm ci --legacy-peer-deps`, giống Dockerfile của nó) | lockfile lệch |
| Design tokens | `npm run check:tokens` | màu không sinh từ `tokens.json` (luật "no hex in app code") |
| Lint | `npm run lint` — **đang tắt cho admin** vì app chưa có `eslint.config.js` (ESLint 9), R325 | |
| Test | `npm test` (Vitest + axe) | hồi quy UI, lỗi accessibility |
| Build | `tsc -b && vite build` / `vue-tsc -b && vite build` | lỗi type |
| Image | build context `src/frontend/<app>`, Trivy, push, promote — y hệt backend | |

Image SPA **không chứa địa chỉ môi trường**: `docker-entrypoint.sh` của nginx điền lúc container khởi động → cùng một image dùng cho staging và production.
Path filter có thêm `docs/frontend/design-system/**` vì `check:tokens` đọc file ở đó.

## 5. Mobile (`mobile.yml`)
- PR / master: ma trận `admin | customer | staff` → `npm ci` → `check:tokens` → `jest --ci` (gồm test parity `components/ui` giữa staff và admin, test module registry).
- **Không deploy lên K8s.** Build APK bằng tay: *Actions → mobile → Run workflow*, chọn `app`, `tenant_code`, `api_url`.
  Job ghi 2 giá trị này vào `eas.json` (profile `preview`) của bản checkout — vì `env` trong `eas.json` thắng biến môi trường trên máy build EAS — rồi `eas build --no-wait`. Cần secret `EXPO_TOKEN`.
  Mobile Admin: **một APK cho mỗi tenant** (tenant = host, địa chỉ được nướng vào bundle) — xem `docs/frontend/mobile-admin-app.md`.

## 6. Deploy và rollback (`actions/k8s-rollout`)
1. Ghi kubeconfig (secret của *environment*) ra file tạm `chmod 600`, xóa khi kết thúc.
2. `kubectl set image deployment/<dep> <container>=ghcr.io/…@sha256:<digest>` — chỉ đổi image, **không** `kubectl apply` lại manifest (env, secret, probe giữ nguyên như trên cluster).
3. Ghi `kubernetes.io/change-cause` = commit / release → `kubectl rollout history` cho biết bản nào đến từ đâu.
4. `kubectl rollout status --timeout` (staging 180s, production 300s). Pod mới phải **Ready** = `readinessProbe GET /health` trả 200.
5. Hết giờ / pod crash → in danh sách pod, **`kubectl rollout undo`** về ReplicaSet trước, job đỏ (có thông báo trên GitHub).

Rollback tay về bất kỳ bản nào:
```bash
kubectl -n erp rollout history deployment/payment-service
kubectl -n erp rollout undo deployment/payment-service --to-revision=<n>
# hoặc chạy lại pipeline của commit cũ: Actions → svc · payment → Run workflow (chọn commit/nhánh)
```
Lưu ý: rollback image **không** rollback database — migration chỉ được *thêm*, phải tương thích ngược (§7.3).

Manifest trong repo vẫn ghi `erp/<x>:latest` để `deployment/k8s/apply-all.ps1` chạy được trên Docker Desktop. **Chạy lại `apply-all.ps1` trên cluster
do CI quản lý sẽ đưa image về `latest` local** → trên staging/production chỉ đổi image qua pipeline.

## 7. Database migrations

### 7.1 Tại sao tách khỏi service
Service **không** tự migrate khi khởi động (EF không tạo schema — repo dùng SQL viết tay, append-only, idempotent). Migration là một bước riêng,
chạy **trước** service trong release, vì: nhiều replica cùng khởi động sẽ đua nhau migrate; migration lỗi phải dừng release chứ không làm crash-loop pod.

### 7.2 Cách chạy
- `deployment/docker/Dockerfile.migrations` 2 target:
  `sqlserver` (image `mssql/server` chỉ để có `sqlcmd`; chứa `src/Database/**` + `deployment/sql/init-databases.sql` + `run-migrations.sh`) và
  `postgres` (chứa `src/Database/Manufacturing` + `run-pg-migrations.sh`).
- `run-migration-jobs.sh` tạo K8s Job `db-migrate-<run>` rồi `pg-migrate-<run>`, chờ `Complete`/`Failed`, in log, **dừng ngay** nếu job đầu lỗi.
  `backoffLimit: 0` (migration lỗi phải được người xem, không thử lại mù), log giữ 7 ngày.
- Cần trong `erp-secrets`: `sql-host`, `sql-sa-password` (mới, xem `secrets.yaml.example`), `pg-user`, `pg-password`.

### 7.3 Quy tắc an toàn (bắt buộc)
| Quy tắc | Lý do |
|---|---|
| Script **append-only, idempotent** (`IF NOT EXISTS`), chạy lại bao nhiêu lần cũng được | Job có thể chạy lại; drift test PG chạy script **2 lần** để chứng minh |
| Thay đổi **tương thích ngược** (thêm cột nullable/có default; xóa cột ở release *sau* khi code không còn dùng) | Lúc rollout, pod cũ và mới chạy song song trên cùng schema; rollback image không rollback DB |
| **`SKIP_DEMO_SEED=true`** trong image migration (và trong CI) bỏ các file `*SeedDemo*` | Dữ liệu demo có user dùng chung mật khẩu đã biết, đơn hàng giả — không bao giờ lên cluster. Seed quyền / role / template **vẫn chạy** (không phải demo). Stack local (`docker compose`) không đổi hành vi. |
| `run-migrations.sh` **bỏ qua thư mục `Manufacturing`** | Đó là script PostgreSQL; trước đây chạy nhầm vào SQL Server (sửa 2026-11-17) |

## 8. Release production (`release.yml`)
1. Owner: `git tag v1.4.0 <commit trên master> && git push origin v1.4.0`.
2. `plan`: từ chối tag không nằm trên `master`; đọc `.github/services.json` để chia đợt.
3. `approve`: **một lần duyệt** (environment `release-approval` có required reviewers). Environment `production` không có reviewer, chỉ cho phép tag `v*` → các đợt sau chạy không hỏi lại.
4. Thứ tự (theo `CLAUDE.md`, mỗi đợt chờ đợt trước xanh):

| Đợt | Nội dung | Lý do thứ tự |
|---|---|---|
| 1 | migrations (SQL Server rồi PostgreSQL) | code mới cần schema mới; script Identity (quyền) phải có trước Manufacturing |
| 2 | 17 service còn lại (tối đa 6 song song, `fail-fast`) | *consumer* phải lên trước Warehouse ("decimal quantities: consumers deploy before the Warehouse") |
| 3 | Warehouse | |
| 4 | Manufacturing | "deploy order Warehouse, Identity script, `pg-migrate`, ManufacturingService, Admin" |
| 5 | admin, storefront | màn hình lên sau khi API nó gọi đã sẵn sàng |

Một đợt đỏ → các đợt sau **không chạy**; service đã lên ở đợt đó tự rollback; các đợt trước giữ bản mới (tương thích ngược theo §7.3). Sửa lỗi → tag mới `v1.4.1`.

## 9. Cổng chung `ci · platform` (nên đặt là *required check* của `master`)
| Job | Kiểm tra |
|---|---|
| `workflows` | `actionlint` (cú pháp, biểu thức, shellcheck mọi `run:`) + `check-services.js` (bảng release khớp pipeline, compose, manifest; file `.csproj` tồn tại đúng hoa/thường trên Linux) |
| `architecture` | `ERP.Architecture.Tests` (tenant isolation, mọi endpoint có authorization, không `Jwt:Key`, không tenant fallback trong `deployment/`, `/internal` không qua Gateway, refusal baseline, stock ledger…) + `ERP.Shared.UnitTests` (outbox, EventJson, revocation) |
| `drift-sql` | SQL Server trong runner → `run-migrations.sh` (`SKIP_DEMO_SEED=true`) → `LiveSchemaDriftTests`: mọi bảng/cột EF model cần đều có trong DB dựng từ script |
| `drift-pg` | PostgreSQL → `PostgreSqlSchemaDriftTests`: script Manufacturing chạy 2 lần, so với `MfgDbContext` |

Đây là các luật xuyên service trong `CLAUDE.md` — trước đây chỉ chạy khi ai đó nhớ chạy tay; giờ mọi PR đều qua.

## 10. Cài đặt một lần (owner)
| # | Việc | Ở đâu |
|---|---|---|
| 1 | Tạo environment `staging` (secret `KUBE_CONFIG_STAGING`) | Settings → Environments |
| 2 | Tạo environment `production` (secret `KUBE_CONFIG_PRODUCTION`, deployment rule: tag `v*`, **không** reviewer) | Settings → Environments |
| 3 | Tạo environment `release-approval` (required reviewers = người duyệt release) | Settings → Environments |
| 4 | Biến repo `K8S_DEPLOY_ENABLED=true` (chưa đặt = pipeline chỉ build/test/push, không deploy) | Settings → Variables |
| 5 | (tùy) secret `CI_SA_PASSWORD` (không đặt = dùng mật khẩu tạm của container CI, không lộ ra ngoài runner); `EXPO_TOKEN` cho EAS | Settings → Secrets |
| 6 | Trên mỗi cluster: `kubectl -n erp create secret docker-registry ghcr-pull --docker-server=ghcr.io --docker-username=<user> --docker-password=<PAT read:packages>` | cluster |
| 7 | Thêm `sql-host`, `sql-sa-password` vào `erp-secrets` | cluster |
| 8 | ServiceAccount riêng cho CI (quyền tối thiểu, bên dưới) → xuất kubeconfig dùng token của nó | cluster |
| 9 | Branch protection `master`: require PR + status checks `ci · platform` | Settings → Branches |
| 10 | **Đổi mật khẩu SuperAdmin** sau lần migrate đầu (script `Identity/001` tạo sẵn với mật khẩu mặc định, R326) | Admin |

Quyền tối thiểu cho kubeconfig của CI (namespace `erp`, không cluster-admin):
```yaml
apiVersion: rbac.authorization.k8s.io/v1
kind: Role
metadata: { name: ci-deployer, namespace: erp }
rules:
- apiGroups: [apps]
  resources: [deployments]
  verbs: [get, list, watch, patch]          # set image, annotate, rollout status/undo
- apiGroups: [apps]
  resources: [replicasets]
  verbs: [get, list, watch]                 # rollout undo / status
- apiGroups: [""]
  resources: [pods, pods/log]
  verbs: [get, list]                        # in pod khi lỗi, log migration
- apiGroups: [batch]
  resources: [jobs]
  verbs: [get, list, create, patch]         # migration Jobs
```
(+ `RoleBinding` cho ServiceAccount `ci-deployer`.) Không cấp quyền đọc `secrets`: CI không cần và không được thấy secret ứng dụng.

## 10a. Cập nhật thư viện (Dependabot) — quy tắc sau sự cố 2026-11-18

**Sự cố:** lô PR Dependabot #112–#136 được merge vào `master` khi CI chưa xanh (chưa bật branch protection). Kết quả:
| Hỏng | Nguyên nhân |
|---|---|
| Admin `npm ci` lỗi `ERESOLVE` | `@vitejs/plugin-react` 6 (cần Vite 8) trên Vite 5; kèm Vitest 5, Tailwind 4, lucide-react 1.x |
| Storefront chỉ cài được nhờ `--legacy-peer-deps` | Vite 8 + plugin-vue 6 + TypeScript 7 trong khi Vitest 2 chỉ hỗ trợ Vite 5 |
| **Mọi image backend không khởi động** | `Dockerfile` lên `dotnet/sdk|aspnet:11.0` nhưng 88 project là `net10.0` (runtime không roll-forward qua major) |
| Mọi service lỗi restore `NU1605` (package downgrade = lỗi) | NuGet `directory: /` chỉ thấy 19/107 `.csproj` trong `.sln` → nâng `ERP.Shared` (Confluent.Kafka 2.16, JwtBearer 10.0.15…) mà không nâng service |
| Thêm giấy phép | FluentAssertions 8 cần license trả phí cho dùng thương mại |

**Đã sửa (nhánh `fix/dependabot-majors`):** major trả về bản cũ (Vite 5, plugin-react 4, plugin-vue 5, Vitest 2, Tailwind 3, TS 5, lucide 0.468,
Asp.Versioning 8.1, CsvHelper 33, FluentAssertions 6, FluentValidation 11, xunit.runner 2.5.6, image .NET 10.0); minor/patch **giữ** và
**đồng bộ cho mọi project** (Microsoft.* 10.0.15, Jwt 8.23, Confluent.Kafka 2.16, Moq 4.21, xunit 2.9.3…).

**Quy tắc từ nay (BR-CICD-06..08):**
- BR-CICD-06 Dependabot chỉ mở PR **minor / patch** (npm, NuGet, Docker); **major là một task có kế hoạch** (nhánh riêng, sửa code/config, test).
- BR-CICD-07 NuGet quét **mọi project** (`/src/**/*`, `/tests/**/*`) và gom vào **một** PR — một package luôn cùng version ở mọi project.
- BR-CICD-08 Chỉ merge PR (kể cả Dependabot) khi **mọi check xanh** → bắt buộc bật branch protection (§10 bước 9).
- Base image `dotnet/sdk` / `aspnet` phải cùng major với `<TargetFramework>`; nâng .NET = đổi cả hai trong một PR.

Việc nâng major còn chờ (R338): Vite 8 + plugin-react 6 / plugin-vue 6 + Vitest 5, Tailwind 4 (đổi cấu hình), TypeScript 7, lucide-react 1.x,
FluentValidation 12, CsvHelper 34, Asp.Versioning 10, .NET 11 (khi ra LTS/được duyệt). FluentAssertions 8: cân nhắc license trước.

## 11. Thêm một service mới
1. Thêm service vào `docker-compose.yml` và `deployment/k8s/deployments/<x>.yaml` như thường lệ.
2. Copy một `svc-*.yml`, sửa tên, path filter, 7 giá trị `with:`.
3. Thêm một dòng vào `.github/services.json` (cùng giá trị). Nếu service có ràng buộc thứ tự deploy, sửa đợt trong `release.yml`.
4. `node .github/check-services.js` phải in `OK` (CI cũng chạy).

## 12. Xử lý sự cố
| Triệu chứng | Nguyên nhân thường gặp | Làm gì |
|---|---|---|
| `build-test` đỏ | lỗi biên dịch / test | mở artifact `test-results-<svc>` (`.trx`) |
| Trivy đỏ | base image `mcr.microsoft.com/dotnet/aspnet:10.0` / `nginx` / package có CVE CRITICAL đã vá | rebuild để lấy base mới; cập nhật package; tạm thời (có lý do, có hạn) thêm `.trivyignore` |
| `drift-sql` báo `MISSING` | thêm cột/entity mà quên script SQL | viết migration mới (append-only) trong `src/Database/<Service>/` |
| `drift-pg` báo `SCRIPT … pass 2` | script Manufacturing không idempotent | thêm `IF NOT EXISTS` |
| `check-services.js` đỏ | bảng release lệch pipeline / manifest | sửa cho khớp (§11) |
| Deploy: `rollout status` hết giờ, đã rollback | pod không Ready: thiếu env/secret, `/health` lỗi, `ImagePullBackOff` | `kubectl -n erp describe pod …`; `ImagePullBackOff` = thiếu/sai `ghcr-pull` |
| Job migration `Failed` | lỗi SQL thật | `kubectl -n erp logs job/db-migrate-<run>`; sửa bằng script mới, tag release mới |
| Deploy bị bỏ qua (skipped) | chưa bật `K8S_DEPLOY_ENABLED` hoặc PR | đúng thiết kế |

## 13. Phân tích nghiệp vụ (BA)

### 13.1 Tác nhân
| Tác nhân | Vai trò |
|---|---|
| Developer | mở PR; nhận kết quả CI |
| Reviewer | duyệt PR (code) |
| Release approver | duyệt *một lần* trong `release-approval` |
| Owner / DevOps | cài đặt một lần (§10), xử lý sự cố |
| GitHub Actions | thực thi pipeline |
| Kubernetes cluster | chạy service, Job migration |

### 13.2 Yêu cầu và tiêu chí chấp nhận
| Mã | Yêu cầu | Tiêu chí chấp nhận |
|---|---|---|
| CICD-01 | Mỗi service có pipeline riêng | Đổi 1 file trong `PaymentService` → chỉ `svc · payment` (+ `ci · platform`) chạy |
| CICD-02 | Đổi Shared kiểm chứng mọi service | Đổi 1 file trong `src/Shared` → 19 `svc-*` chạy |
| CICD-03 | Không đóng gói code đỏ | Test đỏ → job `image` không chạy |
| CICD-04 | Không push image có CVE CRITICAL đã vá | Trivy đỏ → không có tag mới trên GHCR |
| CICD-05 | Image bất biến, truy được nguồn | Mỗi image master có tag `sha-<commit>` + SBOM + provenance; Deployment trỏ `@sha256:` |
| CICD-06 | Staging tự cập nhật | Merge vào master → trong ~15 phút staging chạy commit đó (`rollout history` ghi commit) |
| CICD-07 | Tự rollback | Image không lên được → Deployment về bản trước, job đỏ |
| CICD-08 | Production chỉ qua release có duyệt | Không có đường nào khác tới `KUBE_CONFIG_PRODUCTION` ngoài tag `v*` + 1 lần duyệt |
| CICD-09 | Đúng thứ tự deploy | migrations → backend → Warehouse → Manufacturing → web; đợt đỏ chặn đợt sau |
| CICD-10 | Production = đúng bản đã test | Release promote cùng digest đã build từ master |
| CICD-11 | Migration an toàn | Không file `*SeedDemo*` nào chạy trên cluster; Manufacturing không chạy trên SQL Server; Job lỗi dừng release |
| CICD-12 | Luật kiến trúc luôn được kiểm | Mọi PR chạy architecture tests + drift SQL/PG |

### 13.3 Quy tắc
- BR-CICD-01 Chỉ `master` deploy staging; chỉ tag `v*` trên `master` deploy production.
- BR-CICD-02 Deploy theo digest, không theo tag thay đổi được.
- BR-CICD-03 Migration append-only, idempotent, tương thích ngược; không seed demo trên cluster.
- BR-CICD-04 CI không đọc, không giữ secret ứng dụng (DB, Key Vault, `Security:SettingsEncryptionKey`) — chỉ kubeconfig quyền tối thiểu.
- BR-CICD-05 `services.json` và `svc-*.yml` phải khớp (kiểm bằng `check-services.js`).

### 13.4 Use case chính — "Release v1.4.0"
- **Luồng chính:** owner tag → `plan` hợp lệ → approver duyệt → migrations Complete → 17 service rollout OK → Warehouse → Manufacturing → admin + storefront → release xanh.
- **Luồng thay thế:** service không đổi từ lần build master trước → vẫn được *build* ở bước promote (không có `sha-` tương ứng) — chậm hơn nhưng đúng.
- **Ngoại lệ:** tag không nằm trên master → `plan` từ chối. Migration Failed → dừng, không service nào đổi. Một service rollout lỗi → service đó rollback, đợt sau không chạy, các service đã lên giữ bản mới; ra `v1.4.1`.

### 13.5 Kịch bản UAT
| # | Kịch bản | Kết quả mong đợi |
|---|---|---|
| UAT-1 | PR chỉ sửa `src/services/CRMService/...` | chỉ `svc · crm` + `ci · platform` chạy; không push |
| UAT-2 | PR thêm một property EF không có script SQL | `drift-sql` đỏ với dòng `MISSING` |
| UAT-3 | PR thêm controller thiếu attribute authorization | `architecture` đỏ (`EndpointAuthorizationTests`) |
| UAT-4 | Merge PR CRM (đã bật deploy) | staging `crm-service` chạy image `sha-<commit>`; `kubectl rollout history` có change-cause |
| UAT-5 | Cố ý deploy image có `/health` trả 500 | staging rollback, job đỏ, service vẫn phục vụ bản cũ |
| UAT-6 | Tag `v0.0.1-test` trên nhánh khác master | `plan` từ chối |
| UAT-7 | Tag hợp lệ, approver từ chối | không có gì thay đổi trên production |
| UAT-8 | Release đủ | thứ tự đợt đúng bảng §8; digest production = digest staging |
| UAT-9 | Kiểm tra DB production sau migrate | không có user demo của `015_SeedDemoUsersAndRoles` |
| UAT-10 | Run `mobile` với app=admin, tenant=glcheck | build EAS bắt đầu, `eas.json` dùng `TENANT_CODE=glcheck` |

### 13.6 Giả định và vấn đề mở
- Cluster staging/production đã có hạ tầng (`apply-all.ps1`: namespace, configmap, secrets, Redis, RabbitMQ, Kafka, ES, Postgres) — CI không dựng hạ tầng.
- Chưa có integration / E2E test chạy trong CI (`tests/integration/ProductService.IntegrationTests` không có `.csproj`; kịch bản `scripts/manufacturing-live` cần stack thật) — R328.
- Rủi ro chi tiết: `docs/DEFERRED-ISSUES.md` §6, R325–R330.

## 14. Kiểm chứng đã làm (2026-11-17, máy dev)
- `actionlint 1.7.7`: 0 lỗi trên toàn bộ workflow. `kubeconform`: 49 resource / 22 file hợp lệ. `check-services.js`: OK 19 service.
- Image migration build được cả 2 target; chạy với SQL Server rỗng: 16 DB, **0 lỗi SQL**, Manufacturing bỏ qua, demo seed bỏ qua.
- Trên DB đó: `LiveSchemaDriftTests` và `PostgreSqlSchemaDriftTests` **Passed**.
- `ERP.Architecture.Tests` 93/93, `ERP.Shared.UnitTests` 151/151, `ERP.Gateway.UnitTests` 44/44, `ERP.Identity.UnitTests` 342/342.
- Admin: tokens OK, Vitest 479/479, build OK (lint không có config). Storefront: tokens OK, 51/51, build OK. Mobile admin 248/248, customer 76/76, staff 68/68.
- **Chưa** chạy trên GitHub thật (cần push) và chưa deploy vào cluster thật (cần §10).
