import './ScoreTabs.css'

export type ScoreTab = {
  score: number
  count: number
  /** A ranking of this score is in progress: a small dot after the count, with its meaning for a screen reader. */
  marked?: boolean
}

type ScoreTabsProps = {
  tabs: ScoreTab[]
  selected: number | null
  onSelect: (score: number) => void
}

// The row of score tabs the ranking pop-up and the This or That page share
// (add-this-or-that-ranking design.md D11), taken out of AnimeRankOverlay with
// its markup and values unchanged so the two can't drift apart. A tab names the
// score and how many anime it holds. The page marks a score whose ranking is in
// progress; the pop-up marks none.
export function ScoreTabs({ tabs, selected, onSelect }: ScoreTabsProps) {
  return (
    <div className="score-tabs" role="tablist" aria-label="Score">
      {tabs.map((tab) => (
        <button
          key={tab.score}
          type="button"
          role="tab"
          aria-selected={selected === tab.score}
          className={selected === tab.score ? 'score-tab score-tab--active' : 'score-tab'}
          onClick={() => onSelect(tab.score)}
        >
          {tab.score} <span className="score-tab__count">({tab.count})</span>
          {tab.marked && (
            <>
              <span className="score-tab__dot" aria-hidden="true" />
              <span className="visually-hidden"> in progress</span>
            </>
          )}
        </button>
      ))}
    </div>
  )
}
