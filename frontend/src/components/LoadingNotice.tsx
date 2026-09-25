import type { ReactNode } from 'react'
import { useDelayedFlag } from '../hooks/useDelayedFlag.ts'
import './LoadingNotice.css'

// The one way the app says a read is in progress (page-load-states). Draws
// nothing until the read has been in flight for the loading-indicator delay,
// then fades the page's own loading line in, so a load that settles quickly
// never paints "Loading…" and one that settles just after the delay is still
// close to transparent when its content replaces it. `className` is the
// page's existing loading class, so each page keeps its own look.
export function LoadingNotice({
  active = true,
  className,
  children = 'Loading…',
}: {
  active?: boolean
  className?: string
  children?: ReactNode
}) {
  const shown = useDelayedFlag(active)
  if (!shown) return null

  return (
    <div className="loading-notice">
      <p className={className}>{children}</p>
    </div>
  )
}
