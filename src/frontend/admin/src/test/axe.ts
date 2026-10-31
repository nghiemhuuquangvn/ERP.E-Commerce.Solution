import { configureAxe } from 'vitest-axe'

// jsdom has no layout or canvas, so axe cannot compute colour contrast here; contrast is guarded by
// `npm run check:tokens` (docs/frontend/design-system/tools/build-tokens.js) instead.
export const axe = configureAxe({ rules: { 'color-contrast': { enabled: false } } })
