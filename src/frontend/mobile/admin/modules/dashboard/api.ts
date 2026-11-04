import { apiClient } from '@/api/client'

export interface SalesDashboard {
  totalRevenue: number
  totalOrders: number
  averageOrderValue: number
  revenueByDay: { date: string; revenue: number; orderCount: number }[]
}

export const dashboardApi = {
  /** ReportingService sales dashboard (needs reports:sales:view); the same call the Admin web Dashboard makes. */
  sales: (from: string, to: string) => apiClient.get<SalesDashboard>('/v1/reports/sales', { params: { from, to } }).then((r) => r.data),
}
