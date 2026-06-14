# Deployment Guide

Complete guide for building, deploying, and managing the ERP E-Commerce Solution in development, staging, and production environments.

## Deployment Architecture

```
Source Code
    ↓
CI/CD Pipeline (GitHub Actions)
    ↓
Build Artifacts
    ↓
Docker Image Registry (Docker Hub/Azure Container Registry)
    ↓
Environment Deployment
    ├─ Development (Local Docker Compose)
    ├─ Staging (Kubernetes Cluster)
    └─ Production (Kubernetes Cluster)
```

## Build Process

### 1. Docker Image Building

#### Build Individual Service Image

```bash
# Build Identity Service
docker build -t identity-service:latest \
  -f src/services/IdentityService/Dockerfile \
  .

# Build Sales Service
docker build -t sales-service:latest \
  -f src/services/SaleOrderService/Dockerfile \
  .
```

#### Multi-Stage Dockerfile Example

```dockerfile
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

COPY . .
RUN dotnet restore "src/services/IdentityService/IdentityService.csproj"
RUN dotnet build "src/services/IdentityService/IdentityService.csproj" -c Release -o /app/build

# Stage 2: Publish
FROM build AS publish
RUN dotnet publish "src/services/IdentityService/IdentityService.csproj" -c Release -o /app/publish

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

EXPOSE 80
ENTRYPOINT ["dotnet", "IdentityService.dll"]
```

### 2. Push to Registry

```bash
# Login to registry
docker login -u <username> -p <password>

# Tag image
docker tag identity-service:latest myregistry.azurecr.io/identity-service:latest

# Push image
docker push myregistry.azurecr.io/identity-service:latest
```

## Development Deployment

### Docker Compose

```yaml
version: '3.8'

services:
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2019-latest
    environment:
      SA_PASSWORD: ${SA_PASSWORD}
      ACCEPT_EULA: Y
    ports:
      - "1433:1433"
    volumes:
      - sqlserver-data:/var/opt/mssql

  rabbitmq:
    image: rabbitmq:3-management
    environment:
      RABBITMQ_DEFAULT_USER: ${RABBITMQ_USER}
      RABBITMQ_DEFAULT_PASS: ${RABBITMQ_PASSWORD}
    ports:
      - "5672:5672"
      - "15672:15672"
    healthcheck:
      test: ["CMD", "rabbitmq-diagnostics", "ping"]
      interval: 10s
      timeout: 5s
      retries: 5

  kafka:
    image: confluentinc/cp-kafka:7.0.0
    depends_on:
      - zookeeper
    environment:
      KAFKA_BROKER_ID: 1
      KAFKA_ZOOKEEPER_CONNECT: zookeeper:2181
    ports:
      - "9092:9092"

  identity-service:
    build:
      context: .
      dockerfile: src/services/IdentityService/Dockerfile
    ports:
      - "5001:80"
    environment:
      ConnectionStrings__IdentityDatabase: ${CONNECTION_STRING_IDENTITY}
      Jwt__SecretKey: ${JWT_SECRET}
      RabbitMQ__HostName: rabbitmq
    depends_on:
      - sqlserver
      - rabbitmq
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost/health"]
      interval: 30s
      timeout: 10s
      retries: 3

  api-gateway:
    build:
      context: .
      dockerfile: src/ApiGateway/Dockerfile
    ports:
      - "5000:80"
    environment:
      ASPNETCORE_ENVIRONMENT: Development
    depends_on:
      - identity-service
      - sales-service
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost/health"]
      interval: 30s
      timeout: 10s
      retries: 3

volumes:
  sqlserver-data:

networks:
  default:
    name: erp-network
```

#### Deploy

```bash
# Start services
docker-compose up -d

# View logs
docker-compose logs -f identity-service

# Scale service
docker-compose up -d --scale sales-service=3

# Stop services
docker-compose down

# Clean up
docker-compose down -v
```

## Kubernetes Deployment

### Namespace Setup

```bash
# Create namespace
kubectl create namespace erp

# Set default namespace
kubectl config set-context --current --namespace=erp
```

### Secrets Management

