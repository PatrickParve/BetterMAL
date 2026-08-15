import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import { getAnimeDetail, refreshAnime, updateEntry } from "../api/client.ts";
import type {
  AnimeDetailDto,
  IncrementTarget,
  NextEpisodeEtaDto,
} from "../api/types.ts";
import { ProgressBar } from "../components/ProgressBar.tsx";
import { RelatedAnimeOverlay } from "../components/RelatedAnimeOverlay.tsx";
import { ScoreValue } from "../components/ScoreValue.tsx";
import { useEntryEditor } from "../context/EntryEditorContext.tsx";
import {
  useEpisodeIncrement,
  useSetEpisodesWatched,
} from "../context/CompletionPromptContext.tsx";
import { usePageData } from "../hooks/usePageData.ts";
import {
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

// Single anime detail page: large picture + progress/edit on the left, a
// rank/score box (plus a my-score/rewatches box only once a score's been
// given), an info box, and a synopsis/background box on the right. The
// external links are plain URL templates (no API call); prequel/sequel/
// main-series buttons only render when those relations exist on the cached
// record, and a More button opens the overlay for everything else.
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
  const [incrementPending, setIncrementPending] = useState(false);
  const [actionPending, setActionPending] = useState(false);
  const [showRelatedOverlay, setShowRelatedOverlay] = useState(false);
  const { openEditor } = useEntryEditor();
  const increment = useEpisodeIncrement();
  const setEpisodesWatched = useSetEpisodesWatched();

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

  async function handleAddToWatching() {
    if (!detail || actionPending) return;
    setActionPending(true);
    try {
      const saved = await updateEntry(detail.animeId, { status: "Watching" });
      setDetail((prev) => (prev ? { ...prev, entry: saved } : prev));
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
    } finally {
      setActionPending(false);
    }
  }

  function handleOpenRelatedOverlay() {
    setShowRelatedOverlay(true);
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

  // Only the first prequel/sequel/parent-story MAL reports gets a dedicated
  // button (by reference, not by relation type — so a second prequel still
  // ends up in `moreRelations` rather than being excluded outright).
  const prequel =
    detail.relatedAnime.find((r) => r.relationType === "prequel") ?? null;
  const sequel =
    detail.relatedAnime.find((r) => r.relationType === "sequel") ?? null;
  const parentStory =
    detail.relatedAnime.find((r) => r.relationType === "parent_story") ??
    null;
  const moreRelations = detail.relatedAnime.filter(
    (r) => r !== prequel && r !== sequel && r !== parentStory,
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
    detail.entry?.status === "Dropped";

  return (
    <div className="anime-detail-page">
      <div className="anime-detail-page__top">
        <div>
          <h1>{pickDisplayTitle(detail.title, detail.englishTitle)}</h1>
        </div>

        {(hasSeriesRelation || prequel || sequel || parentStory || moreRelations.length > 0) && (
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
            {prequel && (
              <Link
                to={`/anime/${prequel.animeId}`}
                className="anime-detail-page__related-link"
                title={prequel.title}
              >
                ← Prequel
              </Link>
            )}
            {sequel && (
              <Link
                to={`/anime/${sequel.animeId}`}
                className="anime-detail-page__related-link"
                title={sequel.title}
              >
                Sequel →
              </Link>
            )}
          </div>
        )}
      </div>

      {showRelatedOverlay && (
        <RelatedAnimeOverlay
          relations={moreRelations}
          onClose={() => setShowRelatedOverlay(false)}
        />
      )}

      <div className="anime-detail-page__body">
        <div className="anime-detail-page__picture-col">
          {detail.pictureUrl ? (
            <img
              src={detail.pictureUrl}
              alt=""
              className="anime-detail-page__picture"
            />
          ) : (
            <div
              className="anime-detail-page__picture anime-detail-page__picture--placeholder"
              aria-hidden="true"
            />
          )}

          <div className="anime-detail-page__progress-row">
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
          </div>
        </div>

        <div className="anime-detail-page__main">
          <div className="anime-detail-page__score-boxes">
            <section className="detail-box">
              <p>
                MAL score:{" "}
                <span className="score--mal">
                  <ScoreValue value={detail.malScore} completed={detail.entry?.status === 'Completed'} />
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
                <dd>
                  {detail.mediaType ? detail.mediaType.toUpperCase() : NO_INFO}
                </dd>
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
                <dt>Aired</dt>
                <dd>{formatAiredRange(detail.airedFrom, detail.airedTo)}</dd>
              </div>
              <div>
                <dt>Rating</dt>
                <dd>{formatRating(detail.rating)}</dd>
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
              <div>
                <dt>Genres</dt>
                <dd>
                  {detail.genres && detail.genres.length > 0
                    ? detail.genres.join(", ")
                    : NO_INFO}
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
