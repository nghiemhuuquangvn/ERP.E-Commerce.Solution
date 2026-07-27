import axios from 'axios'
import { apiClient, SO_BASE } from './soClient'

export interface CustomerSalesProfile {
  customerId: string
  creditLimit: number
  currentBalance: number
  updatedAt: string
}

export const customerSalesProfilesApi = {
  /** Returns null (not an error) if no profile is configured — treated as "no limit enforced". */
  get: async (customerId: string): Promise<CustomerSalesProfile | null> => {
    try {
      const { data } = await apiClient.get<CustomerSalesProfile>(`${SO_BASE}/customer-sales-profiles/${customerId}`)
      return data
    } catch (err) {
      if (axios.isAxiosError(err) && err.response?.status === 404) return null
      throw err
    }
  },

  setCreditLimit: (customerId: string, creditLimit: number) =>
    apiClient
      .put<CustomerSalesProfile>(`${SO_BASE}/customer-sales-profiles/${customerId}`, { creditLimit })
      .then((r) => r.data),
}
