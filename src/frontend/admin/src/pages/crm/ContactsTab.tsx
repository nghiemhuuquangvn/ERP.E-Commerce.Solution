import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { contactsApi, CONTACT_SOURCE, type Contact } from '@/api/crm/contacts.api'
import { DataTable } from '@/components/shared/DataTable'
import { Pagination } from '@/components/shared/Pagination'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'

// ─── Create / Edit modal ────────────────────────────────────────────────────

interface ContactFormValues {
  name: string
  email: string
  phone: string
  company: string
}

function ContactModal({ contact, onClose }: { contact: Contact | null; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const isEdit = !!contact
  const { register, handleSubmit, formState: { errors } } = useForm<ContactFormValues>({
    defaultValues: {
      name: contact?.name ?? '',
      email: contact?.email ?? '',
      phone: contact?.phone ?? '',
      company: contact?.company ?? '',
    },
  })

  const mutation = useMutation({
    mutationFn: (values: ContactFormValues) => {
      if (isEdit) {
        return contactsApi.update(contact!.id, {
          contactId: contact!.id,
          name: values.name,
          email: values.email.trim() || null,
          phone: values.phone.trim() || null,
          company: values.company.trim() || null,
        })
      }
      return contactsApi.create({
        name: values.name,
        email: values.email.trim() || null,
        phone: values.phone.trim() || null,
        company: values.company.trim() || null,
        contactSource: CONTACT_SOURCE.Manual,
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['crm', 'contacts'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">
          {isEdit ? t('crm.contacts.editTitle') : t('crm.contacts.addTitle')}
        </h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('common.name')}</label>
            <input className="input" {...register('name', { required: true })} />
            {errors.name && <p className="mt-1 text-xs text-red-600">{t('crm.contacts.required')}</p>}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('crm.contacts.email')}</label>
            <input type="email" className="input" {...register('email')} />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('crm.contacts.phone')}</label>
            <input className="input" {...register('phone')} />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('crm.contacts.company')}</label>
            <input className="input" {...register('company')} />
          </div>
          {mutation.isError && <p className="text-xs text-red-600">{t('crm.contacts.failedToSave')}</p>}
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

// ─── Tags modal ─────────────────────────────────────────────────────────────

function TagsModal({ contact, onClose }: { contact: Contact; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [newTag, setNewTag] = useState('')

  const { data } = useQuery({
    queryKey: ['crm', 'contacts', contact.id, 'tags'],
    queryFn: () => contactsApi.getTags(contact.id),
  })

  const addMutation = useMutation({
    mutationFn: (tag: string) => contactsApi.addTag(contact.id, tag),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['crm', 'contacts', contact.id, 'tags'] })
      setNewTag('')
    },
  })

  const removeMutation = useMutation({
    mutationFn: (tag: string) => contactsApi.removeTag(contact.id, tag),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['crm', 'contacts', contact.id, 'tags'] }),
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">
          {t('crm.contacts.tagsTitle', { name: contact.name })}
        </h3>
        <div className="mb-4 flex flex-wrap gap-2">
          {(data?.tags ?? []).length === 0 && (
            <p className="text-sm text-gray-400">{t('crm.contacts.noTags')}</p>
          )}
          {(data?.tags ?? []).map((tag) => (
            <span key={tag} className="badge flex items-center gap-1 bg-gray-100 text-gray-700">
              {tag}
              <button
                type="button"
                className="text-gray-400 hover:text-red-600"
                onClick={() => removeMutation.mutate(tag)}
              >
                ×
              </button>
            </span>
          ))}
        </div>
        <form
          onSubmit={(e) => {
            e.preventDefault()
            if (newTag.trim()) addMutation.mutate(newTag.trim())
          }}
          className="flex gap-2"
        >
          <input
            className="input"
            placeholder={t('crm.contacts.newTag')}
            value={newTag}
            onChange={(e) => setNewTag(e.target.value)}
          />
          <button type="submit" disabled={addMutation.isPending || !newTag.trim()} className="btn-primary">
            {t('common.add')}
          </button>
        </form>
        <div className="flex justify-end pt-4">
          <button type="button" onClick={onClose} className="btn-outline">
            {t('common.close')}
          </button>
        </div>
      </div>
    </div>
  )
}

// ─── Tab ─────────────────────────────────────────────────────────────────────

type ModalKind = 'edit' | 'tags' | null

export function ContactsTab() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const { page, pageSize, setPage } = usePagination()
  const [search, setSearch] = useState('')
  const [modal, setModal] = useState<ModalKind>(null)
  const [activeContact, setActiveContact] = useState<Contact | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['crm', 'contacts', { page, pageSize, search }],
    queryFn: () => contactsApi.list({ page, pageSize, search: search || undefined }),
    staleTime: 30_000,
  })

  const canManage = hasPermission('crm:contacts:manage')

  const openModal = (kind: ModalKind, contact: Contact) => {
    setActiveContact(contact)
    setModal(kind)
  }

  const closeModal = () => {
    setModal(null)
    setActiveContact(null)
  }

  const columns = useMemo(
    () => [
      { key: 'name', label: t('common.name') },
      { key: 'email', label: t('crm.contacts.email'), render: (c: Contact) => c.email ?? '—' },
      { key: 'phone', label: t('crm.contacts.phone'), render: (c: Contact) => c.phone ?? '—' },
      { key: 'company', label: t('crm.contacts.company'), render: (c: Contact) => c.company ?? '—' },
      { key: 'contactSource', label: t('crm.contacts.source') },
      {
        key: 'actions',
        label: '',
        render: (c: Contact) =>
          canManage ? (
            <div className="flex gap-3">
              <button className="text-xs text-primary-600 hover:underline" onClick={() => openModal('edit', c)}>
                {t('common.edit')}
              </button>
              <button className="text-xs text-primary-600 hover:underline" onClick={() => openModal('tags', c)}>
                {t('crm.contacts.tags')}
              </button>
            </div>
          ) : null,
      },
    ],
    [canManage, t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <input
          className="input max-w-xs"
          placeholder={t('common.search')}
          value={search}
          onChange={(e) => {
            setSearch(e.target.value)
            setPage(1)
          }}
        />
        {canManage && (
          <button className="btn-primary" onClick={() => setModal('edit')}>
            {t('crm.contacts.add')}
          </button>
        )}
      </div>

      <DataTable columns={columns} data={data?.items ?? []} isLoading={isLoading} />
      <Pagination page={page} pageSize={pageSize} totalCount={data?.totalCount ?? 0} onPageChange={setPage} />

      {modal === 'edit' && <ContactModal contact={activeContact} onClose={closeModal} />}
      {modal === 'tags' && activeContact && <TagsModal contact={activeContact} onClose={closeModal} />}
    </div>
  )
}
