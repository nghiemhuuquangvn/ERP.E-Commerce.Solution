import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { departmentsApi } from '@/api/masterdata/departments.api'
import type { Department } from '@/api/masterdata/departments.api'
import { employeesApi } from '@/api/masterdata/employees.api'
import { divisionsApi } from '@/api/masterdata/divisions.api'
import { DataTable } from '@/components/shared/DataTable'
import { Pagination } from '@/components/shared/Pagination'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'

interface DepartmentFormValues {
  name: string
  code: string
  managerId: string
  parentDepartmentId: string
  divisionId: string
}

function DepartmentModal({
  department,
  departments,
  onClose,
}: {
  department: Department | null
  departments: Department[]
  onClose: () => void
}) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const isEdit = !!department

  const { data: employeesPage } = useQuery({
    queryKey: ['masterdata', 'employees', 'picker'],
    queryFn: () => employeesApi.list({ page: 1, pageSize: 100 }),
    staleTime: 60_000,
  })

  const { data: divisionsPage } = useQuery({
    queryKey: ['masterdata', 'divisions', 'picker'],
    queryFn: () => divisionsApi.list({ page: 1, pageSize: 200 }),
    staleTime: 60_000,
    enabled: isEdit,
  })

  const { register, handleSubmit, formState: { errors } } = useForm<DepartmentFormValues>({
    defaultValues: {
      name: department?.departmentName ?? '',
      code: department?.code ?? '',
      managerId: department?.managerId ?? '',
      parentDepartmentId: department?.parentDepartmentId ?? '',
      divisionId: department?.divisionId ?? '',
    },
  })

  const mutation = useMutation({
    mutationFn: async (values: DepartmentFormValues) => {
      const managerId = values.managerId.trim() || null
      const parentDepartmentId = values.parentDepartmentId.trim() || null
      if (isEdit) {
        const result = await departmentsApi.update(department!.id, {
          name: values.name,
          code: values.code,
          managerId,
        })
        const divisionId = values.divisionId.trim() || null
        if (divisionId !== (department!.divisionId ?? null)) {
          return departmentsApi.setDivision(department!.id, { divisionId })
        }
        return result
      }
      return departmentsApi.create({
        name: values.name,
        code: values.code,
        managerId,
        parentDepartmentId,
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['masterdata', 'departments'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">
          {isEdit ? t('masterdata.departments.editTitle') : t('masterdata.departments.addTitle')}
        </h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('masterdata.departments.name')}</label>
            <input className="input" {...register('name', { required: true })} />
            {errors.name && <p className="mt-1 text-xs text-red-600">{t('common.name')} is required.</p>}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('masterdata.departments.code')}</label>
            <input className="input" {...register('code', { required: true })} />
            {errors.code && <p className="mt-1 text-xs text-red-600">{t('common.code')} is required.</p>}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('masterdata.departments.manager')}</label>
            <select className="input" {...register('managerId')}>
              <option value="">{t('common.none')}</option>
              {(employeesPage?.items ?? []).map((e) => (
                <option key={e.id} value={e.id}>
                  {e.fullName}
                </option>
              ))}
            </select>
          </div>
          {!isEdit && (
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">
                {t('masterdata.departments.parentDepartment')}
              </label>
              <select className="input" {...register('parentDepartmentId')}>
                <option value="">{t('common.none')}</option>
                {departments.map((d) => (
                  <option key={d.id} value={d.id}>
                    {d.departmentName}
                  </option>
                ))}
              </select>
            </div>
          )}
          {isEdit && (
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">
                {t('masterdata.departments.division')}
              </label>
              <select className="input" {...register('divisionId')}>
                <option value="">{t('common.none')}</option>
                {(divisionsPage?.items ?? []).map((div) => (
                  <option key={div.id} value={div.id}>
                    {div.name}
                  </option>
                ))}
              </select>
            </div>
          )}
          {mutation.isError && (
            <p className="text-xs text-red-600">{t('masterdata.departments.failedToSave')}</p>
          )}
          <div className="flex justify-end gap-3 pt-2">
            <button type="button" onClick={onClose} className="btn-outline">
              {t('common.cancel')}
            </button>
            <button type="submit" disabled={mutation.isPending} className="btn-primary">
              {mutation.isPending ? t('common.saving') : t('common.save')}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

export function DepartmentsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const { page, pageSize, setPage } = usePagination()
  const [modalState, setModalState] = useState<{ open: boolean; department: Department | null }>({
    open: false,
    department: null,
  })

  const { data, isLoading } = useQuery({
    queryKey: ['masterdata', 'departments', { page, pageSize }],
    queryFn: () => departmentsApi.list({ page, pageSize }),
    staleTime: 30_000,
  })

  const canManage = hasPermission('masterdata:departments:manage')

  const columns = useMemo(
    () => [
      { key: 'code', label: t('common.code'), className: 'font-mono text-xs' },
      { key: 'departmentName', label: t('common.name') },
      {
        key: 'managerId',
        label: t('masterdata.departments.manager'),
        render: (d: Department) => d.managerId ?? '—',
      },
      {
        key: 'parentDepartmentId',
        label: t('masterdata.departments.parentDepartment'),
        render: (d: Department) => d.parentDepartmentId ?? '—',
      },
      {
        key: 'divisionId',
        label: t('masterdata.departments.division'),
        render: (d: Department) => d.divisionId ?? '—',
      },
      {
        key: 'actions',
        label: '',
        render: (d: Department) =>
          canManage ? (
            <button
              className="text-xs text-primary-600 hover:underline"
              onClick={() => setModalState({ open: true, department: d })}
            >
              {t('common.edit')}
            </button>
          ) : null,
      },
    ],
    [canManage, t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('masterdata.departments.title')}</h1>
        {canManage && (
          <button
            className="btn-primary"
            onClick={() => setModalState({ open: true, department: null })}
          >
            {t('masterdata.departments.add')}
          </button>
        )}
      </div>

      <DataTable columns={columns} data={data?.items ?? []} isLoading={isLoading} />
      <Pagination
        page={page}
        pageSize={pageSize}
        totalCount={data?.totalCount ?? 0}
        onPageChange={setPage}
      />

      {modalState.open && (
        <DepartmentModal
          department={modalState.department}
          departments={data?.items ?? []}
          onClose={() => setModalState({ open: false, department: null })}
        />
      )}
    </div>
  )
}
