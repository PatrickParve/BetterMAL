## MODIFIED Requirements

### Requirement: Series composition from the relation graph
The system SHALL derive a series as the connected component of the stored related-anime graph, traversing only story relations: `sequel`, `prequel`, `side_story`, `parent_story`, `summary`, `full_story`, `spin_off`, and `alternative_version`. Every other relation MAL reports — including `alternative_setting`, `character`, and any unrecognized relation string — SHALL be stored as it already is but SHALL NOT be traversed, so shows that merely share a universe or a cast never merge into one series.

The `other` relation SHALL be traversed in exactly one case: when precisely one of its two ends is an anime whose media type is one of a fixed **companion-media set** — `music` and `pv`. MAL links a franchise's opening/ending/image songs and its promotional videos to the show they belong to with `other` and nothing else, so a franchise's music and PV entries are otherwise unreachable — they either vanish from the series entirely or form their own companion-only series. `cm` (commercial) SHALL NOT be in the companion-media set.

`other` relations where neither end is in the companion-media set, and where both ends are in it, SHALL NOT be traversed — so a promo for a theme song never fuses two franchises, and one promotional video never chains to another. Because the rule is stated over the relation's two ends rather than over the direction it is stored in, a series built from the companion entry and a series built from the show SHALL produce the same component.

Recognising an `other` edge's far end requires that end's media type, which MAL does not carry on the relation itself. When an `other` edge's far end has **no cached metadata row at all**, the system SHALL spend a bounded **probe** on it — a fetch made solely to learn its media type — within the probe budget the bounded-builds requirement defines. A probe SHALL cache that anime's row, so each `other` far end SHALL be probed at most once across all builds, and every later build SHALL decide that edge from cache at no cost. A far end whose probe reveals a media type in the companion-media set SHALL be admitted as a member using the row the probe already produced; any other media type SHALL leave the edge untraversed thereafter without further cost. An `other` edge whose far end is still unprobed because the budget is exhausted SHALL NOT be traversed on that build.

Traversal SHALL be undirected: from a member the system SHALL follow both that anime's own relation rows and relation rows pointing at it, so a member whose own relations have never been fetched still connects the component.

A story relation an external source **contradicts** — one end asserts it, the other end was fetched and does not, and AniList knows both anime and relates them not at all — SHALL NOT be traversed. Such an edge is one anime's unreciprocated claim about another that no other source supports, and traversing it silently admits an unrelated anime to the franchise. The edge SHALL remain stored and SHALL remain visible on the detail page; only its power to pull a member into a series is withdrawn. An edge that is merely unreciprocated, with no external source to settle it, SHALL still be traversed, so a franchise never shrinks on the strength of missing information alone.

An anime SHALL belong to at most one series.

Every series stored under the previous traversal or ordering rules SHALL be rebuilt once, on its next read, so a franchise's music entries, its promotional videos, and this change's corrections reach an already-stored series without the user having to request a rebuild.

#### Scenario: Sequels and prequels form one series
- **WHEN** a series is built from an anime whose relations chain through two sequels and one prequel
- **THEN** all four anime are members of the same series

#### Scenario: Alternative-setting relations do not merge series
- **WHEN** an anime is related to another only by `alternative_setting` or `character`
- **THEN** the other anime is not a member of its series

#### Scenario: Reverse edges keep the component connected
- **WHEN** anime A stores a `sequel` relation to anime B, and B has never been full-fetched and stores no relations of its own
- **THEN** B is still a member of A's series

#### Scenario: A contradicted edge does not admit a member
- **WHEN** MAL reports `17965 --sequel--> 39360`, 39360 has been fetched and stores no relations at all, and AniList knows both and relates them not at all
- **THEN** 39360 is not a member of 17965's series

#### Scenario: An unreciprocated edge with no external verdict still connects
- **WHEN** anime A stores a `sequel` relation to anime B, B stores nothing back, and AniList does not know one of them
- **THEN** B remains a member of A's series, exactly as before this change

