import { useAuthStore } from '@/stores/auth.store'

/** The permission that lets someone book an order / quotation line at a price other than the system price (R113). */
export const PRICE_OVERRIDE_PERMISSION = 'sales_order:price_override'

/**
 * May the signed-in user type a price on an order or quotation line? Reads the permission itself, never the role name: the server decides from the token's
 * permissions only, so a user whose role is "Admin" but who was not granted the permission gets a locked price field (and the server would refuse the price anyway).
 */
export function usePriceOverride(): boolean {
  const permissions = useAuthStore((s) => s.user?.permissions)
  return !!permissions?.includes(PRICE_OVERRIDE_PERMISSION)
}
