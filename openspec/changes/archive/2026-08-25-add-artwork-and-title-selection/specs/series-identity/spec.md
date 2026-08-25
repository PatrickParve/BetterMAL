## ADDED Requirements

### Requirement: A series' displayed title and picture are resolved in one place
The system SHALL resolve a series' displayed title, English title, and picture through a single shared resolution, used by every surface that names or pictures a series: the series page, the series browser's cards, series search results, and the profile's Top series.

The resolution SHALL be: the series' chosen title when it has one, else its root member's title; the series' chosen picture when it has one, else its root member's displayed picture.

No surface SHALL project a series' title or picture from its root member directly.

#### Scenario: One resolution, every surface
- **WHEN** a series has a chosen title and a chosen picture
- **THEN** the series page, the browser card, the search result, and the profile's Top series all show that title and that picture

#### Scenario: An unchosen series falls back to its root
- **WHEN** a series has neither a chosen title nor a chosen picture
- **THEN** every surface shows its root member's title and displayed picture, exactly as before this capability existed

### Requirement: A chosen title replaces the English title too
When a series has a chosen title, the resolution SHALL report that title and SHALL report **no** English title for the series.

This exists because surfaces choose between a title and an English title when displaying an anime or a series. Leaving the root's English title in place beside a chosen title would let some surfaces display precisely the title that was rejected.

#### Scenario: The rejected title does not reappear
- **WHEN** a series whose root is titled "Beyblade: Metal Fusion" with English title "Beyblade: Metal Fusion" is given the chosen title "Beyblade"
- **THEN** every surface shows "Beyblade", and none shows the root's English title

#### Scenario: An unchosen series keeps its English title
- **WHEN** a series has no chosen title
- **THEN** the root's English title is reported as it is today, and the usual title-preference rules apply

### Requirement: The titles offered for a series
The system SHALL offer, as the titles a series may take, every main-line member's MAL title and every main-line member's English title where MAL provides one, deduplicated, in main-line watch order.

Extras' titles SHALL NOT be offered. A series' title describes the franchise's main line, and extras carry names — commercials, promos, recap specials — that are not candidates for it.

#### Scenario: Offered titles span the main line
- **WHEN** a series has three main-line members, two of which have English titles
- **THEN** the picker offers five titles, in main-line order, with duplicates removed

#### Scenario: Extras are not offered
- **WHEN** a series has extras with titles of their own
- **THEN** none of them is offered

### Requirement: A custom series title must be a contiguous trim of an offered title
The system SHALL accept a custom series title only when it appears as an **unbroken run of text** within at least one of the offered titles. A title may be shortened; it SHALL NOT gain text, and SHALL NOT be assembled from pieces taken from different places.

Before comparing, both the candidate and each offered title SHALL be normalized: surrounding whitespace trimmed, internal runs of whitespace collapsed to a single space. The candidate SHALL additionally have leading and trailing punctuation trimmed — colons, semicolons, commas, hyphens, dashes, full stops, exclamation and question marks, and quotation marks — so that trimming a title at a punctuation boundary is accepted rather than failing on a dangling mark.

Comparison SHALL be case-insensitive, and the title SHALL be stored as typed: changing capitalisation introduces no text that was not already there.

An empty or whitespace-only title SHALL be rejected. A title that satisfies no offered title SHALL be rejected.

The rule SHALL be enforced by the system, not only by the interface that collects the title.

#### Scenario: Trimming to a prefix
- **WHEN** the offered titles include "Beyblade: Metal Fusion" and I set the title to "Beyblade"
- **THEN** it is accepted, and the trailing colon is not required

#### Scenario: Trimming to a suffix
- **WHEN** the offered titles include "Beyblade: Metal Fusion" and I set the title to "Metal Fusion"
- **THEN** it is accepted

#### Scenario: Cuts from the middle are refused
- **WHEN** the offered titles include "Beyblade: Metal Fusion" and I set the title to "Beyblade Fusion"
- **THEN** it is rejected, because that text does not appear unbroken in any offered title

#### Scenario: Invented text is refused
- **WHEN** I set a series title to "bladusin"
- **THEN** it is rejected

#### Scenario: Words cannot be added
- **WHEN** the offered titles include "Beyblade: Metal Fusion" and I set the title to "Beyblade: Metal Fusion Remastered"
- **THEN** it is rejected

#### Scenario: An empty title is refused
- **WHEN** I set a series title to an empty string or to whitespace alone
- **THEN** it is rejected

#### Scenario: A whole offered title is accepted
- **WHEN** I set the title to one of the offered titles verbatim
- **THEN** it is accepted

#### Scenario: The rule is enforced server-side
- **WHEN** a request sets a series title that violates the rule, bypassing the interface
- **THEN** the request is rejected and the stored title is unchanged

### Requirement: A renamed series is still found by its members' titles
A chosen title SHALL NOT narrow how a series is matched in search. A series SHALL continue to match on every member's title and English title exactly as it does today, and SHALL additionally match on its chosen title.

The chosen title SHALL be used for display only.

#### Scenario: Searching a member title finds the renamed series
- **WHEN** a series titled "Beyblade" has a member titled "Beyblade: Metal Fusion" and I search for "Metal Fusion"
- **THEN** the series is found, and is shown as "Beyblade"

#### Scenario: Searching the chosen title finds it
- **WHEN** a series has been given a trimmed title no member title begins with
- **THEN** searching for that title finds the series

### Requirement: Chosen titles and pictures survive a rebuild
A series rebuild SHALL preserve the series' chosen title and chosen picture. This SHALL hold whether the rebuild is triggered by a visit, by the rebuild control, by a background build, or by a change in how the main line is classified.

When a rebuild **absorbs** other stored series into the one it keeps, the kept series' own choices SHALL win. Where the kept series has no chosen title, or no chosen picture, and an absorbed series has one, that value SHALL be adopted before the absorbed series is deleted; where several absorbed series qualify, the one with the largest member overlap SHALL be taken.

A stored choice SHALL NOT be re-validated against the rebuilt membership. A chosen title whose source member has left the series, or a chosen picture belonging to a member that is no longer part of it, SHALL be kept.

#### Scenario: A rebuild keeps the choices
- **WHEN** a series with a chosen title and picture is rebuilt
- **THEN** both are unchanged

#### Scenario: An absorption fills in a missing choice
- **WHEN** a rebuild merges two series, the survivor has no chosen title, and the absorbed one does
- **THEN** the survivor takes the absorbed series' title

#### Scenario: An absorption does not overwrite a choice
- **WHEN** a rebuild merges two series and both have chosen titles
- **THEN** the survivor keeps its own

#### Scenario: A choice outlives its source member
- **WHEN** a rebuild removes the member whose title was chosen for the series
- **THEN** the chosen title is kept

### Requirement: Series titles and pictures are never sent to MyAnimeList
Choosing a series' title or picture SHALL NOT cause any write to MyAnimeList, and SHALL NOT alter any member anime's own stored title.

#### Scenario: Nothing is pushed
- **WHEN** I set a series' title
- **THEN** no MyAnimeList request is made, and no member's title changes
