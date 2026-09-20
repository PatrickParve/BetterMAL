## MODIFIED Requirements

### Requirement: A schedule change is recorded with what it moved from

Because a schedule change is news about a *movement*, the system SHALL store, on the update itself, **both ends of every field that moved**: the premiere date it moved from and the premiere date it moved to; the broadcast day and time it moved from and the broadcast day and time it moved to; and — for moved episodes — the episode number together with the dates it moved from and to.

Neither end SHALL be re-derived from the anime's current record. The moved-from value cannot be, since that record by then holds only the new value; the moved-to value SHALL NOT be either, because the anime's record holds where its schedule stands **now**, which is the same thing only until it moves again. An update SHALL therefore report the same pair of values for as long as it exists, however many times the anime's schedule moves afterwards.

An update recorded **before** both ends were stored SHALL keep working: where no moved-to value was recorded, the anime's current value SHALL be reported in its place, exactly as it was before. This fallback SHALL apply only where the recorded value is absent, and SHALL NOT be used to override one that is present.

Where several unaired episodes move in one detection, the update SHALL name the **earliest** of them, since that is the next one a viewer is waiting for.

Times SHALL be stored as the system stores broadcast times generally and presented in local time, so an update never reports a slot in a different timezone from the rest of the app. This SHALL hold for both ends of a slot move alike.

#### Scenario: A delay reports both dates

- **WHEN** an anime's premiere date moves from 5 October to 12 October
- **THEN** the update reports that it moved from 5 October to 12 October

#### Scenario: A slot change reports the old slot

- **WHEN** an anime's broadcast slot moves
- **THEN** the update reports the previous slot and the new one, both in local time

#### Scenario: Several episodes moving names the earliest

- **WHEN** episodes 7, 8 and 9 all move a week later in one detection
- **THEN** one update is recorded naming episode 7 and the dates it moved between

#### Scenario: A later correction does not rewrite an earlier update

- **WHEN** an anime's premiere date moves again after a premiere-date-changed update was recorded
- **THEN** the earlier update still reports the pair of dates it was recorded for

#### Scenario: A premiere that moves twice

- **WHEN** an anime's premiere moves from 8 October to 1 October, an update is recorded, and the premiere later moves to 20 November
- **THEN** the first update still reports a move from 8 October to 1 October — including the direction it states — and the second reports the move from 1 October to 20 November

#### Scenario: A slot that moves twice

- **WHEN** an anime's broadcast slot moves twice and an update was recorded for each move
- **THEN** each update reports the pair of slots it was recorded for, and neither reports the other's

#### Scenario: An update recorded before both ends were stored

- **WHEN** an update recorded before the moved-to values were stored is shown
- **THEN** it reports the anime's current value as the value it moved to, as it did before, rather than showing nothing

### Requirement: An update names the anime, what happened, and why it concerns me

Each shown update SHALL report the anime's picture and title; what happened, naming which kinds it covers; the anime's **current** episode count and premiere date, each shown only where known and omitted entirely where not; and why it concerns me — the entry it is affiliated with together with the relation it holds to it, or, where the anime is my own entry and has no such affiliation, that entry's own status.

**Layout.** An update SHALL be presented as a card whose picture sits on the **left**, with the title beside it at the top, the news beneath the title, and the reason beneath the news. Cards SHALL share a common width wherever they are listed, and their height SHALL follow their content, so a card with a tall picture or more to say is taller than one without.

**The picture SHALL be drawn whole, at its own proportions.** No part of it SHALL be cropped away, and it SHALL NOT be letterboxed inside a box of a different shape: a portrait picture SHALL be drawn portrait, a landscape picture SHALL be drawn landscape and correspondingly wider and shorter, and a square picture SHALL be drawn square — the same rule the `artwork-selection` picker applies to its options. The drawn picture SHALL be bounded in **both** directions, so that a portrait picture cannot make a card towering and a wide picture cannot leave the text beside it unreadably narrow; a picture too wide to fit that bound SHALL be reduced whole rather than cropped. An anime with no picture SHALL show a placeholder of the app's usual poster proportions rather than a card with no picture area at all.

