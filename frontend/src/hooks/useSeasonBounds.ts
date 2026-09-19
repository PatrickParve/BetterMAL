import { useEffect, useState } from 'react'
import { getSeasonBounds, probeSeasonHorizon } from '../api/client.ts'
import { seasonPointIndex, shiftSeason } from '../utils/anime.ts'
import { currentSeasonTarget, FUTURE_SEASON_WINDOW, isSeasonName, type SeasonTarget } from '../utils/browseRange.ts'

function defaultCeiling(): SeasonTarget {
  const current = currentSeasonTarget()
  return shiftSeason(current.year, current.season, FUTURE_SEASON_WINDOW)
}

function higherOf(a: SeasonTarget, b: SeasonTarget): SeasonTarget {
  return seasonPointIndex(b.year, b.season) > seasonPointIndex(a.year, a.season) ? b : a
}

function mergeIntoCache(next: SeasonTarget): SeasonTarget {
  cachedCeiling = cachedCeiling ? higherOf(cachedCeiling, next) : next
  return cachedCeiling
}

// Module-memoised so the last resolved ceiling is returned synchronously on a
// later mount — stale-while-revalidate (design D4): only the very first mount
// of a session waits on the network, every mount after it renders immediately
// with last session's answer while a fresh GET /api/season/bounds silently
// revalidates it in the background. Concurrent mounts share one in-flight
// request rather than firing two.
let cachedCeiling: SeasonTarget | null = null
let inFlightBounds: Promise<SeasonTarget> | null = null

function fetchBounds(): Promise<SeasonTarget> {
  if (inFlightBounds) return inFlightBounds
  inFlightBounds = getSeasonBounds()
    .then((bounds) => (isSeasonName(bounds.latestSeason) ? { year: bounds.latestYear, season: bounds.latestSeason } : defaultCeiling()))
    .catch(() => cachedCeiling ?? defaultCeiling())
    .then(mergeIntoCache)
    .finally(() => {
      inFlightBounds = null
    })
  return inFlightBounds
}

// Fires the background form of the horizon probe alongside every bounds read
// (design D10) and folds a raised ceiling into the same module cache. Never
// awaited by a caller and a failure is silently dropped — a probe must never
// delay or fail a page visit. Deduplicated like fetchBounds above: the guard
// and its view mount in the same commit on an ordinary visit, so without this
// each would otherwise fire its own POST in the same tick.
let inFlightProbe: Promise<void> | null = null

function probeInBackground(): void {
  if (inFlightProbe) return
  inFlightProbe = probeSeasonHorizon(false)
    .then((bounds) => {
      if (isSeasonName(bounds.latestSeason)) mergeIntoCache({ year: bounds.latestYear, season: bounds.latestSeason })
    })
    .catch(() => {
      // A failed probe is invisible to the page (season-browser spec) — nothing to do.
    })
    .finally(() => {
      inFlightProbe = null
    })
}

// The one case a guard waits on a MAL round trip (design D10a): a URL
// addressing the probe's own target directly. Calls the same probe endpoint
// on-demand — ignoring its last-month window, still honouring its once-per-
// day gate — and folds a raised ceiling into the shared cache exactly like
// the background form above, so a later mount of any hook consumer sees it
// too.
export function probeHorizonOnDemand(): Promise<SeasonTarget> {
  return probeSeasonHorizon(true).then((bounds) =>
    mergeIntoCache(isSeasonName(bounds.latestSeason) ? { year: bounds.latestYear, season: bounds.latestSeason } : defaultCeiling()),
  )
}

// The season and year guards' one source of the navigable ceiling (design
// D4). `isPending` is true only until the very first GET /api/season/bounds
// this session settles — a later mount, thanks to the module memo above,
// reports `isPending: false` immediately with last session's answer.
export function useSeasonBounds(): { ceiling: SeasonTarget; isPending: boolean } {
  const [ceiling, setCeiling] = useState<SeasonTarget>(() => cachedCeiling ?? defaultCeiling())
  const [isPending, setIsPending] = useState(() => cachedCeiling === null)

  useEffect(() => {
    let cancelled = false
    fetchBounds().then((next) => {
      if (cancelled) return
      setCeiling(next)
      setIsPending(false)
    })
    probeInBackground()
    return () => {
      cancelled = true
    }
  }, [])

  return { ceiling, isPending }
}

// Backs the one-season, one-year on-demand check a URL addressing the probe's
// own target triggers (tasks 5.3a/6.2a). `active` only turns true once a
// guard has confirmed that target still sits past the ceiling GET /bounds
// already reported, so this never fires on an ordinary visit (design D10a,
// task 4.7) — the probe endpoint itself takes no target, so nothing further
// needs passing in.
export function useOnDemandProbe(active: boolean): SeasonTarget | null {
  const [result, setResult] = useState<SeasonTarget | null>(null)

  useEffect(() => {
    if (!active) return
    let cancelled = false
    probeHorizonOnDemand().then((next) => {
      if (!cancelled) setResult(next)
    })
    return () => {
      cancelled = true
    }
  }, [active])

  return result
}
