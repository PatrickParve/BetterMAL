## MODIFIED Requirements

### Requirement: Leftover space in a picture's box is filled from the picture itself

Wherever an upright or wide picture drawn whole leaves part of its box uncovered, that uncovered space SHALL be filled with a softened, dimmed, blurred rendering of the same picture, so the box reads as belonging to the artwork rather than as empty bands. This covers a fixed poster box, a grid card's picture area, a height-bound tile past its bound, and the series page's cards and tiles. A banner box is the exception: it draws every picture whole on its own flat background and mounts no fill for any shape. The recap podium's box is a narrower exception: it mounts no fill for any shape, and an upright or wide picture there is drawn whole and centred on the card itself, with no box of its own colour behind it.

The fill SHALL:

- sit behind the whole picture and never over any part of it;
- be decoration only, not interactive, focusable, or announced to assistive technology;
- cause no additional request, since it is the picture the box already shows;
- be legible in both the light and the dark theme without overpowering the card around it;
- leave the card's own colours, borders, badges and text exactly as they are.

A poster SHALL carry no fill. A box whose picture is drawn to its full extent shows none. A placeholder shown for an anime with no picture SHALL keep its existing appearance, including any transparency its surface requires.

#### Scenario: A landscape picture's spare space carries its colours
- **WHEN** a score board tile shows a landscape picture whole inside its poster box
- **THEN** the space above and below the picture is filled with a blurred, dimmed rendering of that same picture rather than a flat band

#### Scenario: A poster has no fill
- **WHEN** any card, tile or box shows a poster
- **THEN** it renders exactly as it does today, with no fill behind it

#### Scenario: A banner box has no fill
- **WHEN** the airing page's slot band shows a poster, a square picture or a very wide picture
- **THEN** the space around the picture shows the band's flat background, and no blurred rendering of the picture is drawn

#### Scenario: A podium picture that is not a poster has nothing behind it
- **WHEN** a recap podium card shows a landscape, square or upright picture
- **THEN** the picture is drawn whole and centred in the card's picture area, with no blurred rendering of it and no box of its own colour behind it, so only the card's own surface shows around it

#### Scenario: The fill costs no request
- **WHEN** a card shows an upright picture with the fill behind it
- **THEN** no request is made beyond the one that loaded the picture itself

#### Scenario: A missing picture is unchanged
- **WHEN** the recap podium's first-ranked anime has no picture
- **THEN** its placeholder looks exactly as it does today, and the card's animated sheen still shows through it

#### Scenario: Both themes
- **WHEN** I view a card with a filled picture box in the light theme and again in the dark theme
- **THEN** the picture stands clear of its fill, and the card's own colours and text read the same as its neighbours', in both