**One fact SHALL read as one line.** The news SHALL be one line per kind the update covers, and a kind whose news *is* a value SHALL state that value in its own line rather than announcing the kind and leaving the value to a separate line — `Total episodes: 12`, not `Episode count revealed` above `12 episodes`; a premiere date released, a premiere moved, a broadcast slot moved and an episode moved SHALL each read the same way, as one self-contained statement. A schedule change SHALL name both the value it moved to and the value it moved from, including for a moved episode.

**A premiere that moved SHALL say which way it moved, in words that cannot be read in reverse.** A premiere that moved to an **earlier** date SHALL be described as having moved *earlier*; a premiere that moved to a **later** date SHALL be described as *delayed*. Neither wording SHALL rest on an up/down metaphor, since "up" is read both as *sooner* and as *later in the calendar* and so cannot state the direction unambiguously. Where the date the premiere moved to and the date it moved from are the same date, the line SHALL report the move **without asserting a direction**, rather than claiming one of the two directions between a date and itself. The direction SHALL be derived from the pair of values the update reports, so it cannot change after the update was recorded.

The current-value lines SHALL then be shown for each fact **no news line already stated**: an episode-count release suppresses the episode-count line, and a premiere date released or changed suppresses the premiere-date line, while an announcement — which states neither — still reports both where known.

**Which values are read live, and which are read from the update.** Episode count and premiere date, where shown as **facts** about the anime, SHALL be read from the anime's current cached record rather than from anything captured when the update was recorded, so a later correction is reflected on the update rather than leaving it stating a superseded value. The values a schedule change **moved between** SHALL be read from the update itself — both the value moved from and the value moved to — and SHALL NOT be read from the anime's current record, which holds only where its schedule stands now. The one exception is an update recorded before the moved-to value was stored, which falls back to the anime's current value as set out under "A schedule change is recorded with what it moved from".

A date SHALL be shown with its year, so news about a premiere or an episode a year out is unambiguous.

**Nothing SHALL be clipped except a title in the dropdown.** In the navbar dropdown, a title too long for its card MAY be cut off, and SHALL then be recoverable in full the way the app's other truncated titles are. Every other line, on every surface, SHALL be shown in full, wrapping onto as many lines as it needs.

Where an update's anime is affiliated with more than one non-dropped entry, the system SHALL name one of them deterministically, preferring the most specific relation. Where the anime is both my own entry and affiliated with another, the affiliation SHALL be named, as it says more than the entry's own status does.

**The affiliated entry SHALL be named by the title the app displays for it** — its English title where one is known, its MyAnimeList title otherwise — so the reason names that anime exactly as its own card, row and detail page name it, rather than in a title the app shows nowhere else. This holds although the name sits inside a composed line rather than standing on its own as a title, and requires no request the app does not already make: the affiliated entry is a list entry of my own, whose metadata the app already holds. Where two candidate entries hold the same most-specific relation, that same displayed title SHALL settle which is named, so the choice matches what is shown.

Selecting an update SHALL open that anime's detail page.

#### Scenario: An episode count reveal reads as one line

- **WHEN** an update covering an episode-count release is shown for an anime whose total is 12
- **THEN** its news reads `Total episodes: 12` on one line, and no separate episode-count line is shown beneath it

#### Scenario: A premiere reveal reads as one line

- **WHEN** an update covering a premiere-date release is shown
- **THEN** its news states the premiere date itself on one line, and no separate premiere-date line is shown beneath it

#### Scenario: An announcement still reports the facts it arrived with

- **WHEN** an announcement update is shown for an anime whose episode count and premiere date are both known
- **THEN** the card reads `Announced` and then reports the episode count and the premiere date

#### Scenario: A premiere brought forward

- **WHEN** a premiere-date-changed update reports a move from 8 October 2026 to 1 October 2026
- **THEN** its line states that the premiere moved **earlier**, naming both dates, and states it in no wording that could be read as the premiere having been pushed later

#### Scenario: A premiere pushed back