#### Scenario: A franchise's song joins the franchise
- **WHEN** a TV series stores an `other` relation to a `music` entry — its opening theme's music video — and that entry has a cached metadata row
- **THEN** the music entry is a member of that series

#### Scenario: A franchise's promotional video joins the franchise
- **WHEN** a TV series stores an `other` relation to an entry whose media type is `pv`
- **THEN** that promotional video is a member of that series

#### Scenario: The song's own series is the show's series
- **WHEN** a series is built starting from that music entry instead of from the show
- **THEN** the same component is produced, with the show and its seasons as members, rather than a music-only series

#### Scenario: A music-only series is absorbed
- **WHEN** a music entry and its cover version are stored as their own two-member series, and the franchise the song belongs to is rebuilt under these rules
- **THEN** both are members of the franchise's series and the music-only series no longer exists

#### Scenario: One promotional video does not chain to another
- **WHEN** two `pv` entries store an `other` relation to each other
- **THEN** that relation is not traversed, and neither pulls the other into its series

#### Scenario: A promo for a theme song does not fuse two franchises
- **WHEN** a `pv` entry stores an `other` relation to a `music` entry
- **THEN** that relation is not traversed, since both of its ends are companion media

#### Scenario: Commercials and crossovers still do not merge series
- **WHEN** a show stores an `other` relation to a commercial or a crossover short
- **THEN** that anime is not pulled into the show's series

#### Scenario: An uncached `other` end is probed once
- **WHEN** a member stores an `other` relation to an anime with no cached metadata row, and probe budget remains
- **THEN** that anime is fetched once, its media type decides whether the edge is traversed, and no later build spends a probe on that edge again

#### Scenario: A probed commercial is remembered as untraversable
- **WHEN** a probe reveals an `other` far end to be a commercial, and the series is built again later
- **THEN** the edge is skipped from cache with no fetch, and the commercial is not a member

#### Scenario: A stored series re-derives itself after a rules change
- **WHEN** a series stored under the previous rules is next read
- **THEN** it is rebuilt under the current rules, without the user pressing Rebuild

### Requirement: Main line and extras
Within a series the system SHALL identify a main line: the connected chain over `sequel`/`prequel` relations among the members holding the most main-line-eligible members — ties broken in favour of the chain containing the earliest-aired eligible member — reduced to just its eligible members. Every other member of the series SHALL be an extra.

A member SHALL be main-line-eligible unless it is any of:
- a member whose media type is `special`, `music`, or `pv`;
- a recap of another member — a `summary`/`full_story` relation to it;
- side content of another member — an outgoing `parent_story` relation to another member, or an incoming `side_story` relation from another member.

A promotional video is never a chapter of a story, so a `pv` member SHALL never be main line however MAL relates it — including in a franchise whose real entries carry no `sequel`/`prequel` relations at all, where a chain of promos could otherwise out-rank the show.

Chains SHALL be formed over every member, eligible or not, and ranked afterwards by their eligible members only. Excluding an ineligible member from the ranking SHALL NOT split the chain it sits in, so a recap or side entry that bridges two seasons still keeps those seasons in one chain while never being main line itself.

When any chain holds an eligible member whose media type is `tv`, only such chains SHALL be ranked. A long-running show with no separately-listed seasons is a one-node chain, and without this restriction a handful of side movies that chain to each other could out-count it; when no chain holds an eligible `tv` member — a movie-only or ONA-only franchise — every chain SHALL be ranked.

Because eligibility, not raw chain size, decides the ranking, a franchise whose members carry no `sequel`/`prequel` relations at all — every member its own one-node chain — SHALL still resolve to its actual show rather than to whichever promotional short happens to have aired first.

A recap or side-content tag overrides a sequel/prequel edge on the same member: MAL routinely gives a recap special both a `summary`/`full_story` relation to the season it recaps and a `sequel`/`prequel` relation bridging it to the next season, and often types it `tv_special` rather than `special` — the media-type filter alone would not catch it, so the explicit tags are checked independently.

