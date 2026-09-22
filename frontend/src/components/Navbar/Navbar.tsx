import { SearchBar } from '../SearchBar.tsx'
import { UpdatesMenu } from '../Updates/UpdatesMenu.tsx'
import { useScoreVisibility } from '../../context/ScoreVisibilityContext.tsx'
import { measureNavbar, useNavbarHidden } from '../../state/navbarReveal.ts'
import { currentSeasonTarget } from '../../utils/browseRange.ts'
import { NavbarPageLink } from './NavbarPageLink.tsx'
import { SettingsLink } from './SettingsLink.tsx'
import './Navbar.css'

type NavLinkSpec = { to: string; label: string; end?: boolean }

// One ordered list, so the left group's order (navigation-and-search: Home,
// My List, Series, Recap, Top, Season, Year, Airing) reads directly from
// this source rather than from where three separate constants happened to
// sit around the three dynamically-linked entries (Recap, Season, Year).
// Series and Home both need `end` — NavbarPageLink's underlying NavLink
// match would otherwise mark Series current on an individual series' page
// (/series/:animeId) too, and Home current on every route. Every entry here
// renders through NavbarPageLink (below), which layers the current-page
// click rule (navigation-and-search "The current page's navbar link returns
// me to the top"; design D5) on top of NavLink's own matching.
function leftNavLinks(recapLink: string, seasonLink: string, yearLink: string): NavLinkSpec[] {
  return [
    { to: '/', label: 'Home', end: true },
    { to: '/my-list', label: 'My List' },
    { to: '/series', label: 'Series', end: true },
    { to: recapLink, label: 'Recap' },
    { to: '/top', label: 'Top' },
    { to: seasonLink, label: 'Season' },
    { to: yearLink, label: 'Year' },
    { to: '/airing', label: 'Airing' },
  ]
}

function linkClassName({ isActive }: { isActive: boolean }) {
  return isActive ? 'navbar__link navbar__link--active' : 'navbar__link'
}

export function Navbar() {
  const { hidden, toggle } = useScoreVisibility()
  // Sticky reveal state (design D1/D3) — a class on this header, not layout,
  // so hiding it never shifts the page's content.
  const navbarHidden = useNavbarHidden()
  // Built at render, not hoisted into NAV_LINKS, so a long-lived tab open
  // across midnight still targets the current year (design.md decision 1).
  // NavbarPageLink's underlying NavLink match compares pathname only, so
  // this is marked active — and current for the click rule above — on
  // /recap whatever period is actually showing.
  const recapLink = `/recap?mode=yearly&year=${new Date().getFullYear()}&filter=aired`
  // Same reasoning as recapLink above, and for the Season link below — built
  // at render so the link always targets the current year/season, even in a
  // tab left open across New Year or a season boundary (design D7).
  const yearLink = `/year?year=${new Date().getFullYear()}`
  // The current season's address, in the same year-then-season parameter
  // order as SeasonPage's own replacementUrl — so this link's string equals
  // the address the page sits at once there, and a click at the top replaces
  // rather than pushing a second entry for the same view (design D6/D7).
  // Opening Season from the navbar no longer passes through the page's own
  // redirect for a bare /season, which is what caused its reload.
  const seasonTarget = currentSeasonTarget()
  const seasonLink = `/season?year=${seasonTarget.year}&season=${seasonTarget.season}`

  return (
    <header className={navbarHidden ? 'navbar navbar--hidden' : 'navbar'} ref={measureNavbar}>
      <nav className="navbar__links navbar__links--left" aria-label="Primary">
        {leftNavLinks(recapLink, seasonLink, yearLink).map((link) => (
          <NavbarPageLink key={link.label} to={link.to} end={link.end} className={linkClassName}>
            {link.label}
          </NavbarPageLink>
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
          <UpdatesMenu />
          <NavbarPageLink to="/profile" className={linkClassName}>
            Profile
          </NavbarPageLink>
          <SettingsLink />
        </div>
      </div>
    </header>
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
