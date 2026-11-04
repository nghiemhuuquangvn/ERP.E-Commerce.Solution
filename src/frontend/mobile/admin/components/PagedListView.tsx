import type { ReactElement, ReactNode } from 'react'
import { FlatList, RefreshControl, View } from 'react-native'
import { useTranslation } from 'react-i18next'
import { Inbox } from 'lucide-react-native'
import { Alert, Button, EmptyState, Skeleton } from '@/components/ui'
import { NoAccess } from '@/components/NoAccess'
import { OfflineBanner } from '@/components/OfflineBanner'
import { ListFooter } from '@/components/ListFooter'
import { isNetworkError } from '@/api/client'
import { serverMessage } from '@/lib/serverMessage'
import { colors, icon } from '@/theme/theme'
import type { usePagedList } from '@/modules/usePagedList'

type PagedQuery<T> = ReturnType<typeof usePagedList<T>>

interface Props<T> {
  query: PagedQuery<T>
  renderItem: (item: T) => ReactElement
  keyOf: (item: T) => string
  /** Search box / chips above the rows. */
  header?: ReactNode
  emptyTitle: string
  emptyHint?: string
  /** Permission shown on the "No access" state. */
  permission?: string
}

const isForbidden = (e: unknown) => (e as { response?: { status?: number } })?.response?.status === 403

/**
 * The list every module uses: skeleton while the first page loads, pull-to-refresh, next page at the end, an empty state,
 * an error with Retry (the server's own sentence), "no access" on a 403, and an offline banner when older rows are still shown.
 */
export function PagedListView<T>({ query, renderItem, keyOf, header, emptyTitle, emptyHint, permission }: Props<T>) {
  const { t } = useTranslation()
  const { items, isPending, isError, error, isFetchingNextPage, isRefetching, hasNextPage, fetchNextPage, refetch } = query

  let body: ReactNode = null
  if (isError && isForbidden(error)) body = <NoAccess permission={permission} />
  else if (isPending) {
    body = (
      <View className="gap-3 p-4" accessibilityLabel={t('common.loading')} accessibilityLiveRegion="polite">
        {[0, 1, 2, 3].map((i) => <Skeleton key={i} className="h-20 w-full" />)}
      </View>
    )
  } else if (isError && items.length === 0) {
    body = (
      <View className="gap-3 p-4">
        <Alert tone="danger">{isNetworkError(error) ? t('common.networkError') : serverMessage(error, t('common.loadError'))}</Alert>
        <Button variant="outline" onPress={() => refetch()}>{t('common.retry')}</Button>
      </View>
    )
  } else if (items.length === 0) {
    body = <EmptyState icon={<Inbox color={colors.inkMuted} size={40} strokeWidth={icon.stroke} />} title={emptyTitle} hint={emptyHint ?? t('common.pullToRefresh')} />
  }

  return (
    <View className="flex-1 bg-page">
      {header ? <View className="gap-2 px-4 pb-2 pt-3">{header}</View> : null}
      <OfflineBanner show={isError && items.length > 0 && isNetworkError(error)} />
      {body ?? (
        <FlatList
          data={items}
          keyExtractor={keyOf}
          renderItem={({ item }) => <View className="px-4 pb-3">{renderItem(item)}</View>}
          onEndReached={() => hasNextPage && !isFetchingNextPage && fetchNextPage()}
          onEndReachedThreshold={0.4}
          ListFooterComponent={<ListFooter loading={isFetchingNextPage} />}
          refreshControl={<RefreshControl refreshing={isRefetching && !isFetchingNextPage} onRefresh={() => refetch()} />}
        />
      )}
    </View>
  )
}
