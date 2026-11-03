import { apiClient } from '@/api/client'

/** A product the cashier can put on a sale. `productId` is the catalogue Guid the POS service binds. */
export interface PosProduct {
  productId: string
  name: string
  sku: string
  price: number
  /** Only known from the catalogue search (the POS barcode cache does not carry stock). */
  stockQuantity?: number
}

interface CatalogueProduct {
  id: string
  name: string
  sku: string
  price: number
  stockQuantity: number
}

const fromCatalogue = (p: CatalogueProduct): PosProduct => ({ productId: p.id, name: p.name, sku: p.sku, price: p.price, stockQuantity: p.stockQuantity })

/** Product search by name or SKU (ProductService, the same call the shop's customer app uses). */
export async function searchProducts(term: string): Promise<PosProduct[]> {
  const { data } = await apiClient.get<CatalogueProduct[]>('/v1/products/search', { params: { searchTerm: term.trim() } })
  return (data ?? []).map(fromCatalogue)
}

/**
 * A scanned barcode is the product's SKU. The POS service answers from its own product cache; when that has no such product (a shop whose
 * cache was never filled answers 404 for everything) the catalogue is asked for an exact SKU match instead.
 */
export async function findByBarcode(code: string): Promise<PosProduct | null> {
  const wanted = code.trim()
  if (!wanted) return null
  try {
    const { data } = await apiClient.get<{ productId: string; name: string; sku: string; price: number; isActive: boolean }>(`/v1/pos/products/lookup/${encodeURIComponent(wanted)}`)
    if (data?.isActive) return { productId: data.productId, name: data.name, sku: data.sku, price: data.price }
  } catch {
    // 404 (not in the cache) or an error: fall through to the catalogue.
  }
  try {
    const found = await searchProducts(wanted)
    return found.find((p) => p.sku.toLowerCase() === wanted.toLowerCase()) ?? null
  } catch {
    return null
  }
}
