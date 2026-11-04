export type InvoiceActionKey = 'approve' | 'approveVariance' | 'reject'

/**
 * Which actions a supplier invoice offers. Approve: a matched invoice (supplier_invoice:approve); an invoice on hold needs the
 * variance approval as well (supplier_invoice:approve_variance) and a reason. Reject: any open invoice (supplier_invoice:manage).
 * The server checks the permissions again.
 */
export function invoiceActions(status: string, has: (permission: string) => boolean): InvoiceActionKey[] {
  const out: InvoiceActionKey[] = []
  if (status === 'Matched' && has('supplier_invoice:approve')) out.push('approve')
  if (status === 'OnHold' && has('supplier_invoice:approve') && has('supplier_invoice:approve_variance')) out.push('approveVariance')
  if (['Recorded', 'Matched', 'OnHold'].includes(status) && has('supplier_invoice:manage')) out.push('reject')
  return out
}

export type ReturnActionKey = 'ship' | 'cancel' | 'credit'

/** A draft return can be shipped or cancelled, a shipped one credited; all need vendor_return:manage (the server checks again). */
export function returnActions(status: string, has: (permission: string) => boolean): ReturnActionKey[] {
  if (!has('vendor_return:manage')) return []
  if (status === 'Draft') return ['ship', 'cancel']
  if (status === 'Shipped') return ['credit']
  return []
}
