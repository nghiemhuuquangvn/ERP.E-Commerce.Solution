import { useState } from 'react'
import { Pressable, Text, View } from 'react-native'
import { useTranslation } from 'react-i18next'
import { ChevronLeft, ChevronRight, SlidersHorizontal, X } from 'lucide-react-native'
import { Alert, BottomSheet, Button } from '@/components/ui'
import { DateField } from '@/components/DateField'
import { FilterChips, type Chip } from '@/components/FilterChips'
import { SearchField } from '@/components/SearchField'
import { SegmentedControl, type Segment } from '@/components/SegmentedControl'
import type { ListFilters as Filters } from '@/hooks/useListFilters'
import type { PeriodKind } from '@/lib/periods'
import { colors, icon } from '@/theme/theme'

interface Props {
  filters: Filters
  /** Text of the search box: its label for a screen reader and what it searches for. */
  searchLabel: string
  /** Statuses the list can be filtered by (the "all" chip is added). Leave out when the list has no status. */
  statuses?: Chip<string>[]
  /** Hide the period / date controls for a list that has no date. */
  noDates?: boolean
  /** Hide the search box for a list with nothing to search by. */
  noSearch?: boolean
  /** Period kinds offered (default: all, month, quarter, year, custom). */
  kinds?: PeriodKind[]
}

const DEFAULT_KINDS: PeriodKind[] = ['all', 'month', 'quarter', 'year', 'custom']

/**
 * The filters above every list: the search box (a number or a name), a Filters button with the number of active filters, and a summary line
 * with a clear control per active filter. The sheet holds the status, the period (month / quarter / year, stepped) and a from-to range.
 * Changes apply at once; nothing is lost when the sheet closes.
 */
export function ListFilters({ filters, searchLabel, statuses, noDates, noSearch, kinds = DEFAULT_KINDS }: Props) {
  const { t } = useTranslation()
  const [open, setOpen] = useState(false)
  const { status, kind, search, periodText, activeCount, rangeError } = filters
  const statusLabel = statuses?.find((s) => s.value === status)?.label

  const kindSegments: Segment<PeriodKind>[] = kinds.map((k) => ({ value: k, label: t(`filters.kind.${k}`) }))
  const canFilter = !!statuses || !noDates

  return (
    <View className="gap-2">
      <View className="flex-row items-center gap-2">
        {noSearch ? (
          <View className="flex-1" />
        ) : (
          <View className="flex-1">
            <SearchField value={search} onChangeText={filters.setSearch} placeholder={t('common.search')} label={searchLabel} />
          </View>
        )}
        {canFilter ? (
          <Pressable
            onPress={() => setOpen(true)}
            accessibilityRole="button"
            accessibilityLabel={activeCount ? t('filters.openActive', { count: activeCount }) : t('filters.open')}
            className="min-h-[44px] flex-row items-center gap-1 rounded-control border border-line-strong bg-surface px-3"
          >
            <SlidersHorizontal color={colors.ink} size={18} strokeWidth={icon.stroke} />
            <Text className="text-sm font-semibold text-ink">{t('filters.title')}</Text>
            {activeCount ? (
              <View className="min-w-[20px] items-center rounded-full bg-brand-action px-1.5">
                <Text className="text-xs font-bold text-on-brand">{activeCount}</Text>
              </View>
            ) : null}
          </Pressable>
        ) : null}
      </View>

      {status || periodText ? (
        <View className="flex-row flex-wrap gap-2">
          {status ? <Summary label={statusLabel ?? status} clear={t('filters.clear', { name: statusLabel ?? status })} onClear={() => filters.setStatus('')} /> : null}
          {periodText ? <Summary label={periodText} clear={t('filters.clear', { name: periodText })} onClear={() => filters.setKind('all')} /> : null}
        </View>
      ) : null}

      <BottomSheet visible={open} title={t('filters.title')} onClose={() => setOpen(false)} closeLabel={t('common.close')}>
        <View className="gap-4">
          {statuses ? (
            <View className="gap-1">
              <Text accessibilityRole="header" className="font-semibold text-ink">{t('filters.status')}</Text>
              <FilterChips chips={[{ value: '', label: t('common.all') }, ...statuses]} value={status} onChange={filters.setStatus} />
            </View>
          ) : null}

          {!noDates ? (
            <View className="gap-2">
              <Text accessibilityRole="header" className="font-semibold text-ink">{t('filters.period')}</Text>
              <SegmentedControl<PeriodKind> segments={kindSegments} value={kind} onChange={filters.setKind} />
              {kind === 'month' || kind === 'quarter' || kind === 'year' ? (
                <View className="flex-row items-center justify-between">
                  <Pressable onPress={() => filters.step(-1)} accessibilityRole="button" accessibilityLabel={t('filters.previousPeriod')} className="h-11 w-11 items-center justify-center">
                    <ChevronLeft color={colors.ink} size={22} strokeWidth={icon.stroke} />
                  </Pressable>
                  <Text accessibilityLiveRegion="polite" className="text-base font-semibold text-ink">{periodText}</Text>
                  <Pressable onPress={() => filters.step(1)} accessibilityRole="button" accessibilityLabel={t('filters.nextPeriod')} className="h-11 w-11 items-center justify-center">
                    <ChevronRight color={colors.ink} size={22} strokeWidth={icon.stroke} />
                  </Pressable>
                </View>
              ) : null}
              {kind === 'custom' ? (
                <View className="gap-2">
                  <View className="flex-row gap-2">
                    <DateField label={t('filters.from')} value={filters.from} onChange={filters.setFrom} fallback={filters.to} />
                    <DateField label={t('filters.to')} value={filters.to} onChange={filters.setTo} fallback={filters.from} />
                  </View>
                  {rangeError ? <Alert tone="danger">{t('filters.rangeError')}</Alert> : null}
                </View>
              ) : null}
            </View>
          ) : null}

          <View className="flex-row gap-2">
            <View className="flex-1"><Button variant="outline" onPress={filters.reset}>{t('filters.reset')}</Button></View>
            <View className="flex-1"><Button onPress={() => setOpen(false)}>{t('filters.done')}</Button></View>
          </View>
        </View>
      </BottomSheet>
    </View>
  )
}

function Summary({ label, clear, onClear }: { label: string; clear: string; onClear: () => void }) {
  return (
    <Pressable onPress={onClear} accessibilityRole="button" accessibilityLabel={clear} className="min-h-[44px] flex-row items-center gap-1 rounded-full bg-brand-soft px-3">
      <Text className="text-sm text-brand-soft-text">{label}</Text>
      <X color={colors.brandSoftText} size={14} strokeWidth={icon.stroke} />
    </Pressable>
  )
}
