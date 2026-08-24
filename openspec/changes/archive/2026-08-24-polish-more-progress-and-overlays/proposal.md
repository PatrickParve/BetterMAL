## Why

Five small things get in the way while using the app day to day.

The series page's More section is the worst of them. With the "in my list" filter on — the state every series page opens in — a media-type group holding nothing of mine renders as a heading, a count, and a `+7 more` link, and its heading button appears to do nothing at all: clicking it collapses and expands a group whose tiles the filter is hiding either way. The only thing that opens such a group is the `+N more` link, which is exactly the wrong control to lean on, since it is also offered on groups that are already showing something.

The other four are each one behaviour that stops short of finishing what it started. Watching an episode of a show sitting in Plan to watch, On hold, or Dropped raises the count but leaves the entry filed where it was. Every overlay in the app — the editor, the ranking overlay, the score board — leaves the page behind it scrolling under the pointer. The anime page's two score boxes are sized independently, so the pair reads as mismatched whenever my box carries a rewatch count or a finish date. And a my-list row for a currently-airing anime shows a plain watched bar, while the same anime's card on the home page, its detail page, and its series page all show broadcast progress behind it.

## What Changes

- **A More group's heading opens the whole group.** Clicking a media-type heading on the series page shows every extra in that group, filter or no filter; clicking it again collapses it. Other groups keep showing whatever they were showing. The "in my list" control reads as off while any group is open in full, and turning it back on re-filters every group at once.
- **`+N more` is offered only where something is already shown.** A group hiding extras behind the filter while showing at least one of its own keeps its hidden-count link. A group showing no tiles — because nothing in it is in my list, or because it is collapsed — offers none, since its heading now does that job and its count is already in the heading.
- **Raising progress resumes an entry.** Increasing episodes watched on an entry that is not Watching or Rewatching, by any means and without reaching everything available, moves it to Watching. The editor's status dropdown moves with the count as it is typed, so the change is visible before saving, and choosing a status explicitly in that same edit still wins.
- **An overlay holds the page behind it still.** While any overlay is open, the page underneath does not scroll — by wheel, trackpad, or keyboard — and it keeps the scroll position it had, with no sideways shift of the page as the scrollbar goes away. The overlay's own content scrolls as it does today.
- **The anime page's two score boxes share one size.** The MAL box (score, rank, popularity) and my box (my score, rewatch count, finish date) are both drawn at the width of whichever needs more, so the pair stays matched however many lines my box happens to carry. They stay content-sized rather than stretching to fill the column.
- **My-list rows show broadcast progress.** A row whose anime is currently airing draws the aired-episode fill behind my watched fill, in the same blue used on the home page, the detail page, and the series page.

## Capabilities

### New Capabilities

- `overlay-behaviour`: the behaviour every overlay in the app shares regardless of which capability owns the overlay's contents — currently one rule, that an open overlay holds the page behind it still.

### Modified Capabilities

- `series-page`: a More group's heading opens that group in full; the hidden-count control is offered only on a group already showing tiles; the "in my list" control reports itself off while any group is open in full and re-filters everything when turned back on.
- `list-editing`: raising an entry's episodes watched without reaching everything available moves it to Watching from any status other than Watching or Rewatching; an explicit status in the same edit takes precedence.
- `anime-detail`: the two score boxes are drawn at one shared width.
- `library-views`: a my-list row for a currently-airing anime shows broadcast progress behind its watched progress.

## Impact

**Backend**

- `Services/Entries/UserAnimeEntryEditService.cs` — the new resume-to-Watching arm in `ApplyEpisodesWatched`.
- `Services/Entries/UserAnimeEntryEditRequest.cs` — a `HasStatus` flag mirroring the existing `HasStartedAt`/`HasCompletedAt` pattern, so an explicitly sent status suppresses the resume rule even when it matches the stored one.

**Frontend**

- `pages/SeriesPage.tsx` — the More section's group-heading, hidden-count, and filter-control behaviour.
- `components/Modal.tsx` (+ a small scroll-lock hook) — the shared overlay scroll lock, which reaches every overlay in the app through this one component.
- `components/EntryEditorOverlay.tsx` — the status dropdown follows a raised count, and the status is sent explicitly when the count is raised.
- `pages/AnimeDetailPage.css` — the shared width for the two score boxes.
- `components/MyListRow.tsx` — passes the aired count to the progress bar it already renders.

**Not affected**

`ProgressBar` and `AiringProgressBar` themselves: the blue aired fill already exists in `ProgressBar` for the detail page and the currently-watching cards, so the my-list row change is a prop, not a new rendering mode. The completion rules, the completion-score prompt, and the aired-episode gate are untouched — the resume rule fires only where an edit does *not* reach everything available.
