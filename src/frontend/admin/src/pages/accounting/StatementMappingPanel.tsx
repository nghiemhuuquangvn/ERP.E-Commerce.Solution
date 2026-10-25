import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { statementMappingsApi } from '@/api/accounting/statement-mappings.api'
import type { MappingSide, StatementMappingInput } from '@/api/accounting/statement-mappings.api'

const STATEMENTS = ['B01-DN', 'B02-DN', 'B03-DN', 'B03-DN-GT', 'B09-DN']

/** The tenant's own account placement on B01 / B02: pick an unmapped account's prefix and the row it belongs to. */
export function StatementMappingPanel({ canEdit }: { canEdit: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [statementId, setStatementId] = useState('B01-DN')
  const [prefix, setPrefix] = useState('')
  const [rowCode, setRowCode] = useState('')
  const [side, setSide] = useState<MappingSide>('Net')

  const { data, isLoading } = useQuery({
    queryKey: ['accounting', 'statementMapping', statementId],
    queryFn: () => statementMappingsApi.get(statementId),
    staleTime: 15_000,
  })

  const current: StatementMappingInput[] = (data?.rows ?? []).flatMap((r) =>
    r.mappings.map((m) => ({ rowCode: r.code, accountPrefix: m.accountPrefix, side: m.side })),
  )

  const save = useMutation({
    mutationFn: (mappings: StatementMappingInput[]) => statementMappingsApi.update(statementId, mappings),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['accounting'] })
      setPrefix('')
      setRowCode('')
      setSide('Net')
    },
  })
  const error = (save.error as { response?: { data?: { detail?: string } } } | null)?.response?.data?.detail

  const mappable = (data?.rows ?? []).filter((r) => r.isMappable)

  return (
    <div className="space-y-4">
      <p className="text-sm text-gray-500">{t('accounting.mapping.hint')}</p>

      <div className="flex flex-wrap items-end gap-3">
        <div>
          <label className="mb-1 block text-sm font-medium text-gray-700">{t('accounting.mapping.statement')}</label>
          <select className="input" value={statementId} onChange={(e) => setStatementId(e.target.value)}>
            {STATEMENTS.map((s) => (
              <option key={s} value={s}>
                {s}
              </option>
            ))}
          </select>
        </div>
      </div>

      {isLoading && <p className="text-sm text-gray-400">{t('common.loading')}</p>}

      {data && (
        <>
          <div className="card p-4">
            <h3 className="mb-2 text-sm font-semibold text-gray-900">{t('accounting.mapping.unmapped')}</h3>
            {data.unmappedOnReport ? (
              <p className="text-sm text-gray-600">{t('accounting.mapping.unmappedOnReport')}</p>
            ) : data.unmapped.length === 0 ? (
              <p className="text-sm text-green-700">{t('accounting.mapping.noneUnmapped')}</p>
            ) : (
              <ul className="space-y-1 text-sm text-gray-700">
                {data.unmapped.map((u) => (
                  <li key={u.accountNumber}>
                    <button className="font-mono text-blue-600 hover:underline" onClick={() => setPrefix(u.accountNumber)}>
                      {u.accountNumber}
                    </button>{' '}
                    {u.accountName}
                  </li>
                ))}
              </ul>
            )}
          </div>

          {canEdit && (
            <div className="card flex flex-wrap items-end gap-3 p-4">
              <div>
                <label className="mb-1 block text-sm font-medium text-gray-700">{t('accounting.mapping.prefix')}</label>
                <input className="input" value={prefix} onChange={(e) => setPrefix(e.target.value)} />
              </div>
              <div>
                <label className="mb-1 block text-sm font-medium text-gray-700">{t('accounting.mapping.row')}</label>
                <select className="input" value={rowCode} onChange={(e) => setRowCode(e.target.value)}>
                  <option value="" />
                  {mappable.map((r) => (
                    <option key={r.code} value={r.code}>
                      {r.code} — {r.name}
                    </option>
                  ))}
                </select>
              </div>
              {data.sideApplies && (
              <div>
                <label className="mb-1 block text-sm font-medium text-gray-700">{t('accounting.mapping.side')}</label>
                <select className="input" value={side} onChange={(e) => setSide(e.target.value as MappingSide)}>
                  <option value="Net">{t('accounting.mapping.sideNet')}</option>
                  <option value="DebitOnly">{t('accounting.mapping.sideDebit')}</option>
                  <option value="CreditOnly">{t('accounting.mapping.sideCredit')}</option>
                </select>
              </div>
              )}
              <button
                className="btn-primary"
                disabled={!prefix || !rowCode || save.isPending}
                onClick={() => save.mutate([...current, { rowCode, accountPrefix: prefix, side }])}
              >
                {t('accounting.mapping.add')}
              </button>
              {save.isError && <span className="text-sm text-red-600">{error ?? t('accounting.mapping.failed')}</span>}
            </div>
          )}

          <div className="card overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b text-left text-xs uppercase text-gray-500">
                  <th className="p-2">{t('accounting.mapping.row')}</th>
                  <th className="p-2">{t('accounting.mapping.standard')}</th>
                  <th className="p-2">{t('accounting.mapping.own')}</th>
                </tr>
              </thead>
              <tbody>
                {data.rows
                  .filter((r) => r.isMappable)
                  .map((r) => (
                    <tr key={r.code} className="border-b">
                      <td className="p-2">
                        {r.code} — {r.name}
                      </td>
                      <td className="p-2 font-mono text-xs text-gray-500">{r.standardPrefixes.join(', ')}</td>
                      <td className="p-2">
                        {r.mappings.map((m) => (
                          <span key={m.accountPrefix} className="mr-3 font-mono text-xs">
                            {m.accountPrefix}
                            {m.side !== 'Net' ? ` (${m.side})` : ''}
                            {canEdit && (
                              <button
                                className="ml-1 text-red-600 hover:underline"
                                onClick={() => save.mutate(current.filter((c) => !(c.rowCode === r.code && c.accountPrefix === m.accountPrefix)))}
                              >
                                ×
                              </button>
                            )}
                          </span>
                        ))}
                      </td>
                    </tr>
                  ))}
              </tbody>
            </table>
          </div>
        </>
      )}
    </div>
  )
}
