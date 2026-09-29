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
- **Id mapping and TMDB pictures.** Once a week the app downloads a
  community-maintained table that maps MAL ids to TMDB and IMDb ids. It needs no
  key, it's what puts an **IMDb** link on the anime and series pages, and it has
  no control on this page: if a download fails, the previous mapping simply stays
  in use and the failure is only in the backend log. With the optional
  `TMDB_API_KEY` set (see the README), pictures also come from TMDB: an anime on
  your list, or a series, fetches its TMDB pictures the first time you open its
  page and again after 30 days. A list of pictures nobody has opened for about
  five months is deleted again, since TMDB's terms limit how long its data may
  be kept, and fetched anew if a page needs it; a picture you picked stays.
  Without a key nothing is fetched, and IMDb links work anyway.
- **Airing dates.** Per-episode airing dates come from AniList. The first run
  fetches them for your whole list (the shows that matter now first), and after
  that a daily pass and rechecks keep the airing shows current, while an hourly
  check catches up any anime on your list that has no airing dates yet. An
  anime AniList has no entry for is looked up again after about 90 days.

The actions below are the manual overrides for those automatic flows. The
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
| **Refresh all airing dates** | AniList → Local | Yes | Medium | Airing dates look wrong or missing across your list |
| **Force all airing dates** | AniList → Local | Yes | Large | They still look wrong after a Refresh all |

The **Sync status** panel at the top shows *Pending / retrying* (how many of
your edits are still queued to push to MAL) and *Last successful sync*. The
**Library data** entry in Data tools isn't an action: it shows what the first run
brought in and what is still being fetched (see below).

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
re-authorizing is the first thing to try. MyAnimeList sends you back to
Settings afterwards. If the sign-in didn't go through, the Account section says
why (cancelled or refused on MyAnimeList, expired — for example because the app
restarted while you were signing in — or couldn't be completed, with the
details in the backend log) until you leave or reload the page, and
**Re-authorize** tries again.

## Library data

**What it shows.** The first run (see the README) reads your list, fetches every
anime's details, builds your series and fetches airing dates. The entry at the
top of Data tools shows the steps of that setup that still have something left
— *Reading your list*, *Fetching anime details*, *Building series*, *Airing
dates* — each with its state, its counts and, while it runs, its bar. Once all
four are done no step is listed at all.

Home opens before setup's airing work is over: the shows that matter now come
first, and the rest of the airing dates are fetched in the background. This is
the one place that shows it — no other page draws a bar for it, and the navbar
says nothing.

It also lists **issues**, each with when it resumes: anime waiting to be retried,
a service that stopped answering (AniList, after Home), and a service that is
limiting requests. A **Retry now** button appears while there is one; it tries
everything that is waiting at once, but doesn't cut short a wait the service
itself asked for.

**Skipped anime.** Below the steps, every anime setup skipped for good, with its
title (a link to its page) and why:
- *MyAnimeList doesn't have it* — MyAnimeList answered 404 for its details.
- *MyAnimeList lists it with a status this app doesn't recognize* — the status
  is named. Nothing is stored for it, so it isn't on your list here either.

If nothing was skipped, it says so. A show MyAnimeList didn't have drops off the
list on its own once a later scheduled refresh fetches it.

**When to use it.** To see whether setup's background work has finished, or to
find out why an anime from your MyAnimeList list is missing after the first run.

**Why / notes.** There is no button to start setup: it runs once. The entry
reads the same status the setup screen does, refreshing every couple of seconds
while anything is still going and once otherwise.

## Refresh all / Force all airing dates

**What they do.** Both fetch per-episode airing dates from AniList for the anime
on your list and store them, as one background job with a progress bar. Only one
of them runs at a time: pressing either while one runs starts nothing new.

- **Refresh all airing dates** skips the finished shows whose airing history is
  complete: AniList knows the show, and the highest episode number stored
  reaches its known episode count. The check uses the highest episode number,
  not how many rows are stored: AniList holds rows for episodes 5 to 28 of
  *Frieren*, because episodes 1 to 4 premiered together, and it still counts as
  complete. Everything else is fetched again: airing and upcoming shows,
  finished shows with gaps or no rows, and shows AniList had no entry for, which
  are looked up again at once instead of after 90 days.
- **Force all airing dates** skips nothing. It fetches every anime on your list
  again, complete or not, so it takes longer.

**When to use them.**
- Refresh all: airing dates or an episode band look wrong or missing on several
  shows and you don't want to wait for the automatic passes.
- Force all: they still look wrong after a Refresh all, or you want every show's
  stored dates redone from AniList.

**Why / notes.** Both are paced to AniList's rate limit, and a failure on one
anime doesn't stop the rest. While the job runs, both buttons are disabled and
the one you pressed says it is running. Neither touches your list or anything on
MyAnimeList.

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

**TMDB pictures too.** The same action — it's also the **Refresh data** button on
an anime's page — refetches that anime's TMDB pictures whatever their age, when
the anime is on your list, has a TMDB match and a `TMDB_API_KEY` is set. That is
how to pick up posters TMDB added since its last fetch, which otherwise wait for
the 30-day refresh. A TMDB failure never fails the refresh: the pictures already
cached stay as they were.

---

## Credits

The last group on the page, below **Account**. It holds no controls: it carries
TMDB's logo and the notice its API terms of use require, so the app credits TMDB
whether or not you have a `TMDB_API_KEY` set. The picture picker shows the same
logo and notice wherever it offers TMDB images.

*This application uses TMDB and the TMDB APIs but is not endorsed, certified, or
otherwise approved by TMDB.*
