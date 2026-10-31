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
import { Alert, Button, FormField, Input, Modal, Select, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

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
  const toast = useToast()
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
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={isEdit ? t('masterdata.departments.editTitle') : t('masterdata.departments.addTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="department-form" loading={mutation.isPending}>{mutation.isPending ? t('common.saving') : t('common.save')}</Button>
        </>
      }
    >
      <form id="department-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        <FormField label={t('masterdata.departments.name')} required error={errors.name ? t('common.required') : undefined}>
          <Input {...register('name', { required: true })} />
        </FormField>
        <FormField label={t('masterdata.departments.code')} required error={errors.code ? t('common.required') : undefined}>
          <Input {...register('code', { required: true })} />
        </FormField>
        <FormField label={t('masterdata.departments.manager')}>
          <Select {...register('managerId')}>
            <option value="">{t('common.none')}</option>
            {(employeesPage?.items ?? []).map((e) => (
              <option key={e.id} value={e.id}>
                {e.fullName}
              </option>
            ))}
          </Select>
        </FormField>
        {!isEdit && (
          <FormField label={t('masterdata.departments.parentDepartment')}>
            <Select {...register('parentDepartmentId')}>
              <option value="">{t('common.none')}</option>
              {departments.map((d) => (
                <option key={d.id} value={d.id}>
                  {d.departmentName}
                </option>
              ))}
            </Select>
          </FormField>
        )}
        {isEdit && (
          <FormField label={t('masterdata.departments.division')}>
            <Select {...register('divisionId')}>
              <option value="">{t('common.none')}</option>
              {(divisionsPage?.items ?? []).map((div) => (
                <option key={div.id} value={div.id}>
                  {div.name}
                </option>
              ))}
            </Select>
          </FormField>
        )}
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('masterdata.departments.failedToSave'))}</Alert>}
      </form>
    </Modal>
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
            <Button variant="link" className="text-xs"
              onClick={() => setModalState({ open: true, department: d })}>
              {t('common.edit')}
            </Button>
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
          <Button
            onClick={() => setModalState({ open: true, department: null })}>
            {t('masterdata.departments.add')}
          </Button>
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
