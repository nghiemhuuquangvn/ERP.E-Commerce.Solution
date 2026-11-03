import { Pressable, Text, View } from 'react-native'
import { useQuery } from '@tanstack/react-query'
import axios from 'axios'
import { useTranslation } from 'react-i18next'
import { apiClient } from '@/api/client'
import { Alert, BottomSheet, Button, EmptyState, Skeleton } from '@/components/ui'

export interface PosTerminal {
  id: string
  name: string
  isActive: boolean
}

/** "Which POS terminal are you selling from?" - the active terminals of the shop, one tap to choose. */
export function TerminalSheet({ visible, selectedId, onClose, onChoose }: { visible: boolean; selectedId: string | null; onClose: () => void; onChoose: (t: PosTerminal) => void }) {
  const { t } = useTranslation()
  const { data, isLoading, isError, error, refetch } = useQuery({
    queryKey: ['pos-terminals'],
    enabled: visible,
    retry: false,
    queryFn: async () => (await apiClient.get<PosTerminal[]>('/v1/pos/terminals')).data.filter((x) => x.isActive),
  })
  const forbidden = isError && axios.isAxiosError(error) && error.response?.status === 403

  return (
    <BottomSheet visible={visible} title={t('pos.terminalTitle')} onClose={onClose} closeLabel={t('common.close')}>
      {isLoading ? (
        <View accessibilityLabel={t('common.loading')} accessibilityLiveRegion="polite" className="gap-2">
          {[0, 1, 2].map((i) => <Skeleton key={i} className="h-14 w-full" />)}
        </View>
      ) : forbidden ? (
        <Alert tone="danger">{t('pos.terminalForbidden')}</Alert>
      ) : isError ? (
        <View className="gap-3">
          <Alert tone="danger">{t('common.loadError')}</Alert>
          <Button size="lg" variant="outline" onPress={() => refetch()}>{t('common.retry')}</Button>
        </View>
      ) : (data ?? []).length === 0 ? (
        <EmptyState title={t('pos.noTerminal')} hint={t('pos.noTerminalHint')} />
      ) : (
        <View accessibilityRole="radiogroup" accessibilityLabel={t('pos.terminalTitle')} className="gap-2">
          {(data ?? []).map((term) => {
            const selected = term.id === selectedId
            return (
              <Pressable
                key={term.id}
                onPress={() => onChoose(term)}
                accessibilityRole="radio"
                accessibilityState={{ selected, checked: selected }}
                aria-checked={selected}
                className={`min-h-[52px] justify-center rounded-control border px-4 ${selected ? 'border-brand-action bg-brand-action' : 'border-line-strong bg-surface'}`}
              >
                <Text className={selected ? 'font-semibold text-on-brand' : 'text-ink'}>{term.name}</Text>
              </Pressable>
            )
          })}
        </View>
      )}
    </BottomSheet>
  )
}