Where no member of the series is eligible at all, the system SHALL fall back to the unreduced chain, so a specials-only or side-story-only franchise still has a main line to render.

Extras SHALL be grouped by media type in the fixed display order Movie, OVA, ONA, Special, Music, PV, TV, Other, and ordered by aired-from date within each group. `pv` SHALL therefore be its own group rather than falling into Other, placed with the other short-form non-story extras and ahead of the TV and Other catch-alls.

#### Scenario: Sequels and story movies are main line
- **WHEN** a series contains three TV seasons and a movie, all linked by sequel relations
- **THEN** all four are main-line entries

#### Scenario: Specials are extras even when MAL calls them sequels
- **WHEN** a member whose media type is `special` is linked into the sequel chain
- **THEN** it is an extra, not a main-line entry

#### Scenario: A promotional video is never main line
- **WHEN** a member whose media type is `pv` is linked into the sequel chain
- **THEN** it is an extra, not a main-line entry

#### Scenario: Promotional videos have their own More group
- **WHEN** a series has two `pv` members
- **THEN** they appear under a "PV" group in the More section, between the Music and TV groups, rather than under Other

#### Scenario: Side stories and music videos are extras
- **WHEN** a series contains a side story, an OVA run, and a music video
- **THEN** none of them are main-line entries, and they appear grouped by media type

#### Scenario: A recap special stays an extra even when it bridges two seasons
- **WHEN** a `tv_special` recaps one season (`summary`/`full_story`) and also carries a `sequel`/`prequel` edge into the next season
- **THEN** it is an extra, not a main-line entry, and the two real seasons it bridges are still main line

#### Scenario: A member tagged as another member's side story is an extra
- **WHEN** a member declares a `parent_story` relation to another member, or another member declares a `side_story` relation to it
- **THEN** it is an extra, not a main-line entry, even when its media type would otherwise allow it

#### Scenario: A side entry that bridges two seasons does not split the main line
- **WHEN** a side-story member carries `sequel`/`prequel` edges linking two seasons that have no direct edge to each other
- **THEN** both seasons remain in one main-line chain and the side entry itself is an extra

#### Scenario: A lone TV show outranks a chain of side movies
- **WHEN** a franchise's only `tv` member has no `sequel`/`prequel` relations of its own while several of its movies chain to each other
- **THEN** the `tv` member is the main line and the movies are extras

#### Scenario: A franchise with no sequel relations is led by its show, not its promo
- **WHEN** a franchise's members are linked only by `parent_story` relations — a show plus concept and character shorts that each name it as their parent story, with no `sequel`/`prequel` relation anywhere
- **THEN** the show is the sole main-line entry and the shorts are extras, regardless of the shorts having aired years earlier

#### Scenario: Arriving from a spin-off does not make it the main line
- **WHEN** a series is built starting from the second season of a spin-off whose sequel chain is shorter than the parent series' chain
- **THEN** the parent series' chain is the main line and the spin-off's entries are extras

#### Scenario: A franchise of only side entries still renders a main line
- **WHEN** every member of a series is ineligible — all specials, recaps, or side content of one another
- **THEN** the largest chain is used unreduced rather than leaving the series with no main line

### Requirement: Bounded series builds
A series build SHALL be bounded by three limits: at most 400 members, at most 8 live MAL full-detail fetches on a visit-triggered build or 20 on an explicitly requested rebuild, and a separate **probe budget** of at most 4 fetches on a visit-triggered build or 10 on an explicitly requested rebuild. Fetches SHALL be spent first on members that have no cached metadata row at all, since those cannot be displayed otherwise; remaining budget SHALL be spent expanding members with a lean cached row (no relations of its own), since an unexpanded lean member can hide a real season from the series or from main-line classification.

