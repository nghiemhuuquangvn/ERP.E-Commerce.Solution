import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { usePermission } from '@/hooks/usePermission'
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

  const visibleTabs = useMemo(() => TABS.filter((tab) => hasPermission(tab.permission)), [hasPermission])
  const [activeTab, setActiveTab] = useState<TabKey | null>(visibleTabs[0]?.key ?? null)

  if (visibleTabs.length === 0) {
    return <p className="text-sm text-gray-400">{t('common.noPermission')}</p>
  }

  const current = visibleTabs.find((tab) => tab.key === activeTab) ?? visibleTabs[0]

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t('crm.title')}</h1>

      <div className="flex gap-1 border-b border-gray-200">
        {visibleTabs.map((tab) => (
          <button
            key={tab.key}
            onClick={() => setActiveTab(tab.key)}
            className={`px-4 py-2 text-sm font-medium transition-colors ${
              current.key === tab.key
                ? 'border-b-2 border-primary-600 text-primary-700'
                : 'text-gray-500 hover:text-gray-700'
            }`}
          >
            {t(tab.labelKey)}
          </button>
        ))}
      </div>

      <current.Component />
    </div>
  )
}
