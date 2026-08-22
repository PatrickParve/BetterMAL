## Why

Two things in the navbar are off. The Settings gear is a 32×32 box while the links beside it and the search field beside those render at roughly 41–44px, so it reads as a small square dropped into a row of taller controls — and because each control derives its height from its own padding against the app's fluid root font, the mismatch widens as the window gets wider.

The active-page marker is also weaker than the hover state it sits next to. A navbar link for the page you are on gets a tinted background but leaves its border transparent, while merely hovering any link gives it both a background and a border. The result is that hovering a link looks more "selected" than actually being on its page. The Settings gear has no active state at all, so the settings page is the one page the navbar never marks.

## What Changes

- Give every navbar control that leads to a page — the six left links, Profile, and the Settings gear — plus the navbar's search field **one shared height**, defined once rather than emerging from each control's own padding, so the row is flush and stays flush as the root font scales.
- The **hide/unhide score toggle is explicitly excluded** and is not touched: it keeps its own pill shape, its own height, and every part of its knob, eye, and transition behaviour.
- Give the **active** navbar control a visible border as well as its tinted background, so being on a page reads at least as strongly as hovering a link does. The active border uses the accent at full strength while hover keeps the softer accent border, so an active link that is also hovered still reads as the current page.
- Bring the **Settings gear into the active treatment**, so the settings page is marked in the navbar like every other page.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `navigation-and-search`: adds a shared height for the navbar's page controls and its search field, with the score toggle excluded; adds the active border and extends the active treatment to the Settings gear.

## Impact

**Frontend**

- `components/Navbar/Navbar.css` — the shared height definition, the link/Profile/Settings/search-field sizing, and the active border.
- `components/Navbar/Navbar.tsx` — the Settings `NavLink` takes an active-aware class function instead of a fixed class string.
- `components/SearchBar.css` — untouched; the navbar scopes the field's height under `.navbar__links--right` so the Settings page's own search field keeps its current sizing.

**Not affected**

The score-visibility toggle's markup, CSS, and behaviour. The navbar's ordering, wrapping at narrow widths, hover treatments, and accessible labelling.
