/**
 * The server's refusal text (a BusinessRuleException answers HTTP 400 with ProblemDetails `detail`; older endpoints use
 * `message`), or the fallback when the request never got an answer. The one helper for toasts and alerts.
 */
export function serverMessage(e: unknown, fallback: string): string {
  const data = (e as { response?: { data?: { detail?: string; message?: string } | string } } | null)?.response?.data
  if (data && typeof data === 'object') return data.detail ?? data.message ?? fallback
  return fallback
}
