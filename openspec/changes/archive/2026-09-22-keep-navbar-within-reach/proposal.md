## Why

Once I scroll down any page the navbar is gone: it sits in the page's normal flow, so the only way back to it — to change page, search, or toggle scores — is to scroll all the way up. And when I do get back to it, clicking the link for the page I'm already on doesn't do what it looks like it should. The link navigates to that page's *default* address, so on Year 2020 it jumps to 2026, and on a Recap period, a Top page 3 or a filtered My List it quietly throws the view away. Season behaves worst: its link is a bare `/season`, which the page's own guard redirects to the current season's address, unmounting and remounting the whole page on the way. That is the reload you see there and not elsewhere.

## What Changes

**The navbar follows me, out of the way**

- The navbar becomes sticky. Scrolling down slides it up out of view; scrolling up slides it back in, smoothly, from wherever I am on the page. At the top of the page it is always shown.
- It stays put while I'm using it: while focus is inside it or its search results or Updates dropdown is open, scrolling doesn't hide it. Tabbing into a hidden navbar brings it back.
- Arriving at a page, by a link or by Back/Forward, shows it. Scrolling the client does itself (a Back navigation returning me to where I was, a series page pinning a section to the top) never reads as my scrolling. A pin that moves content to the top of the window hides the navbar so the navbar can't cover what was just pinned.
- With reduced motion requested, it appears and disappears without the slide.

**The link for the page I'm on takes me to the top**

- Clicking the navbar link for the page I'm already on, while scrolled down, scrolls smoothly to the top and does nothing else. It doesn't navigate, reload, reset a view control or change the address. Year 2020 stays 2020, Top page 3 stays page 3, and my My List filters stay set. This applies to every navbar page control (the eight left links, Profile and the Settings gear).
- Clicking it again once I'm already at the top takes me to the page's default view, the same page a click from anywhere else would open (the current season, the current year, the default recap period, page 1, default filters). When that default view has a different address from the one I'm on, it is a new history entry, so Back returns me to the view I left. A double-click while scrolled down only scrolls; it never resets.
- Modifier clicks (open in new tab/window) are untouched.

**Season stops reloading**

- The navbar's Season link points straight at the current season's address, the way Year already points at the current year. Opening Season from the navbar no longer bounces through a redirect that remounts the page, and resetting to the current season while already on it has nothing to reload.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `navigation-and-search`: two new requirements. The navbar stays within reach while scrolling (sticky, hides going down, returns going up, stays while in use, never covers content the client pinned or keyboard focus). The current page's navbar link returns me to the top without changing anything, and only resets to the page's default view when I'm already at the top.
- `page-state-restoration`: "fresh visit" is narrowed so it no longer covers clicking the current page's link while scrolled down, which is no navigation at all. The scenarios that reached a page "by clicking its navbar link" now say *from another page*, and a scenario is added that selections survive a click on the current page's link.

## Impact

- **Frontend only.** `components/Navbar/Navbar.tsx` and `Navbar.css` (sticky positioning, hidden state, the current-page link click rule), `components/Navbar/SettingsLink.tsx` (same click rule for the gear), a new small module holding the navbar's shown/hidden state and the one helper every client-initiated window scroll goes through, `hooks/useScrollRestoration.ts` and `pages/SeriesPage.tsx` (their `window.scrollTo` calls move onto that helper), `hooks/useRestorableScroll.ts` (a strip returns to its start when a fresh visit reuses its mounted node), and `index.css` (`scroll-padding-top` so keyboard focus never lands under the navbar).
- **No backend, API, data or migration change.**
- **Not changing:** navbar layout, order, sizes, the current-page marking and hover treatment; how a page reached from *another* page behaves (still a fresh visit at the top with default view state); Back/Forward restoration; the Season and Year route guards, which still replace a bare or invalid address with the current season/year. `season-browser`'s "opening the Season page from the navbar defaults to the current season" is still met, now by the link itself rather than by the redirect.
