# Per-Service Deploy Guide

> **Mục tiêu của tài liệu này:** khi chỉ sửa code 1-2 service, chỉ build/deploy đúng
> (những) service đó — không rebuild/rollout cả 19 service. Rebuild toàn bộ vừa
> chậm (mỗi image ~7-10s khi cache ấm, nhưng dồn cả 19 cái vẫn mất vài phút), vừa có
> rủi ro thật: build song song quá nhiều service từng làm BuildKit quá tải và crash
> Docker Desktop trong session này. Tài liệu chia làm 2 phần độc lập — **Docker
> Compose** và **Kubernetes** — chọn đúng phần đang dùng, xem
> [rebuild-and-redeploy-guide.md §1](rebuild-and-redeploy-guide.md#1-pick-your-target)
> nếu chưa chắc nên dùng cái nào, và **không bao giờ chạy cả hai stack cùng lúc**
> (xem mục "Cảnh báo" cuối file — đây là nguyên nhân đã gây crash Docker Desktop
> lặp lại nhiều lần).

## Mục lục

1. [Quy tắc quyết định: sửa gì → build lại gì](#1-quy-tắc-quyết-định-sửa-gì--build-lại-gì)
2. [Phần 1 — Docker Compose](#2-phần-1--docker-compose)
3. [Phần 2 — Kubernetes](#3-phần-2--kubernetes)
4. [Bảng tra cứu đầy đủ 19 service](#4-bảng-tra-cứu-đầy-đủ-19-service)
5. [Cảnh báo / lỗi thường gặp](#5-cảnh-báo--lỗi-thường-gặp)

---

## 1. Quy tắc quyết định: sửa gì → build lại gì

| Bạn vừa sửa... | Cần build lại | Vì sao |
|---|---|---|
| 1 file trong `src/services/<X>Service/**` | Chỉ **service đó** | Image của service khác không tham chiếu code này |
| `src/Gateway/Ocelot.API/**` hoặc `ocelot.json` | Chỉ **gateway** | `ocelot.json` được bake vào image Gateway lúc build, không phải config mount rời |
| `src/Shared/**` (Core/Contracts/Messaging/Authorization/Middleware...) | **Mọi service** tham chiếu project đó (thực tế gần như cả 19) | Đây là shared library — mọi service build lại từ source, không cache chung binary |
| `src/frontend/admin/**` | Chỉ **admin** | Build độc lập, context riêng (`src/frontend/admin`) |
| `src/frontend/storefront/**` | Chỉ **storefront** | Tương tự |
| Chỉ file `.sql` trong `src/Database/**` | **Không cần build/deploy gì** | Migration chạy thẳng vào DB qua `sqlcmd`, không nằm trong image |
| `deployment/k8s/deployments/<x>.yaml` | Không cần build lại image, chỉ `kubectl apply -f` lại đúng file đó | Đổi manifest (env, port, replicas...), không đổi code |

**Cách kiểm tra nhanh trước khi build:** `git status --short` hoặc `git diff --stat` để
biết chính xác những file nào đổi, từ đó suy ra đúng cột "Cần build lại" ở trên —
đừng đoán.

---

## 2. Phần 1 — Docker Compose

### 2.1 Deploy 1 service

```powershell
cd d:\Documents\ERP\Sources\ERP.E-Commerce.Solution

docker compose build salesorderservice        # build lại đúng 1 image
docker compose up -d salesorderservice         # recreate đúng 1 container
docker compose logs -f salesorderservice       # xem log, Ctrl+C khi thấy khởi động sạch
```

`docker compose up -d <service>` **chỉ** recreate container có image thực sự đổi —
các service khác không bị động tới, kể cả khi chúng đang chạy.

### 2.2 Deploy 2-3 service cùng lúc (vẫn tách biệt, không phải "deploy all")

```powershell
docker compose build salesorderservice purchaseorderservice
docker compose up -d salesorderservice purchaseorderservice
```

Build nhiều service trong 1 lệnh vẫn build **tuần tự từng cái** (không song song
toàn bộ), nên an toàn hơn nhiều so với `docker compose build` không tham số (build
cả 19 cái, dễ làm BuildKit quá tải nếu máy đang tải nặng).

### 2.3 Verify sau khi deploy

```powershell
docker compose ps salesorderservice                       # "Up", không restart loop
Invoke-WebRequest http://localhost:5002/health -TimeoutSec 5
```

### 2.4 Rollback 1 service

```powershell
docker compose down salesorderservice          # dừng + xóa container (giữ image cũ nếu chưa prune)
git checkout HEAD -- src/services/SaleOrderService     # hoặc sửa code lại
docker compose build salesorderservice
docker compose up -d salesorderservice
```

---

## 3. Phần 2 — Kubernetes

### 3.1 Deploy 1 service

```powershell
cd d:\Documents\ERP\Sources\ERP.E-Commerce.Solution

# Bước 1 — build image (giống hệt lệnh Compose — cùng Dockerfile, cùng tag :latest)
docker compose build salesorderservice

# Bước 2 — bắt buộc, vì imagePullPolicy: IfNotPresent + tag không đổi (:latest)
# nên Kubernetes sẽ KHÔNG tự nhận ra image mới nếu không được bảo restart
kubectl rollout restart deployment/salesorder-service -n erp

# Bước 3 — đợi rollout xong (khuyến nghị luôn chờ, đừng bỏ qua)
kubectl rollout status deployment/salesorder-service -n erp --timeout=180s
```

> **Lưu ý tên khác nhau:** lệnh build dùng tên compose-service
> (`salesorderservice`, không gạch nối), lệnh `kubectl rollout` dùng tên
> K8s Deployment (`salesorder-service`, có gạch nối). Bảng đầy đủ ở mục 4.

### 3.2 Build image bằng `docker build` thuần (không qua Compose)

Dùng khi muốn kiểm soát build-arg trực tiếp, hoặc debug lỗi build:

```powershell
docker build --build-arg SERVICE_PATH=src/services/SaleOrderService/src/ERP.SO.Api `
             --build-arg PROJECT_FILE=ERP.SO.Api.csproj `
             --build-arg ASSEMBLY_NAME=ERP.SO.Api `
             -t erp/salesorder-service:latest -f Dockerfile .
```

Ra đúng image tag `erp/salesorder-service:latest` mà manifest K8s tham chiếu — có
thể dùng thay thế bước 1 ở §3.1 hoàn toàn tương đương.

### 3.3 Verify sau khi deploy

```powershell
kubectl get pods -n erp -l app=salesorder-service          # phải "1/1 Running", RESTARTS=0
kubectl logs -n erp -l app=salesorder-service --tail=50     # không có Exception ngay sau start
kubectl describe pod -n erp -l app=salesorder-service | Select-String "Warning"   # probe/scheduling issues nếu có
```

### 3.4 Rollback 1 service

```powershell
kubectl rollout undo deployment/salesorder-service -n erp
kubectl rollout status deployment/salesorder-service -n erp
```

`rollout undo` quay lại **revision trước đó của chính Deployment này** (image +
manifest) — không ảnh hưởng service khác.

### 3.5 Khi chỉ đổi manifest YAML (không đổi code)

```powershell
kubectl apply -f deployment/k8s/deployments/salesorder.yaml
```

Không cần `docker compose build` hay `rollout restart` — `kubectl apply` tự phát
hiện thay đổi (env, replicas, resources...) và tự rollout nếu cần.

---

## 4. Bảng tra cứu đầy đủ 19 service

| # | Compose service (`docker compose build <this>`) | K8s Deployment (`kubectl rollout restart deployment/<this>`) | Port | `SERVICE_PATH` (cho `docker build` thuần) |
|---|---|---|---|---|
| 1 | `gateway` | `gateway` | 5000 | `src/Gateway/Ocelot.API` |
| 2 | `identityservice` | `identity-service` | 5001 | `src/services/IdentityService/src/ERP.Identity.Api` |
| 3 | `salesorderservice` | `salesorder-service` | 5002 | `src/services/SaleOrderService/src/ERP.SO.Api` |
| 4 | `purchaseorderservice` | `purchaseorder-service` | 5003 | `src/services/PurchaseOrderService/src/ERP.PO.Api` |
| 5 | `warehouseservice` | `warehouse-service` | 5004 | `src/services/WareHouseService/src/ERP.WH.API` |
| 6 | `accountingservice` | `accounting-service` | 5005 | `src/services/AccountingService/src/ERP.ACC.API` |
| 7 | `crmservice` | `crm-service` | 5006 | `src/services/CRMService/src/ERP.CRM.API` |
| 8 | `hrmservice` | `hrm-service` | 5007 | `src/services/HRMService/src/ERP.HRM.API` |
| 9 | `productservice` | `product-service` | 5008 | `src/services/ProductService/src/ERP.PRD.API` |
| 10 | `paymentservice` | `payment-service` | 5009 | `src/services/PaymentService/src/ERP.PAY.API` |
| 11 | `shippingservice` | `shipping-service` | 5010 | `src/services/ShippingService/src/ERP.SHP.API` |
| 12 | `posservice` | `pos-service` | 5011 | `src/services/POSService/src/ERP.POS.API` |
| 13 | `promotionservice` | `promotion-service` | 5012 | `src/services/PromotionService/src/ERP.PRM.API` |
| 14 | `notificationservice` | `notification-service` | 5013 | `src/services/NotificationService/src/ERP.NTF.API` |
| 15 | `reportingservice` | `reporting-service` | 5014 | `src/services/ReportingService/src/ERP.RPT.API` |
| 16 | `masterdataservice` | `masterdata-service` | 5015 | `src/services/SharedMasterDataService/src/ERP.MST.API` |
| 17 | `loggingservice` | `logging-service` | 5016 | `src/services/LoggingService/src/ERP.LOG.API` |
| 18 | `admin` | `admin` | 3000 (Compose) / 80 (K8s, qua Service) | `src/frontend/admin` (context riêng — không dùng root Dockerfile) |
| 19 | `storefront` | `storefront` | 3001 (Compose) / 80 (K8s, qua Service) | `src/frontend/storefront` (context riêng) |

`PROJECT_FILE`/`ASSEMBLY_NAME` (chỉ cần khi build bằng `docker build` thuần, §3.2)
luôn có dạng `<tên cuối của SERVICE_PATH>.csproj` — vd service #3:
`ERP.SO.Api.csproj` / `ERP.SO.Api`. Tra chính xác trong `docker-compose.yml` nếu
không chắc (mỗi service có khối `build.args` riêng).

---

## 5. Cảnh báo / lỗi thường gặp

**Không bao giờ chạy `docker compose up` và Kubernetes cùng lúc.** Cả 2 đều dựng
SQL Server/Kafka/RabbitMQ/Redis/Elasticsearch **riêng** — chạy song song gần như
gấp đôi tải CPU/RAM trên VM Docker Desktop. Đây là nguyên nhân đã gây crash Docker
Desktop lặp đi lặp lại trong thực tế (phát hiện 2026-08-07: một stack compose chạy
ngầm 5 tiếng cùng lúc với K8s, nhiều container crash-loop, `kubectl`/`docker build`
timeout liên tục cho tới khi dừng hẳn stack compose). Trước khi chuyển sang test
K8s, luôn chạy `docker compose down` trước.

**Sửa `ocelot.json`/Gateway nhưng chỉ deploy service downstream → Gateway vẫn phục
vụ route cũ.** `ocelot.json` bake vào image Gateway lúc build, không phải config
đọc động. Thêm/sửa route → phải rebuild + rollout **cả Gateway lẫn service liên
quan**, không chỉ 1 trong 2.

**Sửa `src/Shared/**` → coi như sửa mọi service.** Không có cách "chỉ deploy 1
service" trong trường hợp này — phải rebuild tuần tự tất cả service tham chiếu
project đó (thực tế gần như toàn bộ 19, vì tất cả đều tham chiếu `ERP.Shared`/
`ERP.Shared.Core`). Build tuần tự từng cái (không dùng `docker compose build` không
tham số cho toàn bộ cùng lúc) để tránh quá tải BuildKit.

**`imagePullPolicy: IfNotPresent` + tag `:latest` không đổi → K8s không tự nhận
image mới.** Luôn phải `kubectl rollout restart` sau khi build lại — build xong mà
quên bước này là lỗi phổ biến nhất khi chỉ deploy 1 service.

**Tên compose-service ≠ tên K8s Deployment ≠ tên K8s Service.** Compose không có
gạch nối (`salesorderservice`), K8s Deployment/Service có gạch nối
(`salesorder-service`) — nhưng K8s **Service** object (DNS name nội bộ mà
`ocelot.json` gọi tới) lại **không** có gạch nối, khớp với tên compose-service. 3
tên khác nhau cho cùng 1 service — dùng sai tên ở bước `kubectl rollout restart`
sẽ ra lỗi `deployment.apps "salesorderservice" not found` (thiếu gạch nối) hoặc
gọi nhầm sang service khác nếu gõ tên gần giống. Xem bảng §4 để tra chính xác.

Xem thêm các lỗi hạ tầng khác (probe timeout, thiếu `envFrom`, PVC giữ credential
cũ...) tại [rebuild-and-redeploy-guide.md §5](rebuild-and-redeploy-guide.md#5-pitfalls-that-will-cost-you-an-hour-if-you-dont-know-them).
