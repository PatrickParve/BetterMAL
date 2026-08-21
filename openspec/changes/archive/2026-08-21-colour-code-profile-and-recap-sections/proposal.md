## Why

The recap page already speaks in colour — blue is MAL, purple is mine, the podium runs gold/silver/bronze, and the score board tiers a period from an apex 10 down to bronze 1 — but that language stops at the section boundary. Every section heading on both the recap and the profile page is the same neutral `--text-h`, so "Biggest Hot takes" reads exactly like "Season ranking", and a pair of lists whose whole point is *whose* opinion they carry ("They liked it, I didn't" / "I liked it, they didn't") is drawn in the one colour that says neither. Inside the sections the language lapses too: the recap's rating distribution paints all ten bars in the flat accent even though the score board directly beside it has already assigned every one of those scores a tier colour; ranks 6–10 of the top 10 drop their score to neutral text the moment they fall off the podium, losing the purple/blue role the five cards above them carry; and the **MAL score** button that switches a ranking to the community's opinion lights up purple — the colour this app uses for *my* score — on both pages.

## What Changes

- **Section titles are tinted by what they are about.** Seven headings take a two-colour gradient drawn across the width of the section they head, so the title's letters pick up the leading slice of a ramp that spans its list:
  - Profile — **"They liked it, I didn't"** in the MAL blue family and **"I liked it, they didn't"** in the mine-purple family, the same two colours the recap's "MAL liked it more" / "I liked it more" labels already carry, so the pair reads as the same claim in two directions.
  - Profile — **"Favourite years"** in an achromatic silver-and-black family, **"Favourite seasons"** in an orange-and-yellow family.
  - Recap — **"Year ranking"** and **"Years by time watched"** in that same achromatic family; **"Season ranking"** and **"Seasons by time watched"** in the same orange-and-yellow one, so a year-level ranking is tellable from a season-level one before its text is read.
  - Recap — **"Biggest Hot takes"** in a fiery red-to-orange family of its own.
- **The year family is the one with no hue.** It runs silver to black in the light theme and silver to white in the dark one — the theme's own extreme at the far end — so a year-level section is marked by the *absence* of colour among four coloured families. It opens at silver rather than at the near-black an untinted heading uses, so a tinted year title is never mistakable for one nobody tinted.
- **The recap's Yearly and Season mode tabs take their family's colours.** Selected, hovered, focused, and hovered-while-selected all keep exactly the treatment they have today; only the colour changes — Yearly silver-to-black, Season orange-to-yellow, tying each tab to the ranking titles it produces. **Multi-year** keeps today's purple accent.
- **A MAL-score control is blue on both pages.** The recap's top-10 ranking-basis toggle and the profile's Top series ranking-basis tabs light their **MAL score** option in the app's MAL blue rather than the purple accent; **My score** stays purple, which it already is. A control that selects a score role now wears that role's colour.
- **Ranks 6–10 of the recap's top 10 keep the score colour.** Each row's score renders in purple under the my-score basis and blue under MAL's, matching the podium cards above it, instead of falling back to neutral text at rank 6.
- **The recap's rating distribution is coloured by score-board tier.** Each row's bar and its score numeral take the tier colour that score's slot carries on the score board — 10 apex, 9 red, 8 blue, 7 gold, 6–5 silver, 4–1 bronze — so the block beside the **Score board** button and the board it opens describe the same ladder in the same colours. Counts and shares stay neutral. The profile page's all-anime distribution is deliberately left on today's flat accent bars.

## Capabilities

### New Capabilities

- `section-colour-language`: the cross-page colour families a section can be drawn in — the achromatic year family, season, hot-take, and the existing MAL/mine score roles — and the tinted-title treatment that carries them: a two-colour gradient measured across the section's width, legible in both themes, never the only thing that identifies a section.

### Modified Capabilities

