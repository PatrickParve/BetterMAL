import type { ScoreDistributionBucketDto } from '../api/types.ts'
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
  compact?: boolean
}

// Shared by the profile page (its whole-list distribution, with a mean line)
// and the recap page (its period distribution, without one — design.md
// decision 5) so the two can never draw the block differently. Buckets come
// in ascending score order (matching ProfileService.BuildScoreDistribution's
// shape); rendered highest first.
export function ScoreDistribution({ buckets, meanScore, compact }: ScoreDistributionProps) {
  const totalRated = buckets.reduce((sum, b) => sum + b.count, 0)
  const maxBucketCount = Math.max(0, ...buckets.map((b) => b.count))

  return (
    <>
      <div className={compact ? 'score-distribution score-distribution--compact' : 'score-distribution'}>
        {[...buckets].reverse().map((bucket) => {
          const share = formatShare(bucket.count, totalRated)
          return (
            <div key={bucket.score} className="score-distribution__row">
              <span className="score-distribution__label">{bucket.score}</span>
              <div className="score-distribution__bar-track">
                <div
                  className="score-distribution__bar"
                  style={{ width: `${maxBucketCount > 0 ? (bucket.count / maxBucketCount) * 100 : 0}%` }}
                />
              </div>
              <span className="score-distribution__count">{bucket.count}</span>
              <span className="score-distribution__share">{share ?? ''}</span>
            </div>
          )
        })}
      </div>
      {meanScore !== undefined && (
        <p className="score-distribution__mean">Mean score: {meanScore?.toFixed(2) ?? '—'}</p>
      )}
    </>
  )
}
