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

Beside the status pill the page SHALL show a personal badge describing where I stand in the main line, chosen by this precedence:

1. `Completed` — every member of the series has finished airing, at least one main-line entry has finished airing, and every main-line entry that has finished airing is marked Completed in my list.
2. `Caught up` — every main-line entry that has finished airing (if any) is marked Completed in my list, every currently-airing main-line entry has me watching at least as many episodes as it has broadcast so far, and at least one main-line entry has finished airing or is currently airing.
3. `N behind` — the conditions of (2) hold except that currently-airing main-line entries have broadcast episodes I have not watched. N SHALL be the total of those unwatched broadcast episodes across the main line.
4. No badge — when a main-line entry that has finished airing is not marked Completed in my list, or when no main-line entry has finished airing or is currently airing (nothing has aired yet to be caught up on or behind on).

`Caught up` SHALL therefore never be claimed while episodes of a running main-line season sit unwatched. An entry that has not finished airing SHALL count against the badge only to the extent it has actually broadcast — it cannot be completed yet, but the episodes it has already aired are episodes I could have watched. An entry that has not aired at all SHALL NOT count against the badge, and an entry that is not in my list SHALL count as zero episodes watched.

A main line consisting of a single still-running entry — a long-running show that has never split into a "finished" season, such as a long-running weekly series — has no finished-airing entry at all; the "every main-line entry that has finished airing is marked Completed" condition is vacuously satisfied in that case; rather than showing no badge for the entire run, it holds the currently-airing entry itself to the same behind-count check as any other currently-airing main-line entry, so a franchise with only ever one continuous entry can still show `Caught up` or `N behind`.

When a currently-airing main-line entry's broadcast episode count is unknown, the page SHALL show no badge rather than claiming `Caught up` or inventing a behind count, since it cannot tell whether I am current.

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
- **THEN** the header shows a "Completed" badge next to the status pill

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

#### Scenario: Unfinished series is not badged
- **WHEN** one main-line entry that finished airing is not marked Completed in my list
- **THEN** no personal badge is shown

#### Scenario: Behind on a single continuously-airing entry
- **WHEN** a series' main line is a single entry that has never finished airing (no prior season to be "finished"), currently airing, with 1173 episodes broadcast so far of which I have watched 1100
- **THEN** the header shows a "73 behind" badge

#### Scenario: An entirely unaired series is not badged
- **WHEN** no main-line entry has finished airing or is currently airing (every main-line entry is not yet aired)
- **THEN** no personal badge is shown

#### Scenario: Unknown broadcast count shows no badge
- **WHEN** a currently-airing main-line entry has no known count of episodes broadcast so far
- **THEN** no personal badge is shown, rather than "Caught up"
