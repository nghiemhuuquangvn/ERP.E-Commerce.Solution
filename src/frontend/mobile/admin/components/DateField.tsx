import { useMemo, useState } from 'react'
import { Pressable, Text, View } from 'react-native'
import { addMonths, eachDayOfInterval, endOfMonth, endOfWeek, format, isSameMonth, parseISO, startOfMonth, startOfWeek } from 'date-fns'
import { useTranslation } from 'react-i18next'
import { CalendarDays, ChevronLeft, ChevronRight } from 'lucide-react-native'
import { BottomSheet } from '@/components/ui'
import { isDay, todayText } from '@/lib/periods'
import { colors, icon } from '@/theme/theme'

interface Props {
  label: string
  /** yyyy-MM-dd or empty. */
  value: string
  onChange: (value: string) => void
  /** Day the calendar opens on when there is no value yet. */
  fallback?: string
}

const WEEK_STARTS_ON = 1 // Monday
const WEEKDAYS = ['mon', 'tue', 'wed', 'thu', 'fri', 'sat', 'sun'] as const

/**
 * A date box: shows the chosen day (yyyy-MM-dd) and opens a month grid to pick one. Plain JavaScript (no native date picker), cells are 44 pt,
 * every day is announced by its full date and the chosen day as selected.
 */
export function DateField({ label, value, onChange, fallback }: Props) {
  const { t } = useTranslation()
  const [open, setOpen] = useState(false)
  const start = isDay(value) ? parseISO(value) : parseISO(isDay(fallback ?? '') ? (fallback as string) : todayText())
  const [month, setMonth] = useState(startOfMonth(start))

  const days = useMemo(
    () => eachDayOfInterval({ start: startOfWeek(month, { weekStartsOn: WEEK_STARTS_ON }), end: endOfWeek(endOfMonth(month), { weekStartsOn: WEEK_STARTS_ON }) }),
    [month],
  )

  function openSheet() {
    setMonth(startOfMonth(start))
    setOpen(true)
  }

  return (
    <>
      <Pressable
        onPress={openSheet}
        accessibilityRole="button"
        accessibilityLabel={`${label}: ${value || t('filters.noDate')}`}
        className="min-h-[48px] flex-1 flex-row items-center gap-2 rounded-control border border-line-strong bg-surface px-3"
      >
        <CalendarDays color={colors.inkMuted} size={18} strokeWidth={icon.stroke} />
        <View className="flex-1">
          <Text className="text-xs text-ink-muted">{label}</Text>
          <Text className={`text-base ${value ? 'text-ink' : 'text-ink-muted'}`}>{value || t('filters.noDate')}</Text>
        </View>
      </Pressable>

      <BottomSheet visible={open} title={label} onClose={() => setOpen(false)} closeLabel={t('common.close')}>
        <View className="gap-2">
          <View className="flex-row items-center justify-between">
            <Pressable onPress={() => setMonth((m) => addMonths(m, -1))} accessibilityRole="button" accessibilityLabel={t('filters.previousMonth')} className="h-11 w-11 items-center justify-center">
              <ChevronLeft color={colors.ink} size={22} strokeWidth={icon.stroke} />
            </Pressable>
            <Text accessibilityRole="header" accessibilityLiveRegion="polite" className="text-base font-semibold text-ink">{format(month, 'yyyy-MM')}</Text>
            <Pressable onPress={() => setMonth((m) => addMonths(m, 1))} accessibilityRole="button" accessibilityLabel={t('filters.nextMonth')} className="h-11 w-11 items-center justify-center">
              <ChevronRight color={colors.ink} size={22} strokeWidth={icon.stroke} />
            </Pressable>
          </View>
          <View className="flex-row">
            {WEEKDAYS.map((w) => (
              <Text key={w} className="flex-1 text-center text-xs text-ink-muted">{t(`filters.weekday.${w}`)}</Text>
            ))}
          </View>
          <View className="flex-row flex-wrap">
            {days.map((d) => {
              const text = format(d, 'yyyy-MM-dd')
              const selected = text === value
              const inMonth = isSameMonth(d, month)
              return (
                <Pressable
                  key={text}
                  onPress={() => { onChange(text); setOpen(false) }}
                  accessibilityRole="button"
                  accessibilityLabel={text}
                  accessibilityState={{ selected }}
                  style={{ width: `${100 / 7}%` }}
                  className="h-11 items-center justify-center"
                >
                  <View className={`h-10 w-10 items-center justify-center rounded-full ${selected ? 'bg-brand-action' : ''}`}>
                    <Text className={`text-sm ${selected ? 'font-semibold text-on-brand' : inMonth ? 'text-ink' : 'text-ink-muted'}`}>{d.getDate()}</Text>
                  </View>
                </Pressable>
              )
            })}
          </View>
          <View className="flex-row gap-2 pt-1">
            <Pressable onPress={() => { onChange(todayText()); setOpen(false) }} accessibilityRole="button" accessibilityLabel={t('filters.today')} className="min-h-[44px] flex-1 items-center justify-center rounded-control border border-line">
              <Text className="text-sm font-semibold text-brand-action">{t('filters.today')}</Text>
            </Pressable>
            <Pressable onPress={() => { onChange(''); setOpen(false) }} accessibilityRole="button" accessibilityLabel={t('filters.clearDate')} className="min-h-[44px] flex-1 items-center justify-center rounded-control border border-line">
              <Text className="text-sm text-ink-secondary">{t('filters.clearDate')}</Text>
            </Pressable>
          </View>
        </View>
      </BottomSheet>
    </>
  )
}
