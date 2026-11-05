import { RefreshControl, ScrollView, Text, View } from 'react-native'
import { Stack, useLocalSearchParams, useRouter } from 'expo-router'
import { useTranslation } from 'react-i18next'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Gauge } from 'lucide-react-native'
import { Badge, Card, EmptyState } from '@/components/ui'
import { ApprovalBar, parseAmount, type ApprovalAction } from '@/components/ApprovalBar'
import { DetailHeader, DetailSection } from '@/components/DetailSection'
import { DetailRows } from '@/components/DetailRows'
import { DetailState } from '@/components/DetailState'
import { ListFilters } from '@/components/ListFilters'
import { PagedListView } from '@/components/PagedListView'
import { useListFilters } from '@/hooks/useListFilters'
import { formatDate } from '@/lib/dates'
import { formatOrderMoney } from '@/lib/money'
import { useAuthStore } from '@/stores/auth.store'
import { usePagedList } from '@/modules/usePagedList'
import { salesExtrasApi, type CreditProfile } from '@/modules/sales/api'
import { colors, icon } from '@/theme/theme'

export type CreditLevel = 'over' | 'low' | 'ok'

/** Over the limit (nothing available), low (under a tenth of the limit is left) or fine. The word is always shown; the colour only backs it up. */
export function creditLevel(p: Pick<CreditProfile, 'creditLimit' | 'availableCredit'>): CreditLevel {
  if (p.availableCredit <= 0) return 'over'
  return p.creditLimit > 0 && p.availableCredit < p.creditLimit / 10 ? 'low' : 'ok'
}

const TONE = { over: 'danger', low: 'warning', ok: 'success' } as const

export const displayName = (p: Pick<CreditProfile, 'customerName' | 'customerId'>) => p.customerName || p.customerId.slice(0, 8)

/** Customers that have a credit limit, highest open balance first. Search by customer name or id. */
export function CreditLimitsScreen() {
  const { t } = useTranslation()
  const router = useRouter()
  const filters = useListFilters()
  const query = usePagedList(['so', 'credit', filters.params], (page) => salesExtrasApi.creditLimits(filters.params, page))
  return (
    <PagedListView<CreditProfile>
      query={query}
      keyOf={(c) => c.customerId}
      permission="sales_order:read"
      emptyTitle={t('lists.credit.empty')}
      emptyHint={t('lists.credit.emptyHint')}
      header={<ListFilters filters={filters} noDates searchLabel={t('lists.credit.search')} />}
      renderItem={(c) => {
        const level = creditLevel(c)
        return (
          <Card
            onPress={() => router.push(`/sales/credit/${c.customerId}`)}
            accessibilityLabel={`${displayName(c)}, ${t('credit.limit')} ${formatOrderMoney(c.creditLimit)}, ${t('credit.available')} ${formatOrderMoney(c.availableCredit)}, ${t(`credit.level.${level}`)}`}
          >
            <View className="flex-row items-center justify-between gap-2">
              <Text className="flex-1 text-base font-semibold text-ink">{displayName(c)}</Text>
              <Badge tone={TONE[level]} label={t(`credit.level.${level}`)} />
            </View>
            <View className="mt-1 flex-row justify-between gap-2">
              <Text className="text-sm text-ink-secondary">{t('credit.limit')}: {formatOrderMoney(c.creditLimit)}</Text>
              <Text className="text-sm text-ink-secondary">{t('credit.balance')}: {formatOrderMoney(c.currentBalance)}</Text>
            </View>
            <Text className="mt-1 text-sm font-semibold text-ink">{t('credit.available')}: {formatOrderMoney(c.availableCredit)}</Text>
          </Card>
        )
      }}
    />
  )
}

function actionList(p: CreditProfile, t: (k: string, o?: Record<string, unknown>) => string): ApprovalAction[] {
  return [
    {
      key: 'change', label: t('credit.change'), variant: 'primary', confirmTitle: t('credit.changeTitle', { name: displayName(p) }),
      fields: [{ key: 'creditLimit', label: t('credit.newLimit'), kind: 'number', required: true, initial: String(p.creditLimit) }],
      run: (_reason, v) => salesExtrasApi.setCreditLimit(p.customerId, parseAmount(v?.creditLimit ?? '') ?? 0),
      successMessage: t('credit.changed', { name: displayName(p) }),
    },
  ]
}

/** One customer's limit, open balance and what is still available; the limit can be changed with sales_order:approve. */
export function CreditLimitDetailScreen() {
  const { t } = useTranslation()
  const { customerId } = useLocalSearchParams<{ customerId: string }>()
  const qc = useQueryClient()
  const has = useAuthStore((s) => s.hasPermission)
  const query = useQuery({ queryKey: ['so', 'credit', customerId], queryFn: () => salesExtrasApi.creditLimit(customerId) })
  const done = () => { void qc.invalidateQueries({ queryKey: ['so'] }) }

  return (
    <View className="flex-1 bg-page">
      <Stack.Screen options={{ title: t('credit.title') }} />
      <DetailState query={query} permission="sales_order:read">
        {(p) =>
          p === null ? (
            <EmptyState icon={<Gauge color={colors.inkMuted} size={40} strokeWidth={icon.stroke} />} title={t('credit.noLimit')} hint={t('credit.noLimitHint')} />
          ) : (
            <>
              <ScrollView contentContainerClassName="gap-3 p-4" refreshControl={<RefreshControl refreshing={query.isRefetching} onRefresh={() => void query.refetch()} />}>
                <DetailHeader title={displayName(p)} badge={<Badge tone={TONE[creditLevel(p)]} label={t(`credit.level.${creditLevel(p)}`)} />} />
                <DetailSection title={t('credit.summary')}>
                  <DetailRows
                    rows={[
                      { label: t('credit.limit'), value: formatOrderMoney(p.creditLimit) },
                      { label: t('credit.balance'), value: formatOrderMoney(p.currentBalance) },
                      { label: t('credit.available'), value: formatOrderMoney(p.availableCredit) },
                      { label: t('credit.updated'), value: formatDate(p.updatedAt) },
                      { label: t('credit.customerId'), value: p.customerId },
                    ]}
                  />
                </DetailSection>
                <Text className="text-xs text-ink-muted">{t('credit.explain')}</Text>
              </ScrollView>
              <ApprovalBar actions={has('sales_order:approve') ? actionList(p, t as never) : []} onDone={done} />
            </>
          )
        }
      </DetailState>
    </View>
  )
}
