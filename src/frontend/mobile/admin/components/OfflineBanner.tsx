import { useTranslation } from 'react-i18next'
import { Alert } from '@/components/ui'

/** Shown above a list when the last refresh failed for lack of network but older rows are still on screen. */
export function OfflineBanner({ show }: { show: boolean }) {
  const { t } = useTranslation()
  return show ? <Alert tone="warning">{t('common.offline')}</Alert> : null
}
