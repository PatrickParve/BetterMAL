# Personal Anime Tracker — Known Issues

## English titles

- Show English names for anime when available, instead of only the default
  title. Affects: Home, Seasonal, Top anime, Anime detail page, Profile, and
  Search.

## Data / Sync

- MAL data isn't saving correctly. 577 entries imported vs. 595 actual on
  MAL. All imported entries show status "plan to watch," with no score and
  no episodes watched recorded.

## Navbar

- My List is missing from the navbar. Should be: Home, My List, Top,
  Season, Airing — plus the other existing nav items.

## Season page

- Anime that aired or is still airing in a season, but started well before
  it, incorrectly shows up in later seasons too (e.g. One Piece showing up
  in 2026 seasons despite starting before 2000). Each anime should appear
  in exactly one season.
- Season selection doesn't persist through back-navigation — picking a
  season, opening an anime, then pressing back returns to the current
  season instead of the one that was selected. Navigating to Season from
  the navbar should still default to the current season.
- No dropdown to quickly jump to a specific season instead of stepping
  through one at a time.

## Home page

- Currently-watching arrows show even when all anime already fit on
  screen with no need to scroll.
- Doesn't show all the anime in my list — possibly related to the sync
  issue.

## Top anime page

- Only shows 100 results. Should be paginated: 50 per page, page-number
  controls plus left/right arrows at the bottom (standard pagination),
  and arrow buttons at the top-right as well. Max rank shown is 500.

## Airing page

- Shows nothing currently, even though there are anime in my list that
  should be airing. Possibly no broadcast/episode-airing data exists.

## Anime page

- Info section is missing fields: Duration, Status, Source. If a field
  doesn't exist for a given anime, don't show it as blank — show "no
  info" instead.
- AniList link sometimes doesn't work for certain shows — needs
  investigation into how AniList's links actually work (title language
  used, ID scheme, etc.).

## Profile page

- The "Latest updates" feed should only show: anime added to the list
  (any status — plan to watch, watching, or completed), and episode-count
  increases (multiple increments in a row should collapse into a single
  update, not one per increment). It should NOT show episode decreases or
  drops.
