import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { mfgApi, type StockDocument, type StockDocumentStatus, type StockDocumentType } from '@/api/manufacturing/mfg.api'
import { Pagination } from '@/components/shared/Pagination'
import { Alert, Input, Select, Skeleton } from '@/components/ui'
import { usePermission } from '@/hooks/usePermission'
import { ORDER_PERMISSION } from '@/components/manufacturing/orderActions'
import { StockDocumentsTable } from '@/components/manufacturing/StockDocumentsTable'
import { NoPlant, PageHeader, PlantSelector, usePlantContext } from '@/components/manufacturing/shared'

type StatusChoice = 'attention' | '' | StockDocumentStatus
const PAGE_SIZE = 20
const TYPES: StockDocumentType[] = ['Issue', 'Return', 'Backflush', 'Receipt']

/** The plant's stock documents. The default view is what needs a person: documents still on their way to the Warehouse (Pending) and the ones it refused (Failed). */
export function StockDocumentsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const { plantId, isLoading: plantsLoading } = usePlantContext()
  const [status, setStatus] = useState<StatusChoice>('attention')
  const [type, setType] = useState<StockDocumentType | ''>('')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const base = { plantId: plantId ?? '', type: type || undefined, search: search.trim() || undefined }

  const attention = status === 'attention'
  const pending = useQuery({ queryKey: ['mfg', 'documents', 'attention', 'Pending', base], queryFn: () => mfgApi.listDocuments({ ...base, status: 'Pending', page: 1, pageSize: 100 }), enabled: !!plantId && attention })
  const failed = useQuery({ queryKey: ['mfg', 'documents', 'attention', 'Failed', base], queryFn: () => mfgApi.listDocuments({ ...base, status: 'Failed', page: 1, pageSize: 100 }), enabled: !!plantId && attention })
  const plain = useQuery({ queryKey: ['mfg', 'documents', status, base, page], queryFn: () => mfgApi.listDocuments({ ...base, status: status === 'attention' || status === '' ? undefined : status, page, pageSize: PAGE_SIZE }), enabled: !!plantId && !attention })

  const documents: StockDocument[] = attention
    ? [...(pending.data?.items ?? []), ...(failed.data?.items ?? [])].sort((a, b) => b.createdAt.localeCompare(a.createdAt))
    : plain.data?.items ?? []
  const loading = attention ? pending.isLoading || failed.isLoading : plain.isLoading
  const isError = attention ? pending.isError || failed.isError : plain.isError

  if (!plantsLoading && !plantId) return <div><PageHeader title={t('mfg.nav.stockDocuments')} /><NoPlant /></div>
  return (
    <div>
      <PageHeader title={t('mfg.nav.stockDocuments')} hint={t('mfg.doc.pageHint')} actions={<PlantSelector />} />
      <div className="mb-3 flex flex-wrap gap-3">
        <div className="w-64"><Input type="search" placeholder={t('mfg.doc.searchHint')} aria-label={t('common.search')} value={search} onChange={(e) => { setSearch(e.target.value); setPage(1) }} /></div>
        <Select className="w-52" aria-label={t('common.status')} value={status} onChange={(e) => { setStatus(e.target.value as StatusChoice); setPage(1) }}>
          <option value="attention">{t('mfg.doc.attention')}</option>
          <option value="">{t('common.all')}</option>
          {(['Pending', 'Failed', 'Posted'] as const).map((s) => <option key={s} value={s}>{t(`mfg.docStatus.${s}`)}</option>)}
        </Select>
        <Select className="w-44" aria-label={t('mfg.doc.type')} value={type} onChange={(e) => { setType(e.target.value as StockDocumentType | ''); setPage(1) }}>
          <option value="">{t('common.all')}</option>
          {TYPES.map((x) => <option key={x} value={x}>{t(`mfg.docType.${x}`)}</option>)}
        </Select>
      </div>
      {isError && <Alert variant="danger" className="mb-3">{t('mfg.order.loadFailed')}</Alert>}
      {loading ? <Skeleton className="h-32 w-full" /> : <StockDocumentsTable documents={documents} showOrder canRetry={hasPermission(ORDER_PERMISSION.retry)} canQcRelease={hasPermission(ORDER_PERMISSION.qc)} />}
      {!attention && <Pagination page={page} pageSize={PAGE_SIZE} totalCount={plain.data?.total ?? 0} onPageChange={setPage} />}
    </div>
  )
}