The probe budget SHALL be separate from the member fetch budget and SHALL be spent only on resolving the media type of an `other` edge's uncached far end, per the composition requirement. A probe SHALL NOT consume member fetch budget and member fetches SHALL NOT consume probe budget, so a franchise carrying many `other` edges to commercials can never starve the fetches that real, story-related members need in order to be displayed at all.

Because a probe caches the row it fetches, no `other` far end is probed more than once across all builds, and the probe budget's steady-state cost for a franchise the user revisits SHALL be zero.

The member limit SHALL be a safety ceiling against a runaway component rather than a working limit: it SHALL be set high enough that no real franchise reaches it, so that reaching it means the traversal has gone wrong and the truncation notice is meaningful.

A build that exhausts either its fetch budget or its probe budget SHALL mark the series partial; a build that reaches the member cap SHALL mark it truncated. A partial series SHALL be rebuilt on the next visit, so successive visits — each starting from more cached data than the last — complete it without any background job. Because probes never repeat, a series left partial by an exhausted probe budget SHALL converge over successive visits rather than re-probing indefinitely.

A series stored as truncated SHALL NOT be rebuilt automatically on every visit, since a component genuinely over the cap would then re-traverse and spend fetch budget on every visit indefinitely. It SHALL pick up a raised cap on the next explicitly requested rebuild or the next staleness-triggered rebuild.

Concurrent builds of the same series SHALL collapse into one, matching the single-flight behaviour of the app's other visit-triggered refreshes.

#### Scenario: Large franchise is not truncated
- **WHEN** I open the series page for a franchise with more than 60 members, such as One Piece with its movies and specials
- **THEN** every member of its component is included, and the series is not marked truncated

#### Scenario: Build stops at the fetch budget
- **WHEN** I open the series page for a franchise with 20 members the app has never fetched
- **THEN** the page returns after at most 8 live MAL fetches, and the series is marked partial

#### Scenario: Probes do not eat the member fetch budget
- **WHEN** a build probes four uncached `other` far ends and also fetches members with no cached row
- **THEN** the member fetches available are still the full 8, unreduced by the probes

#### Scenario: A build stops at the probe budget and is marked partial
- **WHEN** I open a series whose members carry ten `other` edges to anime with no cached row
- **THEN** at most four are probed on that visit and the series is marked partial

#### Scenario: Probing converges over visits
- **WHEN** I reopen that same series on later visits
- **THEN** the remaining far ends are probed a few at a time, none is probed twice, and the series eventually stops being partial

#### Scenario: A partial series completes over later visits
- **WHEN** I reopen a series that was left partial
- **THEN** it is rebuilt, spends its budget on members still missing, and eventually stops being partial

#### Scenario: Lean members are expanded within budget
- **WHEN** a member's metadata was cached by season or top-anime browsing and fetch budget remains
- **THEN** it is re-fetched so its own relations can extend the series and inform main-line classification

#### Scenario: Lean members are still included when budget runs out
- **WHEN** a member's metadata was cached by season or top-anime browsing and no fetch budget remains
- **THEN** it is included in the series without spending a fetch, using only what other members' relations say about it

#### Scenario: A truncated series does not refetch on every visit
- **WHEN** I open a series that was stored as truncated and make no rebuild request
- **THEN** it is served from stored data without spending fetch budget

#### Scenario: Concurrent opens fetch once
- **WHEN** two requests for the same series arrive while it is being built
- **THEN** one build runs and both requests are served from it

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

1. `Completed` — every member of the series has finished airing, at least one main-line entry has finished airing, and every main-line entry that has finished airing is marked **Completed or Rewatching** in my list.
2. `Dropped` — among main-line entries that have aired (finished airing or currently airing), at least one is marked Dropped in my list, and no aired main-line entry released after the most recently aired such drop has ever been watched at all. A drop I later watched past — some later aired main-line entry has any watched episodes — does not count; the badge describes where I stand today, not history.
3. `Caught up` — I have watched at least one main-line episode, and the total I have watched across the aired main line meets the total that has actually broadcast across it.
4. `N behind` — I have watched at least one main-line episode, but fewer than have broadcast across the aired main line. N SHALL be the total broadcast main-line episodes minus the total I have watched, summed across every aired main-line entry — not only a currently-airing one.
5. `Unwatched` — at least one main-line entry has aired, I have watched none of the main line at all, and rule (2) does not already apply.
6. No badge — when nothing in the main line has aired yet, or when a currently-airing main-line entry's broadcast episode count is unknown and rules (3)/(4) cannot otherwise be resolved.