- `list-recaps`:
  - "The recap's segmented controls show every state" — the state treatments stay identical, but the recap-type tabs no longer all share one colour: Yearly and Season carry their own families across every state while Multi-year and the time filter keep the accent.
  - "Top 10 ranking basis control" — the **MAL score** option carries the MAL colour role and **My score** the mine role.
  - "Top 10 of the period" — a row's score at ranks 6–10 carries the same score colour role the podium's cards carry.
  - "Recap rating distribution" — bars and score numerals are coloured by the score board's tier scale, so the distribution and the board agree; the mean-free, count-and-share row structure is unchanged.
  - A new requirement, **"Recap section titles are tinted by family"**, names which of the page's headings are tinted and which stay neutral — Hot takes, both season-level rankings, and both year-level rankings are; Top N, Stats, and Rating distribution are not. It adds a concern the existing "Hot takes", "Season ranking", "Year ranking", and "Most time watched ranking" requirements say nothing about, so none of them changes.
- `profile-stats`:
  - A new requirement, **"Profile section titles are tinted by family"**, covers the two divergence titles and the two favourites titles, leaving "Opinion divergence lists" and "Favourite seasons and years" — which govern membership, ordering, and layout — untouched.
  - "Top series ranking basis" — the **MAL score** option carries the MAL colour role, scoped so the media-type tabs sharing that control form keep the accent.
- `score-presentation`:
  - One new requirement — a control that selects between the two score roles SHALL carry the colour of the role it selects — extending the roles from score figures to the controls that choose between them. "App-wide MAL/mine score colour roles" is left exactly as written: the year family is achromatic, so no green is introduced, and green goes on meaning "on air right now and nothing else". Nothing in this change is breaking.

## Impact

- **Frontend only, presentation only.** `index.css` (new family tokens in both themes), `RecapPage.css`, `RecapPage.tsx` (family modifier on the Yearly/Season tabs and the basis toggle, score-role class on rows 6–10), `ProfilePage.css`, `ProfilePage.tsx` (family modifiers on the two divergence titles and the basis tabs), `RankingSection.tsx`/`.css` (a family prop so a ranking's title can be tinted), `ScoreDistribution.tsx`/`.css` (opt-in tier colouring), and a shared title rule the two pages read from.
- **No backend, DTO, API, or data change.** Nothing new is fetched or computed; every figure being coloured is already on screen.
- **Shared components need opt-in, not replacement.** `ScoreDistribution` renders both pages' distributions and `RankingSection` renders both pages' rankings — the tier colouring and the title tinting each arrive as a prop the caller passes, so the profile page's distribution and any untinted ranking render byte-identically to today.
- **`.profile-media-tabs__tab--active` and `.recap-page__tab--active` are shared classes.** The media-type tabs ("All / TV / Movie / …") on My top anime and Most rewatched, and the recap's time filter, use the same classes as the controls being recoloured — the new colours must arrive as an added modifier on the specific controls, never as a change to the shared active rule.
- **Behaviour that must not regress**: hidden MAL scores stay hidden everywhere a score is recoloured (rows 6–10 keep rendering through `ScoreValue`); the recap's segmented controls keep their six distinct states and their fixed height, so the control cluster still never reflows; the distribution's row structure, counts, shares, hover highlight, and drill-through targets are untouched; the score board's own tier colours are read, not redefined.
- **Contrast is the real risk.** Five colour families now paint text, in a light and a dark theme, some of it through a gradient fill — every family needs a legible value in both themes, and the tinted-title technique needs a plain-colour fallback so a title can never render invisible where `background-clip: text` is unavailable or forced-colors is on.
- **The achromatic family forces a label-colour token.** Because the year family fills toward black in the light theme and toward white in the dark one, the selected Yearly tab cannot print a fixed white label the way every other tab does — the label colour has to follow the fill. That token is new, and it is the piece most likely to be missed: a dark-theme Yearly tab with the existing hard-coded white label would be white text on near-white.
