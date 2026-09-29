import type { ConnectError, SetupStatusDto } from '../../api/types.ts'
import { SetupIssues } from './SetupIssues.tsx'
import { SetupSteps } from './SetupStepRow.tsx'
import { CONNECT_ERROR_MESSAGES, type SetupScreenKind } from './setupFormat.ts'
import './SetupScreen.css'

// What every URL shows until setup has finished (first-run-setup "Nothing else is
// reachable until setup has finished"), in one of three states: the credentials check,
// the connect screen, or the progress of setup's four steps. App.tsx mounts it instead
// of the shell, so no page, navbar or overlay exists while it shows.
//
// `unreachable` is a failed read of the status made while this screen was already
// showing: the screen keeps what it last read and says it is retrying (connection-status
// "First-load failure keeps its full-page message"). `onRefresh` re-reads the status,
// which Retry now calls once it went through.
export function SetupScreen({
  kind,
  status,
  connectError,
  unreachable,
  onRefresh,
}: {
  kind: SetupScreenKind
  status: SetupStatusDto
  connectError: ConnectError | null
  unreachable: boolean
  onRefresh: () => void
}) {
  return (
    <section className="setup-screen">
      {kind === 'credentials' && (
        <div className="setup-card">
          <h1 className="setup-card__title">MyAnimeList credentials are missing</h1>
          <p className="setup-card__lead">
            {status.missingCredentials.length === 1 ? 'This value is' : 'These values are'} not set. Set{' '}
            {status.missingCredentials.length === 1 ? 'it' : 'them'} in your <code>.env</code> file, then restart the
            app. Setup can't start without both the client id and the client secret of your MyAnimeList API app.
          </p>
          <ul className="setup-card__missing">
            {status.missingCredentials.map((name) => (
              <li key={name}>
                <code>{name}</code>
              </li>
            ))}
          </ul>
        </div>
      )}

      {kind === 'connect' && (
        <div className="setup-card setup-card--centered">
          <h1 className="setup-card__title">Connect your MyAnimeList account</h1>
          <p className="setup-card__lead">
            This app mirrors your MyAnimeList list locally and keeps it in sync. Connecting starts a one-time setup: it
            reads your list, fetches every anime's details, builds your series and looks up airing dates. It runs in
            the background, so you can close this tab while it works.
          </p>
          {connectError && (
            <p className="setup-card__error" role="alert">
              {CONNECT_ERROR_MESSAGES[connectError]} Press Connect to try again.
            </p>
          )}
          <a className="setup-card__button" href="/api/mal-auth/start">
            Connect to MyAnimeList
          </a>
        </div>
      )}

      {kind === 'progress' && (
        <div className="setup-card">
          <h1 className="setup-card__title">Setting up your library</h1>
          <p className="setup-card__lead">
            This runs once, in the background, so you can close this tab and come back. Home opens when your list, every
            anime's details and every series are in.
          </p>
          {unreachable && (
            <p className="setup-card__unreachable" role="status">
              Can't reach the backend — retrying. This screen carries on by itself once it answers.
            </p>
          )}
          <SetupSteps status={status} />
          <SetupIssues status={status} connectError={connectError} onRetried={onRefresh} />
        </div>
      )}
    </section>
  )
}
