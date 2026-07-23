import { apiClient, PO_BASE } from './poClient'

export interface Address {
  street: string
  city: string
  state: string
  postalCode: string
  country: string
}

export interface Supplier {
  id: string
  code: string
  name: string
  contactPerson?: string
  email?: string
  phone?: string
  address?: Address
  taxCode?: string
  paymentTermDays: number
  paymentTermId?: string
  currencyCode: string
  isActive: boolean
}

export interface SupplierItem {
  id: string
  supplierId: string
  productId: string
  variantId?: string
  supplierItemCode?: string
  lastPurchasePrice: number
  leadTimeDays: number
  createdAt?: string
  updatedAt?: string
}

export interface SupplierRating {
  id: string
  rating: number
  comment?: string
  ratedBy: string
  createdAt?: string
}

export interface SupplierRatingsResult {
  averageRating: number
  totalRatings: number
  ratings: SupplierRating[]
}

export interface ListSuppliersParams {
  isActive?: boolean
  pageNumber?: number
  pageSize?: number
}

// CreateSupplierDto — Address/TaxCode/PaymentTermDays/CurrencyCode all optional
// (PaymentTermDays defaults server-side to 30, CurrencyCode to "USD"). Note:
// PaymentTermId is NOT settable here — it's only settable via the dedicated
// PUT {id}/payment-term endpoint (see setPaymentTerm below).
export interface CreateSupplierRequest {
  code: string
  name: string
  contactPerson?: string
  email?: string
  phone?: string
  address?: Address
  taxCode?: string
  paymentTermDays?: number
  currencyCode?: string
}

// UpdateSupplierDto — deliberately does NOT include PaymentTermId/PaymentTermDays;
// those are only settable via setPaymentTerm's separate endpoint.
export interface UpdateSupplierRequest {
  name: string
  contactPerson?: string
  email?: string
  phone?: string
  address?: Address
  taxCode?: string
  currencyCode?: string
}

// SetSupplierPaymentTermDto — PaymentTermId references SharedMasterDataService's
// PaymentTerm by Id only (cross-service, no DB FK); omit it to fall back to a
// plain net-days value via paymentTermDays.
export interface SetSupplierPaymentTermRequest {
  paymentTermId?: string | null
  paymentTermDays: number
}

export interface RateSupplierRequest {
  rating: number
  comment?: string
}

export interface CreateSupplierItemRequest {
  productId: string
  variantId?: string
  supplierItemCode?: string
  lastPurchasePrice: number
  leadTimeDays: number
}

export interface UpdateSupplierItemRequest {
  lastPurchasePrice: number
  leadTimeDays: number
  supplierItemCode?: string
}

const BASE = `${PO_BASE}/suppliers`

export const suppliersApi = {
  // NOTE: SuppliersController's GET / currently returns a plain
  // IEnumerable<SupplierDto> (not a { items, totalCount, ... } paginated wrapper
  // like every other list endpoint in this app) — GetSuppliersQueryHandler is
  // presently a stub that always returns an empty list regardless of the
  // isActive/pageNumber/pageSize query params it's given. Params are still
  // passed through here for forward-compatibility once the handler is
  // implemented; pagination is done client-side in the meantime (see
  // SupplierListPage.tsx).
  list: (params: ListSuppliersParams) =>
    apiClient.get<Supplier[]>(BASE, { params }).then((r) => r.data),

  get: (id: string) => apiClient.get<Supplier>(`${BASE}/${id}`).then((r) => r.data),

  create: (body: CreateSupplierRequest) =>
    apiClient.post<Supplier>(BASE, body).then((r) => r.data),

  update: (id: string, body: UpdateSupplierRequest) =>
    apiClient.put<Supplier>(`${BASE}/${id}`, body).then((r) => r.data),

  setPaymentTerm: (id: string, body: SetSupplierPaymentTermRequest) =>
    apiClient.put<Supplier>(`${BASE}/${id}/payment-term`, body).then((r) => r.data),

  deactivate: (id: string) => apiClient.post(`${BASE}/${id}/deactivate`),

  rate: (id: string, body: RateSupplierRequest) =>
    apiClient.post<{ id: string }>(`${BASE}/${id}/ratings`, body).then((r) => r.data),

  getRatings: (id: string) =>
    apiClient.get<SupplierRatingsResult>(`${BASE}/${id}/ratings`).then((r) => r.data),

  listItems: (id: string) =>
    apiClient.get<SupplierItem[]>(`${BASE}/${id}/items`).then((r) => r.data),

  getItem: (id: string, itemId: string) =>
    apiClient.get<SupplierItem>(`${BASE}/${id}/items/${itemId}`).then((r) => r.data),

  createItem: (id: string, body: CreateSupplierItemRequest) =>
    apiClient.post<SupplierItem>(`${BASE}/${id}/items`, body).then((r) => r.data),

  updateItem: (id: string, itemId: string, body: UpdateSupplierItemRequest) =>
    apiClient.put<SupplierItem>(`${BASE}/${id}/items/${itemId}`, body).then((r) => r.data),
}
