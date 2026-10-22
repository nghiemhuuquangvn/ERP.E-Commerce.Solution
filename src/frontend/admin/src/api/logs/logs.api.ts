import { apiClient, gatewayClient } from '../client'

export type LogLevel = 'Information' | 'Warning' | 'Error' | 'Critical'
export type LogKind = 'Trace' | 'Exception'

export interface LogEntry {
  id: string
  serviceName: string
  environment?: string
  level: LogLevel
  kind: LogKind
  message: string
  exceptionType?: string
  stackTrace?: string
  correlationId?: string
  source?: string
  properties?: Record<string, string>
  occurredAt: string
}

// What the browser reports (POST /logs/api/v1/logs/client — anonymous, rate-limited at the
// Gateway); the server assigns id and timestamps.
export interface ClientLogEntry {
  serviceName: string
  level: LogLevel
  kind: LogKind
  message: string
  exceptionType?: string
  stackTrace?: string
  correlationId?: string
  properties?: Record<string, string>
}

export interface LogQuery {
  serviceName?: string
  level?: string
  kind?: string
  fromUtc?: string
  toUtc?: string
  keyword?: string
  requestId?: string
  page: number
  pageSize: number
}

export interface PagedLogs {
  items: LogEntry[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}

// LoggingService (GET /api/v1/logs) — requires the platform permission logs:view.
export const logsApi = {
  search: (query: LogQuery) => {
    const params = Object.fromEntries(
      Object.entries(query).filter(([, value]) => value !== undefined && value !== ''),
    )
    return apiClient.get<PagedLogs>('/v1/logs', { params }).then((r) => r.data)
  },

  reportClient: (entries: ClientLogEntry[]) =>
    gatewayClient.post('/logs/api/v1/logs/client', entries).then(() => undefined),
}
