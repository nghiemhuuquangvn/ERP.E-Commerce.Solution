import { FlatList, Text, View } from 'react-native'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Truck } from 'lucide-react-native'
import { colors, icon } from '@/theme/theme'
import { apiClient } from '@/api/client'
import { Alert, Badge, Button, Card, EmptyState, Screen, Skeleton, useToast } from '@/components/ui'

interface Shipment {
  id: string
  orderId: string
  status: string
  trackingNumber: string
  estimatedDelivery: string
}

const STATUS_TONE: Record<string, 'neutral' | 'info' | 'brand' | 'success' | 'danger'> = {
  Pending: 'neutral',
  PickedUp: 'info',
  InTransit: 'brand',
  Delivered: 'success',
  Failed: 'danger',
}

export default function DeliveriesScreen() {
  const { t } = useTranslation()
  const toast = useToast()
  const queryClient = useQueryClient()

  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ['shipments'],
    queryFn: async () => {
      const { data } = await apiClient.get('/v1/shipments', { params: { pageSize: 50 } })
      return data.items as Shipment[]
    },
  })

  const addTracking = useMutation({
    mutationFn: async ({ id, status, location }: { id: string; status: string; location: string }) =>
      apiClient.post(`/v1/shipments/${id}/tracking`, { status, location, description: `Updated by staff` }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['shipments'] }),
    onError: () => toast.error(t('deliveries.errorUpdatingTracking')),
  })

  return (
    <Screen scroll={false}>
      {isLoading ? (
        <View accessibilityLabel={t('deliveries.loading')} accessibilityLiveRegion="polite" className="gap-3 p-4">
          {[0, 1, 2].map((i) => <Skeleton key={i} className="h-28 w-full" />)}
        </View>
      ) : isError ? (
        <View className="gap-3 p-4">
          <Alert tone="danger">{t('common.loadError')}</Alert>
          <Button size="lg" variant="outline" onPress={() => refetch()}>{t('common.retry')}</Button>
        </View>
      ) : (
        <FlatList
          data={data ?? []}
          keyExtractor={(s) => s.id}
          contentContainerClassName="p-4 gap-3"
          renderItem={({ item }) => (
            <Card>
              <View className="mb-2 flex-row items-center justify-between">
                <Text accessibilityLabel={t('deliveries.tracking', { number: item.trackingNumber })} className="font-medium text-ink">#{item.trackingNumber}</Text>
                <Badge tone={STATUS_TONE[item.status] ?? 'neutral'} label={t(`deliveries.statuses.${item.status}`, { defaultValue: item.status })} />
              </View>
              <Text className="mb-3 text-sm text-ink-secondary">{t('deliveries.order')}: {item.orderId.slice(0, 8)}…</Text>
              {item.status !== 'Delivered' && (
                <Button
                  size="lg"
                  variant="outline"
                  loading={addTracking.isPending && addTracking.variables?.id === item.id}
                  accessibilityLabel={t('deliveries.markInTransitFor', { number: item.trackingNumber })}
                  onPress={() => addTracking.mutate({ id: item.id, status: 'InTransit', location: 'Warehouse' })}
                >
                  {t('deliveries.markInTransit')}
                </Button>
              )}
            </Card>
          )}
          ListEmptyComponent={
            <EmptyState icon={<Truck size={40} strokeWidth={icon.stroke} color={colors.inkMuted} />} title={t('deliveries.empty')} />
          }
        />
      )}
    </Screen>
  )
}