- **WHEN** a premiere-date-changed update reports a move from 5 October 2026 to 12 October 2026
- **THEN** its line states that the premiere was **delayed**, naming both dates

#### Scenario: A premiere that reads as moving to the date it came from

- **WHEN** a premiere-date-changed update would state a move whose two dates are the same date
- **THEN** its line reports the move and the date without claiming that the premiere moved earlier or was delayed

#### Scenario: The direction does not change when the anime moves again

- **WHEN** a premiere-date-changed update stating that the premiere moved earlier is shown after the anime's premiere has moved again, to a date later than either of the two it names
- **THEN** it still states that the premiere moved earlier, between the two dates it was recorded for

#### Scenario: A moved episode reports both dates

- **WHEN** an episodes-moved update is shown
- **THEN** its line names the episode, the date it moved to, and the date it moved from

#### Scenario: A portrait picture

- **WHEN** a shown update's anime has a portrait poster
- **THEN** the card draws it portrait, whole, and the card is correspondingly taller

#### Scenario: A landscape picture

- **WHEN** a shown update's anime has a picture wider than it is tall
- **THEN** the card draws it wide and short at its own proportions, whole, without cropping it to a portrait slice, and the text beside it still has room to read

#### Scenario: An extremely wide picture

- **WHEN** a shown update's anime has a picture too wide to fit the space a card gives it
- **THEN** it is reduced until it fits whole, rather than being cropped or pushing the card wider than its neighbours

#### Scenario: Cards differ in height but not in width

- **WHEN** a list holds one card with a tall picture and one with a short picture
- **THEN** the two cards are the same width and differ in height

#### Scenario: A long title in the dropdown

- **WHEN** a card in the navbar dropdown has a title too long for its width
- **THEN** the title is cut off, and the full title is available on hover as elsewhere in the app

#### Scenario: A card with a lot to say

- **WHEN** an update covers a broadcast-slot change, naming both the new slot and the previous one
- **THEN** the whole of that text is shown, wrapping as needed, rather than being cut off at one line

#### Scenario: An anime with no episode count yet

- **WHEN** a shown update's anime has no known episode count
- **THEN** no episode count is displayed for it, and its premiere date is displayed if known

#### Scenario: An anime with neither count nor date

- **WHEN** a shown update's anime has neither a known episode count nor a known premiere date
- **THEN** neither is displayed, and the update still shows its picture, title, news and reason

#### Scenario: A corrected count is reflected

- **WHEN** an anime's episode count is corrected from 12 to 13 after its episode-count update was recorded
- **THEN** that update displays 13

#### Scenario: A delay keeps the dates it was recorded with

- **WHEN** a premiere-date-changed update recorded a move from 5 October to 12 October, and the date later moves again
- **THEN** that update still reports the move from 5 October to 12 October, while a newer update reports the newer move

#### Scenario: A slot change keeps the slots it was recorded with

- **WHEN** a broadcast-slot-changed update was recorded, and the anime's slot later moves again
- **THEN** that update still reports the pair of slots it was recorded for

#### Scenario: The affiliation is named

- **WHEN** an update concerns the sequel of an entry I am watching
- **THEN** it names that entry and states that the update's anime is its sequel

#### Scenario: The affiliation is named in English

- **WHEN** an update concerns the sequel of an entry of mine whose MyAnimeList title is "Tensei shitara Ken deshita" and whose English title is "Reincarnated as a Sword"
- **THEN** the reason names it "Reincarnated as a Sword", the same title its own card carries, rather than the romaji one

#### Scenario: An affiliated entry with no English title

- **WHEN** an update concerns the sequel of an entry of mine for which no English title is known
- **THEN** the reason names it by its MyAnimeList title

#### Scenario: My own standalone entry names its status

- **WHEN** an update concerns an anime that is my own Plan to watch entry with no affiliation in my list
- **THEN** it reports that entry's own status rather than an affiliation

#### Scenario: Opening an update

- **WHEN** I select a shown update
- **THEN** the detail page for the anime it concerns opens