```bash
# Create database credentials secret
kubectl create secret generic db-credentials \
  --from-literal=connection-string='Server=sqlserver;Database=IdentityDB;...' \
  -n erp

# Create JWT secret
kubectl create secret generic jwt-secret \
  --from-literal=secret-key='your-very-long-secret-key' \
  -n erp

# Create RabbitMQ credentials
kubectl create secret generic rabbitmq-credentials \
  --from-literal=username=guest \
  --from-literal=password=guest \
  -n erp
```

### Service Deployment

#### Identity Service Deployment

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: identity-service
  namespace: erp
spec:
  replicas: 3
  selector:
    matchLabels:
      app: identity-service
  template:
    metadata:
      labels:
        app: identity-service
    spec:
      containers:
      - name: identity-service
        image: myregistry.azurecr.io/identity-service:latest
        ports:
        - containerPort: 80
        env:
        - name: ConnectionStrings__IdentityDatabase
          valueFrom:
            secretKeyRef:
              name: db-credentials
              key: connection-string
        - name: Jwt__SecretKey
          valueFrom:
            secretKeyRef:
              name: jwt-secret
              key: secret-key
        - name: RabbitMQ__HostName
          value: rabbitmq-service
        resources:
          requests:
            memory: "256Mi"
            cpu: "250m"
          limits:
            memory: "512Mi"
            cpu: "500m"
        livenessProbe:
          httpGet:
            path: /health
            port: 80
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 80
          initialDelaySeconds: 10
          periodSeconds: 5

---
apiVersion: v1
kind: Service
metadata:
  name: identity-service
  namespace: erp
spec:
  type: ClusterIP
  selector:
    app: identity-service
  ports:
  - protocol: TCP
    port: 80
    targetPort: 80
```

#### Deploy to Kubernetes

```bash
# Apply configuration
kubectl apply -f k8s/identity-service.yaml

# Check deployment status
kubectl get deployments -n erp

# View pods
kubectl get pods -n erp

# View service
kubectl get svc -n erp

# Check logs
kubectl logs -f deployment/identity-service -n erp

# Scale deployment
kubectl scale deployment identity-service --replicas=5 -n erp

# Update image (rolling update)
kubectl set image deployment/identity-service \
  identity-service=myregistry.azurecr.io/identity-service:v1.1.0 \
  -n erp

# Check rollout status
kubectl rollout status deployment/identity-service -n erp
```

### Helm Charts (Optional)

Create `helm/identity-service/Chart.yaml`:

```yaml
apiVersion: v2
name: identity-service
version: 1.0.0
appVersion: 1.0.0
description: Identity Service Helm Chart
```

Deploy with Helm:
```bash
helm install identity-service helm/identity-service \
  --namespace erp \
  --values helm/identity-service/values.yaml
```

## SQL Server Deployment

### Azure SQL Database

```bash
# Create server
az sql server create \
  --name erp-sql-server \
  --resource-group erp-rg \
  --location eastus \
  --admin-user sqladmin \
  --admin-password YourPassword123

# Create databases
az sql db create \
  --server erp-sql-server \
  --name IdentityDB \
  --resource-group erp-rg

# Allow Azure services
az sql server firewall-rule create \
  --server erp-sql-server \
  --name AllowAzureServices \
  --resource-group erp-rg \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0

# Run migrations
dotnet ef database update \
  --connection "Server=erp-sql-server.database.windows.net;Database=IdentityDB;User Id=sqladmin;Password=YourPassword123;"
```

## Message Broker Deployment

### RabbitMQ on Kubernetes

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: rabbitmq-config
  namespace: erp
data:
  rabbitmq.conf: |
    management.ssl.port = 15671

---
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: rabbitmq
  namespace: erp
spec:
  serviceName: rabbitmq
  replicas: 3
  selector:
    matchLabels:
      app: rabbitmq
  template:
    metadata:
      labels:
        app: rabbitmq
    spec:
      containers:
      - name: rabbitmq
        image: rabbitmq:3-management
        ports:
        - containerPort: 5672
          name: amqp
        - containerPort: 15672
          name: management
        env:
        - name: RABBITMQ_DEFAULT_USER
          value: guest
        - name: RABBITMQ_DEFAULT_PASS
          value: guest
        resources:
          requests:
            memory: "512Mi"
            cpu: "500m"
          limits:
            memory: "1Gi"
            cpu: "1000m"
        volumeMounts:
        - name: rabbitmq-data
          mountPath: /var/lib/rabbitmq
  volumeClaimTemplates:
  - metadata:
      name: rabbitmq-data
    spec:
      accessModes: ["ReadWriteOnce"]
      resources:
        requests:
          storage: 10Gi

---
apiVersion: v1
kind: Service
metadata:
  name: rabbitmq-service
  namespace: erp
spec:
  clusterIP: None
  selector:
    app: rabbitmq
  ports:
  - port: 5672
    name: amqp
  - port: 15672
    name: management
```

