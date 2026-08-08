# Port Map: ERP E-Commerce Solution

**Purpose:** Define network topology, port assignments, container communication, and service discovery for all 14 microservices.

---

## Port Assignments (Local Development)

### **Infrastructure Services**

| Service | Port | Protocol | Purpose |
|---------|------|----------|---------|
| **SQL Server** | 1433 | TCP | Database server (all 14 databases) |
| **Kafka Broker 1** | 9092 | TCP | Message broker (event streaming) |
| **Kafka Broker 2** | 9093 | TCP | Message broker (HA) |
| **Kafka Broker 3** | 9094 | TCP | Message broker (HA) |
| **Zookeeper** | 2181 | TCP | Kafka coordination |
| **Redis** | 6379 | TCP | Caching, sessions, distributed locks |
| **Azurite** | 10000 | TCP | Product image blob storage (Azurite emulator) |
| **Nginx (Reverse Proxy)** | 80/443 | HTTP/HTTPS | API Gateway frontend |

### **API Services (Microservices)**

| Service | Code | Port | HTTPS Port | Swagger | Database |
|---------|------|------|------------|---------|----------|
| **API Gateway (Ocelot)** | — | 5000 | 7000 | /swagger | — |
| **IdentityService** | Identity | 5001 | 7001 | /swagger | AuthDB |
| **SaleOrderService** | SO | 5002 | 7002 | /swagger | SalesDB |
| **PurchaseOrderService** | PO | 5003 | 7003 | /swagger | PurchasingDB |
| **WareHouseService** | WH | 5004 | 7004 | /swagger | WarehouseDB |
| **AccountingService** | ACC | 5005 | 7005 | /swagger | AccountingDB |
| **CRMService** | CRM | 5006 | 7006 | /swagger | CrmDB |
| **HRMService** | HRM | 5007 | 7007 | /swagger | HrmDB |
| **ProductService** | PRD | 5008 | 7008 | /swagger | ProductDB |
| **PaymentService** | PAY | 5009 | 7009 | /swagger | PaymentDB |
| **ShippingService** | SHP | 5010 | 7010 | /swagger | ShippingDB |
| **POSService** | POS | 5011 | 7011 | /swagger | PosDB |
| **PromotionService** | PRM | 5012 | 7012 | /swagger | PromotionDB |
| **NotificationService** | NTF | 5013 | 7013 | /swagger | NotificationDB |
| **ReportingService** | RPT | 5014 | 7014 | /swagger | ReportingDB |
| **SharedMasterDataService** | MST | 5015 | 7015 | /swagger | MasterDataDB |
| **LoggingService** | LOG | 5016 | 7016 | /swagger | — (Elasticsearch `service-logs-*`, no SQL DB) |

---

## Service-to-Service Communication

### **DNS/Service Discovery (Local Development)**

All services resolve via `localhost` + port:

```
IdentityService:   http://localhost:5001 (or https://localhost:7001)
ProductService:    http://localhost:5008
PaymentService:    http://localhost:5009
etc.
```

### **DNS/Service Discovery (Docker Compose)**

Services communicate via container hostname + internal port:

```
IdentityService:   http://identity-service:5001
ProductService:    http://product-service:5008
PaymentService:    http://payment-service:5009
SQL Server:        sqlserver:1433
Kafka:             kafka:9092
Redis:             redis:6379
Azurite:           azurite:10000
```

### **DNS/Service Discovery (Kubernetes)**

Services resolve via Kubernetes DNS + ClusterIP Service:

```
IdentityService:   http://identity-service.default.svc.cluster.local:5001
ProductService:    http://product-service.default.svc.cluster.local:5008
PaymentService:    http://payment-service.default.svc.cluster.local:5009
SQL Server (RDS):  rds-instance.region.rds.amazonaws.com:1433
Kafka (MSK):       kafka-brokers.region.msk.amazonaws.com:9092
Redis (ElastiCache): redis-endpoint.region.cache.amazonaws.com:6379
Azurite:           azurite.default.svc.cluster.local:10000
```

---

## Docker Compose Network Topology

### **docker-compose.yml Structure**

