import { NavLink, useMatch, type NavLinkProps } from 'react-router-dom'
import type { MouseEvent, ReactNode } from 'react'
import { isScrollToTopSettling, scrollWindowTo } from '../../state/navbarReveal.ts'

// How long after a navbar scroll-to-top settles a further click on the same
// control still only scrolls rather than resetting — long enough that a
// double-click lands inside the window even on a short page, where the
// smooth scroll to 0 finishes before the second click arrives (design D5).
// Starting value — settled by trying it in the app (design.md Open
// Questions).
const DOUBLE_CLICK_GRACE_MS = 500

type NavbarPageLinkProps = {
  to: string
  end?: boolean
  className: NavLinkProps['className']
  'aria-label'?: string
  children: ReactNode
}

function isPlainLeftClick(event: MouseEvent): boolean {
  return event.button === 0 && !event.metaKey && !event.ctrlKey && !event.shiftKey && !event.altKey
}

// One wrapper around NavLink carrying the current-page click rule every
// navbar page control shares (navigation-and-search "The current page's
// navbar link returns me to the top"; design D5): scrolled down on the page
// it already marks current, a click only scrolls to the top instead of
// navigating. `useMatch` reads only `to`'s pathname — `to` can carry a query
// string (Recap, Year, Season), which a path pattern can't include — with the
// same `end` NavLink itself defaults to (false), so this and NavLink's own
// active marking can never disagree.
export function NavbarPageLink({ to, end, className, children, ...rest }: NavbarPageLinkProps) {
  const pathname = to.split('?')[0]
  const isCurrent = useMatch({ path: pathname, end: end ?? false }) !== null

  function handleClick(event: MouseEvent<HTMLAnchorElement>) {
    if (!isCurrent || !isPlainLeftClick(event)) return
    // At the top, and no scroll-to-top from an earlier click still running or
    // freshly settled: nothing to intercept, let NavLink navigate to the
    // default view (design D6).
    if (window.scrollY <= 1 && !isScrollToTopSettling(DOUBLE_CLICK_GRACE_MS)) return

    event.preventDefault()
    // Tells useScrollRestoration's retry loop this is my own scrolling, so a
    // restore still settling doesn't drag the page back down under it.
    window.dispatchEvent(new Event('bettermal:user-scroll'))
    scrollWindowTo(0, { navbar: 'show', smooth: !window.matchMedia('(prefers-reduced-motion: reduce)').matches })
  }

  return (
    <NavLink to={to} end={end} className={className} onClick={handleClick} {...rest}>
      {children}
    </NavLink>
  )
}
