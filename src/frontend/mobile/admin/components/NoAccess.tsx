import { useTranslation } from 'react-i18next'
import { Lock } from 'lucide-react-native'
import { EmptyState } from '@/components/ui'
import { colors, icon } from '@/theme/theme'

/** The signed-in user lacks the permission for this screen's data (a 403 or a missing permission). */
export function NoAccess({ permission }: { permission?: string }) {
  const { t } = useTranslation()
  return (
    <EmptyState
      icon={<Lock color={colors.inkMuted} size={40} strokeWidth={icon.stroke} />}
      title={t('common.noAccess')}
      hint={permission ? t('common.noAccessHint', { permission }) : undefined}
    />
  )
}
