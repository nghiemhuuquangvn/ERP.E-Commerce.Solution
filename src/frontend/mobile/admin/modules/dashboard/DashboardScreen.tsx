import { useState } from 'react'
import { Pressable, RefreshControl, ScrollView, Text, View } from 'react-native'
import { useRouter } from 'expo-router'
import { useTranslation } from 'react-i18next'
import { useQueries, useQuery } from '@tanstack/react-query'
import { Alert, Badge, Button, Card, Skeleton } from '@/components/ui'
import { KpiCard } from '@/components/KpiCard'
import { NoAccess } from '@/components/NoAccess'
import { RevenueSparkline } from '@/components/RevenueSparkline'
import { SegmentedControl } from '@/components/SegmentedControl'
import { isNetworkError } from '@/api/client'
import { daysAgo } from '@/lib/dates'
import { formatMoney } from '@/lib/money'
import { serverMessage } from '@/lib/serverMessage'
import { useAuthStore } from '@/stores/auth.store'
import { MODULES } from '@/modules'
import { useVisibleModules } from '@/modules/visible'
import { dashboardApi } from '@/modules/dashboard/api'

type Period = '7' | '30' | '90'
const SALES_PERMISSION = 'reports:sales:view'

function NeedsAction() {
  const { t } = useTranslation()
  const router = useRouter()
  const { visible } = useVisibleModules()
  const permissions = useAuthStore((s) => s.permissions)
  const lines = MODULES.filter((m) => visible.includes(m)).flatMap((m) => m.attention ?? []).filter((a) => permissions.includes(a.permission))
  const counts = useQueries({ queries: lines.map((a) => ({ queryKey: ['attention', a.key], queryFn: a.count, staleTime: 30_000 })) })
  if (lines.length === 0) return null
  const waiting = lines.map((a, i) => ({ a, n: counts[i].data ?? 0 })).filter((x) => x.n > 0)
  return (
    <Card>
      <Text accessibilityRole="header" className="mb-2 font-semibold text-ink">{t('home.needsAction')}</Text>
      {waiting.length === 0 ? (
        <Text className="text-sm text-ink-secondary">{counts.some((c) => c.isPending) ? t('common.loading') : t('home.nothingToDo')}</Text>
      ) : (
        waiting.map(({ a, n }) => (
          <Pressable
            key={a.key}
            onPress={() => router.push(a.href as never)}
            accessibilityRole="button"
            accessibilityLabel={`${n} ${t(a.labelKey)}`}
            className="min-h-[48px] flex-row items-center justify-between gap-2 border-t border-line"
          >
            <View className="flex-1 flex-row items-center gap-2">
              <Badge tone="warning" label={String(n)} />
              <Text className="flex-1 text-sm text-ink">{t(a.labelKey)}</Text>
            </View>
            <Text className="text-ink-muted">›</Text>
          </Pressable>
        ))
      )}
    </Card>
  )
}

export default function DashboardScreen() {
  const { t } = useTranslation()
  const [period, setPeriod] = useState<Period>('30')
  const from = daysAgo(Number(period))
  const to = daysAgo(0)
  const query = useQuery({ queryKey: ['dashboard', 'sales', period, to], queryFn: () => dashboardApi.sales(from, to) })
  const status = (query.error as { response?: { status?: number } } | null)?.response?.status

  const data = query.data
  const summary = data
    ? t('home.chartSummary', { days: data.revenueByDay.length, first: formatMoney(data.revenueByDay[0]?.revenue ?? 0), last: formatMoney(data.revenueByDay.at(-1)?.revenue ?? 0) })
    : ''

  return (
    <ScrollView
      className="flex-1 bg-page"
      contentContainerClassName="gap-3 p-4"
      refreshControl={<RefreshControl refreshing={query.isRefetching} onRefresh={() => query.refetch()} />}
    >
      <SegmentedControl
        segments={(['7', '30', '90'] as Period[]).map((p) => ({ value: p, label: t('home.days', { count: Number(p) }) }))}
        value={period}
        onChange={setPeriod}
      />
      {query.isPending ? (
        <View className="gap-3" accessibilityLabel={t('common.loading')} accessibilityLiveRegion="polite">
          <Skeleton className="h-20 w-full" />
          <Skeleton className="h-28 w-full" />
        </View>
      ) : query.isError ? (
        status === 403 ? (
          <NoAccess permission={SALES_PERMISSION} />
        ) : (
          <View className="gap-3">
            <Alert tone="danger">{isNetworkError(query.error) ? t('common.networkError') : serverMessage(query.error, t('common.loadError'))}</Alert>
            <Button variant="outline" onPress={() => query.refetch()}>{t('common.retry')}</Button>
          </View>
        )
      ) : data ? (
        <>
          <View className="flex-row gap-3">
            <KpiCard label={t('home.revenue')} value={formatMoney(data.totalRevenue)} />
            <KpiCard label={t('home.orders')} value={String(data.totalOrders)} />
          </View>
          <View className="flex-row gap-3">
            <KpiCard label={t('home.averageOrder')} value={formatMoney(data.averageOrderValue)} />
          </View>
          <Card>
            <Text className="mb-2 text-sm text-ink-secondary">{t('home.revenueByDay')}</Text>
            <RevenueSparkline values={data.revenueByDay.map((d) => d.revenue)} summary={summary} />
          </Card>
        </>
      ) : null}
      <NeedsAction />
    </ScrollView>
  )
}
