## MODIFIED Requirements

### Requirement: Every page provides a dedicated header block for its title
The system SHALL render each of the My List, Top Anime, Seasonal Anime, Schedule, Search results, Settings, Profile, and anime detail pages' `<h1>` inside a page-specific header wrapper that owns the title's spacing, rather than leaving the `<h1>` as a bare child of the page's outer container. The wrapper SHALL zero the `h1`'s own top/bottom margin, so spacing above and below the title is set exactly once — by the wrapper's own layout — instead of the global `h1` margin stacking on top of the wrapper's own gap or padding.

The anime detail page SHALL place that wrapper above the page's body block rather than inside it, so its title begins at the same left edge and the same height on the page as every other page's title.

The Series page is unaffected by this requirement: its title continues to render beside the poster inside the page's hero block, per the `series-page` capability, rather than in a dedicated header wrapper.

#### Scenario: Settings and Profile gain a header wrapper
- **WHEN** the Settings or Profile page loads
- **THEN** its title renders inside a page-specific header wrapper whose `h1` has zero margin, rather than as a bare `h1` carrying the raw global heading margin stacked on the page's own layout gap

#### Scenario: Existing header-row pages keep their wrapper
- **WHEN** the My List, Top Anime, Seasonal Anime, Schedule, or Search results page loads
- **THEN** its title still renders inside that page's header wrapper with the `h1` margin zeroed

#### Scenario: Anime detail gains a header wrapper
- **WHEN** the anime detail page loads
- **THEN** its title renders inside a page-specific header wrapper above the page's body, with the `h1` margin zeroed

### Requirement: Page-appropriate title treatment distinct from the raw default heading
The system SHALL give each of the eight pages covered by the previous requirement a page-appropriate visual treatment beyond the raw global `h1` default, chosen to fit that page's header content. Pages whose header row also carries controls (My List, Top Anime, Seasonal Anime, Schedule, Search results, anime detail) SHALL present the title sized and weighted to share that row cleanly with its controls, rather than at the full-bleed size the raw global heading uses for a bare headline. Pages with no competing header-row content (Settings, Profile) SHALL present the title as a distinct standalone header block with more presence than a plain, unstyled line of text. Different pages MAY use different treatments from one another; the treatments are not required to match.

Every one of these titles SHALL nonetheless share the same typographic vocabulary — an explicit font size drawn from the set the other pages already use, an explicit weight, a zeroed margin, and normal letter-spacing — so that no page's title is left carrying the raw global heading's size, weight, or letter-spacing by default. The anime detail title in particular SHALL be brought onto that shared vocabulary rather than keeping its own ad-hoc values.

#### Scenario: Title shares a row with controls without dominating it
- **WHEN** a page whose header row also carries controls (for example Search's result count and sort control, or Top Anime's pagination) renders its title
- **THEN** the title and the controls sit in the header row together without the title visually overwhelming or crowding them

#### Scenario: Standalone pages present a distinct header block
- **WHEN** the Settings or Profile page renders
- **THEN** its title is presented as a distinct header block rather than a plain, unstyled line of text

#### Scenario: The anime detail title shares its row with the related links
- **WHEN** the anime detail page renders for an anime with related entries
- **THEN** its title and the related-entry links sit in the header row together, with the title sized to share the row rather than dominating it

#### Scenario: No page's title is left on the raw default
- **WHEN** I compare the titles of the eight pages this requirement covers
- **THEN** each carries an explicit size, weight, zeroed margin, and normal letter-spacing, with none falling back to the global heading defaults
