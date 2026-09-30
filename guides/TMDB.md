# TMDB pictures

MyAnimeList's own pictures are fairly small. With a free
[TMDB](https://www.themoviedb.org/) API key, the **Choose picture** picker on an
anime's or a series' page also offers TMDB's full-size posters and backdrops,
next to MyAnimeList's, and you can pick any of them.

It's optional. Without a key the app makes no TMDB requests and otherwise works
the same.

## Getting a key

1. Create a free account at https://www.themoviedb.org/ and open
   [Settings → API](https://www.themoviedb.org/settings/api).
2. Request an API key for personal, non-commercial use.
3. Copy the **API Key**: the short v3 key, not the long "API Read Access Token".
4. Set it as `TMDB_API_KEY` in `.env` and restart the app (with Docker,
   `docker compose up -d`).

TMDB's terms allow free use in non-commercial projects, and each installation
needs its own key, so the app never ships with one.

## When pictures are fetched

- An anime on your list, or a series, fetches its TMDB pictures the first time
  you open its page, and again after 30 days. **Refresh data** on an anime's
  page fetches them again right away.
- There is no background sweep over your list.
- TMDB's terms limit how long its data may be kept, so a list of pictures that
  hasn't been fetched for about five months is deleted, and fetched again if a
  page needs it. A picture you picked is stored with the anime or series, so it
  stays.
- TMDB's images load straight from TMDB at full size, so the picker keeps each
  group of them closed until you open it.

## No pictures, or the wrong ones

TMDB pictures need the anime's TMDB id, which comes from a community-maintained
mapping the app downloads once a week. A brand-new show may not have one yet,
and a few entries point at the wrong season. You can fix both in
`backend/custom/id-mapping.json`; see
[backend/custom/README.md](../backend/custom/README.md).

## Attribution

TMDB's terms require its logo and this notice in the app. The picker shows them
wherever it offers TMDB images, and the **Credits** group on the Settings page
always does.

<img src="../frontend/assets/TMDB_Logo.svg" alt="The Movie Database (TMDB)" height="20">

*This application uses TMDB and the TMDB APIs but is not endorsed, certified, or
otherwise approved by TMDB.*
