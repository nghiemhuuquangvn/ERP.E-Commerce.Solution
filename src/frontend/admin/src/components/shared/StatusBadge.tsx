const STATUS_COLORS: Record<string, string> = {
  Active: 'bg-green-100 text-green-700',
  Confirmed: 'bg-green-100 text-green-700',
  Completed: 'bg-green-100 text-green-700',
  Delivered: 'bg-green-100 text-green-700',
  Captured: 'bg-green-100 text-green-700',
  Sent: 'bg-green-100 text-green-700',

  Processing: 'bg-blue-100 text-blue-700',
  Authorized: 'bg-blue-100 text-blue-700',
  InTransit: 'bg-blue-100 text-blue-700',
  APPROVED: 'bg-blue-100 text-blue-700',
  ORDERED: 'bg-blue-100 text-blue-700',

  Pending: 'bg-yellow-100 text-yellow-700',
  Draft: 'bg-gray-100 text-gray-600',
  Queued: 'bg-gray-100 text-gray-600',
  DRAFT: 'bg-gray-100 text-gray-600',
  SUBMITTED: 'bg-yellow-100 text-yellow-700',
  PARTIALLY_RECEIVED: 'bg-yellow-100 text-yellow-700',

  Cancelled: 'bg-red-100 text-red-700',
  Failed: 'bg-red-100 text-red-700',
  Discontinued: 'bg-red-100 text-red-700',
  Voided: 'bg-red-100 text-red-700',
  CANCELLED: 'bg-red-100 text-red-700',

  Shipped: 'bg-purple-100 text-purple-700',
  Refunded: 'bg-orange-100 text-orange-700',
  RECEIVED: 'bg-green-100 text-green-700',
}

interface StatusBadgeProps {
  status: string
  label?: string
}

export function StatusBadge({ status, label }: StatusBadgeProps) {
  const colorClass = STATUS_COLORS[status] ?? 'bg-gray-100 text-gray-600'
  return (
    <span className={`badge ${colorClass}`}>
      {label ?? status}
    </span>
  )
}
