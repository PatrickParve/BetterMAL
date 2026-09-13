## MODIFIED Requirements

### Requirement: A run always ends as complete or failed

Every run SHALL end as **complete** or **failed**. A run whose work throws SHALL be reported as failed, with the progress it had reached, whether it throws before its total is known or partway through. No run SHALL stay reported as running once its work has stopped.

A run that gets through its work but cannot do all of it SHALL end as failed rather than complete, saying how many items were left. This covers:
- edits it could not send
- held changes that stayed held
- anime it could not fetch or refresh
- series it could not build
- anime it left out because MyAnimeList gave a list status the app does not recognize (see `mal-write-sync`, "A MyAnimeList list status the app does not recognize is never guessed")

Such a run SHALL still process every item it can. Ending as failed reports the items left undone and does not stop the run early.

An item that was handled and simply had nothing to store is **not** left undone. This covers an anime AniList has no airing data for, and an anime whose story relations lead to no other anime. It SHALL NOT make a run fail.

A run stopped because the application itself is stopping is exempt: its state is lost with the process.

#### Scenario: A crash is reported
- **WHEN** the full airing-date refresh throws partway through
- **THEN** it is reported as failed with the progress it had reached, and not as running

#### Scenario: A crash before the total is known
- **WHEN** a job throws before it has found how much work it has
- **THEN** it is reported as failed rather than left running

#### Scenario: A partial run is not complete
- **WHEN** sync now sends 3 of 5 edits and the other 2 fail
- **THEN** it ends as failed, saying that 2 of 5 could not be sent

#### Scenario: An airing-date refresh with failed anime is not complete
- **WHEN** the full airing-date refresh processes 595 anime and the refresh of 3 of them fails
- **THEN** it still processes all 595, and ends as failed, saying that 3 of 595 could not be refreshed

#### Scenario: Anime with no airing data do not fail the refresh
- **WHEN** the full airing-date refresh processes 595 anime, AniList has no airing data for 12 of them, and none fail
- **THEN** it ends as complete

#### Scenario: A bulk series build with failed targets is not complete
- **WHEN** build all series processes 40 targets and building 2 of them fails
- **THEN** it still processes all 40, and ends as failed, saying that 2 of 40 could not be built
