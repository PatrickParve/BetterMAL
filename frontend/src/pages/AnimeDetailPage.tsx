import { useCallback, useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { getAnimeDetail, refreshAnime } from "../api/client.ts";
import type { AnimeDetailDto, WatchStatus } from "../api/types.ts";
import { ProgressBar } from "../components/ProgressBar.tsx";
import { ScoreValue } from "../components/ScoreValue.tsx";
import { useEntryEditor } from "../context/EntryEditorContext.tsx";
import { pickDisplayTitle } from "../utils/anime.ts";
import "./AnimeDetailPage.css";

const STATUS_LABELS: Record<WatchStatus, string> = {
  Watching: "Watching",
  OnHold: "On hold",
  PlanToWatch: "Plan to watch",
  Completed: "Completed",
  Dropped: "Dropped",
};

const AIRING_STATUS_LABELS: Record<string, string> = {
  currently_airing: "Currently airing",
  finished_airing: "Finished airing",
  not_yet_aired: "Not yet aired",
};

const NO_INFO = "No info";

function formatDate(value: string | null): string {
  if (!value) return NO_INFO;
  return new Date(value).toLocaleDateString(undefined, {
    year: "numeric",
    month: "short",
    day: "numeric",
  });
}

function formatAiringStatus(status: string | null): string {
  if (!status) return NO_INFO;
  return AIRING_STATUS_LABELS[status] ?? status;
}

// MAL sends raw source values like "light_novel" — prettify to "Light novel".
function formatSource(source: string | null): string {
  if (!source) return NO_INFO;
  const spaced = source.replace(/_/g, " ");
  return spaced.charAt(0).toUpperCase() + spaced.slice(1);
}

function formatDuration(seconds: number | null): string {
  if (!seconds) return NO_INFO;
  return `${Math.round(seconds / 60)} min`;
}

// Single anime detail page: large picture + progress/edit on the left, a
// "rank & MAL score" box and a "my score & rewatches" box side by side, an
// info box, and a synopsis/background box on the right. The external
// MyAnimeList link is a plain URL template from the id (no API call);
// prequel/sequel buttons only render when those relations exist on the
// cached record.
export function AnimeDetailPage() {
  const { id } = useParams();
  const animeId = Number(id);
  const [detail, setDetail] = useState<AnimeDetailDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const { openEditor } = useEntryEditor();

  const load = useCallback(() => {
    return getAnimeDetail(animeId)
      .then(setDetail)
      .catch(() => setDetail(null));
  }, [animeId]);

  useEffect(() => {
    setLoading(true);
    load().finally(() => setLoading(false));
  }, [load]);

  async function handleRefresh() {
    if (refreshing) return;
    setRefreshing(true);
    try {
      await refreshAnime(animeId);
      await load();
    } catch {
      // Leave the page showing whatever was already cached.
    } finally {
      setRefreshing(false);
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

  return (
    <div className="anime-detail-page">
      <div className="anime-detail-page__top">
        <div>
          <h1>{pickDisplayTitle(detail.title, detail.englishTitle)}</h1>
        </div>

        {(detail.prequelMalId || detail.sequelMalId) && (
          <div className="anime-detail-page__related">
            {detail.prequelMalId && (
              <Link
                to={`/anime/${detail.prequelMalId}`}
                className="anime-detail-page__related-link"
              >
                ← Prequel: {detail.prequelTitle}
              </Link>
            )}
            {detail.sequelMalId && (
              <Link
                to={`/anime/${detail.sequelMalId}`}
                className="anime-detail-page__related-link"
              >
                Sequel: {detail.sequelTitle} →
              </Link>
            )}
          </div>
        )}
      </div>

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
            />
            <span className="anime-detail-page__status">
              {detail.entry
                ? STATUS_LABELS[detail.entry.status]
                : "Not in my list"}
            </span>
            <button
              type="button"
              className="anime-detail-page__edit"
              onClick={handleEdit}
            >
              Edit
            </button>
          </div>

          <button
            type="button"
            className="anime-detail-page__refresh"
            onClick={handleRefresh}
            disabled={refreshing}
          >
            {refreshing ? "Refreshing…" : "Refresh data"}
          </button>
        </div>

        <div className="anime-detail-page__main">
          <div className="anime-detail-page__score-boxes">
            <section className="detail-box">
              <h2>Rank &amp; MAL score</h2>
              <p>
                Rank:{" "}
                {detail.popularityRank ? `#${detail.popularityRank}` : "—"}
              </p>
              <p>
                MAL score: <ScoreValue value={detail.malScore} />
              </p>
            </section>
            <section className="detail-box">
              <h2>My score &amp; rewatches</h2>
              <p>My score: {detail.entry?.myScore ?? "—"}</p>
              {detail.entry?.rewatchCount !== 0 && (
                <p>Rewatch count: {detail.entry?.rewatchCount ?? 0}</p>
              )}
            </section>
          </div>

          <section className="detail-box">
            <h2>Info</h2>
            <dl className="anime-detail-page__info-grid">
              <div>
                <dt>Type</dt>
                <dd>
                  {detail.mediaType ? detail.mediaType.toUpperCase() : NO_INFO}
                </dd>
              </div>
              <div>
                <dt>Status</dt>
                <dd>{formatAiringStatus(detail.airingStatus)}</dd>
              </div>
              <div>
                <dt>Source</dt>
                <dd>{formatSource(detail.source)}</dd>
              </div>
              <div>
                <dt>Duration</dt>
                <dd>{formatDuration(detail.averageEpisodeDurationSeconds)}</dd>
              </div>
              <div>
                <dt>Studio</dt>
                <dd>{detail.studio ?? NO_INFO}</dd>
              </div>
              <div>
                <dt>Aired</dt>
                <dd>
                  {formatDate(detail.airedFrom)} – {formatDate(detail.airedTo)}
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
              <div className="anime-detail-page__external-links">
                <a
                  href={`https://myanimelist.net/anime/${detail.animeId}`}
                  target="_blank"
                  rel="noreferrer"
                >
                  MyAnimeList
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
