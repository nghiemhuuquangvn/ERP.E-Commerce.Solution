import { useMemo, useState } from 'react'
import { ChevronDown, ChevronRight } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import type { IndentedNode } from '@/api/manufacturing/mfg.api'
import { Badge, Button } from '@/components/ui'
import { fmtQty } from './shared'

/** The paths ("0.2.1") of every node that has children - what Collapse all closes. */
function parents(node: IndentedNode, path = '0', out: string[] = []): string[] {
  if (node.children.length > 0) out.push(path)
  node.children.forEach((c, i) => parents(c, `${path}.${i}`, out))
  return out
}

function Row({ node, path, collapsed, toggle }: { node: IndentedNode; path: string; collapsed: Set<string>; toggle: (path: string) => void }) {
  const { t } = useTranslation()
  const open = !collapsed.has(path)
  const has = node.children.length > 0
  return (
    <li role="treeitem" aria-expanded={has ? open : undefined} aria-level={node.level + 1}>
      <div className="flex items-center gap-2 border-b border-line py-1.5" style={{ paddingLeft: `${node.level * 1.5}rem` }}>
        {has ? (
          <button type="button" onClick={() => toggle(path)} aria-label={`${open ? t('mfg.bom.collapse') : t('mfg.bom.expand')} ${node.itemCode}`} className="rounded p-0.5 text-ink-secondary hover:bg-surface-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-action">
            {open ? <ChevronDown size={16} aria-hidden="true" /> : <ChevronRight size={16} aria-hidden="true" />}
          </button>
        ) : <span className="inline-block w-5" aria-hidden="true" />}
        <span className={`font-mono text-xs ${node.isSubstitute ? 'text-ink-muted line-through' : 'text-ink'}`}>{node.itemCode}</span>
        <span className={`text-sm ${node.isSubstitute ? 'text-ink-muted' : 'text-ink'}`}>{node.itemName}</span>
        {node.isPhantom && <Badge variant="info">{t('mfg.bom.phantom')}</Badge>}
        {node.alternativeGroup && <Badge variant={node.isSubstitute ? 'muted' : 'brand'}>{node.alternativeGroup}{node.isSubstitute ? ` · ${t('mfg.bom.substitute')}` : ''}</Badge>}
        {node.operationSeq != null && <span className="text-xs text-ink-secondary">{t('mfg.bom.opShort', { seq: node.operationSeq })}</span>}
        {node.bomVersion != null && <span className="text-xs text-ink-secondary">v{node.bomVersion}</span>}
        <span className="ml-auto font-mono text-sm">{fmtQty(node.quantity)}</span>
      </div>
      {has && open && (
        <ul role="group">
          {node.children.map((c, i) => <Row key={i} node={c} path={`${path}.${i}`} collapsed={collapsed} toggle={toggle} />)}
        </ul>
      )}
    </li>
  )
}

/** The multi-level BOM as an expandable tree (S08): phantoms open, substitutes of an alternative group marked and struck through. */
export function BomTree({ root }: { root: IndentedNode }) {
  const { t } = useTranslation()
  const [collapsed, setCollapsed] = useState<Set<string>>(new Set())
  const all = useMemo(() => parents(root), [root])
  const toggle = (path: string) => setCollapsed((s) => { const n = new Set(s); if (n.has(path)) n.delete(path); else n.add(path); return n })
  return (
    <div>
      <div className="mb-2 flex gap-2">
        <Button variant="outline" size="sm" onClick={() => setCollapsed(new Set())}>{t('mfg.bom.expandAll')}</Button>
        <Button variant="outline" size="sm" onClick={() => setCollapsed(new Set(all))}>{t('mfg.bom.collapseAll')}</Button>
      </div>
      <ul role="tree" aria-label={t('mfg.bom.tree')} className="rounded-lg border border-line bg-surface px-3">
        <Row node={root} path="0" collapsed={collapsed} toggle={toggle} />
      </ul>
    </div>
  )
}
