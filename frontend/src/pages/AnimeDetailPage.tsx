import { useEffect, useRef, useState } from "react";
import { Link, useParams } from "react-router-dom";
import {
  ApiError,
  getAnimeDetail,
  refreshAnime,
  refreshAnimePictures,
  refreshAnimeTmdbPictures,
  resetAnimePicture,
  setAnimePicture,
  updateEntry,
} from "../api/client.ts";
import type {
  AnimeDetailDto,
  IncrementTarget,
  NextEpisodeEtaDto,
} from "../api/types.ts";
import { PicturePickerOverlay } from "../components/PicturePickerOverlay.tsx";
import {
  animeTmdbSections,
  malSections,
  pickerOptionCount,
} from "../components/picturePickerSections.ts";
import { ProgressBar } from "../components/ProgressBar.tsx";
import { RelatedAnimeOverlay } from "../components/RelatedAnimeOverlay.tsx";
import { RevealControl } from "../components/RevealControl.tsx";
import { ScoreValue } from "../components/ScoreValue.tsx";
import { SpaceWrappedTitle } from "../components/SpaceWrappedTitle.tsx";
import { useEntryEditor } from "../context/EntryEditorContext.tsx";
import {
  useEpisodeIncrement,
  useSetEpisodesWatched,
} from "../context/CompletionPromptContext.tsx";
import { useActionFailure } from "../context/ActionFailureContext.tsx";
import { useScoreVisibility } from "../context/ScoreVisibilityContext.tsx";
import { useLandscapePicture } from "../hooks/useLandscapePicture.ts";
import { usePageData } from "../hooks/usePageData.ts";
import { usePickerOpenGroups } from "../hooks/usePickerOpenGroups.ts";
import { useScoreReveal } from "../hooks/useScoreReveal.ts";
import {
  dedupePictureOptions,
  episodeCeiling,
  formatRuntime,
  hasAiredEpisodes,
  imdbLinks,
  isScoreRevealableStatus,
  isTmdbImageUrl,
  mediaTypeLabel,
  pickDisplayTitle,
  SERIES_TRAVERSAL_RELATIONS,
  STATUS_LABELS,
} from "../utils/anime.ts";
import "./AnimeDetailPage.css";

const AIRING_STATUS_LABELS: Record<string, string> = {
  currently_airing: "Currently airing",
  finished_airing: "Finished airing",
  not_yet_aired: "Not yet aired",
};

const NO_INFO = "No info";

const RATING_LABELS: Record<string, string> = {
  g: "G",
  pg: "PG",
  pg_13: "PG-13",
  r: "R",
  "r+": "R+",
  rx: "Rx",
};

function formatDate(value: string | null): string {
  if (!value) return NO_INFO;
  return new Date(value).toLocaleDateString(undefined, {
    year: "numeric",
    month: "short",
    day: "numeric",
  });
}

// A movie or special that aired in a single day reads better as one date
// than as a same-day-to-same-day range.
function formatAiredRange(from: string | null, to: string | null): string {
  if (from && from === to) return formatDate(from);
  return `${formatDate(from)} – ${formatDate(to)}`;
}

function formatRating(rating: string | null): string {
  if (!rating) return NO_INFO;
  return RATING_LABELS[rating] ?? rating;
}

function formatSeason(seasonYear: number | null, season: string | null): string | null {
  if (!season || seasonYear === null) return null;
  return `${season.charAt(0).toUpperCase()}${season.slice(1)} ${seasonYear}`;
}

function formatAiringStatus(
  status: string | null,
  episodesAired: number | null,
  totalEpisodes: number | null,
  nextEpisode: NextEpisodeEtaDto | null,
): string {
  if (!status) return NO_INFO;
  const label = AIRING_STATUS_LABELS[status] ?? status;
  const base =
    status === "currently_airing" && episodesAired !== null
      ? `${label}: ${episodesAired}/${totalEpisodes ? totalEpisodes : "?"} ep aired`
      : label;
  return nextEpisode
    ? `${base} · next in ${nextEpisode.days}d ${nextEpisode.hours}h`
    : base;
}

