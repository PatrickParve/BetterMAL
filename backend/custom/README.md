# Custom id mapping

BetterMAL finds an anime's TMDB and IMDb ids in a community-maintained mapping
(Fribb's `anime-lists`), which it downloads once a week. That mapping has gaps
and a few mistakes. `id-mapping.json` in this folder is where you fix them: the
app reads it on top of the downloaded mapping.

How an edit takes effect depends on how the backend runs:

- **With Docker**, this folder is copied into the backend image when it is built,
  so apply an edit with `docker compose up -d --build`. It takes a few seconds,
  since only the last layer of the image changes.
- **Natively** (`dotnet run` from `backend/AnimeTracker.Api`), the backend reads
  the file where it sits, and an edit applies within a few seconds, with no
  restart.

## An entry

```json
{
  "mal_id": 60568,
  "themoviedb_id": { "tv": 280564 },
  "season": { "tmdb": 1 },
  "imdb_id": ["tt38754770"],
  "note": "False Memory (2026). The source lists it with no ids."
}
```

It has the same shape as an entry of the downloaded file, so you can copy one
from there. All of these are optional except `mal_id`:

| Field | Meaning |
|---|---|
| `mal_id` | The MyAnimeList id (the number in the anime's MAL address). |
| `themoviedb_id` | `{ "tv": 280564 }` for a series, or `{ "movie": [635302] }` for a film. TMDB numbers TV shows and movies separately: 280564 is a TV show *and* an unrelated 2007 film, so read the address on themoviedb.org (`/tv/…` or `/movie/…`). |
| `season.tmdb` | Which TMDB season this MAL entry is. Counts only with a TV id. Check the show's *Seasons* page on TMDB: many shows keep every MAL season inside TMDB's season 1. |
| `imdb_id` | A list of `tt…` ids. |
| `override` | `true` to win over the downloaded mapping. See below. |
| `note` | Free text for you: which show this is and why it is here. It only appears in log lines. |

## Fill, or override

An entry has two groups: the TMDB ids together with the season, and the IMDb
ids. Each is decided separately.

- **By default an entry only fills a gap.** It applies to a group only while the
  downloaded mapping has nothing for it. As soon as the source has caught up, its
  ids win and your entry is ignored. The backend then logs that the entry is no
  longer needed, and you can delete it.
- **With `"override": true` it wins even when the source has something**, to
  correct a value the source has wrong (a season TMDB has since split, say). Say
  why in `note`. An override stays in force until you remove it, so the log tells
  you when the source starts to agree with it.

A group your entry doesn't give is read from the downloaded mapping.

## When something is wrong in the file

Comments and trailing commas are allowed. A syntax error leaves the last good
entries in force, so a typo can't switch your overrides off, and the backend logs
what and where. A bad entry (no `mal_id`, no ids at all, an IMDb id that isn't
`tt` and digits, a repeated `mal_id`) is skipped and logged, and the others still
apply. To see what the backend made of the file:

```bash
docker compose logs backend | grep -i "custom id mapping"
```

## Where the real fix belongs

The downloaded mapping is generated from
[Anime-Lists/anime-lists](https://github.com/Anime-Lists/anime-lists)
(`anime-list-master.xml`, keyed by AniDB id; Fribb's `anime-list-full.json`
gives the AniDB id for a MAL id). A missing or wrong entry there is fixed for
everyone with a pull request that edits that one file, and most are merged within
a day. Once Fribb regenerates, your entry becomes unnecessary and can go.