```yaml
version: '3.8'

networks:
  erp-network:
    driver: bridge

services:
  # Infrastructure
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    ports:
      - "1433:1433"
    networks:
      - erp-network
    environment:
      SA_PASSWORD: ${SA_PASSWORD}
      ACCEPT_EULA: Y

  kafka:
    image: confluentinc/cp-kafka:7.5.0
    ports:
      - "9092:9092"
    networks:
      - erp-network
    depends_on:
      - zookeeper
    environment:
      KAFKA_BROKER_ID: 1
      KAFKA_ZOOKEEPER_CONNECT: zookeeper:2181
      KAFKA_ADVERTISED_LISTENERS: PLAINTEXT://kafka:9092
      KAFKA_AUTO_CREATE_TOPICS_ENABLE: "true"

  zookeeper:
    image: confluentinc/cp-zookeeper:7.5.0
    ports:
      - "2181:2181"
    networks:
      - erp-network
    environment:
      ZOOKEEPER_CLIENT_PORT: 2181

  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    networks:
      - erp-network

  # API Services
  identity-service:
    build:
      context: ./src/services/IdentityService
      dockerfile: Dockerfile
    ports:
      - "5001:5001"
    networks:
      - erp-network
    depends_on:
      - sqlserver
      - kafka
    environment:
      ASPNETCORE_ENVIRONMENT: Docker
      ConnectionStrings__AuthDB: "Server=sqlserver;Database=AuthDB;User Id=sa;Password=YourPassword123;TrustServerCertificate=True"
      KafkaSettings__BootstrapServers: "kafka:9092"

  product-service:
    build:
      context: ./src/services/ProductService
      dockerfile: Dockerfile
    ports:
      - "5008:5008"
    networks:
      - erp-network
    depends_on:
      - sqlserver
      - kafka
    environment:
      ASPNETCORE_ENVIRONMENT: Docker
      ConnectionStrings__ProductDB: "Server=sqlserver;Database=ProductDB;User Id=sa;Password=YourPassword123;TrustServerCertificate=True"
      KafkaSettings__BootstrapServers: "kafka:9092"

  # ... (repeat for all 14 services)
```

---

## Kubernetes Deployment Topology

### **Service Discovery Pattern**

```yaml
apiVersion: v1
kind: Service
metadata:
  name: product-service
spec:
  selector:
    app: product-service
  ports:
    - port: 5008
      targetPort: 5008
  type: ClusterIP  # Internal service

---

apiVersion: v1
kind: Service
metadata:
  name: api-gateway
spec:
  selector:
    app: api-gateway
  ports:
    - port: 5000
      targetPort: 5000
  type: LoadBalancer  # External exposure
```

### **Internal Kubernetes DNS**

Service-to-service calls:

```
Service: product-service
DNS:     product-service.default.svc.cluster.local
URL:     http://product-service.default.svc.cluster.local:5008/api/products
```

### **Load Balancer + Ingress Topology**

```
┌─────────────────────────────────────┐
│  External Client (Browser, Mobile)  │
└──────────────┬──────────────────────┘
               │ https://api.example.com
               ▼
        ┌──────────────┐
        │   Ingress    │
        │  Controller  │
        └──────┬───────┘
               │ Route /api/* → api-gateway service
               ▼
        ┌──────────────────┐
        │  API Gateway     │
        │  (Ocelot)        │
        │  :5000           │
        └──────┬───────────┘
               │
        ┌──────┴──────────────┬──────────────┬─────────────┐
        │                     │              │             │
        ▼                     ▼              ▼             ▼
   /identity     /product      /payment    /sales
        │             │          │           │
        ▼             ▼          ▼           ▼
  identity-svc  product-svc  payment-svc  sales-svc
   :5001         :5008         :5009       :5002
```

---

## Kafka Topic-to-Service Mapping

### **Service-to-Topic Publishing**

| Service | Topic(s) Published | Event Type |
|---------|------------------|------------|
| **IdentityService** | identity-topic | UserCreated, UserUpdated, RoleAssigned |
| **SaleOrderService** | salesorder-topic | SalesOrderCreated, SalesOrderApproved, OrderShipped |
| **PurchaseOrderService** | purchaseorder-topic | PurchaseOrderCreated, POApproved, POReceived |
| **WareHouseService** | warehouse-topic | InventoryReserved, StockMoved, InvAdjusted |
| **AccountingService** | accounting-topic | InvoiceCreated, BillApproved, JournalEntryPosted |
| **CRMService** | crm-topic | LeadCreated, LeadQualified, OpportunityWon |
| **HRMService** | hrm-topic | EmployeeHired, EmployeeTerminated, PaySlipGenerated |
| **ProductService** | product-topic | ProductCreated, ProductUpdated, ProductPriceChanged |
| **PaymentService** | payment-topic | PaymentProcessed, PaymentRefunded, SettlementCreated |
| **ShippingService** | shipping-topic | ShipmentCreated, TrackingUpdated, DeliveryConfirmed |
| **POSService** | pos-topic | TransactionCreated, CashShiftClosed, RegisterBalanced |
| **PromotionService** | promotion-topic | PromotionCreated, CouponRedeemed, LoyaltyPointsEarned |
| **NotificationService** | — (consumer only) | — |
| **ReportingService** | — (consumer only) | — |
| **SharedMasterDataService** | mst-topic | DepartmentCreated, EmployeeHired, EmployeeTerminated, EmployeeTransferred, UnitOfMeasureCreated |

