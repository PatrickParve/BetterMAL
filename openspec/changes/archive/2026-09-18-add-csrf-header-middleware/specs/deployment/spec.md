## ADDED Requirements

### Requirement: Mutating API requests must prove they came from the app

The backend SHALL refuse any request whose path is under `/api` and whose method is not `GET` unless it carries the header `X-Requested-With: BetterMAL`. A refused request SHALL be answered `403 Forbidden` and SHALL be stopped before any controller or service runs, so that nothing it asks for is read, written, or pushed to MyAnimeList. The value SHALL be matched exactly: a missing header, an empty one, or any other value is refused.

The web client SHALL send that header on every non-`GET` request it makes to the backend. It SHALL be added at the single point every request passes through, rather than at each call site, and SHALL be merged with whatever headers the caller already set, so that a request carrying its own `Content-Type` keeps it.

`GET` SHALL be exempt on both sides: the backend SHALL NOT require the header on a `GET`, and the client SHALL NOT add it to one, so that reads go out exactly as they do today and stay collapsible by URL alone.

This exemption is a deliberate limit on what the requirement covers, not a claim that reads have no effects. Some `GET` endpoints do write — `GET /api/sync/held` clears held changes MyAnimeList already agrees with, and several browse reads fill caches — so a cross-site `GET` can still trigger those effects and the outbound MyAnimeList calls they make. What it cannot do is read the response, or reach anything that pushes a change to MyAnimeList or deletes list data.

That last point is what the exemption rests on, so it SHALL hold rather than merely happen to be true: no endpoint served over `GET` under `/api` SHALL push a change to MyAnimeList, delete a list entry, or discard unsent local edits. Filling a cache or reconciling state as a side effect of a read is permitted. This SHALL be enforced against drift, so that adding or renaming a `GET` route under `/api` cannot quietly widen what a cross-site request can reach.

This closes cross-site request forgery. The mutating endpoints bind nothing from the request body, so a cross-site form submit or a `no-cors` fetch is a CORS "simple request" that needs no preflight and reaches them today. A browser will not attach a custom header to a cross-site request without first sending a preflight, and no preflight can succeed here, because no `Access-Control-Allow-Origin` is ever sent. The header is therefore only ever present on a request the app itself made.

#### Scenario: A hostile page submits a cross-site form

- **WHEN** a page on another origin submits a form or issues a `no-cors` fetch to `POST /api/sync/held/accept`, carrying no `X-Requested-With` header
- **THEN** the backend answers `403 Forbidden`, the controller and its service never run, and no held change is accepted or pushed to MyAnimeList

#### Scenario: A cross-site request guesses at the header

- **WHEN** a non-`GET` request to an `/api` path carries `X-Requested-With` with any value other than `BetterMAL`
- **THEN** it is refused with `403 Forbidden` before reaching a controller

#### Scenario: The app's own mutations keep working

- **WHEN** the web client edits a list entry, runs a sync, accepts or declines a held change, or reorders a ranking
- **THEN** the request carries `X-Requested-With: BetterMAL` and is served exactly as before

#### Scenario: An import carries both its own header and this one

- **WHEN** the web client posts a transfer file to `/api/transfer/import`, a call that sets its own `Content-Type: application/json`
- **THEN** the request carries both that `Content-Type` and `X-Requested-With: BetterMAL`, and the import runs

#### Scenario: A read needs no header

- **WHEN** a `GET` request is made to any `/api` path with no `X-Requested-With` header
- **THEN** it is served as before, unaffected by this check

#### Scenario: A new read endpoint could widen the exemption

- **WHEN** a `GET` route under `/api` is added, renamed, or removed
- **THEN** the change fails its tests until the new set of read endpoints is reviewed and recorded, so that no `GET` gains a destructive effect unnoticed

#### Scenario: A method the app never uses

- **WHEN** an `OPTIONS` or `HEAD` request is made to an `/api` path without the header
- **THEN** it is refused with `403 Forbidden`, which is a refusal either way — no route maps either method
