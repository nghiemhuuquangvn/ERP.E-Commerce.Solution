# Bản Đồ Hệ Thống ERP & E-Commerce Core Context

## 1. Tổng Quan Kiến Trúc (Architecture Overview)
- **Pattern:** Microservices, Event-Driven Architecture, Clean Architecture.
- **Technology Stack:** .NET 10, Web API, C#, PostgreSQL/MSSQL, Kafka (MassTransit), Docker, Kubernetes.
- **Cơ chế giao tiếp:** Đồng bộ qua RESTfull API (Nội bộ / External), Bất đồng bộ qua Kafka (Saga Pattern).

## 2. Bản Đồ Các Domain (Bounded Contexts Mapping)
Dưới đây là danh sách các file tài liệu chi tiết cho từng phân hệ:

- `01_PURCHASE_ORDER_DOMAIN.md`: Quản lý quy trình mua hàng, NCC (Vendor), đồng bộ tồn kho dự kiến.
- `02_SALES_ORDER_DOMAIN.md`: Quản lý đơn hàng từ sàn E-commerce, giỏ hàng, cổng thanh toán, khuyến mãi.
- `03_WAREHOUSE_MANAGEMENT_DOMAIN.md`: Quản lý nhập/xuất kho (GRN/GDN), kiểm kho, vị trí (Bin/Rack), tồn kho thực tế.
- `04_CRM_DOMAIN.md`: Quản lý thông tin khách hàng, phân hạng (Loyalty), phễu chăm sóc, cổng ticket hỗ trợ.
- `05_HRM_DOMAIN.md`: Quản lý hồ sơ nhân sự, phòng ban, chấm công, bảng lương, phân quyền RBAC toàn hệ thống.
- `06_WORK_MANAGEMENT_DOMAIN.md`: Quản lý tiến độ sản xuất, task việc, quy trình phê duyệt (Approval Workflow).

## 3. Distributed Transactions & Shared Events
- Chi tiết luồng giao tiếp liên dịch vụ (Cross-domain Saga) được định nghĩa tại file `08_DISTRIBUTED_SAGA_WORKFLOW.md`.