### **Topic Consumer Registration**

**kafka/kafka-topics.sh** (creation script):

```bash
# Create all 12 producer topics + metadata topics
for topic in identity-topic salesorder-topic purchaseorder-topic warehouse-topic \
             accounting-topic crm-topic hrm-topic product-topic payment-topic \
             shipping-topic pos-topic promotion-topic; do
  kafka-topics --create --bootstrap-server kafka:9092 --topic $topic \
    --partitions 3 --replication-factor 3 --config retention.ms=604800000
done

# Create internal Kafka topics
kafka-topics --create --bootstrap-server kafka:9092 --topic __consumer_offsets \
  --partitions 3 --replication-factor 3 --config compression.type=snappy
```

---

## API Gateway (Ocelot) Route Configuration

### **Ocelot Routes** (localhost:5000 → internal services)

```json
{
  "Routes": [
    {
      "DownstreamPathTemplate": "/api/identity/{everything}",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        { "Host": "localhost", "Port": 5001 }
      ],
      "UpstreamPathTemplate": "/api/identity/{everything}",
      "UpstreamHttpMethod": [ "GET", "POST", "PUT", "DELETE" ],
      "AuthenticationOptions": {
        "AuthenticationProviderKey": "Bearer"
      }
    },
    {
      "DownstreamPathTemplate": "/api/products/{everything}",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        { "Host": "localhost", "Port": 5008 }
      ],
      "UpstreamPathTemplate": "/api/products/{everything}",
      "UpstreamHttpMethod": [ "GET", "POST", "PUT", "DELETE" ]
    },
    {
      "DownstreamPathTemplate": "/api/sales/{everything}",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        { "Host": "localhost", "Port": 5002 }
      ],
      "UpstreamPathTemplate": "/api/sales/{everything}",
      "UpstreamHttpMethod": [ "GET", "POST", "PUT", "DELETE" ]
    },
    {
      "DownstreamPathTemplate": "/api/payments/{everything}",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        { "Host": "localhost", "Port": 5009 }
      ],
      "UpstreamPathTemplate": "/api/payments/{everything}",
      "UpstreamHttpMethod": [ "GET", "POST", "PUT", "DELETE" ]
    }
    // ... (repeat for all 14 services)
  ]
}
```

---

## Health Check Endpoints

### **Per-Service Health Checks**

| Service | Endpoint | Port |
|---------|----------|------|
| IdentityService | http://localhost:5001/health | 5001 |
| ProductService | http://localhost:5008/health | 5008 |
| PaymentService | http://localhost:5009/health | 5009 |
| ... | http://localhost:500X/health | 500X |

**Health Check Response:**

```json
{
  "status": "Healthy",
  "checks": {
    "Database": "Healthy",
    "Kafka": "Healthy",
    "Redis": "Healthy"
  }
}
```

### **Readiness Probe (Kubernetes)**

```yaml
readinessProbe:
  httpGet:
    path: /health/ready
    port: 5001
  initialDelaySeconds: 10
  periodSeconds: 5

livenessProbe:
  httpGet:
    path: /health/live
    port: 5001
  initialDelaySeconds: 30
  periodSeconds: 10
```

---

## Network Segmentation

### **Development (Single Machine)**

```
127.0.0.1:5000 → 127.0.0.1:5001-5014 (localhost)
All services on loopback interface
```

### **Docker Compose (Internal Bridge Network)**

```
erp-network (bridge driver)
├── sqlserver (172.20.0.2:1433)
├── kafka (172.20.0.3:9092)
├── identity-service (172.20.0.10:5001)
├── product-service (172.20.0.11:5008)
├── payment-service (172.20.0.12:5009)
└── ... (all services isolated in erp-network)
```

