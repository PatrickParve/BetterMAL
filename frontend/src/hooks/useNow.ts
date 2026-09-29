import { useEffect, useState } from 'react'

// The current time, re-read every `intervalMs` while `active`. Drives a countdown
// ("resuming in 8 s") that has to keep moving between two reads of the setup status,
// which arrive every second on the setup screen and every two on Settings. Idle
// when nothing is counting down, so a finished screen re-renders for no clock.
export function useNow(active: boolean, intervalMs: number = 1000): number {
  const [now, setNow] = useState(() => Date.now())

  useEffect(() => {
    if (!active) return
    setNow(Date.now())
    const interval = setInterval(() => setNow(Date.now()), intervalMs)
    return () => clearInterval(interval)
  }, [active, intervalMs])

  return now
}
