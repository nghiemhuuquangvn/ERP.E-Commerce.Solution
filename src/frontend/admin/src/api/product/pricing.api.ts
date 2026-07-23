import { apiClient, PRD_BASE } from './prdClient'

// PriceListChannel enum (ERP.PRD.Domain.Enums.PriceListChannel) — serialized as its
// numeric value by default (no [JsonConverter(typeof(JsonStringEnumConverter))] seen
// on the DTO), so these are sent/received as numbers.
export const PriceListChannel = {
  Retail: 0,
  Wholesale: 1,
  Ecommerce: 2,
} as const
export type PriceListChannel = (typeof PriceListChannel)[keyof typeof PriceListChannel]

export const PRICE_LIST_CHANNEL_OPTIONS: { value: PriceListChannel; label: string }[] = [
  { value: PriceListChannel.Retail, label: 'Retail' },
  { value: PriceListChannel.Wholesale, label: 'Wholesale' },
  { value: PriceListChannel.Ecommerce, label: 'Ecommerce' },
]

// PriceListItemDto (GetPriceListQuery)
export interface PriceListItem {
  variantId: string
  basePrice: number
  discountedPrice?: number | null
  minQuantity: number
  effectivePrice: number
}

// PriceListDetailDto (GetPriceListQuery)
export interface PriceListDetail {
  id: string
  name: string
  description?: string | null
  effectiveDate: string
  expiryDate?: string | null
  isActive: boolean
  channel: PriceListChannel
  currencyId?: string | null
  items: PriceListItem[]
}

// PriceListItemInput (CreatePriceListCommand.Items)
export interface PriceListItemInput {
  variantId: string
  basePrice: number
  discountedPrice?: number | null
  minQuantity?: number
}

// CreatePriceListCommand(Name, Description, EffectiveDate, ExpiryDate, Channel, CurrencyId, Items)
// — Items must have at least one entry (server-validated); IsActive is always true on
// create (no IsActive input) and there is no API to deactivate/reactivate afterward.
export interface CreatePriceListRequest {
  name: string
  description?: string
  effectiveDate: string
  expiryDate?: string | null
  channel: PriceListChannel
  currencyId?: string | null
  items: PriceListItemInput[]
}

export interface CreatePriceListResponse {
  priceListId: string
  name: string
}

// SetPriceListChannelCommand(Channel, CurrencyId) — priceListId is in the route.
// This is the ONLY field group that can be changed on an existing price list via
// the API: Name/Description/EffectiveDate/ExpiryDate/IsActive/Items are write-once
// at creation (the domain methods for changing them exist but no controller action
// calls them for an already-created list).
export interface SetPriceListChannelRequest {
  channel: PriceListChannel
  currencyId?: string | null
}

export const pricingApi = {
  create: (body: CreatePriceListRequest) =>
    apiClient.post<CreatePriceListResponse>(`${PRD_BASE}/pricing/lists`, body).then((r) => r.data),

  // There is no "list all price lists" endpoint — only fetch-by-id. Callers must
  // track known price list IDs themselves (e.g. from create responses).
  get: (id: string) => apiClient.get<PriceListDetail>(`${PRD_BASE}/pricing/lists/${id}`).then((r) => r.data),

  setChannel: (id: string, body: SetPriceListChannelRequest) =>
    apiClient.put(`${PRD_BASE}/pricing/lists/${id}/channel`, body),
}
