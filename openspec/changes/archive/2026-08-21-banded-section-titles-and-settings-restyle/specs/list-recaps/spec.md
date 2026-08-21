## ADDED Requirements

### Requirement: Recap section titles are banded by family
The recap page SHALL present the titles of the sections whose subject a colour family names as bands, using the banded-title treatment the `section-colour-language` capability defines:

- **Biggest Hot takes** SHALL carry the hot-take family;
- **Season ranking** and **Seasons by time watched** SHALL carry the season family;
- **Year ranking** and **Years by time watched** SHALL carry the year family.

Each band SHALL span the full width of the list it heads, from that list's left edge to its right edge, with its title centred on it. On a multi-year recap, where the four rankings sit in two columns, each band SHALL span its own column rather than the grid, so a band always measures the list directly beneath it.

A season-level ranking and a year-level ranking SHALL therefore be tellable apart by colour alone, which matters most on a multi-year recap where the two sit side by side in adjacent columns saying nearly the same words.

The page's remaining section titles — **Top N**, **Stats**, and **Rating distribution** — SHALL stay plain and unbanded: none of them is about a season, a year, or a disagreement, and banding every title would leave the banded ones saying nothing.

Banding a ranking's title SHALL NOT change anything else about that ranking: its rows, its shared row height, its posters, its "See all" control and overlay, and the two-column grouping of the four rankings SHALL be exactly as they are unbanded.

#### Scenario: A season ranking and a year ranking are tellable apart
- **WHEN** a multi-year recap shows a season ranking beside a year ranking
- **THEN** the two bands are filled in different families' colours, so which column ranks seasons and which ranks years is readable before either title is read

#### Scenario: A band spans its own column
- **WHEN** a multi-year recap shows its four rankings in two columns
- **THEN** each band runs the full width of the ranking directly beneath it and no wider

#### Scenario: Both of a level's rankings share a family
- **WHEN** a multi-year recap shows **Season ranking** and **Seasons by time watched** in one column
- **THEN** both bands carry the season family

#### Scenario: Hot takes carry their own family
- **WHEN** a recap shows its **Biggest Hot takes** section
- **THEN** its band carries the hot-take family, distinct from every other family on the page

#### Scenario: Plain titles stay plain
- **WHEN** a recap renders its **Top N**, **Stats**, and **Rating distribution** headings
- **THEN** each is drawn as a plain heading with no band behind it

#### Scenario: A banded ranking behaves identically
- **WHEN** a ranking whose title is banded holds more rows than it shows
- **THEN** its "See all" control opens the same overlay with the same rows in the same order as it does unbanded

## MODIFIED Requirements

### Requirement: Recap rows highlight on hover
Every row of the recap page that links to an anime, a season, or a year — the top-10 rows below the podium, the hot-take rows, and the rows of all four rankings, inline and in the "See all" overlay alike — SHALL adopt the app's standard hover treatment: the same tinted background, coloured border, and elevation that the anime cards on my list, the season page, and the top-anime page already use, applied on both pointer hover and keyboard focus.

The colour that treatment is drawn in SHALL follow what the row is about, per the `section-colour-language` capability:

- a row of a ranking whose title is banded SHALL highlight in that ranking's family — season-level rankings in the season family, year-level rankings in the year family — inline and in the "See all" overlay alike, the overlay carrying the family of the ranking that opened it and banding its own title in it;
- a **hot-take row** SHALL highlight in the score role its own direction names: blue where MAL liked it more and purple where I liked it more, matching the direction label the row already carries. The hot-takes band itself SHALL stay the hot-take family's own colour, so the section is named by one colour while each row is answered by its own;
- every other followable row, including the top-10 rows below the podium and the rating-distribution rows, SHALL keep the page's accent.

Apart from that colour, the treatment SHALL be the same one those pages use rather than a recap-specific variant, so a hovered row reads identically wherever it appears. It SHALL NOT change a row's size or position, and a row's resting appearance SHALL be unaffected.

The podium cards are not rows and are covered by "The podium is animated" instead; their richer treatment SHALL NOT be applied to any row.

#### Scenario: A hovered top-10 row
- **WHEN** I move the pointer over a row of the recap's top 10
- **THEN** that row takes the same accent highlight an anime card takes on my list

#### Scenario: A hovered hot take answers in its own direction
- **WHEN** I move the pointer over a hot-take row labelled "MAL liked it more" and then over one labelled "I liked it more"
- **THEN** the first takes the highlight in MAL's blue and the second in my own purple, while the section's band stays the hot-take family's colour

#### Scenario: A hovered ranking row
- **WHEN** I move the pointer over a row of any of the recap's rankings
- **THEN** that row takes the standard highlight drawn in that ranking's family colour

#### Scenario: An overlay row keeps its ranking's family
- **WHEN** I open the "See all" overlay from **Seasons by time watched** and hover one of its rows
- **THEN** the overlay's title is banded in the season family and the row highlights in that same family

#### Scenario: Keyboard focus matches hover
- **WHEN** I reach one of these rows by keyboard rather than by pointer
- **THEN** it takes the same highlight it takes on hover

#### Scenario: Highlighting does not reflow the list
- **WHEN** I move the pointer down a list of rows
- **THEN** no row changes size or position as it gains or loses the highlight

## REMOVED Requirements

### Requirement: Recap section titles are tinted by family
**Reason**: Replaced by "Recap section titles are banded by family" — the same five sections in the same three families, presented as full-width bands rather than as gradient-painted title text.

**Migration**: No section gains or loses a family and no ranking's contents change; only the way a family is drawn on the title changes.
