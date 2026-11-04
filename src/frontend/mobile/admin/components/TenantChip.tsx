import { Badge } from '@/components/ui'
import { tenantConfig } from '@/lib/tenant'

/** The tenant this build belongs to, shown in every header so nobody mistakes one shop for another. */
export function TenantChip() {
  return <Badge tone="info" label={tenantConfig.code} />
}
