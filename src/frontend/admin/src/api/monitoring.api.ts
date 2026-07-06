import { apiClient } from './client'

export interface ProcessMetrics {
  machineName: string
  processName: string
  workingSetMb: number
  threadCount: number
  handleCount: number
  startTime: string
  uptimeSeconds: number
  gcGen0Collections: number
  gcGen1Collections: number
  gcGen2Collections: number
  totalMemoryMb: number
  dotNetVersion: string
}

export interface ServiceHealth {
  serviceName: string
  status: 'Healthy' | 'Degraded' | 'Unreachable'
  responseTimeMs: number
  errorMessage?: string
  checkedAt: string
}

export interface RouteStats {
  method: string
  pathPattern: string
  count: number
  avgElapsedMs: number
  errorCount: number
  errorRate: number
}

export interface ApiRequestLog {
  method: string
  path: string
  statusCode: number
  elapsedMs: number
  tenantId?: string
  timestamp: string
}

export interface ExceptionLog {
  exceptionType: string
  message: string
  stackTrace?: string
  path?: string
  method?: string
  timestamp: string
}

export interface MonitoringDashboard {
  process: ProcessMetrics
  services: ServiceHealth[]
  api: {
    totalRequests: number
    totalErrors: number
    errorRate: number
    topRoutes: RouteStats[]
  }
  generatedAt: string
}

export const monitoringApi = {
  getDashboard: () =>
    apiClient.get<MonitoringDashboard>('/v1/monitoring/dashboard').then((r) => r.data),

  getRecentRequests: () =>
    apiClient.get<ApiRequestLog[]>('/v1/monitoring/requests').then((r) => r.data),

  getExceptions: () =>
    apiClient.get<ExceptionLog[]>('/v1/monitoring/exceptions').then((r) => r.data),

  getServicesHealth: () =>
    apiClient.get<ServiceHealth[]>('/v1/monitoring/services').then((r) => r.data),

  getRouteStats: () =>
    apiClient.get<RouteStats[]>('/v1/monitoring/routes').then((r) => r.data),
}
