import { useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { FileUp } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { ImportDialog } from '@/components/import/ImportDialog'
import { Button } from '@/components/ui'
import { usePermission } from '@/hooks/usePermission'
import { employeesImportEndpoints } from '@/api/masterdata/employees.api'

/**
 * Import of the employee list from a CSV file. The employees are master data (SharedMasterDataService owns them); HRM's list follows through
 * the EmployeeHired events a moment later, so the HRM pickers are refreshed after the import.
 */
export function EmployeeImportPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const [open, setOpen] = useState(false)
  const allowed = hasPermission('masterdata:employees:manage')

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t('import.employees.pageTitle')}</h1>
      <p className="max-w-2xl text-sm text-ink-secondary">{t('import.employees.pageIntro')}</p>
      {allowed && (
        <Button onClick={() => setOpen(true)}>
          <FileUp size={16} strokeWidth={1.75} aria-hidden="true" />
          {t('import.employees.open')}
        </Button>
      )}
      <ImportDialog
        open={open}
        onClose={() => setOpen(false)}
        titleKey="import.employees.title"
        columnsKey="import.employees.columns"
        endpoints={employeesImportEndpoints}
        templateFileName="employees-import-template.csv"
        onFinished={() => {
          void queryClient.invalidateQueries({ queryKey: ['masterdata', 'employees'] })
          void queryClient.invalidateQueries({ queryKey: ['hrm'] })
        }}
      />
    </div>
  )
}
