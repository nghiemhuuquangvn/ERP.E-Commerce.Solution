import type { ReactNode } from 'react'
import { useSearchParams } from 'react-router-dom'

export interface DocTab {
  key: string
  label: string
  content: ReactNode
}

/** Two tabs kept in the URL (`?tab=`), with the tablist keyboard pattern (arrow keys, roving tabIndex). */
export function DocTabs({ id, label, tabs }: { id: string; label: string; tabs: DocTab[] }) {
  const [params, setParams] = useSearchParams()
  const current = tabs.find((tab) => tab.key === params.get('tab')) ?? tabs[0]
  const select = (key: string) => setParams({ tab: key }, { replace: true })
  const move = (index: number, delta: number) => {
    const next = tabs[(index + delta + tabs.length) % tabs.length]
    select(next.key)
    document.getElementById(`${id}-tab-${next.key}`)?.focus()
  }

  return (
    <>
      <div role="tablist" aria-label={label} className="flex gap-1 border-b border-line">
        {tabs.map((tab, i) => (
          <button
            key={tab.key}
            id={`${id}-tab-${tab.key}`}
            type="button"
            role="tab"
            aria-selected={current.key === tab.key}
            aria-controls={`${id}-tabpanel`}
            tabIndex={current.key === tab.key ? 0 : -1}
            onClick={() => select(tab.key)}
            onKeyDown={(e) => {
              if (e.key === 'ArrowRight') { e.preventDefault(); move(i, 1) }
              else if (e.key === 'ArrowLeft') { e.preventDefault(); move(i, -1) }
            }}
            className={`px-4 py-2 text-sm font-medium transition-colors ${
              current.key === tab.key ? 'border-b-2 border-brand-action text-brand-action' : 'text-ink-secondary hover:text-ink'
            }`}
          >
            {tab.label}
          </button>
        ))}
      </div>
      <div id={`${id}-tabpanel`} role="tabpanel" aria-labelledby={`${id}-tab-${current.key}`}>
        {current.content}
      </div>
    </>
  )
}