A main-line entry marked **Rewatching** SHALL count as **fully watched** throughout this precedence — as the greater of its own episodes-watched figure and its aired-episode figure, rather than as its current in-progress count. Entering Rewatching resets episodes-watched to zero, so without this rule a franchise I have seen in full and am part-way through watching again would read `N behind`, `Unwatched`, or `Dropped` on the strength of a reset counter. `Rewatching` also satisfies rule (1) alongside `Completed`, so starting a rewatch of a finished franchise SHALL NOT downgrade its badge from `Completed`.

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

#### Scenario: Rewatching a finished franchise does not lose the Completed badge
- **WHEN** every member of a series has finished airing, I have completed every main-line entry, and I then mark its first season Rewatching with two episodes watched
- **THEN** the header still shows "Completed", not "Caught up" and not a behind count

#### Scenario: A rewatch in progress does not read as behind
- **WHEN** a series' three finished main-line seasons total 36 broadcast episodes, I have completed the second and third, and the first is marked Rewatching with two of its twelve episodes watched
- **THEN** the header does not show "10 behind"; the rewatching season counts as fully watched

#### Scenario: A rewatch in progress does not read as unwatched
- **WHEN** a series' only main-line entry has finished airing and is marked Rewatching with zero episodes watched
- **THEN** the header does not show "Unwatched"

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

#### Scenario: A rewatch after a drop counts as watching past it
- **WHEN** I marked an early main-line entry Dropped and a later main-line entry is marked Rewatching with zero episodes watched
- **THEN** the header does not show "Dropped", because the rewatching entry counts as watched

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

### Requirement: Series stats
The series page SHALL show, for the main line: total episode count, total runtime, and my progress through it — episodes watched against total, entries completed against total, time watched, and time left to finish. Extras' episode count and runtime SHALL be reported separately rather than folded into the main-line totals.

Wherever this page counts **my watched main-line episodes** — the progress bar's watched fill, the named watched figure beside it, time watched, and therefore time left — a main-line entry marked **Rewatching** SHALL count as fully watched, as the greater of its own episodes-watched figure and its aired-episode figure. This is the same rule the header's personal badge applies, so the badge and the progress figures directly beside it can never disagree about the same entry. It governs only the watched side of each pair: the episode total, the aired figure, and the runtime describe the anime rather than me, and SHALL be unchanged by a rewatch in progress. It does not multiply anything by a rewatch count — a rewatched entry still counts once.

The entries-completed stat SHALL cover the extras as well as the main line, as two separately labelled figures within one stat: how many main-line entries I have completed out of the main-line total, and how many extras I have completed out of the extras total. The two SHALL NOT be summed into a single figure, so which half of the series is unfinished stays visible. When the series has no extras, the extras figure SHALL be omitted and the stat SHALL show the main-line figure alone rather than an "0 of 0". An entry marked Rewatching SHALL count as completed in this stat, since a rewatch can only follow a completed run.

Time watched and time left SHALL be shown only while there is time left to watch. When time left computes to zero — I have watched at least as much of the main line as its runtime accounts for — neither stat SHALL be rendered, since "0min left" alongside a time watched that equals the runtime restates what the entries-completed and progress figures already say. Both SHALL be withheld together: the page SHALL NOT show time watched with time left hidden, or the reverse.

