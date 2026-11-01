import { useMemo } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { usePermission } from '@/hooks/usePermission'
import { EmptyState } from '@/components/ui'
import { ContactsTab } from './ContactsTab'
import { LeadsTab } from './LeadsTab'
import { OpportunitiesTab } from './OpportunitiesTab'
import { CasesTab } from './CasesTab'
import { ActivitiesTab } from './ActivitiesTab'

type TabKey = 'contacts' | 'leads' | 'opportunities' | 'cases' | 'activities'

const TABS: { key: TabKey; labelKey: string; permission: string; Component: () => JSX.Element }[] = [
  { key: 'contacts', labelKey: 'crm.tabs.contacts', permission: 'crm:contacts:view', Component: ContactsTab },
  { key: 'leads', labelKey: 'crm.tabs.leads', permission: 'crm:leads:view', Component: LeadsTab },
  { key: 'opportunities', labelKey: 'crm.tabs.opportunities', permission: 'crm:opportunities:view', Component: OpportunitiesTab },
  { key: 'cases', labelKey: 'crm.tabs.cases', permission: 'crm:cases:view', Component: CasesTab },
  { key: 'activities', labelKey: 'crm.tabs.activities', permission: 'crm:activities:view', Component: ActivitiesTab },
]

export function CustomersPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const [params, setParams] = useSearchParams()

  const visibleTabs = useMemo(() => TABS.filter((tab) => hasPermission(tab.permission)), [hasPermission])

  if (visibleTabs.length === 0) {
    return <EmptyState variant="forbidden" title={t('common.noPermission')} />
  }

  // the active tab lives in the URL (?tab=leads) so a reload or a shared link opens the same tab
  const current = visibleTabs.find((tab) => tab.key === params.get('tab')) ?? visibleTabs[0]
  const select = (key: TabKey) => setParams({ tab: key }, { replace: true })

  const move = (index: number, delta: number) => {
    const next = visibleTabs[(index + delta + visibleTabs.length) % visibleTabs.length]
    select(next.key)
    document.getElementById(`crm-tab-${next.key}`)?.focus()
  }

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t('crm.title')}</h1>

      <div role="tablist" aria-label={t('crm.title')} className="flex gap-1 border-b border-line">
        {visibleTabs.map((tab, i) => (
          <button
            key={tab.key}
            id={`crm-tab-${tab.key}`}
            type="button"
            role="tab"
            aria-selected={current.key === tab.key}
            aria-controls="crm-tabpanel"
            tabIndex={current.key === tab.key ? 0 : -1}
            onClick={() => select(tab.key)}
            onKeyDown={(e) => {
              if (e.key === 'ArrowRight') { e.preventDefault(); move(i, 1) }
              else if (e.key === 'ArrowLeft') { e.preventDefault(); move(i, -1) }
            }}
            className={`px-4 py-2 text-sm font-medium transition-colors ${
              current.key === tab.key
                ? 'border-b-2 border-brand-action text-brand-action'
                : 'text-ink-secondary hover:text-ink'
            }`}
          >
            {t(tab.labelKey)}
          </button>
        ))}
      </div>

      <div id="crm-tabpanel" role="tabpanel" aria-labelledby={`crm-tab-${current.key}`}>
        <current.Component />
      </div>
    </div>
  )
}
