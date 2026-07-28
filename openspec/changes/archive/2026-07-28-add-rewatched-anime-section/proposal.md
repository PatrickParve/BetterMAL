## Why

The profile page already tracks how many anime I have rewatched as a single number in the stats box, but there is no way to see *which* ones, or which I have come back to most often. Rewatch count is a stronger signal of what I actually love than a one-time score, and the data (`UserAnimeEntry.RewatchCount`) is already cached locally — it just isn't surfaced anywhere.

## What Changes

- Add a new "Most rewatched" box on the profile page, directly under "My top anime", visually matching the existing top-anime strip (poster tiles, horizontal scroll, drag-to-scroll).
- The strip lists every entry with a rewatch count above zero, ordered most-rewatched first, with no cap — it scrolls right to the end of the list rather than truncating at a fixed size.
- Each tile shows its rewatch count as a badge (the analogue of the score badge on the top-anime strip) and links to the anime detail page.
- The box carries the same media-type filter tabs as "My top anime" (All, TV, Movie, OVA, ONA, Specials), recomputing the strip from that media type's entries only.
- Empty states are per-scope: "No shows have been rewatched" under All, and "No TV shows have been rewatched" / "No movies have been rewatched" / etc. under each media-type tab.
- No reordering: the section has no edit control and no persisted ordering — its order is derived entirely from rewatch counts.
- New backend endpoint `GET /api/profile/rewatched?mediaType=` mirroring the existing `GET /api/profile/top-anime`, plus a `rewatched` section embedded in the profile payload for the initial (All) render.

## Capabilities

### New Capabilities
<!-- None: this extends the existing profile page capability rather than introducing a new one. -->

### Modified Capabilities
- `profile-stats`: adds requirements for a "Most rewatched" section on the profile page — its membership and ordering rules, its uncapped horizontal scrolling, its media-type filter, and its per-scope empty-state messages.

## Impact

- Backend: `Services/Profile/ProfileDto.cs` (new section + entry DTOs), `Services/Profile/ProfileService.cs` and `IProfileService.cs` (build the section, expose a per-scope getter), `Controllers/ProfileController.cs` (new endpoint). Reuses `TopAnimeMediaTypeScope` for filter validation and matching.
- Frontend: `api/types.ts` and `api/client.ts` (new DTO types + fetch function), `pages/ProfilePage.tsx` (new section, its own filter state), `pages/ProfilePage.css` (strip styling that scrolls past the viewport edge).
- No database schema change, no migration, no MAL API calls — `RewatchCount` is already persisted and synced.
- No change to "My top anime": its data, ordering, and edit overlay are untouched.
