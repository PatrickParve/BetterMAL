# page-header-design Specification

## Purpose
TBD - created by archiving change polish-headers-filters-and-top-series. Update Purpose after archive.

## Requirements

### Requirement: Every page provides a dedicated header block for its title
The system SHALL render each of the My List, Top Anime, Seasonal Anime, Schedule, Search results, Settings, and Profile pages' `<h1>` inside a page-specific header wrapper that owns the title's spacing, rather than leaving the `<h1>` as a bare child of the page's outer container. The wrapper SHALL zero the `h1`'s own top/bottom margin, so spacing above and below the title is set exactly once — by the wrapper's own layout — instead of the global `h1` margin stacking on top of the wrapper's own gap or padding.

#### Scenario: Settings and Profile gain a header wrapper
- **WHEN** the Settings or Profile page loads
- **THEN** its title renders inside a page-specific header wrapper whose `h1` has zero margin, rather than as a bare `h1` carrying the raw global heading margin stacked on the page's own layout gap

#### Scenario: Existing header-row pages keep their wrapper
- **WHEN** the My List, Top Anime, Seasonal Anime, Schedule, or Search results page loads
- **THEN** its title still renders inside that page's header wrapper with the `h1` margin zeroed

### Requirement: Page-appropriate title treatment distinct from the raw default heading
The system SHALL give each of the seven pages' titles a page-appropriate visual treatment beyond the raw global `h1` default, chosen to fit that page's header content. Pages whose header row also carries controls (My List, Top Anime, Seasonal Anime, Schedule, Search results) SHALL present the title sized and weighted to share that row cleanly with its controls, rather than at the full-bleed size the raw global heading uses for a bare headline. Pages with no competing header-row content (Settings, Profile) SHALL present the title as a distinct standalone header block with more presence than a plain, unstyled line of text. Different pages MAY use different treatments from one another; the treatments are not required to match.

#### Scenario: Title shares a row with controls without dominating it
- **WHEN** a page whose header row also carries controls (for example Search's result count and sort control, or Top Anime's pagination) renders its title
- **THEN** the title and the controls sit in the header row together without the title visually overwhelming or crowding them

#### Scenario: Standalone pages present a distinct header block
- **WHEN** the Settings or Profile page renders
- **THEN** its title is presented as a distinct header block rather than a plain, unstyled line of text
