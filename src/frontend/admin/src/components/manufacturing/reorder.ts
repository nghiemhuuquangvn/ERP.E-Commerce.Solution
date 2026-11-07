/**
 * The order of operation numbers after the item at position `from` is moved to position `to` (drag and drop, or the Up / Down buttons).
 * Null when nothing moves. The result is what `POST routings/{id}/renumber` takes: every current number exactly once, in the new order.
 */
export function reorderSeqs(seqs: number[], from: number, to: number): number[] | null {
  if (from === to || from < 0 || to < 0 || from >= seqs.length || to >= seqs.length) return null
  const next = seqs.slice()
  const [moved] = next.splice(from, 1)
  next.splice(to, 0, moved)
  return next
}