## Monitoring & Logging

### Prometheus Metrics

```yaml
apiVersion: v1
kind: Service
metadata:
  name: prometheus
  namespace: erp
spec:
  ports:
  - port: 9090
  selector:
    app: prometheus

---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: prometheus
  namespace: erp
spec:
  replicas: 1
  selector:
    matchLabels:
      app: prometheus
  template:
    metadata:
      labels:
        app: prometheus
    spec:
      containers:
      - name: prometheus
        image: prom/prometheus
        ports:
        - containerPort: 9090
        volumeMounts:
        - name: config
          mountPath: /etc/prometheus
      volumes:
      - name: config
        configMap:
          name: prometheus-config
```

### ELK Stack (Elasticsearch, Logstash, Kibana)

Configure in appsettings.json:
```json
{
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": [
      {
        "Name": "Console"
      },
      {
        "Name": "Elasticsearch",
        "Args": {
          "nodeUris": "http://elasticsearch:9200",
          "indexFormat": "erp-{0:yyyy.MM.dd}"
        }
      }
    ]
  }
}
```

## CI/CD Pipeline

### GitHub Actions Example

```yaml
name: Deploy ERP Services

on:
  push:
    branches: [main]

env:
  REGISTRY: myregistry.azurecr.io

jobs:
  build-and-push:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v2
    
    - name: Login to Registry
      uses: docker/login-action@v1
      with:
        registry: ${{ env.REGISTRY }}
        username: ${{ secrets.REGISTRY_USERNAME }}
        password: ${{ secrets.REGISTRY_PASSWORD }}
    
    - name: Build and Push Identity Service
      uses: docker/build-push-action@v2
      with:
        context: .
        file: src/services/IdentityService/Dockerfile
        push: true
        tags: ${{ env.REGISTRY }}/identity-service:latest

    - name: Deploy to Kubernetes
      run: |
        kubectl set image deployment/identity-service \
          identity-service=${{ env.REGISTRY }}/identity-service:latest \
          -n erp
```

## Backup & Recovery

### Database Backup

```bash
# Full backup
az sql db export \
  --server erp-sql-server \
  --name IdentityDB \
  --admin-user sqladmin \
  --admin-password YourPassword123 \
  --storage-key "storageKey" \
  --storage-uri "https://storage.blob.core.windows.net/backups/identitydb.bacpac"

# Automated daily backups
az sql db update \
  --server erp-sql-server \
  --name IdentityDB \
  --backup-storage-redundancy GRS
```

### Restore from Backup

```bash
# Import from bacpac
az sql db import \
  --server erp-sql-server \
  --name IdentityDB-Restored \
  --storage-uri "https://storage.blob.core.windows.net/backups/identitydb.bacpac" \
  --storage-key "storageKey" \
  --admin-user sqladmin \
  --admin-password YourPassword123
```

## Health Checks & Monitoring

```bash
# Check service health
kubectl get pods -n erp

# View service logs
kubectl logs -f service/identity-service -n erp

# Check resource usage
kubectl top pods -n erp

# Port forward to local machine
kubectl port-forward svc/identity-service 5001:80 -n erp
```

## Troubleshooting

### Pod Crashes

```bash
# Check pod events
kubectl describe pod <pod-name> -n erp

# View logs
kubectl logs <pod-name> -n erp

# Get detailed status
kubectl get pod <pod-name> -o yaml -n erp
```

### Connection Issues

```bash
# Test RabbitMQ connectivity
kubectl exec -it <pod-name> -n erp -- \
  rabbitmqctl -n / list_connections

# Test database connectivity
kubectl exec -it <pod-name> -n erp -- \
  sqlcmd -S <db-server> -U sa
```

---

**Last Updated**: 2026-06-13  
**Version**: 1.0.0
