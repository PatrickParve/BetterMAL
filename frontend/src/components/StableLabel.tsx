import './StableLabel.css'

type StableLabelProps = {
  current: string
  candidates: string[]
}

// Reserves the widest candidate's width so a control's label never resizes
// as its value changes (design D5 of redo-my-list-filtering-and-sorting):
// every candidate renders in one grid cell, the current one visible, the
// rest hidden with visibility: hidden + aria-hidden so they take no
// accessible-tree space but still occupy layout, sizing the cell to the
// widest.
export function StableLabel({ current, candidates }: StableLabelProps) {
  return (
    <span className="stable-label">
      {candidates.map((candidate) => (
        <span
          key={candidate}
          className="stable-label__candidate"
          aria-hidden={candidate === current ? undefined : true}
        >
          {candidate}
        </span>
      ))}
    </span>
  )
}
