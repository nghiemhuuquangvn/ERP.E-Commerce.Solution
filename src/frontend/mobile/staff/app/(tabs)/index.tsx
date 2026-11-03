import { View, Text } from 'react-native'
import { useTranslation } from 'react-i18next'
import { useAuthStore } from '@/stores/auth.store'
import { Card, Screen } from '@/components/ui'

function KpiCard({ label, value, tone }: { label: string; value: string; tone: string }) {
  return (
    <View className="flex-1">
      <Card>
        <Text className="mb-1 text-sm text-ink-secondary">{label}</Text>
        <Text accessibilityLabel={`${label}: ${value}`} className={`text-2xl font-bold ${tone}`}>{value}</Text>
      </Card>
    </View>
  )
}

export default function DashboardScreen() {
  const { t } = useTranslation()
  const user = useAuthStore((s) => s.user)

  return (
    <Screen>
      <Text accessibilityRole="header" className="mb-4 text-lg font-semibold text-ink">
        {t('dashboard.goodMorning', { name: user?.firstName })}
      </Text>

      <View className="mb-4 flex-row gap-3">
        <KpiCard label={t('dashboard.todaysSales')} value={t('dashboard.salesValue')} tone="text-brand-action" />
        <KpiCard label={t('dashboard.orders')} value="0" tone="text-ink" />
      </View>

      <View className="mb-6 flex-row gap-3">
        <KpiCard label={t('dashboard.lowStock')} value="0" tone="text-warning" />
        <KpiCard label={t('dashboard.pendingShipments')} value="0" tone="text-info-soft-text" />
      </View>

      <Card>
        <Text accessibilityRole="header" className="mb-3 font-semibold text-ink">{t('dashboard.quickActions')}</Text>
        <Text className="text-sm text-ink-secondary">{t('dashboard.quickActionsHint')}</Text>
      </Card>
    </Screen>
  )
}
