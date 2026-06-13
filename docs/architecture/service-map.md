# Service Map

ERP E-Commerce Solution is built using a Microservice Architecture.

Core Domains:

- Identity
- Sales
- Purchasing
- Warehouse

Communication:

- REST API
- RabbitMQ
- Kafka

Gateway:

- Ocelot API Gateway

## Service Relationships

```mermaid
graph TD

Gateway[API Gateway]

Identity[Identity Service]
Sales[Sales Service]
Purchase[Purchase Order Service]
Warehouse[Warehouse Service]

Gateway --> Identity
Gateway --> Sales
Gateway --> Purchase
Gateway --> Warehouse

Identity --> Sales
Identity --> Purchase
Identity --> Warehouse

Sales --> Warehouse
Purchase --> Warehouse

```



