import { NavLink } from 'react-router-dom'
import { SearchBar } from '../SearchBar.tsx'
import { useScoreVisibility } from '../../context/ScoreVisibilityContext.tsx'
import './Navbar.css'

const NAV_LINKS_BEFORE_RECAP: { to: string; label: string; end?: boolean }[] = [
  { to: '/', label: 'Home', end: true },
  { to: '/my-list', label: 'My List' },
]

const NAV_LINKS_BETWEEN_RECAP_AND_SEASON: { to: string; label: string; end?: boolean }[] = [
  { to: '/top', label: 'Top' },
  { to: '/season', label: 'Season' },
]

const NAV_LINKS_AFTER_YEAR: { to: string; label: string; end?: boolean }[] = [{ to: '/airing', label: 'Airing' }]

function linkClassName({ isActive }: { isActive: boolean }) {
  return isActive ? 'navbar__link navbar__link--active' : 'navbar__link'
}

function settingsClassName({ isActive }: { isActive: boolean }) {
  return isActive ? 'navbar__settings navbar__settings--active' : 'navbar__settings'
}

export function Navbar() {
  const { hidden, toggle } = useScoreVisibility()
  // Built at render, not hoisted into NAV_LINKS, so a long-lived tab open
  // across midnight still targets the current year (design.md decision 1).
  // NavLink's active match compares pathname only, so this is marked active
  // on /recap whatever period is actually showing.
  const recapLink = `/recap?mode=yearly&year=${new Date().getFullYear()}&filter=aired`
  // Same reasoning as recapLink above — built at render so the Year link
  // always opens the current year, even in a tab left open across New Year.
  const yearLink = `/year?year=${new Date().getFullYear()}`

  return (
    <header className="navbar">
      <nav className="navbar__links navbar__links--left" aria-label="Primary">
        {NAV_LINKS_BEFORE_RECAP.map((link) => (
          <NavLink key={link.to} to={link.to} end={link.end} className={linkClassName}>
            {link.label}
          </NavLink>
        ))}
        <NavLink to={recapLink} className={linkClassName}>
          Recap
        </NavLink>
        {NAV_LINKS_BETWEEN_RECAP_AND_SEASON.map((link) => (
          <NavLink key={link.to} to={link.to} end={link.end} className={linkClassName}>
            {link.label}
          </NavLink>
        ))}
        <NavLink to={yearLink} className={linkClassName}>
          Year
        </NavLink>
        {NAV_LINKS_AFTER_YEAR.map((link) => (
          <NavLink key={link.to} to={link.to} end={link.end} className={linkClassName}>
            {link.label}
          </NavLink>
        ))}
      </nav>

      <div className="navbar__links navbar__links--right">
        <SearchBar />
        {/* Grouped so these three wrap as one unit at narrow widths — only
            the search field gets its own row (Navbar.css). */}
        <div className="navbar__controls">
          <button
            type="button"
            className={`navbar__score-switch${hidden ? '' : ' navbar__score-switch--checked'}`}
            role="switch"
            aria-checked={!hidden}
            aria-label={hidden ? 'Show scores' : 'Hide scores'}
            onClick={toggle}
          >
            <span className="navbar__score-switch-track" aria-hidden="true">
              Scores
            </span>
            <span className="navbar__score-switch-knob" aria-hidden="true">
              <EyeIcon />
            </span>
          </button>
          <NavLink to="/profile" className={linkClassName}>
            Profile
          </NavLink>
          <NavLink to="/settings" className={settingsClassName} aria-label="Settings">
            <GearIcon />
          </NavLink>
        </div>
      </div>
    </header>
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

// The eye is one fixed icon whose open/closed state is carried entirely by
// CSS, keyed off the switch's --checked class (Navbar.css) — the slash draws
// via a normalized-length stroke-dashoffset (pathLength="1" lets the offset
// be specified in [0, 1] rather than the path's real length) and the pupil
// scales under it, rather than JS swapping between two `d` values.
function EyeIcon() {
  return (
    <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path
        className="navbar__score-switch-eye-outline"
        d="M2 12C2 12 6 5 12 5C18 5 22 12 22 12C22 12 18 19 12 19C6 19 2 12 2 12Z"
      />
      <circle className="navbar__score-switch-eye-pupil" cx="12" cy="12" r="3" />
      <path className="navbar__score-switch-eye-slash" d="M4 4L20 20" pathLength="1" />
    </svg>
  )
}
