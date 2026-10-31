import { Badge } from '@/components/ui/Badge'
import { statusRole } from '@/components/ui/statusRoles'

interface StatusBadgeProps {
  status: string
  label?: string
}

/** A status as a coloured pill. The colour role comes from the one map in components/ui/statusRoles.ts. */
export function StatusBadge({ status, label }: StatusBadgeProps) {
  return <Badge variant={statusRole(status)}>{label ?? status}</Badge>
}
