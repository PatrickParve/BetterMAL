import { Link } from 'react-router-dom'
import type { ScoreDistributionBucketDto } from '../api/types.ts'
import { scoreTier } from '../utils/anime.ts'
import './ScoreDistribution.css'

// `<1%` covers a bucket that has anime in it but rounds down to nothing —
// showing a flat 0% there would read as "no anime has this score", which is
// false. No share at all renders when nothing is rated, since a percentage
// of zero is a meaningless comparison.
function formatShare(count: number, totalRated: number): string | null {
  if (totalRated === 0) return null
  const rounded = Math.round((count / totalRated) * 100)
  if (count > 0 && rounded === 0) return '<1%'
  return `${rounded}%`
}

type ScoreDistributionProps = {
  buckets: ScoreDistributionBucketDto[]
  meanScore?: number | null
  scoredCount?: number
  compact?: boolean
  hrefForScore?: (score: number) => string
  tiered?: boolean
}

// Shared by the profile page (its whole-list distribution, with a mean line)
// and the recap page (its period distribution, without one — design.md
// decision 5) so the two can never draw the block differently. Buckets come
// in ascending score order (matching ProfileService.BuildScoreDistribution's
// shape); rendered highest first.
//
// `hrefForScore` (design.md decision 6) makes each row a link into my list
// scoped to that score, used only by the recap page — left unset, the
// profile page's block renders exactly as it did before this existed. A
// bucket with nothing in it has no anime to lead to, so it keeps the same
// accent highlight as the rows around it but renders as an unlinked `<div>`
// rather than a `<Link>`.
//
// `tiered` (design.md decision 5) colours each row's bar and label by the
// same scoreTier the score board reads, rather than the flat accent — used
// only by the recap page, so the profile page's whole-list distribution,
// which never sets it, renders exactly as it did before this existed.
export function ScoreDistribution({ buckets, meanScore, scoredCount, compact, hrefForScore, tiered }: ScoreDistributionProps) {
  const totalRated = buckets.reduce((sum, b) => sum + b.count, 0)
  const maxBucketCount = Math.max(0, ...buckets.map((b) => b.count))

  return (
    <>
      <div className={compact ? 'score-distribution score-distribution--compact' : 'score-distribution'}>
        {[...buckets].reverse().map((bucket) => {
          const share = formatShare(bucket.count, totalRated)
          const tierClass = tiered ? ` score-distribution__row--tiered score-distribution__row--tier-${scoreTier(bucket.score)}` : ''
          const cells = (
            <>
              <span className="score-distribution__label">{bucket.score}</span>
              <div className="score-distribution__bar-track">
                <div
                  className="score-distribution__bar"
                  style={{ width: `${maxBucketCount > 0 ? (bucket.count / maxBucketCount) * 100 : 0}%` }}
                />
              </div>
              <span className="score-distribution__count">{bucket.count}</span>
              <span className="score-distribution__share">{share ?? ''}</span>
            </>
          )
          return hrefForScore && bucket.count > 0 ? (
            <Link
              key={bucket.score}
              to={hrefForScore(bucket.score)}
              className={`score-distribution__row score-distribution__row--link${tierClass}`}
            >
              {cells}
            </Link>
          ) : (
            <div
              key={bucket.score}
              className={`${hrefForScore ? 'score-distribution__row score-distribution__row--link' : 'score-distribution__row'}${tierClass}`}
            >
              {cells}
            </div>
          )
        })}
      </div>
      {(scoredCount !== undefined || meanScore !== undefined) && (
        <div className="score-distribution__summary">
          {scoredCount !== undefined && <p className="score-distribution__mean">Scored: {scoredCount}</p>}
          {meanScore !== undefined && (
            <p className="score-distribution__mean">Mean score: {meanScore?.toFixed(2) ?? '—'}</p>
          )}
        </div>
      )}
    </>
  )
}
