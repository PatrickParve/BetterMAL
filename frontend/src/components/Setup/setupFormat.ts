import type { ConnectError, SetupStatusDto, SetupStepsDto } from '../../api/types.ts'

// The setup screen's four steps, in the order the spec fixes (first-run-setup "The
// setup screen shows each step's progress"), shared by the setup screen and Settings'
// Library data entry so the two can never name or count them differently.
export type SetupStepKey = keyof SetupStepsDto

export const SETUP_STEPS: { key: SetupStepKey; title: string; noun: string }[] = [
  { key: 'list', title: 'Reading your list', noun: 'entries' },
  { key: 'details', title: 'Fetching anime details', noun: 'anime' },
  { key: 'series', title: 'Building series', noun: 'anime' },
  { key: 'airing', title: 'Airing dates', noun: 'anime' },
]

export type SetupScreenKind = 'credentials' | 'connect' | 'progress'

// Which screen the status calls for (design D18), or null when the app itself is shown.
// While setup has not finished, a missing credential stops everything, then a missing
// login, and otherwise the work is under way; a lost login is not a missing one, so it
// stays on the progress screen, with Reconnect. Once setup has finished the app opens,
// except that a login that is missing altogether still gets the connect screen
// (mal-api-integration "The connection state is reported"); a missing credential no
// longer blocks anything then, and a lost login is explained inside the app.
export function setupScreenKind(status: SetupStatusDto): SetupScreenKind | null {
  if (status.finished) return status.connection.state === 'NotConnected' ? 'connect' : null
  if (status.missingCredentials.length > 0) return 'credentials'
  if (status.connection.state === 'NotConnected') return 'connect'
  return 'progress'
}

// "about 6 min left". Under a minute is not worth a number, and an hour or more
// reads better as hours.
export function formatEta(seconds: number): string {
  if (seconds < 60) return 'less than a minute left'
  const minutes = Math.round(seconds / 60)
  if (minutes < 60) return `about ${minutes} min left`
  const hours = Math.floor(minutes / 60)
  const rest = minutes % 60
  return rest === 0 ? `about ${hours} h left` : `about ${hours} h ${rest} min left`
}

// "30 s", "1 min 5 s": what a throttle countdown counts down. Each number is joined to its
// unit with a no-break space, so on a phone the line never wraps with "s." alone on the
// next row.
export function formatCountdown(ms: number): string {
  const nb = ' '
  const total = Math.max(0, Math.ceil(ms / 1000))
  if (total < 60) return `${total}${nb}s`
  const minutes = Math.floor(total / 60)
  if (minutes >= 60) {
    const hours = Math.floor(minutes / 60)
    return minutes % 60 === 0 ? `${hours}${nb}h` : `${hours}${nb}h ${minutes % 60}${nb}min`
  }
  const seconds = total % 60
  return seconds === 0 ? `${minutes}${nb}min` : `${minutes}${nb}min ${seconds}${nb}s`
}

// "14:32" in the viewer's own clock: when a paused queue or a waiting anime is tried
// again.
export function formatClock(iso: string): string {
  return new Date(iso).toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' })
}

// What the three ?connectError= codes say (first-run-setup "The connect screen starts
// setup"). Shared by the connect screen, the progress screen's Reconnect and Settings'
// Account section, so a failed sign-in is worded the same wherever it lands.
export const CONNECT_ERROR_MESSAGES: Record<ConnectError, string> = {
  denied: 'Sign-in was cancelled or refused on MyAnimeList.',
  expired: 'The sign-in attempt expired, for example because the app restarted while you were signing in.',
  failed: "Sign-in couldn't be completed. The backend's logs have the details.",
}
