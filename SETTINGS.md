# Settings page

The **Settings** page is the app's operational surface — the one place to see
sync health and to run the handful of manual sync/repair actions by hand. You
almost never *need* it: this app keeps your local database and your real
MyAnimeList account in sync automatically, in both directions. Settings exists
for the moments when you want to force something *now*, or when automatic
syncing has drifted, stalled, or imported data incorrectly.

To understand *when* to reach for each button, it helps to know what happens
without you:

- **Your edits → MAL (push).** Every edit you make in the app marks that entry
  "pending" and starts an 8-second debounce timer; when it expires the change
  is pushed to MAL. A background retry pass sweeps every 2 minutes to re-push
  anything that failed or was lost to a restart.
- **MAL → your edits (pull).** A full **reconciliation** runs automatically
  **once a week**, pulling your MAL list and flagging anything that changed
  outside the app for your review.
- **Cached anime metadata refresh.** A background job trickles score/metadata
  refreshes for anime on your list on a staleness schedule (currently-airing
  shows refresh daily, recently-finished every few days, older shows weekly to
  monthly), capped so it never hammers the MAL API.

The five actions below are the manual overrides for those automatic flows. The
Settings page also has a **Content** section with a **Hide NSFW** checkbox —
unrelated to sync, it's a display preference: enabling it excludes hentai
(anime MAL rates `rx`) from the season browser only. `r`/`r+` titles, search
results, my list, and everything else are unaffected. It's unchecked (off) by
default and takes effect immediately without refetching from MAL.

---

## Quick reference

| Action | Direction | Applies immediately? | Cost | Reach for it when… |
|---|---|---|---|---|
| **Resync now** | Local → MAL (push) | Yes | Cheap | Your recent edits aren't on MAL yet and you don't want to wait |
| **Run full reconciliation** | MAL → Local (pull) | No — held for review | Medium | You changed your list *outside* this app and want those changes in |
| **Re-authorize with MAL** | — (auth) | — | Cheap | Syncs fail with auth errors, or you're "Not connected" |
| **Force-refresh anime metadata** | MAL → Local (one anime) | Yes | Cheap | One specific show's cached info (score, air dates, etc.) is stale |

The **Sync status** panel at the top shows *Pending / retrying* (how many of
your edits are still queued to push to MAL) and *Last successful sync*.

---

## Resync now

**What it does.** Immediately pushes every entry that's still marked pending to
MAL, skipping the 8-second debounce timers and not waiting for the 2-minute
retry pass. It's push-only: it sends your local edits *up* to MAL and never
pulls anything down.

**When to use it.**
- You just made edits and want them reflected on MAL right away — e.g. before
  closing the app or opening MAL's own site to check something.
- The *Pending / retrying* count is stuck above 0 and you don't want to wait
  out the next automatic retry.

**Why / notes.** Safe and idempotent — running it with nothing pending simply
does nothing. If a push keeps failing (network down, MAL API error, expired
auth), the entry stays pending and the count won't clear; that's usually a sign
to check your MAL connection (see **Re-authorize** below).

## Run full reconciliation

**What it does.** Pulls your complete MyAnimeList list and compares it against
your local data (status, episodes watched, score, rewatch count, start/finish
dates). Instead of applying changes, it computes a **diff and holds it for your
review** — a *Pending reconciliation diff* section appears listing every entry
that would be added, updated **or removed**, with **Accept** (apply everything)
and **Cancel** (discard) buttons. This is the same job that runs automatically
once a week; the button just triggers it on demand.

**When to use it.**
- You edited your list somewhere *other than this app* — MAL's website or
  mobile app, another client — and want those changes pulled in.
- You suspect the app and MAL have drifted apart and want to see the
  differences before deciding.

**Why / notes.**
- **Nothing is applied until you Accept.** Cancel discards the diff; the next
  run recomputes a fresh one.
- Entries with **unsynced local edits are skipped**, so a pending push of yours
  is never silently overwritten by MAL's older value.
- New anime that exist on MAL but not locally are added; their *catalog*
  metadata is cached right away, but the *list entry* (your status/score) still
  goes through the review step.
- An anime **deleted on MyAnimeList's site** is offered here for removal.
  Accepting removes it locally only — nothing is sent to MyAnimeList, since
  it's already in that state.
- An anime whose MyAnimeList list status this app doesn't recognize is **left
  out** of the diff entirely. The run (or the weekly check) reports it as
  failed, naming how many were left out; the backend log names each one.

## Re-authorize with MAL

**What it does.** Sends you through MyAnimeList's OAuth login flow again and
stores a fresh access token. On success it also kicks off an import so a
freshly-connected account is populated. The *MyAnimeList connection* line shows
whether a token is currently on file (*Connected* / *Not connected*).

**When to use it.**
- The connection shows **Not connected**, or syncs start failing with
  authorization errors.
- Your token expired or was revoked (e.g. you removed the app's access from
  your MAL account settings).
- You switched MAL accounts, or re-created your MAL API application/credentials.

**Why / notes.** This is the fix for anything auth-related. If *Resync now*
won't clear its pending count, or reconciliation fails immediately,
re-authorizing is the first thing to try.

## Force-refresh anime metadata

**What it does.** Search for and pick a single anime, then **Refresh** to pull
its full detail from MAL in one call and update that one cached record
immediately — score, episode count, air dates/broadcast schedule, images, and
the rest — along with its sync timestamps.

**When to use it.**
- One specific show is displaying stale or wrong cached info and you don't want
  to wait for the background refresh to get to it — e.g. a currently-airing
  show's next-episode date or score looks off, or an image/title is outdated.

**Why / notes.** The background refresh job is deliberately gentle: it visits
anime on a staleness schedule and, on its routine passes, mostly updates just
the score. This button is the "refresh *this one* fully, *right now*" escape
hatch. It's a single cheap API call scoped to the one anime you pick, so it's
safe to use freely.