This withholding SHALL NOT apply when the main-line runtime is itself unknown — a zero runtime total that the page already marks as unknown rather than as an exact figure. A zero time left derived from a runtime nobody knows reports missing data, not a series I have finished, so both stats SHALL still be shown in that case.

Runtime SHALL be computed as episodes times the entry's average episode duration, falling back to the app's existing 24-minutes-per-episode assumption when a duration is unknown, and SHALL be formatted in days, hours and minutes (e.g. `4d 6h 30min`). When any counted entry's episode count is unknown, the page SHALL mark the total as a lower bound rather than presenting it as exact. An entry whose total episode count is unknown SHALL still contribute its known aired-so-far episode count toward that lower bound, rather than contributing nothing, whenever an aired count is known for it — so a still-airing entry with no announced total makes the lower bound tighter instead of forcing the whole stat to read as wholly unknown. Watched time SHALL count watched episodes only and SHALL NOT multiply by rewatch count.

While any member of the series is currently airing, my progress SHALL be shown with the same broadcast-progress bar the home page uses — episodes aired so far as the primary fill, my watched episodes layered on top of it — so it is visible how much of what has aired I have seen. When no member is airing, my progress SHALL use the plain watched-against-total bar, since aired and total are then the same figure.

The progress figures SHALL be named rather than left to be inferred from a bare `x/y` label: the page SHALL state my watched episode count, the episodes aired so far, and the main-line total as three separately named figures, each keyed to the colour of the fill it describes. The aired figure SHALL be shown only while a member of the series is currently airing, since it is otherwise the same number as the total, and the total SHALL carry its lower-bound marker here exactly as it does in the episode total.

Episodes aired SHALL be summed over exactly those main-line entries whose total episode count is known, counting an entry that has finished airing as its full total, a currently airing entry as the episodes it has aired so far, and an entry that has not yet aired as none — so the aired figure can never exceed the total the page shows.

The page SHALL additionally show the highest MAL-scored entry, my highest-scored entry, and the studios and genres the series spans.

The page SHALL additionally show which entry or entries are tied for the most rewatches across the series (main line and extras alike), naming each tied entry and its rewatch count. This stat SHALL NOT be shown at all when no member of the series has been rewatched, rather than showing a stat naming zero-rewatch entries. It SHALL keep counting recorded rewatches — completed runs — so a rewatch in progress does not alter it.

Where several entries tie for the highest MAL score, for my highest score, or for the most rewatches, the page SHALL list every tied entry rather than picking one. Tied highest-MAL entries and tied most-rewatched entries SHALL be listed in watch order. Tied favourites SHALL be listed in my saved favourite order, with entries I have not ordered following in watch order.

The highest MAL score SHALL be shown in full rather than blurred when the entry holding it is one I have marked **Completed** or **Dropped** in my list. Both statuses settle my relationship with that entry — dropping a show is as much a decision about it as finishing one — so neither leaves a viewing ahead of me that naming the series' best entry could spoil. My having scored that entry SHALL NOT be required: a dropped entry frequently carries no score of mine, and withholding the stat until one exists would hide it indefinitely. Until the entry reaches one of those two statuses, the page SHALL withhold that entry's title and link entirely — not only its score — so an entry I have not settled is never named by this stat; this withholding applies regardless of the hide-scores toggle's own state, since it protects against spoiling which entry is best rather than against exposing a score value.

#### Scenario: Runtime of the main series
- **WHEN** I open a series whose main line totals 62 episodes averaging 24 minutes
- **THEN** the page shows the main-line episode total and a runtime of "1d 0h 48min"

#### Scenario: Extras counted separately
- **WHEN** a series has 5 specials beyond its main line
- **THEN** their episode count and runtime appear as their own figures, not inside the main-line totals

#### Scenario: Unknown episode counts are marked
- **WHEN** a main-line entry is currently airing with an unpublished total episode count
- **THEN** the runtime total is presented as a lower bound rather than an exact figure

