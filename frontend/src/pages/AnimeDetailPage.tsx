import { useEffect, useRef, useState } from "react";
import { Link, useParams } from "react-router-dom";
import {
  ApiError,
  getAnimeDetail,
  refreshAnime,
  refreshAnimePictures,
  setAnimePicture,
  updateEntry,
} from "../api/client.ts";
import type {
  AnimeDetailDto,
  IncrementTarget,
  NextEpisodeEtaDto,
} from "../api/types.ts";
import { PicturePickerOverlay } from "../components/PicturePickerOverlay.tsx";
import { ProgressBar } from "../components/ProgressBar.tsx";
import { RelatedAnimeOverlay } from "../components/RelatedAnimeOverlay.tsx";
import { ScoreValue } from "../components/ScoreValue.tsx";
import { useEntryEditor } from "../context/EntryEditorContext.tsx";
import {
  useEpisodeIncrement,
  useSetEpisodesWatched,
} from "../context/CompletionPromptContext.tsx";
import { useActionFailure } from "../context/ActionFailureContext.tsx";
import { useLandscapePicture } from "../hooks/useLandscapePicture.ts";
import { usePageData } from "../hooks/usePageData.ts";
import {
  dedupePictureOptions,
  formatRuntime,
  hasAiredEpisodes,
  isScoreRevealableStatus,
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
// (design D9) even before pictureUrls itself has ever loaded.
function animePictureOptions(detail: AnimeDetailDto): string[] {
  return dedupePictureOptions(detail.pictureUrls ?? [], [detail.malPictureUrl, detail.pictureUrl]);
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
      .then(({ pictureUrl, pictureUrls }) => {
        setDetail((prev) =>
          prev && prev.animeId === detail.animeId ? { ...prev, pictureUrl, pictureUrls } : prev,
        );
      })
      .catch(() => {
        // Leave picturesFetchPending as the server last reported it — a
        // later visit's read re-evaluates and retries.
      });
  }, [detail, setDetail]);

  async function handleRefresh() {
    if (refreshing) return;
    setRefreshing(true);
    try {
      await refreshAnime(animeId);
      await reload();
    } catch {
      // Leave the page showing whatever was already cached.
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
    setDetail((prev) => (prev ? { ...prev, pictureUrl: url } : prev));
    setAnimePicture(detail.animeId, url).catch(() => {
      // The picker already closed; a later refresh/reload re-syncs if the
      // save failed server-side.
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
      // patch it in here instead of re-reading the anime. null means the
      // user skipped without scoring — onSaved above already has the latest.
      onCompleted: (saved) => {
        if (saved) setDetail((prev) => (prev ? { ...prev, entry: saved } : prev));
      },
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
  const pictureOptions = animePictureOptions(detail);
  const showPicturePickerButton = detail.entry != null && pictureOptions.length > 1;

  return (
    <div className="anime-detail-page">
      <div className="anime-detail-page__header">
        <div className="anime-detail-page__top">
          <h1>{pickDisplayTitle(detail.title, detail.englishTitle)}</h1>

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
                title={parentStory.title}
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
                title={prequel.title}
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
                title={sequel.title}
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
          options={pictureOptions}
          current={detail.pictureUrl}
          onPick={handlePickAnimePicture}
          onClose={() => setShowPicturePicker(false)}
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
                max={detail.episodesAired ?? detail.totalEpisodes}
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
                onClick={() => setShowPicturePicker(true)}
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
              <p>Rank: {detail.rank ? `#${detail.rank}` : "—"}</p>
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
                {detail.entry.status === "Completed" && (
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
              </div>
            </dl>
          </section>

          <section className="detail-box">
            <h2>Synopsis</h2>
            <p className="anime-detail-page__synopsis">
              {detail.synopsis ?? "No synopsis available."}
            </p>
            {detail.background && (
              <>
                <h2>Background</h2>
                <p className="anime-detail-page__synopsis">
                  {detail.background}
                </p>
              </>
            )}
          </section>
        </div>
      </div>
    </div>
  );
}
