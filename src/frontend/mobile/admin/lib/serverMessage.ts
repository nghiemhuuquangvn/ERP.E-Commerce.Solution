type Body = { detail?: string; message?: string; errors?: Record<string, string[] | string> }

/** The first message of a ProblemDetails `errors` map (model-binding / FluentValidation refusals have no `detail`). */
function firstValidationMessage(errors: Body['errors']): string | undefined {
  if (!errors || typeof errors !== 'object') return undefined
  for (const [field, value] of Object.entries(errors)) {
    const text = Array.isArray(value) ? value[0] : value
    if (typeof text === 'string' && text) return field && field !== '$' ? `${field}: ${text}` : text
  }
  return undefined
}

/**
 * The server's refusal text (a BusinessRuleException answers HTTP 400 with ProblemDetails `detail`; older endpoints use
 * `message`; a validation refusal carries an `errors` map), or the fallback when the request never got an answer.
 * The one helper for toasts and alerts. A 403 is the caller's missing permission, so it gets its own fallback.
 */
export function serverMessage(e: unknown, fallback: string, forbidden?: string): string {
  const res = (e as { response?: { status?: number; data?: Body | string } } | null)?.response
  if (res?.status === 403 && forbidden) return forbidden
  const data = res?.data
  if (data && typeof data === 'object') return data.detail ?? data.message ?? firstValidationMessage(data.errors) ?? fallback
  return fallback
}
