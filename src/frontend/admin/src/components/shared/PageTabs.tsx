import type { ReactNode } from 'react'
import { useSearchParams } from 'react-router-dom'

export interface PageTab {
  key: string
  label: string
}

/**
 * The tablist alone (arrow keys move the selection, roving tabIndex), for a page that keeps its own active-tab state and decides what to render
 * for it. Use {@link PageTabs} when the active tab should live in the URL.
 */
export function TabList<K extends string>({ id, label, tabs, current, onSelect }: { id: string; label: string; tabs: { key: K; label: string }[]; current: K; onSelect: (key: K) => void }) {
  const move = (index: number, delta: number) => {
    const next = tabs[(index + delta + tabs.length) % tabs.length]
    onSelect(next.key)
    document.getElementById(`${id}-tab-${next.key}`)?.focus()
  }

  return (
    <div role="tablist" aria-label={label} className="flex gap-1 border-b border-line">
      {tabs.map((tab, i) => (
        <button
          key={tab.key}
          id={`${id}-tab-${tab.key}`}
          type="button"
          role="tab"
          aria-selected={current === tab.key}
          tabIndex={current === tab.key ? 0 : -1}
          onClick={() => onSelect(tab.key)}
          onKeyDown={(e) => {
            if (e.key === 'ArrowRight') { e.preventDefault(); move(i, 1) }
            else if (e.key === 'ArrowLeft') { e.preventDefault(); move(i, -1) }
          }}
          className={`px-4 py-2 text-sm font-medium transition-colors ${
            current === tab.key ? 'border-b-2 border-brand-action text-brand-action' : 'text-ink-secondary hover:text-ink'
          }`}
        >
          {tab.label}
        </button>
      ))}
    </div>
  )
}

/** Tabs kept in the URL (`?tab=`), each with its panel. */
export function PageTabs({ id, label, tabs }: { id: string; label: string; tabs: (PageTab & { content: ReactNode })[] }) {
  const [params, setParams] = useSearchParams()
  const current = tabs.find((tab) => tab.key === params.get('tab')) ?? tabs[0]

  return (
    <>
      <TabList id={id} label={label} tabs={tabs} current={current.key} onSelect={(key) => setParams({ tab: key }, { replace: true })} />
      <div id={`${id}-tabpanel`} role="tabpanel" aria-labelledby={`${id}-tab-${current.key}`}>
        {current.content}
      </div>
    </>
  )
}
