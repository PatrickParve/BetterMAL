## MODIFIED Requirements

### Requirement: Series page header
The series page SHALL show the root entry's picture, the series title, a status pill, and the year span of the series (e.g. `2013 – 2023`, or the single year when every entry aired in one year).

The header SHALL be the page's hero rather than a thumbnail strip: the picture SHALL be rendered large enough to read as the page's subject, and the title, status pill, personal badge, year span, external links, score averages, and main-line progress SHALL all sit inside that one block, so the series' summary is read in one place instead of down a column of separate panels.

The status pill SHALL take one of four values, chosen by this precedence:

1. `Airing` — a **main-line** entry of the series is currently airing.
2. `Ongoing` — no main-line entry is currently airing, and some member of the series is currently airing.
3. `Upcoming` — no member has finished airing and at least one has not yet aired.
4. `Ongoing` — some member has not yet aired.
5. `Finished` — otherwise.

`Airing` SHALL therefore be reserved for a series with something of its main line on the air right now, and `Ongoing` for a series with nothing of its main line on the air but something still to come — either an announced, not-yet-aired member, or a member outside the main line that is currently airing. A series whose main line has finished but whose OVA or special is currently broadcasting SHALL read `Ongoing`, not `Airing` and not `Finished`.

`Finished` SHALL continue to be reserved for a series with nothing left to come: a series whose aired members have all finished but which has an announced, not-yet-aired member SHALL read `Ongoing`, not `Finished`. The pill SHALL have no `Finished · sequel upcoming` state.

The four values SHALL be visually distinguishable from one another, each carrying its own colour rather than two of them sharing one. `Airing` SHALL carry the same colour this page already uses to mark an entry as on the air now, so the header pill and the timeline's on-air marking agree.

The progress bar and progress readout beneath the header SHALL treat `Airing` exactly as they treat `Ongoing`: both values SHALL select the broadcast progress bar and show the aired-episode figure, since both describe a series that is still running.

Beside the status pill the page SHALL show a personal badge describing where I stand in the main line, chosen by this precedence, evaluated over the series' aired main-line entries in release order:

1. `Completed` — every member of the series has finished airing, at least one main-line entry has finished airing, and every main-line entry that has finished airing is marked Completed in my list.
2. `Dropped` — among main-line entries that have aired (finished airing or currently airing), at least one is marked Dropped in my list, and no aired main-line entry released after the most recently aired such drop has ever been watched at all. A drop I later watched past — some later aired main-line entry has any watched episodes — does not count; the badge describes where I stand today, not history.
3. `Caught up` — I have watched at least one main-line episode, and the total I have watched across the aired main line meets the total that has actually broadcast across it.
4. `N behind` — I have watched at least one main-line episode, but fewer than have broadcast across the aired main line. N SHALL be the total broadcast main-line episodes minus the total I have watched, summed across every aired main-line entry — not only a currently-airing one.
5. `Unwatched` — at least one main-line entry has aired, I have watched none of the main line at all, and rule (2) does not already apply.
6. No badge — when nothing in the main line has aired yet, or when a currently-airing main-line entry's broadcast episode count is unknown and rules (3)/(4) cannot otherwise be resolved.

Rules (2) and (5) SHALL be decided without needing any entry's broadcast episode count — only watch status and watched-episode counts — so a currently-airing entry's unknown broadcast count SHALL NOT block them; it SHALL only be able to produce no badge once evaluation reaches rules (3)/(4). An entry that has not aired at all SHALL NOT count toward any of these figures, and an entry that is not in my list SHALL count as zero episodes watched.

`Completed` SHALL carry the colour the app already uses for a Completed watch status. `Caught up` SHALL keep the colour that already marks an entry as on the air now. `N behind` SHALL keep its existing warning colour. `Dropped` SHALL carry the colour the app already uses for a Dropped watch status. `Unwatched` SHALL carry the colour the app already uses for a Plan-to-watch status. All five SHALL remain visually distinct from one another and from the status pill's own four colours.

The status pill and the personal badge SHALL each render at a consistent, uniform size regardless of their label's length, so the two sit beside each other as evenly sized controls rather than ragged text of varying width.

A main line consisting of a single still-running entry — a long-running show that has never split into a "finished" season, such as a long-running weekly series — has no finished-airing entry at all, so rule (1) (which requires at least one finished-airing main-line entry) never applies to it; it falls through to rules (2)–(5) exactly as a multi-entry franchise would, and can still show `Caught up` or `N behind` against its own currently-airing broadcast count.

#### Scenario: A series with a season on the air
- **WHEN** I open a series whose latest main-line season is currently airing
- **THEN** the pill reads "Airing"

#### Scenario: Only an extra is on the air
- **WHEN** every main-line entry of a series has finished airing and one of its OVAs is currently airing
- **THEN** the pill reads "Ongoing", not "Airing" and not "Finished"

