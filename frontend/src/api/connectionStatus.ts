// Plain module, not a React context: `client.ts` (where every request
// reports through this) is not a React module, and the only consumer,
// ConnectionStatusNotice, reads it via useSyncExternalStore — which is built
// for exactly this "external mutable store" shape.
let reachable = true
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
