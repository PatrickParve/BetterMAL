## ADDED Requirements

### Requirement: The detail page title wraps at spaces only

When an anime's title on the detail page is too long for one line, it SHALL wrap at a **space**. A word or token SHALL NOT be split across two lines: a token containing a hyphen, en or em dash, colon, slash, full stop, or any other punctuation SHALL move to the next line whole, together with whatever follows it, rather than being broken at that punctuation with a fragment left hanging at the end of the first line.

This SHALL hold at every window width and at every size of the app's fluid root font, and for a title of any number of lines — every break in it falls at a space.

Nothing else about the title changes: it SHALL keep the same font, size, weight, colour, and position in the page's header block, SHALL keep sharing its row with the related-entry controls, and SHALL still be the English-preferred display title. The text SHALL be selectable and copyable as the plain title it is, with no characters inserted into it that would appear in what is copied.

A single token too long to fit on a line by itself MAY still be broken, since the alternative is text running outside the page. This is a last resort for a token no ordinary title contains, not a licence to break tokens that would fit.

#### Scenario: A title with a colon wraps at a space
- **WHEN** the detail page shows a title long enough to need two lines, containing a colon mid-title
- **THEN** the break falls at a space, and no part of a word sits alone at the end of the first line

#### Scenario: A hyphenated token is not split
- **WHEN** a two-line title contains a hyphenated token such as "Re:ZERO -Starting Life in Another World-"
- **THEN** the token stays whole on one line rather than breaking at its hyphen or dash

#### Scenario: A one-line title is unaffected
- **WHEN** a title fits on one line
- **THEN** it renders exactly as it does today

#### Scenario: The title is still copyable
- **WHEN** I select the title and copy it
- **THEN** what I get is the title itself, with no extra or invisible characters

#### Scenario: The related controls stay put
- **WHEN** a title wraps to two lines
- **THEN** the related-entry controls remain on the title's row as they are today