Services communicate via container hostname (e.g., `identity-service:5001`)

### **Kubernetes (Namespace Isolation)**

```
Namespace: default
├── identity-service (ClusterIP: 10.0.1.10:5001)
├── product-service (ClusterIP: 10.0.1.11:5008)
├── payment-service (ClusterIP: 10.0.1.12:5009)
├── api-gateway (LoadBalancer: external IP:5000)
└── ... (all services in default namespace)

Namespace: monitoring
├── prometheus (ClusterIP: 10.0.2.20:9090)
└── grafana (LoadBalancer: external IP:3000)
```

Services in same namespace resolve via short DNS:
```
http://identity-service:5001
```

Cross-namespace:
```
http://identity-service.default.svc.cluster.local:5001
```

---

## Security Groups & Firewall Rules

### **Local Development (Windows Firewall)**

```
Allow Inbound: 127.0.0.1:5000-7014 (localhost development)
Allow Inbound: 1433, 9092, 6379 (infrastructure)
```

### **Docker Compose (Network Policy)**

```
erp-network: Allow all inter-service communication
Expose only: 5000 (API Gateway), 1433 (SQL debugging), 9092 (Kafka debugging)
```

### **Kubernetes (Network Policy)**

```yaml
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: allow-internal
spec:
  podSelector: {}
  policyTypes:
    - Ingress
  ingress:
    - from:
        - namespaceSelector:
            matchLabels:
              name: default
```

---

## Monitoring & Observability Ports

| Tool | Port | Purpose |
|------|------|---------|
| **Prometheus** | 9090 | Metrics scraping |
| **Grafana** | 3000 | Dashboards |
| **Jaeger UI** | 16686 | Distributed tracing |
| **ELK Stack (Kibana)** | 5601 | Log aggregation |
| **Seq** | 5341 | Structured logging |

---

## Environment-Specific Configuration

### **appsettings.json (Local Development)**

```json
{
  "ConnectionStrings": {
    "ProductDB": "Server=localhost;Database=ProductDB;User Id=sa;Password=YourPassword123;TrustServerCertificate=True"
  },
  "KafkaSettings": {
    "BootstrapServers": "localhost:9092"
  },
  "ServiceUrls": {
    "IdentityService": "http://localhost:5001",
    "ProductService": "http://localhost:5008",
    "PaymentService": "http://localhost:5009"
  }
}
```

### **appsettings.Docker.json**

```json
{
  "ConnectionStrings": {
    "ProductDB": "Server=sqlserver;Database=ProductDB;User Id=sa;Password=YourPassword123;TrustServerCertificate=True"
  },
  "KafkaSettings": {
    "BootstrapServers": "kafka:9092"
  },
  "ServiceUrls": {
    "IdentityService": "http://identity-service:5001",
    "ProductService": "http://product-service:5008",
    "PaymentService": "http://payment-service:5009"
  }
}
```

### **appsettings.Production.json (Kubernetes)**

```json
{
  "ConnectionStrings": {
    "ProductDB": "Server=rds-instance.us-east-1.rds.amazonaws.com;Database=ProductDB;User Id=admin;Password={secret-from-vault}"
  },
  "KafkaSettings": {
    "BootstrapServers": "kafka-brokers.us-east-1.msk.amazonaws.com:9092"
  },
  "ServiceUrls": {
    "IdentityService": "http://identity-service.default.svc.cluster.local:5001",
    "ProductService": "http://product-service.default.svc.cluster.local:5008",
    "PaymentService": "http://payment-service.default.svc.cluster.local:5009"
  }
}
```

---

## Network Throughput Estimation

### **Typical Request Flow**

| Path | Hops | Bandwidth | Latency |
|------|------|-----------|---------|
| Client → API Gateway → Service | 2 | ~10 Mbps | ~5-10ms |
| Service → Service (internal) | 1 | ~100 Mbps | ~1-2ms |
| Service → Kafka (event publish) | 1 | ~50 Mbps | ~5-10ms |
| Service → Database (query) | 1 | ~100 Mbps | ~5-20ms |

**Total Order-to-Cash Flow:** ~50-100ms latency (API call → order creation → event published → warehouse reserved)

---

**Created:** 2026-06-13  
**Services Mapped:** 14 total  
**Ports Assigned:** 5000-7014 (HTTP/HTTPS)  
**Infrastructure Ports:** 1433, 2181, 6379, 9092-9094
