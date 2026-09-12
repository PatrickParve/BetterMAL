import { NavLink } from 'react-router-dom'
import { oldestRunningJob, settingsNeedsAttention, useAppStatus } from '../../context/AppStatusContext.tsx'
import { JobProgressTrack } from '../JobProgressTrack.tsx'

function settingsClassName({ isActive }: { isActive: boolean }) {
  return isActive ? 'navbar__settings navbar__settings--active' : 'navbar__settings'
}

// Composes the gear's accessible name from both facts it carries
// (navbar-settings-status-indicator design.md D11) — states *whether*, never
// *how much*, so a focused gear isn't re-announced every second while the
// bar advances underneath it.
function accessibleName(needsAttention: boolean, jobRunning: boolean): string {
  if (needsAttention && jobRunning) return 'Settings, needs attention, a job is running'
  if (needsAttention) return 'Settings, needs attention'
  if (jobRunning) return 'Settings, a job is running'
  return 'Settings'
}

// The navbar's Settings control (navigation-and-search "Navbar layout";
// navbar-settings-status-indicator design D11) — was an inline NavLink in
// Navbar.tsx, now also carrying the shared "needs attention" dot and a
// decorative progress sliver along its bottom edge, both read from the one
// app-wide status poll (AppStatusContext) so every page and every browser on
// this device agree. Both are aria-hidden; the facts they carry ride on this
// link's own accessible name instead, which is otherwise "Settings" as
// before.
export function SettingsLink() {
  const { status } = useAppStatus()
  const needsAttention = settingsNeedsAttention(status)
  const runningJob = oldestRunningJob(status)

  return (
    <NavLink
      to="/settings"
      className={settingsClassName}
      aria-label={accessibleName(needsAttention, runningJob !== null)}
    >
      <GearIcon />
      {needsAttention && <span className="navbar__status-dot" aria-hidden="true" />}
      {runningJob && (
        <JobProgressTrack done={runningJob.done} total={runningJob.total} className="navbar__settings-progress" />
      )}
    </NavLink>
  )
}

function GearIcon() {
  return (
    <svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true">
      <circle cx="12" cy="12" r="3.2" />
      <circle cx="12" cy="12" r="7.5" strokeDasharray="2.4 2.6" />
    </svg>
  )
}