#### Scenario: An unpublished total still counts what has aired
- **WHEN** a main-line entry is currently airing with no published total episode count but 1,100 episodes are known to have aired for it
- **THEN** the main-line episode total includes those 1,100 aired episodes rather than contributing zero for that entry, and the total is still marked as a lower bound

#### Scenario: My progress through the series
- **WHEN** I have watched 38 of a series' 62 main-line episodes and nothing is airing
- **THEN** the page shows a progress bar with my 38 watched episodes and the 62 total each named, how many entries I have completed, my time watched, and the time left to finish

#### Scenario: A rewatching entry keeps the progress bar full
- **WHEN** a series' 62 main-line episodes are all watched and one 12-episode season is marked Rewatching with two episodes watched
- **THEN** the progress bar still reads 62 of 62 watched, matching the "Completed" badge beside it, rather than dropping to 52

#### Scenario: A rewatching entry counts as a completed entry
- **WHEN** I have completed five of a series' six main-line entries and marked the sixth Rewatching
- **THEN** the entries-completed stat reads "6 of 6"

#### Scenario: The episode total is unmoved by a rewatch
- **WHEN** a main-line entry is marked Rewatching
- **THEN** the main-line episode total, the aired figure, and the runtime are exactly what they were before the rewatch began

#### Scenario: Entries completed covers extras too
- **WHEN** I open a series where I have completed 5 of 6 main-line entries and 2 of its 3 extras
- **THEN** the entries-completed stat shows "5 of 6" for the main line and "2 of 3" for the extras as two labelled figures, rather than one combined "7 of 9"

#### Scenario: A series with no extras shows one figure
- **WHEN** I open a series whose every member is main line
- **THEN** the entries-completed stat shows only the main-line figure, with no extras figure beside it

#### Scenario: A finished series hides the time stats
- **WHEN** I open a series whose main line I have watched in full, so no time is left
- **THEN** neither "Time left" nor "Time watched" is shown

#### Scenario: A part-watched series keeps both time stats
- **WHEN** I open a series with main-line episodes I have not yet watched
- **THEN** both "Time watched" and "Time left" are shown

#### Scenario: An unknown runtime is not mistaken for a finished series
- **WHEN** I open a series whose main-line runtime total is unknown, so it reports zero time left without my having watched it through
- **THEN** both "Time watched" and "Time left" are still shown, because the zero reflects a runtime nobody knows rather than a series I have finished

#### Scenario: Broadcast progress while a season is airing
- **WHEN** a series' latest season is currently airing, 12 of its episodes have aired, and earlier seasons total 50 episodes
- **THEN** my progress shows a bar whose aired fill covers 62 episodes with my watched episodes layered on top of it, and the watched, aired, and total figures are each named beside it

#### Scenario: The aired figure is only shown while something is airing
- **WHEN** I open a series where no member is currently airing
- **THEN** the progress readout names my watched count and the total, and does not repeat the total as a separate aired figure

#### Scenario: Tied highest MAL scores list every entry
- **WHEN** two seasons of a series share the same highest MAL score
- **THEN** both are listed under the highest MAL score, in watch order

#### Scenario: Highest MAL score of a completed entry is not blurred
- **WHEN** the hide-scores toggle is on and the highest-MAL-scored entry is one I have completed
- **THEN** its title, link, and MAL score are shown in full

#### Scenario: Highest MAL score of a dropped entry is shown
- **WHEN** the highest-MAL-scored entry of a series is one I have marked Dropped, and I never gave it a score of my own
- **THEN** the stat names that entry, links to it, and shows its MAL score, exactly as it would for a completed entry

#### Scenario: An unsettled entry's title is withheld from Highest MAL score
- **WHEN** the entry holding the series' highest MAL score is one I am Watching, have On-hold, Plan to watch, or do not have in my list at all
- **THEN** the page shows no title or link for that entry in the Highest MAL score stat, regardless of whether the hide-scores toggle is on or off

#### Scenario: Tied favourites list every entry
- **WHEN** I have given the same highest score to three entries of a series
- **THEN** all three are listed as my favourite

