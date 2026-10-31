import { gatewayClient as apiClient } from '../client'

// NotificationService webhooks (ROADMAP P2-1b), through the Gateway's `/ntf/api/...` route.
const BASE = '/ntf/api/v1/webhooks'

export interface WebhookDelivery {
  id: string
  eventType: string
  success: boolean
  statusCode?: number | null
  error?: string | null
  durationMs: number
  createdAt: string
}

export interface Webhook {
  id: string
  name: string
  url: string
  isActive: boolean
  /** A secret is stored; it is never returned. */
  hasSecret: boolean
  /** Empty = every event. */
  eventTypes: string[]
  createdAt: string
  lastDelivery?: WebhookDelivery | null
}

export interface WebhookInput {
  name: string
  url: string
  /** Empty keeps the stored secret on an edit. */
  secret?: string
  /** Removes the stored secret (edit only). */
  clearSecret?: boolean
  eventTypes: string[]
}

export interface WebhookEventType {
  type: string
  description: string
}

export interface WebhookTestResult {
  success: boolean
  statusCode?: number | null
  durationMs: number
  error?: string | null
}

export interface WebhookDeliveryPage {
  items: WebhookDelivery[]
  totalCount: number
  pageNumber: number
  pageSize: number
}

export const webhooksApi = {
  list: () => apiClient.get<Webhook[]>(BASE).then((r) => r.data),
  eventTypes: () => apiClient.get<WebhookEventType[]>(`${BASE}/event-types`).then((r) => r.data),
  create: (body: WebhookInput) => apiClient.post<Webhook>(BASE, { name: body.name, url: body.url, secret: body.secret || null, eventTypes: body.eventTypes }).then((r) => r.data),
  update: (id: string, body: WebhookInput) =>
    apiClient.put<Webhook>(`${BASE}/${id}`, { name: body.name, url: body.url, secret: body.secret || null, clearSecret: !!body.clearSecret, eventTypes: body.eventTypes }).then((r) => r.data),
  remove: (id: string) => apiClient.delete(`${BASE}/${id}`).then(() => undefined),
  setActive: (id: string, active: boolean) => apiClient.post<Webhook>(`${BASE}/${id}/${active ? 'activate' : 'deactivate'}`).then((r) => r.data),
  test: (id: string) => apiClient.post<WebhookTestResult>(`${BASE}/${id}/test`).then((r) => r.data),
  deliveries: (id: string, page: number, pageSize: number) =>
    apiClient.get<WebhookDeliveryPage>(`${BASE}/${id}/deliveries`, { params: { page, pageSize } }).then((r) => r.data),
}
