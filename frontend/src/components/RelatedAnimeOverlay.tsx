import { Link } from "react-router-dom";
import { Modal } from "./Modal.tsx";
import type { RelatedAnimeDto } from "../api/types.ts";
import "./RelatedAnimeOverlay.css";

type RelatedAnimeOverlayProps = {
  relations: RelatedAnimeDto[];
  loading?: boolean;
  onClose: () => void;
};

// Fixed display order for known relation types; anything else (an
// unrecognized MAL relation) is appended after, in first-seen order.
const GROUP_ORDER = [
  "side_story",
  "alternative_version",
  "summary",
  "spin_off",
  "character",
  "other",
];

// MAL sends raw relation values like "side_story" — prettify to "Side
// story", the same underscore-to-space-and-capitalize transform
// formatSource uses for `source` on the detail page.
function relationLabel(relationType: string): string {
  const spaced = relationType.replace(/_/g, " ");
  return spaced.charAt(0).toUpperCase() + spaced.slice(1);
}

// One-hop related-anime list, grouped by relation type — the overflow for
// every relation that isn't a dedicated prequel/sequel/main-series button.
export function RelatedAnimeOverlay({
  relations,
  loading,
  onClose,
}: RelatedAnimeOverlayProps) {
  const groups = new Map<string, RelatedAnimeDto[]>();
  for (const relation of relations) {
    const group = groups.get(relation.relationType);
    if (group) group.push(relation);
    else groups.set(relation.relationType, [relation]);
  }

  const orderedTypes = [
    ...GROUP_ORDER.filter((type) => groups.has(type)),
    ...[...groups.keys()].filter((type) => !GROUP_ORDER.includes(type)),
  ];

  return (
    <Modal
      onClose={onClose}
      labelledBy="related-anime-overlay-title"
      className="modal--wide"
    >
      <div className="related-anime-overlay">
        <h2
          id="related-anime-overlay-title"
          className="related-anime-overlay__title"
        >
          Related anime
        </h2>

        {loading && (
          <p className="related-anime-overlay__loading">
            Loading media types…
          </p>
        )}

        <div className="related-anime-overlay__body">
          {orderedTypes.map((type) => (
            <section key={type} className="related-anime-overlay__group">
              <h3 className="related-anime-overlay__group-title">
                {relationLabel(type)}
              </h3>
              <ul className="related-anime-overlay__list">
                {groups.get(type)!.map((relation) => (
                  <li key={`${type}-${relation.animeId}`}>
                    <Link
                      to={`/anime/${relation.animeId}`}
                      className="related-anime-overlay__row"
                      onClick={onClose}
                    >
                      {relation.pictureUrl ? (
                        <img
                          src={relation.pictureUrl}
                          alt=""
                          className="related-anime-overlay__thumb"
                        />
                      ) : (
                        <div
                          className="related-anime-overlay__thumb related-anime-overlay__thumb--placeholder"
                          aria-hidden="true"
                        />
                      )}
                      <span className="related-anime-overlay__row-text">
                        <span className="related-anime-overlay__row-title">
                          {relation.title}
                        </span>
                        <span className="related-anime-overlay__row-type">
                          {relation.mediaType
                            ? relation.mediaType.toUpperCase()
                            : "Unknown"}
                        </span>
                      </span>
                    </Link>
                  </li>
                ))}
              </ul>
            </section>
          ))}
        </div>

        <div className="related-anime-overlay__buttons">
          <button type="button" onClick={onClose}>
            Close
          </button>
        </div>
      </div>
    </Modal>
  );
}
