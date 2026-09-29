import type { SetupStatusDto, SetupStepDto, SetupStepPhase } from '../../api/types.ts'
import { JobProgressTrack } from '../JobProgressTrack.tsx'
import { SETUP_STEPS, formatEta, type SetupStepKey } from './setupFormat.ts'
import './SetupStepRow.css'

// What a step that has not started says it is waiting for (first-run-setup "A step that
// is waiting on another SHALL say what it waits for").
const WAITING_NOTES: Record<SetupStepKey, (status: SetupStatusDto) => string> = {
  list: (status) => (status.waitingForReconnect ? 'Waiting for you to reconnect.' : 'Starting…'),
  details: () => 'Starts once your list has been read.',
  series: () => 'Starts after the anime details are fetched.',
  airing: () => 'Starts once your list has been read.',
}

// On a finished install a step that is not done is not waiting for anything setup does:
// its leftovers are picked up by the app's ordinary jobs. (Only Settings' Library data
// entry shows a finished install.)
const IDLE_NOTES: Record<SetupStepKey, string> = {
  list: '',
  details: 'Anime added since are filled in by the scheduled refresh.',
  series: 'Anime added since are built as they come up, or with Build all series.',
  airing: 'The rest is picked up by the hourly airing check.',
}

const PHASE_LABELS: Record<SetupStepPhase, string> = {
  Waiting: 'Waiting',
  Running: 'Running',
  Paused: 'Paused',
  Done: 'Done',
}

function phaseLabel(step: SetupStepDto, finished: boolean): string {
  return step.phase === 'Waiting' && finished ? 'Idle' : PHASE_LABELS[step.phase]
}

function countsText(step: SetupStepDto, noun: string): string {
  if (step.total === null) return step.done > 0 ? `${step.done} ${noun} so far` : ''
  // A step waiting for its turn has nothing to count yet, and "0 of 0" reads as broken.
  if (step.total === 0 && step.phase !== 'Done') return ''
  return `${step.done} of ${step.total} ${noun}`
}

function stepNote(key: SetupStepKey, step: SetupStepDto, status: SetupStatusDto): string | null {
  if (step.phase === 'Paused') {
    if (key === 'list' && status.waitingForReconnect) return 'Waiting for you to reconnect.'
    return `Paused while ${key === 'airing' ? 'AniList' : 'MyAnimeList'} is down.`
  }
  if (step.phase === 'Waiting') return status.finished ? IDLE_NOTES[key] || null : WAITING_NOTES[key](status)
  return null
}

// The airing step is the only one with a part that opens Home before the rest is done,
// so it is the only one that says where that part stands (first-run-setup "The screen
// SHALL show separately when the priority set is done"). Meaningless once setup has
// finished.
function priorityNote(status: SetupStatusDto): string | null {
  const { done, total } = status.airingPriority
  if (status.finished || total === 0) return null
  return done >= total
    ? 'Shows airing now and from recent seasons are in. The rest carries on after Home opens.'
    : `Shows airing now and from recent seasons: ${done} of ${total}.`
}

// One of setup's four steps: its name and state, a bar in the shared job presentation
// (proportional, or moving while the total is unknown), its counts with the time left
// while there is an estimate, and one line for what it waits for. Used by the setup
// screen and by Settings' Library data entry, so the two read the same.
export function SetupStepRow({ stepKey, status }: { stepKey: SetupStepKey; status: SetupStatusDto }) {
  const meta = SETUP_STEPS.find((s) => s.key === stepKey)!
  const step = status.steps[stepKey]

  const counts = countsText(step, meta.noun)
  const eta = step.phase === 'Running' && step.etaSeconds !== null ? formatEta(step.etaSeconds) : null
  const summary = [counts, eta].filter(Boolean).join(' · ')
  const note = stepNote(stepKey, step, status)
  const priority = stepKey === 'airing' ? priorityNote(status) : null

  // JobProgressTrack moves continuously only for a null total, which has to mean "running
  // and not yet knowing it": any other phase passes a number, so a paused or waiting
  // step is a still bar, and a finished one is full even when it had nothing to do.
  let trackDone = step.done
  let trackTotal = step.phase === 'Running' ? step.total : (step.total ?? 0)
  if (step.phase === 'Done' && !trackTotal) {
    trackDone = 1
    trackTotal = 1
  }

  return (
    <li className={`setup-step setup-step--${step.phase.toLowerCase()}`}>
      <div className="setup-step__head">
        <span className="setup-step__title">{meta.title}</span>
        <span className="setup-step__state">{phaseLabel(step, status.finished)}</span>
      </div>
      <JobProgressTrack
        done={trackDone}
        total={trackTotal}
        valueText={`${meta.title}: ${phaseLabel(step, status.finished)}${summary ? `, ${summary}` : ''}`}
      />
      {summary && <p className="setup-step__counts">{summary}</p>}
      {note && <p className="setup-step__note">{note}</p>}
      {priority && <p className="setup-step__note">{priority}</p>}
    </li>
  )
}

// The four rows in order.
export function SetupSteps({ status }: { status: SetupStatusDto }) {
  return (
    <ol className="setup-steps">
      {SETUP_STEPS.map((s) => (
        <SetupStepRow key={s.key} stepKey={s.key} status={status} />
      ))}
    </ol>
  )
}
