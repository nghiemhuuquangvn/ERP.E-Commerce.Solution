import { apiClient } from './client'

export interface SalesDashboard {
  totalRevenue: number
  totalOrders: number
  averageOrderValue: number
  byDate: Array<{ date: string; revenue: number; orderCount: number }>
}

export interface InventoryReport {
  totalSkus: number
  lowStockCount: number
  outOfStockCount: number
  totalValue: number
  movements: Array<{ date: string; receipts: number; issues: number }>
}

export interface PaymentSummary {
  totalProcessed: number
  totalRefunded: number
  byMethod: Array<{ method: string; amount: number; count: number }>
}

export interface ReportParams {
  from?: string
  to?: string
}

export const reportsApi = {
  sales: (params: ReportParams) =>
    apiClient.get<SalesDashboard>('/v1/reports/sales', { params }).then((r) => r.data),

  inventory: (params: ReportParams) =>
    apiClient.get<InventoryReport>('/v1/reports/inventory', { params }).then((r) => r.data),

  payments: (params: ReportParams) =>
    apiClient.get<PaymentSummary>('/v1/reports/payments', { params }).then((r) => r.data),
}
