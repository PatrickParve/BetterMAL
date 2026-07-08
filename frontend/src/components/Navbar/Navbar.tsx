import { NavLink } from 'react-router-dom'
import { SearchBar } from '../SearchBar.tsx'
import { useScoreVisibility } from '../../context/ScoreVisibilityContext.tsx'
import './Navbar.css'

const NAV_LINKS: { to: string; label: string; end?: boolean }[] = [
  { to: '/', label: 'Home', end: true },
  { to: '/season', label: 'Seasonal' },
  { to: '/top', label: 'Top' },
  { to: '/airing', label: 'Airing' },
]

function linkClassName({ isActive }: { isActive: boolean }) {
  return isActive ? 'navbar__link navbar__link--active' : 'navbar__link'
}

export function Navbar() {
  const { hidden, toggle } = useScoreVisibility()

  return (
    <header className="navbar">
      <nav className="navbar__links navbar__links--left" aria-label="Primary">
        {NAV_LINKS.map((link) => (
          <NavLink key={link.to} to={link.to} end={link.end} className={linkClassName}>
            {link.label}
          </NavLink>
        ))}
      </nav>

      <div className="navbar__search">
        <SearchBar />
      </div>

      <div className="navbar__links navbar__links--right">
        <NavLink to="/profile" className={linkClassName}>
          Profile
        </NavLink>
        <button type="button" className="navbar__toggle" onClick={toggle} aria-pressed={hidden}>
          {hidden ? 'Show scores' : 'Hide scores'}
        </button>
        <NavLink to="/settings" className="navbar__settings" aria-label="Settings">
          <GearIcon />
        </NavLink>
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
