## Context

Every navbar control currently sizes itself:

| Control | How its height arises | Height at root 16px → 18px |
| --- | --- | --- |
| `.navbar__link` (six left links, Profile) | `padding: 8px 12px` + a 1px border + the root's computed line-height | ~41px → ~44px |
| `.navbar__links--right .search-bar` | the input's `padding: 8px 12px` + the wrapper's 1px border + the same line-height | ~41px → ~44px |
| `.navbar__settings` | fixed `width: 32px; height: 32px` | 32px, always |
| `.navbar__score-switch` | `height: var(--control-h)` (34px) | 34px, always |

The root font is fluid — `clamp(16px, 14px + 0.3125vw, 18px)` with a `145%` line-height that inherits as a computed *length* — so the two padding-derived heights grow with the window while the two fixed ones do not. The links and the field happen to agree with each other because they use identical padding over the same inherited line box; nothing enforces that.

The active state is `.navbar__link--active`, which sets a background but not a border, while `.navbar__link:hover` sets both. `.navbar__settings` uses a plain class string in `Navbar.tsx` rather than `NavLink`'s active-aware callback, so it has no active state to style.

There is an established precedent for the fix in `page-header-design`: filter and sort clusters "share one height ... from one shared definition rather than from per-control padding values that each happen to land near the same number."

## Goals / Non-Goals

**Goals:**

- One definition of the navbar control height, applied to the controls that lead to pages and to the search field, so a control added later inherits it.
- The height holds across the full range of the fluid root font, rather than only agreeing at one window width.
- Being on a page reads at least as strongly as hovering a link, and stays distinguishable while that link is hovered.
- The Settings gear is marked active on the settings page.

**Non-Goals:**

- Touching the score-visibility toggle in any way. It is deliberately its own control family, and the user asked for it to stay exactly as it is.
- Changing navbar ordering, group membership, wrapping behaviour, hover treatments, or accessible labelling — all of which the `navigation-and-search` capability already fixes.
- Changing the Settings page's own search field, which shares the `.search-bar` component with the navbar.

## Decisions

### D1: A fixed `--navbar-control-h`, not a font-derived calculation

The shared height is a single fixed value (40px) declared once, with the controls set to `box-sizing: border-box`, `display: inline-flex`, `align-items: center`, and horizontal padding only. Their vertical padding stops contributing to height; the line box simply centres inside the fixed box.

Fixed rather than fluid is what makes the row *stay* flush: a `calc()` over the root font would keep the links and the field growing while the gear either had to grow with them (reintroducing two definitions to keep in step) or fall behind again. 40px clears the tallest line box the app ever produces — 18px root × 145% = 26.1px — with room for the 1px borders and visible padding left over, so nothing clips at any supported width. This mirrors `--control-h: 34px`, which the filter clusters already use the same way and for the same stated reason.

The value lands within a pixel of what the links and the search field render today at ordinary widths, so the visible change is the gear growing to meet them rather than the navbar resizing.

*Alternative considered:* size the gear to whatever the links happen to be, leaving three separate definitions. Rejected — that is the state being fixed, and it re-breaks the moment any of the three paddings is touched.

*Alternative considered:* reuse `--control-h` (34px). Rejected — it would shrink the links and the search field by ~7px to match a token whose comment scopes it explicitly to filter/sort clusters, and it is the height of the one control being excluded here, which would make the exclusion read as an accident.

### D2: The search field's height is scoped to the navbar

`.search-bar` is shared with the Settings page's anime-refresh picker. The height rule therefore goes on `.navbar__links--right .search-bar`, the same selector that already gives the field its navbar-specific `flex` basis, so `SearchBar.css` needs no change and the Settings page's field is untouched. The input inside keeps its padding for horizontal spacing; the wrapper's fixed height and `align-items: stretch` carry the vertical sizing.

### D3: Active borders use the accent at full strength; hover keeps the soft one

Hover already sets `border-color: var(--accent-border)` (the accent at 50% alpha). If active used the same colour, an active link under the pointer would be indistinguishable from any other hovered link — which the `navigation-and-search` capability's "Active page stays distinguishable" scenario forbids.

Active therefore uses `var(--accent)` at full strength. The two states then stack legibly: hovering an inactive link gives a soft border, hovering the active one keeps the strong border it already had. The active rules are ordered after the hover rules in the stylesheet so hover cannot override the active border at equal specificity.

### D4: Settings joins the active treatment through the existing class helper

`Navbar.tsx` already has a `linkClassName({ isActive })` helper that the links and Profile use. The Settings `NavLink` gets its own equivalent — it needs its own base class for the icon-box layout, not `navbar__link` — producing `navbar__settings navbar__settings--active`, styled to the same background and border as an active link. The gear's `aria-label` and route are unchanged; `NavLink` already contributes `aria-current="page"` on the active route, so no additional accessible signalling is needed.

## Risks / Trade-offs

**The navbar's overall height changes by a pixel or two at some window widths.** → Accepted and intended: the row becomes one height instead of four. 40px is within a pixel of the current link height at the narrow end of the fluid range and a few pixels under it at the wide end, so nothing reflows around it.

**A fixed height stops tracking the root font, so at very large user zoom the padding shrinks.** → The tallest line box the app produces is 26.1px against a 38px content box, leaving ~6px of vertical breathing room at the extreme. Browser zoom scales the whole layout including the fixed height, so this only bites under a text-only zoom, where the line box still fits.

**The score toggle now differs from its neighbours by more than it did.** → Explicitly requested. It is a pill-shaped switch rather than a link, and it is already the only navbar control with its own shape.
