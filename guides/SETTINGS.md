# Settings page

Most of the time you don't need this page: the app keeps your list and your
MyAnimeList account in sync by itself. Settings is where you see how that's
going, and where you step in when something needs you.

The gear in the navbar shows a dot when something here needs a look: changes
held for review, differences waiting from a reconciliation, a lost connection to
MyAnimeList, or a finished task whose result you haven't seen yet. While a task
runs, a thin bar under the gear shows its progress.

## What happens by itself

- Your edits are sent to MyAnimeList a few seconds after you make them. If
  sending fails, the app keeps trying every couple of minutes.
- Each time the app starts, and after you re-authorize, it adds anything on your
  MyAnimeList list that isn't here yet.
- Once a week it compares your whole list with MyAnimeList. Anything that
  differs waits for your review.
- Anime details refresh in the background: airing shows daily, older ones less
  often.
- Airing dates from AniList are kept current every day, and an anime on your
  list with no airing dates yet is picked up within the hour.

The actions below are for when you don't want to wait, or when one of these has
gone wrong.

## Preferences

- **Always show MAL scores for completed and dropped shows**: when you hide
  scores with the navbar's toggle, the scores of shows you've finished or
  dropped stay visible.
- **Hide NSFW**: hides Rx-rated titles when you browse seasons and years.
  Nothing else is affected.

Both are saved in your browser, so each browser keeps its own.

## Sync

The lines at the top show how many edits are still waiting to be sent
(**Pending / retrying**), how many are **held for review**, the **last
successful sync**, and when the **weekly check** last ran and whether it went
fine.

### MyAnimeList list import

Appears when the app finds anime on your MyAnimeList list that aren't here yet,
and shows its progress while it adds them. It only adds anime: a change to an
anime that's already here comes in through a reconciliation instead. If it can't
finish, it tries again by itself and says when.

### Sync now

**Resync now** sends your unsent edits to MyAnimeList right away, instead of
waiting. Use it
before you check something on MyAnimeList's site, or when the pending count
won't go down. If it keeps failing, re-authorize (see [Account](#account)).
Changes held for review aren't sent; they wait for your decision.

### Changes held for review

If the app stops while an edit is still waiting to be sent, it doesn't send that
edit when it starts again, since MyAnimeList may have changed in the meantime.
Instead the edit is listed here, with what it would send and what MyAnimeList
holds now.

- **Accept** sends your change. **Decline** keeps MyAnimeList's value.
  **Accept all** and **Decline all** do the same for every row.
- Editing the anime again also settles it: your new edit is sent as usual.
- A change MyAnimeList already agrees with disappears by itself.
- When MyAnimeList has no entry for the anime, declining removes it from your
  list here. The row warns you when that's the case.

### Run full reconciliation

Reads your whole MyAnimeList list and compares it with yours. Nothing changes
yet: the differences are listed under **Differences from MyAnimeList** as new
entries, updated ones and anime removed on MyAnimeList. **Accept** applies them
all, and **Cancel** drops them. The same check runs by itself once a week.

Use it when you've changed your list somewhere else, like MyAnimeList's site or
app, and want that change here now.

- An anime with an edit of yours still waiting to be sent is left out, so your
  edit is never overwritten.
- Accepting an anime removed on MyAnimeList removes it here only. Nothing is
  sent to MyAnimeList.
- An anime whose MyAnimeList status the app doesn't recognize is left out, and
  the run says how many there were.

## Data tools

### Library data

Shows what the first run brought in, and anything still loading:

- The setup steps that aren't finished yet, with their progress: *Reading your
  list*, *Fetching anime details*, *Building series* and *Airing dates*.
- Problems, each with when it tries again, and a **Retry now** button.
- The anime setup skipped for good, each with the reason: MyAnimeList doesn't
  have it, or lists it with a status the app doesn't recognize.

Home opens before all the airing dates are in, and this is the only place that
shows the rest arriving. Look here to see whether that has finished, or to find
out why an anime from your MyAnimeList list is missing.

### Airing dates

Fetches per-episode airing dates from AniList for the anime on your list.

- **Refresh all airing dates** skips finished shows whose dates are already
  complete, and fetches everything else again, including shows AniList didn't
  know last time.
- **Force all airing dates** fetches every show again, so it takes longer.

Use Refresh all when airing dates look wrong or missing on several shows, and
Force all if they still do afterwards. Only one runs at a time, and neither
changes your list or anything on MyAnimeList.

### Build all series

The first run builds the series of every anime on your list. For an anime you
add later, the series is built when you first open its series page. **Build all series
from my list** builds every missing one at once, which can take a while. Use it
when the Series page is missing franchises for anime you added after the first
run.

### Force-refresh anime metadata

Search for an anime and press **Refresh** to fetch its details from MyAnimeList
now: score, episodes, air dates, pictures and the rest. Details refresh by
themselves on a schedule; this is for one show that's out of date right now. The
**Refresh data** button on an anime's page does the same.

With a TMDB key, it also fetches the anime's TMDB pictures again, for an anime
on your list that has a TMDB match. That's how you get posters TMDB added
recently. See [TMDB.md](TMDB.md).

## Files

### Back up my list

Saves your list as a JSON file: every anime's status, episodes, score, dates and
rewatches. It's a copy to keep. The app never imports it.

### Export to a file

For using BetterMAL on two computers. Your list itself travels through
MyAnimeList, but your ranking, the pictures and titles you chose and your edit
history don't. **Export** saves them to a file, which you then import on the
other computer.

### Import from a file

Choose a file exported on your other computer, or drop it on the row. Where both
computers changed the same thing, the newer change wins. An import can't be
undone.

When it's done, a card sums up what changed, and **See details** lists
everything, including anything that couldn't be applied.

It refuses a file exported on this same computer, a list backup, and a file from
a newer version of the app. It also can't start until the list import has
checked your MyAnimeList list since the app started, which needs a working
connection. When an import can't start, the page says why.

## Account

Shows whether you're connected to MyAnimeList. If MyAnimeList stops accepting
the app's login, it says the connection was lost, and your changes aren't sent
until you reconnect.

**Re-authorize with MAL** signs you in to MyAnimeList again. Use it when:

- the connection is lost, or it says you're not connected
- syncing fails with login errors
- you removed the app's access on MyAnimeList, switched accounts, or created a
  new API app

You come back to Settings afterwards. If the sign-in didn't go through, it says
why. Once you're connected again, the list import checks for anime to add.

## Credits

TMDB's logo and the notice its terms require. It's always shown, whether or not
you have a TMDB key.
