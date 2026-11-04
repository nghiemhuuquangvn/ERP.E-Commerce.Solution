import { Spinner } from '@/components/ui'

/** Under the last row while the next page loads. */
export function ListFooter({ loading }: { loading: boolean }) {
  return loading ? <Spinner /> : null
}