#### Scenario: Most rewatched entry is shown
- **WHEN** one entry in a series has a rewatch count of 3 and no other member has a higher rewatch count
- **THEN** the page shows a "Most rewatched" stat naming that entry and its count of 3

#### Scenario: Tied most-rewatched entries list every entry
- **WHEN** two entries in a series share the same highest rewatch count
- **THEN** both are listed under "Most rewatched", in watch order

#### Scenario: No rewatch stat when nothing has been rewatched
- **WHEN** no member of a series has a rewatch count above zero
- **THEN** the page shows no "Most rewatched" stat at all

## ADDED Requirements

### Requirement: The More section's view state is restored with the page
The More section's three view controls — the "in my list" filter, each group's collapsed state, and each group's exemption from the filter — SHALL be part of the series page's restorable state, restored on back/forward navigation exactly as every other page's view controls are, per the `page-state-restoration` capability.

Returning to a series page by back/forward navigation SHALL therefore show the More section as it was left: a group opened in full is still open, a collapsed group is still collapsed, and the "in my list" control still reports the state it reported when the page was left.

A fresh visit — a link, a typed URL, a reload — SHALL still open the section on its documented default: the filter on, every group expanded, and no group exempted. The state SHALL NOT be persisted beyond the browser tab's application session, and SHALL NOT be shared between two different series' pages.

A group key held in restored state that matches no group the restored page renders — because the series' extras changed between the two renders — SHALL be ignored rather than treated as an error.

#### Scenario: An opened group is still open on return
- **WHEN** I open a More group in full, open one of its extras, and navigate back
- **THEN** that group is still showing all of its tiles, and the "in my list" control still reads as off

#### Scenario: The filter is not reset by a round trip
- **WHEN** I turn the "in my list" filter off, open an entry, and navigate back
- **THEN** the filter is still off and every extra is still shown

#### Scenario: A collapsed group is still collapsed on return
- **WHEN** I collapse a group, navigate away, and navigate back
- **THEN** that group renders no tiles

#### Scenario: A fresh visit still opens on the default
- **WHEN** I reach a series page by following a link rather than by navigating back
- **THEN** the "in my list" filter is on, every group is expanded, and no group is exempted

#### Scenario: Two series do not share More-section state
- **WHEN** I open one series and expand its More section, then open a different series
- **THEN** the second series' More section opens on the default, unaffected by the first

### Requirement: Opening a More group scrolls it to the top of the viewport
When a More group is opened from its heading, the page SHALL scroll so that group's heading sits at the top of the viewport, so the tiles that were just revealed are what fills the screen rather than remaining below the fold.

When the group is too near the end of the page for its heading to reach the top — the page cannot scroll that far — the page SHALL scroll as far as it can, leaving the heading as high as the page allows rather than not scrolling at all.

The scroll SHALL happen after the revealed tiles have been laid out, since those tiles are what makes the page tall enough to reach the target.

Collapsing a group from its heading SHALL NOT scroll the page: the user is dismissing content, not asking to be moved.

The "in my list" control and the expand/collapse-all control SHALL NOT scroll the page either. They act on every group at once, so there is no single group to scroll to.

#### Scenario: An opened group is brought to the top
- **WHEN** I activate the heading of a group part-way down a long series page
- **THEN** the page scrolls so that group's heading is at the top of the viewport, with its tiles below it

#### Scenario: The last group scrolls as far as the page allows
- **WHEN** I activate the heading of the final group, whose tiles do not fill a screen
- **THEN** the page scrolls to its bottom, leaving that heading as high as it can go rather than staying where it was

#### Scenario: Collapsing does not move the page
- **WHEN** I activate the heading of a group that is showing all of its tiles
- **THEN** the group collapses and the page does not scroll

#### Scenario: The section-wide controls do not scroll
- **WHEN** I activate "Expand all" or the "in my list" control
- **THEN** every group updates and the page's scroll position is unchanged
