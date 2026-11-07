// The search page's two modes (improve-search-page design.md D4, navigation-and-search
// "The search page has its own search bar"). The mode lives in the URL beside the query,
// as `mode=op-ed`: no `mode`, or any other value, is Anime, so a link with no mode, a
// legacy link and a mistyped one all open the page as it always was. `op-ed` rather than
// `themes` because it is the word the switch shows.
//
// Plain TypeScript with no imports, so a scratch Node script can run it with
// `--experimental-strip-types`.
export type SearchMode = 'anime' | 'op-ed'

export function parseSearchMode(value: string | null): SearchMode {
  return value === 'op-ed' ? 'op-ed' : 'anime'
}

/**
 * The address of a submitted search. Anime mode writes no `mode` (a navbar search is an
 * Anime search, and the address is the one that has always meant it); OP/ED writes
 * `mode=op-ed`. Nothing else is carried over: a new search starts from the default sort
 * and every type, as it does from the navbar.
 */
export function searchResultsUrl(query: string, mode: SearchMode): string {
  const base = `/search?q=${encodeURIComponent(query)}`
  return mode === 'op-ed' ? `${base}&mode=op-ed` : base
}

/**
 * A copy of the page's parameters set to a mode, every other parameter kept. `sort` and
 * `type` stay in the address in OP/ED mode, unused, so switching back restores them.
 */
export function withSearchMode(params: URLSearchParams, mode: SearchMode): URLSearchParams {
  const next = new URLSearchParams(params)
  if (mode === 'op-ed') next.set('mode', 'op-ed')
  else next.delete('mode')
  return next
}
