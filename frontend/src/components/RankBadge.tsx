import './RankBadge.css'

// The band a rank falls in (add-my-rank-badges, rank-badges "A rank badge is
// coloured by its rank"). Fixed rank numbers, the same for every list a badge
// ranks within: they never scale with how many anime or series are ranked.
//   1 gold · 2 silver · 3 bronze        — the Top anime top three / recap podium
//   4-10 apex (purple) · 11-25 red · 26-50 blue — the recap score board's 10, 9, 8
//   51 and below plain
type RankBand = 'gold' | 'silver' | 'bronze' | 'apex' | 'red' | 'blue' | 'plain'

function rankBand(rank: number): RankBand {
  if (rank <= 1) return 'gold'
  if (rank === 2) return 'silver'
  if (rank === 3) return 'bronze'
  if (rank <= 10) return 'apex'
  if (rank <= 25) return 'red'
  if (rank <= 50) return 'blue'
  return 'plain'
}

// 'tile' stands beside a block of boxes (the anime detail page's rank tile): a
// label, the number, and "of M". 'inline' is a pill holding only the number,
// sized to sit on a heading's line (the series page's title badge).
type RankBadgeForm = 'tile' | 'inline'

// What the badge ranks, named in its accessible name: "My rank 12 of 501 anime".
type RankBadgeSubject = 'anime' | 'series'

// My own rank, drawn from the shared medal and tier colour definitions through
// local aliases (RankBadge.css). Not a control: not focusable, takes no click,
// shows no hover state — and, since it is mine and never MyAnimeList's, never
// hidden by the hide-scores toggle. role="img" gives a screen reader one
// sentence, so the visible text is aria-hidden and "#12" is never read bare.
export function RankBadge({
  rank,
  rankedCount,
  subject,
  form,
}: {
  rank: number
  rankedCount: number
  subject: RankBadgeSubject
  form: RankBadgeForm
}) {
  const label = `My rank ${rank} of ${rankedCount} ${subject}`

  return (
    <span
      className={`rank-badge rank-badge--${form} rank-badge--${rankBand(rank)}`}
      role="img"
      aria-label={label}
      // The tooltip belongs to the inline pill, which has no words beside it
      // to say what the number is; the tile already says "My rank" and "of M".
      title={form === 'inline' ? label : undefined}
    >
      {form === 'tile' && (
        <span className="rank-badge__label" aria-hidden="true">
          My rank
        </span>
      )}
      <span className="rank-badge__number" aria-hidden="true">
        #{rank}
      </span>
      {form === 'tile' && (
        <span className="rank-badge__total" aria-hidden="true">
          of {rankedCount}
        </span>
      )}
    </span>
  )
}
