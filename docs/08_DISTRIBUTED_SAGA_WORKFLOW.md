# Thiết Kế Distributed Transactions (Saga Pattern)

## Luồng 1: Đặt hàng thành công & Trừ tồn kho (E-commerce Order Saga)
1. **Sales Order Service:** Nhận đơn hàng, xuất bản event `SalesOrderPlacedEvent`.
2. **Warehouse Service:** Subscribed event trên $\rightarrow$ Kiểm tra và trừ bớt `AvailableToPromiseStock`.
   - *Nếu đủ hàng:* Xuất bản event `InventoryReservedEvent`.
   - *Nếu thiếu hàng:* Xuất bản event `InventoryReservationFailedEvent` $\rightarrow$ Kích hoạt luồng bù trừ (Compensating Transaction) để hủy đơn hàng bên Sales Order.
3. **Payment Service:** Nhận `InventoryReservedEvent` $\rightarrow$ Tiến hành trừ tiền $\rightarrow$ Hoàn tất đơn hàng.

## Luồng 2: Duyệt Đơn Mua Hàng & Tăng Tồn Kho Dự Kiến (PO Saga)
1. **Purchase Order Service:** Trưởng phòng bấm "Duyệt PO" $\rightarrow$ Bắn event `PurchaseOrderApprovedEvent`.
2. **Warehouse Service:** Nhận event $\rightarrow$ Tạo một bản ghi Chờ Nhập Kho (Pending GRN) và cập nhật số lượng hàng dự kiến về (Incoming Stock).