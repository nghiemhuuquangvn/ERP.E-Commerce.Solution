import type { ReactNode } from 'react'
import { View } from 'react-native'
import { useTranslation } from 'react-i18next'
import type { UseQueryResult } from '@tanstack/react-query'
import { Alert, Button, Skeleton } from '@/components/ui'
import { NoAccess } from '@/components/NoAccess'
import { isNetworkError } from '@/api/client'
import { serverMessage } from '@/lib/serverMessage'

const status = (e: unknown) => (e as { response?: { status?: number } })?.response?.status

/** Loading / error / no-access around one record; renders `children(data)` once the record is there. */
export function DetailState<T>({ query, permission, children }: { query: UseQueryResult<T>; permission?: string; children: (data: T) => ReactNode }) {
  const { t } = useTranslation()
  if (query.isPending) {
    return (
      <View className="gap-3 p-4" accessibilityLabel={t('common.loading')} accessibilityLiveRegion="polite">
        <Skeleton className="h-24 w-full" />
        <Skeleton className="h-40 w-full" />
      </View>
    )
  }
  if (query.isError) {
    if (status(query.error) === 403) return <NoAccess permission={permission} />
    return (
      <View className="gap-3 p-4">
        <Alert tone="danger">{isNetworkError(query.error) ? t('common.networkError') : serverMessage(query.error, t('common.loadError'))}</Alert>
        <Button variant="outline" onPress={() => query.refetch()}>{t('common.retry')}</Button>
      </View>
    )
  }
  return <>{children(query.data)}</>
}
