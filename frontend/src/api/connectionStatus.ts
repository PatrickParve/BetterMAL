// Plain module, not a React context: `client.ts` (where every request
// reports through this) is not a React module, and the only consumer,
// ConnectionStatusNotice, reads it via useSyncExternalStore — which is built
// for exactly this "external mutable store" shape.
let reachable = true

// How often anything that is waiting for the backend to come back asks again:
// the notice bar's health poll (ConnectionStatusNotice) and the full-page
// message before the shell has mounted (App.tsx) share it, so both recover
// within the same interval (connection-status).
export const HEALTH_POLL_INTERVAL_MS = 5000

const listeners = new Set<() => void>()

export function subscribe(listener: () => void): () => void {
  listeners.add(listener)
  return () => {
    listeners.delete(listener)
  }
}

export function getSnapshot(): boolean {
  return reachable
}

export function reportReachable(): void {
  if (reachable) return
  reachable = true
  listeners.forEach((listener) => listener())
}

export function reportUnreachable(): void {
  if (!reachable) return
  reachable = false
  listeners.forEach((listener) => listener())
}