// MAL sends raw source values like "light_novel" — prettify to "Light novel".
function formatSource(source: string | null): string {
  if (!source) return NO_INFO;
  const spaced = source.replace(/_/g, " ");
  return spaced.charAt(0).toUpperCase() + spaced.slice(1);
}

// AverageEpisodeDurationSeconds is always a per-episode figure; label it as
// such whenever there's more than one episode (or the count isn't known yet)
// so it doesn't read as the show's total runtime. The hour threshold is
// applied to the rounded duration itself rather than to mediaType so a
// feature-length OVA or special reads the same as a movie, while a
// short-episode series that happens to be typed as a movie doesn't.
function formatDuration(seconds: number | null, totalEpisodes: number | null): string {
  if (!seconds) return NO_INFO;
  const minutes = Math.round(seconds / 60);
  const hours = Math.floor(minutes / 60);
  const remainder = minutes % 60;
  const value =
    minutes < 60 ? `${minutes} min` : remainder === 0 ? `${hours}h` : `${hours}h ${remainder}min`;
  return totalEpisodes === 1 ? value : `${value}/ep`;
}

// The show's full runtime — per-episode duration times the published episode
// count — rather than the per-episode figure Duration states. Unknown
// whenever either half is (a still-airing or otherwise uncounted show).
function formatTotalTime(seconds: number | null, totalEpisodes: number | null): string {
  if (!seconds || !totalEpisodes) return NO_INFO;
  return formatRuntime(seconds * totalEpisodes);
}

// Mirrors backend Services/Artwork/AnimePicture.Options: the fetched set
// plus MAL's own main picture and the currently displayed one, deduplicated
// by pictureIdentityKey (not raw URL equality — MAL sometimes serves one
// photo as main_picture's .webp while also listing it as .jpg in `pictures`,
// which would otherwise show as two tiles for one picture), MAL order
// preserved — so the choice MAL has dropped stays visible and replaceable
// (design D9) even before pictureUrls itself has ever loaded. A TMDB choice is
// left out (design D13): it must never surface under the MyAnimeList heading,
// and shows under its own TMDB group instead — or, once TMDB has dropped it,
// as the overlay's "Current picture".
function animePictureOptions(detail: AnimeDetailDto): string[] {
  const chosen = isTmdbImageUrl(detail.selectedPictureUrl) ? null : detail.selectedPictureUrl;
  return dedupePictureOptions(detail.pictureUrls ?? [], [detail.malPictureUrl, chosen]);
}

// MAL's rank is the community score sorted descending, so it follows the
// score's hide/reveal rule. Its reveal is independent of the score's own —
// each useScoreReveal() call holds its own state, so no guard is needed to
// keep the two from cascading into one another.
function RankValue({ rank, completed }: { rank: number | null; completed: boolean }) {
  const { hidden, alwaysShowCompletedScores } = useScoreVisibility();
  const [revealed, reveal] = useScoreReveal();

  // Every branch renders inside .anime-detail-page__rank-slot, not just the
  // hidden one — its vertical-align: middle changes the <p> line's own
  // height (ScoreValue.css / AnimeDetailPage.css), so a branch rendered
  // without it would sit on a shorter line than the others and the box
  // would resize when this rank's reveal state changes.
  if (rank == null) return <span className="anime-detail-page__rank-slot">—</span>;
  if (!hidden || revealed || (completed && alwaysShowCompletedScores)) {
    return <span className="anime-detail-page__rank-slot">#{rank}</span>;
  }

  return (
    <span className="anime-detail-page__rank-slot" style={{ minWidth: `${String(rank).length + 1}ch` }}>
      <RevealControl onReveal={reveal} label="Reveal rank" />
    </span>
  );
}