#### Scenario: A series with an announced sequel is ongoing
- **WHEN** every aired member of a series has finished but one member has not yet aired
- **THEN** the pill reads "Ongoing"

#### Scenario: A main-line season on the air outranks an announced sequel
- **WHEN** one main-line season of a series is currently airing and a further season has been announced but has not aired
- **THEN** the pill reads "Airing"

#### Scenario: A first season airing before anything has finished
- **WHEN** a series' only aired member is a main-line entry that is currently airing, and no member has finished airing
- **THEN** the pill reads "Airing", not "Upcoming"

#### Scenario: Finished means nothing is left to come
- **WHEN** every member of a series has finished airing and no member is unaired
- **THEN** the pill reads "Finished"

#### Scenario: Nothing has aired yet
- **WHEN** no member of a series has finished airing, none is currently airing, and at least one has not yet aired
- **THEN** the pill reads "Upcoming"

#### Scenario: Airing and Ongoing are told apart at a glance
- **WHEN** I compare a series reading "Airing" with one reading "Ongoing"
- **THEN** the two pills carry different colours, and the "Airing" pill carries the same colour the page's timeline uses to mark an entry as on the air now

#### Scenario: An airing series keeps the broadcast progress bar
- **WHEN** I open a series whose pill reads "Airing"
- **THEN** the header's progress bar shows broadcast progress behind my watched progress and the readout states the aired-episode figure, exactly as it does for an "Ongoing" series

#### Scenario: Year span
- **WHEN** a series' earliest entry aired in 2013 and its latest in 2023
- **THEN** the header shows "2013 – 2023"

#### Scenario: Completed series is badged
- **WHEN** I have marked every main-line entry of a fully finished series as Completed
- **THEN** the header shows a "Completed" badge next to the status pill, in the app's Completed-status colour

#### Scenario: Caught up on an ongoing series
- **WHEN** I have completed every main-line entry that has finished airing, and I have watched all 8 episodes the currently airing season has broadcast so far
- **THEN** the header shows a "Caught up" badge rather than "Completed"

#### Scenario: Behind on an airing season is not "caught up"
- **WHEN** I have completed every main-line entry that has finished airing, and the currently airing season has broadcast 8 episodes of which I have watched 5
- **THEN** the header shows a "3 behind" badge and does not show "Caught up"

#### Scenario: An airing season I have not started at all
- **WHEN** I have completed every earlier main-line entry and the currently airing season, with 8 episodes broadcast, is not in my list
- **THEN** the header shows an "8 behind" badge

#### Scenario: Caught up when the next entry has not aired yet
- **WHEN** every main-line entry that has aired is completed and one main-line entry has not yet aired
- **THEN** the header shows a "Caught up" badge

#### Scenario: A partially watched finished entry shows a behind count, not silence
- **WHEN** a series' only main-line entry has finished airing with 12 episodes, I have watched 5, and I have neither completed nor dropped it
- **THEN** the header shows a "7 behind" badge — the same behind-count treatment a currently-airing entry gets, not "no badge"

#### Scenario: A dropped entry with nothing watched after it
- **WHEN** I marked one main-line entry Dropped and have never watched any main-line entry released after it
- **THEN** the header shows a "Dropped" badge, in the app's Dropped-status colour

#### Scenario: A dropped entry I later resumed
- **WHEN** I marked an early main-line entry Dropped but have since watched episodes of a later main-line entry
- **THEN** the header does not show "Dropped"; it shows whatever "Caught up"/"N behind" the combined watched-versus-aired figures produce

#### Scenario: Nothing watched at all shows Unwatched
- **WHEN** at least one main-line entry has finished or is currently airing and I have watched none of the main line, with no entry marked Dropped
- **THEN** the header shows an "Unwatched" badge, in the app's Plan-to-watch colour, rather than no badge

#### Scenario: Behind on a single continuously-airing entry
- **WHEN** a series' main line is a single entry that has never finished airing (no prior season to be "finished"), currently airing, with 1173 episodes broadcast so far of which I have watched 1100
- **THEN** the header shows a "73 behind" badge

#### Scenario: An entirely unaired series is not badged
- **WHEN** no main-line entry has finished airing or is currently airing (every main-line entry is not yet aired)
- **THEN** no personal badge is shown

#### Scenario: Unknown broadcast count shows no badge, but only once Dropped/Unwatched are ruled out
- **WHEN** a currently-airing main-line entry has no known count of episodes broadcast so far, and the series is not already "Dropped" or "Unwatched" by those rules
- **THEN** no personal badge is shown, rather than "Caught up"

#### Scenario: The status pill and personal badge are evenly sized
- **WHEN** I compare a series showing "Airing" and "3 behind" against one showing "Finished" and "Completed"
- **THEN** both pills and both badges render at the same consistent size, regardless of how much shorter or longer their labels are
