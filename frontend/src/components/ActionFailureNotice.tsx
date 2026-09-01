import { useActionFailures } from '../context/ActionFailureContext.tsx'
import './ActionFailureNotice.css'

// Mounted once in AppShell, outside <Routes>, beside ConnectionStatusNotice —
// reports an action that did not take effect, wherever it was taken
// (action-failure-notices capability). role="status" rather than "alert"
// since it must not interrupt what the user is doing.
export function ActionFailureNotice() {
  const { failures, dismissFailure } = useActionFailures()

  if (failures.length === 0) return null

  return (
    <div className="action-failure-notice-stack">
      {failures.map((failure) => (
        <div key={failure.id} className="action-failure-notice" role="status">
          <div className="action-failure-notice__text">
            <span className="action-failure-notice__title">{failure.title}</span>
            {failure.reason && <span className="action-failure-notice__reason">{failure.reason}</span>}
          </div>
          <button
            type="button"
            className="action-failure-notice__dismiss"
            onClick={() => dismissFailure(failure.id)}
            aria-label="Dismiss"
          >
            ×
          </button>
        </div>
      ))}
    </div>
  )
}
