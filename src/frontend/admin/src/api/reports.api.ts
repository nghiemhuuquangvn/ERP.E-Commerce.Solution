import { apiClient } from './client'

export interface RevenueByDay {
  date: string
  revenue: number
  orderCount: number
}

export interface TopProduct {
  productId: string
  productName: string
  unitsSold: number
  revenue: number
}

export interface SalesDashboard {
  totalRevenue: number
  totalOrders: number
  averageOrderValue: number
  newCustomers: number
  revenueByDay: RevenueByDay[]
  topProducts: TopProduct[]
}

export interface StockLevel {
  productId: string
  productName: string
  availableQuantity: number
  stockStatus: 'InStock' | 'LowStock' | 'OutOfStock'
}

export interface InventoryReport {
  totalProducts: number
  lowStockCount: number
  outOfStockCount: number
  stockLevels: StockLevel[]
}

export interface PaymentByMethod {
  method: string
  count: number
  amount: number
}

export interface PaymentSummary {
  totalProcessed: number
  totalRefunded: number
  byMethod: PaymentByMethod[]
}

export interface ReportParams {
  from?: string
  to?: string
}

export const reportsApi = {
  sales: (params: ReportParams) =>
    apiClient.get<SalesDashboard>('/v1/reports/sales', { params }).then((r) => r.data),

  inventory: () =>
    apiClient.get<InventoryReport>('/v1/reports/inventory').then((r) => r.data),

  payments: (params: ReportParams) =>
    apiClient.get<PaymentSummary>('/v1/reports/payments', { params }).then((r) => r.data),
}
