## ADDED Requirements

### Requirement: A picture fades in as it arrives
On every row picture slot, card, tile and poster box this capability governs, a picture that is still downloading SHALL leave its box showing the box's own placeholder surface, and SHALL fade in over that surface once it has loaded, together with any fill its shape takes. It SHALL NOT pop in abruptly, and SHALL NOT be drawn partially as it downloads.

A picture the browser already holds when its surface is drawn, for example after going back to a page, SHALL be shown at once with no fade. So SHALL a picture that finishes downloading within a short delay of its surface being drawn, about 150 ms: the fade marks a picture that was visibly slow, and on a quick one it would only add its own duration to the wait.

The fade SHALL change only how a picture appears. The box's size and position, the picture's shape classification, and when the picture is requested SHALL all stay exactly as this capability already requires, so a grid of posters still does not move as its pictures arrive.

For users who ask for reduced motion, a loaded picture SHALL appear without animating.

A picture that fails to load SHALL leave the box showing its placeholder surface.

#### Scenario: A slow picture fades in
- **WHEN** a season card's picture finishes downloading more than about 150 ms after the card was drawn
- **THEN** the picture fades in over the card's picture box instead of appearing abruptly

#### Scenario: A held picture shows at once
- **WHEN** I go back to a page whose pictures the browser already holds
- **THEN** those pictures are drawn immediately, wide artwork included, with no fade

#### Scenario: A quick picture shows at once
- **WHEN** a card's picture finishes downloading within about 150 ms of the card being drawn, for example from the browser's disk cache on a fresh page load
- **THEN** the picture is drawn immediately with no fade

#### Scenario: The grid still does not move
- **WHEN** a grid of posters fades its pictures in
- **THEN** no card changes size or position

#### Scenario: Reduced motion
- **WHEN** my system asks for reduced motion and a card's picture finishes downloading
- **THEN** the picture appears without a fade
