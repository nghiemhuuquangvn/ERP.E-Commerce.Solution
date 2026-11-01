import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { X } from 'lucide-react'
import { contactsApi, CONTACT_SOURCE, type Contact } from '@/api/crm/contacts.api'
import { DataTable } from '@/components/shared/DataTable'
import { Pagination } from '@/components/shared/Pagination'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'
import { Alert, Button, EmptyState, FormField, Input, Modal, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

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
  const toast = useToast()
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
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={isEdit ? t('crm.contacts.editTitle') : t('crm.contacts.addTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="contact-form" loading={mutation.isPending}>{t('common.save')}</Button>
        </>
      }
    >
      <form id="contact-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        <FormField label={t('common.name')} required error={errors.name ? t('common.required') : undefined}>
          <Input {...register('name', { required: true })} />
        </FormField>
        <FormField label={t('crm.contacts.email')}>
          <Input type="email" {...register('email')} />
        </FormField>
        <FormField label={t('crm.contacts.phone')}>
          <Input {...register('phone')} />
        </FormField>
        <FormField label={t('crm.contacts.company')}>
          <Input {...register('company')} />
        </FormField>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('crm.contacts.failedToSave'))}</Alert>}
      </form>
    </Modal>
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

  const failure = addMutation.isError ? addMutation.error : removeMutation.isError ? removeMutation.error : null

  return (
    <Modal
      open
      size="sm"
      title={t('crm.contacts.tagsTitle', { name: contact.name })}
      onClose={onClose}
      footer={<Button variant="outline" onClick={onClose}>{t('common.close')}</Button>}
    >
      <ul className="mb-4 flex flex-wrap gap-2" aria-label={t('crm.contacts.tags')}>
        {(data?.tags ?? []).length === 0 && (
          <li className="text-sm text-ink-secondary">{t('crm.contacts.noTags')}</li>
        )}
        {(data?.tags ?? []).map((tag) => (
          <li key={tag} className="badge flex items-center gap-1 bg-surface-muted text-ink-secondary">
            {tag}
            <Button
              variant="ghost"
              size="sm"
              iconOnly
              aria-label={`${t('common.delete')} ${tag}`}
              onClick={() => removeMutation.mutate(tag)}
            >
              <X size={14} aria-hidden="true" />
            </Button>
          </li>
        ))}
      </ul>
      <form
        onSubmit={(e) => {
          e.preventDefault()
          if (newTag.trim()) addMutation.mutate(newTag.trim())
        }}
        className="flex gap-2"
      >
        <Input
          aria-label={t('crm.contacts.newTag')}
          placeholder={t('crm.contacts.newTag')}
          value={newTag}
          onChange={(e) => setNewTag(e.target.value)}
        />
        <Button type="submit" loading={addMutation.isPending} disabled={!newTag.trim()}>
          {t('common.add')}
        </Button>
      </form>
      {failure != null && <Alert variant="danger" className="mt-3">{serverMessage(failure, t('crm.contacts.failedToSave'))}</Alert>}
    </Modal>
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
              <Button variant="link" className="text-xs" onClick={() => openModal('edit', c)}>
                {t('common.edit')}
              </Button>
              <Button variant="link" className="text-xs" onClick={() => openModal('tags', c)}>
                {t('crm.contacts.tags')}
              </Button>
            </div>
          ) : null,
      },
    ],
    [canManage, t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <Input
          className="max-w-xs"
          aria-label={t('common.search')}
          placeholder={t('common.search')}
          value={search}
          onChange={(e) => {
            setSearch(e.target.value)
            setPage(1)
          }}
        />
        {canManage && (
          <Button onClick={() => setModal('edit')}>
            {t('crm.contacts.add')}
          </Button>
        )}
      </div>

      <DataTable
        columns={columns}
        data={data?.items ?? []}
        isLoading={isLoading}
        emptyState={
          search ? (
            <EmptyState
              variant="no-results"
              title={t('common.noResults')}
              description={t('common.noResultsHint')}
              action={<Button variant="outline" onClick={() => { setSearch(''); setPage(1) }}>{t('common.clear')}</Button>}
            />
          ) : undefined
        }
      />
      <Pagination page={page} pageSize={pageSize} totalCount={data?.totalCount ?? 0} onPageChange={setPage} />

      {modal === 'edit' && <ContactModal contact={activeContact} onClose={closeModal} />}
      {modal === 'tags' && activeContact && <TagsModal contact={activeContact} onClose={closeModal} />}
    </div>
  )
}