// Single anime detail page: large picture + progress/edit on the left, a
// rank/score box (plus a my-score/rewatches box only once a score's been
// given), an info box, and a synopsis/background box on the right. The
// external links are plain URL templates (no API call); prequel/sequel/
// main-series buttons render the server-resolved ranked pick (relation-
// confidence spec) rather than the first MAL reports, only when one exists,
// and a More button opens the overlay for everything else.
export function AnimeDetailPage() {
  const { id } = useParams();
  const animeId = Number(id);
  const {
    data: detail,
    loading,
    setData: setDetail,
    reload,
  } = usePageData<AnimeDetailDto>(`anime:${animeId}`, () => getAnimeDetail(animeId));
  const [refreshing, setRefreshing] = useState(false);
  const [retrying, setRetrying] = useState(false);
  const [incrementPending, setIncrementPending] = useState(false);
  const [actionPending, setActionPending] = useState(false);
  const [showRelatedOverlay, setShowRelatedOverlay] = useState(false);
  const [showPicturePicker, setShowPicturePicker] = useState(false);
  const { openGroups, seedOpenGroups, toggleGroup } = usePickerOpenGroups(animeId);
  const { openEditor } = useEntryEditor();
  const increment = useEpisodeIncrement();
  const setEpisodesWatched = useSetEpisodesWatched();
  const reportFailure = useActionFailure();
  const [pictureRef, isLandscapePicture] = useLandscapePicture(detail?.pictureUrl);

  // Visit-triggered picture backfill (design D4b) — mirrors the anime's own
  // TTL detail fetch: fire once per anime, as soon as the server says it has
  // never had a picture set fetched, and merge the result in without a
  // reload. The ref (not state) guard is what survives StrictMode's
  // double-mount without double-firing.
  const pictureRefreshRequestedForRef = useRef<number | null>(null);
  useEffect(() => {
    if (!detail || !detail.picturesFetchPending) return;
    if (pictureRefreshRequestedForRef.current === detail.animeId) return;
    pictureRefreshRequestedForRef.current = detail.animeId;
    refreshAnimePictures(detail.animeId)
      .then(({ pictureUrl, selectedPictureUrl, pictureUrls }) => {
        setDetail((prev) =>
          prev && prev.animeId === detail.animeId
            ? { ...prev, pictureUrl, selectedPictureUrl, pictureUrls }
            : prev,
        );
      })
      .catch(() => {
        // Leave picturesFetchPending as the server last reported it — a
        // later visit's read re-evaluates and retries.
      });
  }, [detail, setDetail]);

  // Visit-triggered TMDB fetch (tmdb-artwork) — the twin of the backfill
  // above: the page has already rendered from cache, and when the server says a
  // set is due one follow-up request fetches it and merges only the TMDB
  // fields in, so it can't roll back what the picture backfill just merged.
  // The same ref guard makes it once per anime even under StrictMode's
  // double-mount, and also stops a set whose fetch keeps failing (a rejected
  // key, TMDB down) from being retried within this visit.
  //
  // `tmdbSettledFor` is what the picker's "Fetching…" note follows: the note
  // is true only while that one request is in flight. Following the flag
  // instead would show it forever, because a failed fetch leaves the flag set
  // for the next visit to retry.
  const tmdbRefreshRequestedForRef = useRef<number | null>(null);
  const [tmdbSettledFor, setTmdbSettledFor] = useState<number | null>(null);
  useEffect(() => {
    if (!detail || !detail.tmdbFetchPending) return;
    if (tmdbRefreshRequestedForRef.current === detail.animeId) return;
    tmdbRefreshRequestedForRef.current = detail.animeId;
    refreshAnimeTmdbPictures(detail.animeId)
      .then(({ tmdb, tmdbFetchPending }) => {
        setDetail((prev) =>
          prev && prev.animeId === detail.animeId ? { ...prev, tmdb, tmdbFetchPending } : prev,
        );
      })
      .catch(() => {
        // Leave tmdbFetchPending as the server last reported it — a later
        // visit's read re-evaluates and retries.
      })
      .finally(() => setTmdbSettledFor(detail.animeId));
  }, [detail, setDetail]);

  async function handleRefresh() {
    if (!detail || refreshing) return;
    setRefreshing(true);
    try {
      await refreshAnime(animeId);
      // reload() never rejects — usePageData's runLoad swallows its own
      // failure — so only a failed refreshAnime reaches this catch (design D4).
      await reload();
    } catch (err) {
      // The page keeps what was already cached, and the notice says the
      // refresh didn't happen.
      reportFailure({
        title: `Couldn't refresh ${pickDisplayTitle(detail.title, detail.englishTitle)}`,
        reason: err instanceof ApiError ? err.reason : null,
      });
    } finally {
      setRefreshing(false);
    }
  }

  // Distinct from handleRefresh: this re-reads the anime the same way a
  // first visit does (the normal detail read, which retries the live fetch
  // on its own since a failed fetch never marks the anime as fetched),
  // rather than calling the explicit force-refresh endpoint.
  async function handleRetry() {
    if (retrying) return;
    setRetrying(true);
    try {
      await reload();
    } finally {
      setRetrying(false);
    }
  }

  async function handleAddToWatching() {
    if (!detail || actionPending) return;
    setActionPending(true);
    try {
      const saved = await updateEntry(detail.animeId, { status: "Watching" });
      setDetail((prev) => (prev ? { ...prev, entry: saved } : prev));
    } catch (err) {
      reportFailure({
        title: `Couldn't add ${pickDisplayTitle(detail.title, detail.englishTitle)} to watching`,
        reason: err instanceof ApiError ? err.reason : null,
      });
    } finally {
      setActionPending(false);
    }
  }

  async function handleAddToList() {
    if (!detail || actionPending) return;
    setActionPending(true);
    try {
      const saved = await updateEntry(detail.animeId, {
        status: "PlanToWatch",
      });
      setDetail((prev) => (prev ? { ...prev, entry: saved } : prev));
    } catch (err) {
      reportFailure({
        title: `Couldn't add ${pickDisplayTitle(detail.title, detail.englishTitle)} to list`,
        reason: err instanceof ApiError ? err.reason : null,
      });
    } finally {
      setActionPending(false);
    }
  }

  function handleOpenRelatedOverlay() {
    setShowRelatedOverlay(true);
  }

  // Optimistic: applies locally immediately (spec anime-detail "A chosen
  // picture applies immediately"), then fires the save. useLandscapePicture
  // re-derives isLandscapePicture on its own since detail.pictureUrl (its
  // src argument) just changed.
  function handlePickAnimePicture(url: string) {
    if (!detail) return;
    setDetail((prev) => (prev ? { ...prev, pictureUrl: url, selectedPictureUrl: url } : prev));
    setAnimePicture(detail.animeId, url)
      .then(({ pictureUrl, selectedPictureUrl }) => {
        setDetail((prev) =>
          prev && prev.animeId === detail.animeId ? { ...prev, pictureUrl, selectedPictureUrl } : prev,
        );
      })
      .catch((err) => {
        // The optimistic change stays on screen until a reload, and the
        // notice is what tells you it wasn't saved (design D2).
        reportFailure({
          title: `Couldn't save the picture for ${pickDisplayTitle(detail.title, detail.englishTitle)}`,
          reason: err instanceof ApiError ? err.reason : null,
        });
      });
  }

  // Clearing follows the same optimistic-then-reconcile shape as picking:
  // apply MAL's picture locally right away, then patch both fields from the
  // response.
  function handleClearAnimePicture() {
    if (!detail) return;
    setDetail((prev) =>
      prev ? { ...prev, pictureUrl: prev.malPictureUrl, selectedPictureUrl: null } : prev,
    );
    resetAnimePicture(detail.animeId)
      .then(({ pictureUrl, selectedPictureUrl }) => {
        setDetail((prev) =>
          prev && prev.animeId === detail.animeId ? { ...prev, pictureUrl, selectedPictureUrl } : prev,
        );
      })
      .catch((err) => {
        // The optimistic change stays on screen until a reload, and the
        // notice is what tells you it wasn't saved (design D2).
        reportFailure({
          title: `Couldn't reset the picture for ${pickDisplayTitle(detail.title, detail.englishTitle)}`,
          reason: err instanceof ApiError ? err.reason : null,
        });
      });
  }

  function buildIncrementTarget(): IncrementTarget {
    // Only called when detail.entry exists (guarded by the callers below).
    const entry = detail!.entry!;
    return {
      animeId: detail!.animeId,
      animeTitle: pickDisplayTitle(detail!.title, detail!.englishTitle),
      pictureUrl: detail!.pictureUrl,
      episodesWatched: entry.episodesWatched,
      previousStatus: entry.status,
      currentScore: entry.myScore,
      mediaType: detail!.mediaType,
      onSaved: (saved) =>
        setDetail((prev) => (prev ? { ...prev, entry: saved } : prev)),
      // The completion-score prompt's own save (a separate updateEntry call
      // for myScore) isn't reflected by the increment's onSaved above, so
      // patch it in here instead of re-reading the anime.
      onCompleted: (saved) => setDetail((prev) => (prev ? { ...prev, entry: saved } : prev)),
    };
  }

  async function handleIncrement() {
    if (!detail || !detail.entry || incrementPending) return;
    setIncrementPending(true);
    try {
      await increment(buildIncrementTarget());
    } finally {
      setIncrementPending(false);
    }
  }

  async function handleSetWatched(value: number) {
    if (!detail || !detail.entry || incrementPending) return;
    setIncrementPending(true);
    try {
      await setEpisodesWatched(buildIncrementTarget(), value);
    } finally {
      setIncrementPending(false);
    }
  }

  function handleEdit() {
    if (!detail) return;
    openEditor({
      animeId: detail.animeId,
      animeTitle: pickDisplayTitle(detail.title, detail.englishTitle),
      totalEpisodes: detail.totalEpisodes,
      airingStatus: detail.airingStatus,
      episodesAired: detail.episodesAired,
      mediaType: detail.mediaType,
      entry: detail.entry,
      onSaved: (saved) =>
        setDetail((prev) => (prev ? { ...prev, entry: saved } : prev)),
      onDeleted: () =>
        setDetail((prev) => (prev ? { ...prev, entry: null } : prev)),
    });
  }

  if (Number.isNaN(animeId)) {
    return <p className="anime-detail-page__empty">Anime not found.</p>;
  }

  if (loading) {
    return <p className="anime-detail-page__loading">Loading…</p>;
  }

  if (!detail) {
    return (
      <p className="anime-detail-page__empty">Couldn't load this anime.</p>
    );
  }

  // Server-resolved ranked pick (relation-confidence spec) rather than the
  // first MAL reports — includes a prequel/sequel MAL stored only on the
  // other anime's page. Matched into moreRelations by id, not by reference:
  // the pick is a separate DTO shape (ResolvedRelationDto), not necessarily
  // the same object as any entry in relatedAnime.
  const { prequel, sequel, parentStory } = detail;
  const moreRelations = detail.relatedAnime.filter(
    (r) =>
      r.animeId !== prequel?.animeId &&
      r.animeId !== sequel?.animeId &&
      r.animeId !== parentStory?.animeId,
  );
  // Zero-cost check on relation data already loaded — no probe request for a
  // series that might not exist (design.md decision 11). `detail.inSeries`
  // covers the gap this alone misses: a member reached only by a reverse
  // edge from another anime (this row's own relations are thin or it was
  // only ever lean-fetched) has no traversable relation of its own, even
  // though it's already a member of a built series.
  const hasSeriesRelation =
    detail.inSeries ||
    detail.relatedAnime.some((r) => SERIES_TRAVERSAL_RELATIONS.has(r.relationType));
  const hideAddToWatching =
    detail.entry?.status === "Watching" ||
    detail.entry?.status === "Completed" ||
    detail.entry?.status === "Dropped" ||
    detail.entry?.status === "Rewatching";
  // The picker's sections: MyAnimeList, then one per TMDB scope (design D12).
  // The control counts every option across them, so a lone MAL picture plus
  // TMDB images still offers a choice, and appears when a TMDB follow-up
  // brings the count above one.
  const tmdbSections = animeTmdbSections(detail.tmdb);
  const pickerSections = [...malSections(animePictureOptions(detail)), ...tmdbSections];
  const showPicturePickerButton = detail.entry != null && pickerOptionCount(pickerSections, detail.pictureUrl) > 1;
  const pickerNotes: string[] = [];
  // "Fetching…" only while the follow-up request is in flight; once it has
  // settled with a set still due (a failed fetch) the note says a later visit
  // retries instead, rather than claiming a fetch that isn't running.
  if (detail.tmdbFetchPending) {
    pickerNotes.push(
      tmdbSettledFor !== detail.animeId
        ? "Fetching TMDB pictures — more may appear."
        : "More TMDB pictures may appear on a later visit.",
    );
  }
  // "TMDB has no match" is said only while TMDB is on (a key is configured),
  // since without one the picker says nothing about TMDB. `tmdb` is non-null
  // for any my-list anime, key or not, so it is `configured` that says so.
  if (detail.tmdb?.configured && !detail.tmdb.hasMapping) pickerNotes.push("TMDB has no match for this anime.");
  const hasSynopsis = Boolean(detail.synopsis && detail.synopsis.trim().length > 0);
  const hasBackground = Boolean(detail.background && detail.background.trim().length > 0);

  return (
    <div className="anime-detail-page">
      <div className="anime-detail-page__header">
        <div className="anime-detail-page__top">
          <h1>
            <SpaceWrappedTitle text={pickDisplayTitle(detail.title, detail.englishTitle)} />
          </h1>

          <div className="anime-detail-page__related">
            {hasSeriesRelation && (
              <Link
                to={`/series/${detail.animeId}`}
                className="anime-detail-page__related-link"
              >
                Series
              </Link>
            )}
            {parentStory && (
              <Link
                to={`/anime/${parentStory.animeId}`}
                className="anime-detail-page__related-link"
                title={pickDisplayTitle(parentStory.title, parentStory.englishTitle)}
              >
                Main series
              </Link>
            )}
            {moreRelations.length > 0 && (
              <button
                type="button"
                className="anime-detail-page__related-link"
                onClick={handleOpenRelatedOverlay}
              >
                More
              </button>
            )}
            {prequel ? (
              <Link
                to={`/anime/${prequel.animeId}`}
                className="anime-detail-page__related-link"
                title={pickDisplayTitle(prequel.title, prequel.englishTitle)}
              >
                ← Prequel
              </Link>
            ) : (
              <button
                type="button"
                className="anime-detail-page__related-link anime-detail-page__related-link--disabled"
                disabled
              >
                ← Prequel
              </button>
            )}
            {sequel ? (
              <Link
                to={`/anime/${sequel.animeId}`}
                className="anime-detail-page__related-link"
                title={pickDisplayTitle(sequel.title, sequel.englishTitle)}
              >
                Sequel →
              </Link>
            ) : (
              <button
                type="button"
                className="anime-detail-page__related-link anime-detail-page__related-link--disabled"
                disabled
              >
                Sequel →
              </button>
            )}
          </div>
        </div>

        {detail.refreshFailed && (
          <div className="anime-detail-page__refresh-notice">
            <span>Couldn't refresh this anime's data — showing cached info.</span>
            <button
              type="button"
              className="anime-detail-page__refresh-notice-retry"
              onClick={handleRetry}
              disabled={retrying}
            >
              {retrying ? "Retrying…" : "Retry"}
            </button>
          </div>
        )}
      </div>

      {showRelatedOverlay && (
        <RelatedAnimeOverlay
          relations={moreRelations}
          onClose={() => setShowRelatedOverlay(false)}
        />
      )}

      {showPicturePicker && (
        <PicturePickerOverlay
          title="Choose picture"
          sections={pickerSections}
          openGroups={openGroups}
          onToggleGroup={toggleGroup}
          current={detail.pictureUrl}
          onPick={handlePickAnimePicture}
          onClose={() => setShowPicturePicker(false)}
          onClear={detail.selectedPictureUrl != null ? handleClearAnimePicture : undefined}
          notes={pickerNotes}
          showTmdbAttribution={tmdbSections.length > 0}
        />
      )}

      <div className="anime-detail-page__body">
        <div className={`anime-detail-page__picture-col${isLandscapePicture ? " anime-detail-page__picture-col--landscape" : ""}`}>
          {detail.pictureUrl ? (
            <img
              ref={pictureRef}
              src={detail.pictureUrl}
              alt=""
              className={`anime-detail-page__picture${isLandscapePicture ? " anime-detail-page__picture--landscape" : ""}`}
            />
          ) : (
            <div
              className="anime-detail-page__picture anime-detail-page__picture--placeholder"
              aria-hidden="true"
            />
          )}

          <div className="anime-detail-page__progress-row">
            {hasAiredEpisodes(detail.airingStatus, detail.episodesAired) && (
              <ProgressBar
                watched={detail.entry?.episodesWatched ?? 0}
                total={detail.totalEpisodes}
                aired={detail.airingStatus === "currently_airing" ? detail.episodesAired : null}
                onIncrement={detail.entry ? handleIncrement : undefined}
                onSetWatched={detail.entry ? handleSetWatched : undefined}
                max={episodeCeiling(detail.episodesAired, detail.totalEpisodes)}
                incrementPending={incrementPending}
                incrementLabel={`Increment episodes watched for ${pickDisplayTitle(detail.title, detail.englishTitle)}`}
              />
            )}
            <span className="anime-detail-page__status">
              {detail.entry
                ? STATUS_LABELS[detail.entry.status]
                : "Not in my list"}
            </span>
          </div>

          <div className="anime-detail-page__actions">
            <div className="anime-detail-page__actions-row">
              {!hideAddToWatching && (
                <button
                  type="button"
                  className="anime-detail-page__action"
                  onClick={handleAddToWatching}
                  disabled={actionPending || refreshing}
                >
                  Add to watching
                </button>
              )}
              {detail.entry ? (
                <button
                  type="button"
                  className="anime-detail-page__action"
                  onClick={handleEdit}
                  disabled={actionPending || refreshing}
                >
                  Edit
                </button>
              ) : (
                <button
                  type="button"
                  className="anime-detail-page__action"
                  onClick={handleAddToList}
                  disabled={actionPending || refreshing}
                >
                  Add to list
                </button>
              )}
            </div>
            <button
              type="button"
              className="anime-detail-page__action"
              onClick={handleRefresh}
              disabled={refreshing || actionPending}
            >
              {refreshing ? "Refreshing…" : "Refresh data"}
            </button>
            {showPicturePickerButton && (
              <button
                type="button"
                className="anime-detail-page__action"
                onClick={() => {
                  // Seeds which groups start open the first time only, so
                  // reopening keeps what was last open (design D12).
                  seedOpenGroups(pickerSections, detail.pictureUrl);
                  setShowPicturePicker(true);
                }}
              >
                Choose picture
              </button>
            )}
          </div>
        </div>

        <div className="anime-detail-page__main">
          <div className="anime-detail-page__score-boxes">
            <section className="detail-box">
              <p>
                MAL score:{" "}
                <span className="score--mal">
                  <ScoreValue value={detail.malScore} completed={isScoreRevealableStatus(detail.entry?.status)} />
                </span>
              </p>
              <p>
                Rank: <RankValue rank={detail.rank} completed={isScoreRevealableStatus(detail.entry?.status)} />
              </p>
              <p>
                Popularity:{" "}
                {detail.popularityRank ? `#${detail.popularityRank}` : "—"}
              </p>
            </section>
            {detail.entry && detail.entry.myScore != null && (
              <section className="detail-box">
                <p>
                  My score: <span className="score--mine">{detail.entry.myScore}</span>
                </p>
                {detail.entry.rewatchCount !== 0 && (
                  <p>Rewatch count: {detail.entry.rewatchCount}</p>
                )}
                {/* Completed keeps its "No info" placeholder unchanged; Rewatching shows the
                    line only when a date is stored — a rewatch is a rewatch of a viewing that
                    was already finished, so the stored date is neither lost nor made wrong by it. */}
                {(detail.entry.status === "Completed" ||
                  (detail.entry.status === "Rewatching" && detail.entry.completedAt !== null)) && (
                  <p>Completed: {formatDate(detail.entry.completedAt)}</p>
                )}
              </section>
            )}
          </div>

          <section className="detail-box">
            <dl className="anime-detail-page__info-grid">
              <div>
                <dt>Type</dt>
                <dd>{detail.mediaType ? mediaTypeLabel(detail.mediaType) : NO_INFO}</dd>
              </div>
              <div>
                <dt>Status</dt>
                <dd>{formatAiringStatus(detail.airingStatus, detail.episodesAired, detail.totalEpisodes, detail.nextEpisode)}</dd>
              </div>
              <div>
                <dt>Source</dt>
                <dd>{formatSource(detail.source)}</dd>
              </div>
              <div>
                <dt>Duration</dt>
                <dd>
                  {formatDuration(
                    detail.averageEpisodeDurationSeconds,
                    detail.totalEpisodes,
                  )}
                </dd>
              </div>
              <div>
                <dt>Studio</dt>
                <dd>{detail.studio ?? NO_INFO}</dd>
              </div>
              <div>
                <dt>Total time</dt>
                <dd>
                  {formatTotalTime(
                    detail.averageEpisodeDurationSeconds,
                    detail.totalEpisodes,
                  )}
                </dd>
              </div>
              <div>
                <dt>Rating</dt>
                <dd>{formatRating(detail.rating)}</dd>
              </div>
              <div>
                <dt>Aired</dt>
                <dd>{formatAiredRange(detail.airedFrom, detail.airedTo)}</dd>
              </div>
              <div>
                <dt>Genres</dt>
                <dd>
                  {detail.genres && detail.genres.length > 0
                    ? detail.genres.join(", ")
                    : NO_INFO}
                </dd>
              </div>
              <div>
                <dt>Season</dt>
                <dd>
                  {formatSeason(detail.seasonYear, detail.season) &&
                  detail.season ? (
                    <Link
                      to={`/season?year=${detail.seasonYear}&season=${detail.season}`}
                    >
                      {formatSeason(detail.seasonYear, detail.season)}
                    </Link>
                  ) : (
                    NO_INFO
                  )}
                </dd>
              </div>
              <div className="anime-detail-page__links">
                <a
                  href={`https://myanimelist.net/anime/${detail.animeId}`}
                  target="_blank"
                  rel="noreferrer"
                  className="anime-detail-page__related-link"
                >
                  MyAnimeList
                </a>
                <a
                  href={
                    detail.aniListId != null
                      ? `https://anilist.co/anime/${detail.aniListId}`
                      : `https://anilist.co/search/anime?search=${encodeURIComponent(pickDisplayTitle(detail.title, detail.englishTitle))}`
                  }
                  target="_blank"
                  rel="noreferrer"
                  className="anime-detail-page__related-link"
                >
                  AniList
                </a>
                <a
                  href={`https://seriesgraph.com/show/search/${encodeURIComponent(pickDisplayTitle(detail.title, detail.englishTitle))}`}
                  target="_blank"
                  rel="noreferrer"
                  className="anime-detail-page__related-link"
                >
                  SeriesGraph
                </a>
                {imdbLinks(detail.imdbIds).map(({ href, label }) => (
                  <a
                    key={href}
                    href={href}
                    target="_blank"
                    rel="noreferrer"
                    className="anime-detail-page__related-link"
                  >
                    {label}
                  </a>
                ))}
              </div>
            </dl>
          </section>

          {(hasSynopsis || hasBackground) && (
            <section className="detail-box">
              {hasSynopsis && (
                <>
                  <h2>Synopsis</h2>
                  <p className="anime-detail-page__synopsis">{detail.synopsis}</p>
                </>
              )}
              {hasBackground && (
                <>
                  <h2>Background</h2>
                  <p className="anime-detail-page__synopsis">{detail.background}</p>
                </>
              )}
            </section>
          )}
        </div>
      </div>
    </div>
  );
}
