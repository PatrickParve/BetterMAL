## Why

The backend already flips an entry to Completed the moment the "+" button pushes episodes-watched up to the anime's total episode count, but the UI says nothing about it: the user gets no chance to rate the show they just finished, and the page they clicked from keeps rendering stale data (a finished show stays in the home "Currently watching" carousel, and my-list keeps showing it in its old status group) until a manual reload. Rating a just-finished show is the natural moment to score it, and today it takes a separate trip to the entry editor.

## What Changes

- Finishing an anime with the "+" button — from anywhere the button appears (home "Currently watching" carousel, my list rows, the anime detail page, and any future site) — opens a small score prompt overlay.
- The prompt shows the anime's picture on the left and a score dropdown (No score, 1–10) on the right, pre-filled with the entry's existing score if it already has one.
- Giving a score saves it through the existing entry-edit endpoint (so it is logged and synced to MAL like any other score change). Dismissing the prompt — Skip, Esc, or a click outside — leaves the score untouched.
- Either way, when the prompt closes the page that triggered it re-fetches its data, so the just-completed show drops out of the home carousel and shows as Completed in my list / on the detail page without a manual reload.
- Episode increments funnel through one shared hook so the completion prompt cannot be forgotten at a new "+" site.
- The prompt fires only on the transition *into* Completed. Re-incrementing an already-Completed entry (rewatch) and anime with an unknown total episode count — which never auto-complete — do not trigger it.

## Capabilities

### New Capabilities
<!-- None: this extends existing list-editing behavior rather than introducing a new capability. -->

### Modified Capabilities
- `list-editing`: adds requirements for the completion score prompt that follows a "+"-driven completion, for its dismissal semantics, and for refreshing the triggering view once the prompt closes.

## Impact

- Frontend only — no backend, database, or MAL API changes. The auto-complete rule in `backend/AnimeTracker.Api/Services/Entries/UserAnimeEntryEditService.cs` (`ApplyEpisodesWatched`) and the `PUT` entry-edit endpoint already provide everything the prompt needs.
- New: a completion-prompt context/provider mounted at the app root (alongside `EntryEditorProvider` in `frontend/src/AppShell.tsx`), a `CompletionScoreOverlay` component + CSS, and a shared increment hook.
- Modified: the three current "+" call sites — `frontend/src/components/CurrentlyWatchingCarousel.tsx`, `frontend/src/pages/MyListPage.tsx`, `frontend/src/pages/AnimeDetailPage.tsx` — and `frontend/src/pages/HomePage.tsx` (needs a dashboard re-fetch callback rather than the current in-place episode-count patch).
- Reuses the existing `Modal` component, so Esc/click-outside behavior is unchanged from the entry editor.
