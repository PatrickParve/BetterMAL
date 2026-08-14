## ADDED Requirements

### Requirement: Top series single-entry filter
Top series SHALL offer a control that hides every series whose **main line** holds a single entry, and shows them again when it is switched off. The control SHALL default to showing them, so the section lists the same series it lists today until the control is used.

What counts SHALL be the number of main-line entries, not the series' total member count: a series with one main-line entry plus any number of extras — OVAs, specials, side stories — SHALL count as single-entry and SHALL be hidden while the filter is on. A series with two or more main-line entries SHALL remain listed regardless of how many extras it has.

A main-line entry that has been announced but has not yet aired a single episode SHALL NOT count toward that total: a series is multi-entry only once a second main-line entry has actually started airing, not merely once it has been confirmed.

The control SHALL narrow which series are listed and SHALL NOT change the order of those that remain, nor what any tile shows.

The control SHALL be independent of the ranking basis: switching one SHALL NOT reset the other, and a series SHALL be listed only when it survives both — it has a value under the selected basis and, while the filter is on, more than one main-line entry.

The control SHALL make its current state apparent without requiring the user to compare the strip before and after pressing it.

#### Scenario: Default lists single-entry series
- **WHEN** I open the profile page without having used the control
- **THEN** Top series lists every eligible series, single-entry ones included, exactly as it does today

#### Scenario: Hiding single-entry series
- **WHEN** I switch the filter on
- **THEN** every series whose main line holds a single entry disappears from the strip, and the multi-entry series remain in the same relative order

#### Scenario: Extras do not make a series multi-entry
- **WHEN** the filter is on and a series has one main-line entry plus several extras in my list
- **THEN** that series is hidden, because only its main-line count is considered

#### Scenario: A franchise with extras stays listed
- **WHEN** the filter is on and a series has three main-line seasons plus extras
- **THEN** that series remains in the strip

#### Scenario: An announced-but-unaired sequel doesn't make a series multi-entry
- **WHEN** the filter is on and a series has one main-line entry that has aired plus a second main-line entry that's announced but hasn't aired a single episode
- **THEN** that series is hidden, because the second entry doesn't count until it starts airing

#### Scenario: Showing them again
- **WHEN** I switch the filter back off
- **THEN** the single-entry series reappear in the strip in their ranked positions

#### Scenario: Filtering is immediate
- **WHEN** I switch the filter
- **THEN** the strip updates immediately without reloading the page's data and without collapsing

#### Scenario: Composing with the ranking basis
- **WHEN** the filter is on and I switch the ranking basis to MAL's score
- **THEN** the strip reorders by MAL's main-series average and single-entry series stay hidden

#### Scenario: The control shows its state
- **WHEN** the filter is on
- **THEN** the control reads as active rather than looking identical to its off state

### Requirement: Top series single-entry filter is restored on back-navigation
The single-entry filter SHALL behave like the Top series ranking basis: it SHALL reset to its default — single-entry series shown — on a fresh visit to the profile page, and SHALL be restored when returning to the page by back-navigation.

#### Scenario: Filter resets on a fresh visit
- **WHEN** I switch the filter on, navigate away, and later open the profile page fresh
- **THEN** single-entry series are listed again

#### Scenario: Filter is restored going back
- **WHEN** I switch the filter on, open a series from the strip, and press back
- **THEN** single-entry series are still hidden

#### Scenario: Independent of the ranking basis
- **WHEN** I switch the filter on and then change the ranking basis
- **THEN** the filter is still on, and switching the filter afterwards leaves the basis where I set it

## MODIFIED Requirements

### Requirement: Top series empty and incomplete states
When no series is eligible, the section SHALL say so rather than rendering an empty strip.

Because a series is only known once it has been built, the section SHALL disclose that its coverage grows over time, and its empty state SHALL point at the Settings action that builds every series from my list (see the `series-page` capability) rather than leaving the absence unexplained.

Reading the section SHALL never block on building a series and SHALL never fail because a series is missing — a series not yet built is simply not listed yet.

When series are eligible but the single-entry filter leaves none to show, the section SHALL say that the filter is what emptied it, so the state reads as a filter effect rather than as missing data, and SHALL NOT point at the Settings build action.

#### Scenario: Nothing to rank yet
- **WHEN** no series with a member in my list has been built
- **THEN** the section explains that series are still being discovered and names the Settings action that builds them all, instead of showing an empty strip

#### Scenario: Nothing rankable under the selected basis
- **WHEN** series are eligible but none has a value under the selected basis
- **THEN** the section says so for that basis rather than showing an empty strip

#### Scenario: Reading never waits on a build
- **WHEN** I open the profile page while series are still being built in the background
- **THEN** the section renders immediately with whatever series are already known

#### Scenario: The filter hid everything
- **WHEN** the single-entry filter is on and every series rankable under the selected basis has a single main-line entry
- **THEN** the section says the filter left nothing to show, rather than showing an empty strip or blaming missing series data
