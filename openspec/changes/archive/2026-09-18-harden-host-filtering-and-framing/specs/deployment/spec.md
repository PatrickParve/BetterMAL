## ADDED Requirements

### Requirement: Only loopback hostnames are served

Both layers that accept HTTP requests SHALL serve only requests whose `Host` header names `localhost` or `127.0.0.1`, and SHALL refuse every other hostname. This holds on both run paths and on whatever port `BACKEND_PORT` selects: the hostname is what is matched, never the port.

The backend SHALL take its allowed hostnames from the `AllowedHosts` configuration key, which SHALL list `localhost` and `127.0.0.1` rather than the wildcard `*`, so that a single setting covers the native Development run and the Docker/Production run alike. A request carrying any other `Host` SHALL be refused before it reaches a controller, so that no list entry, transfer import, or export can be reached by it.

The frontend SHALL serve the app and proxy `/api/` only under those two hostnames. A request carrying any other `Host` SHALL have its connection closed without a response, and SHALL be neither proxied to the backend nor answered with any part of the frontend.

This closes DNS rebinding: a page served from another origin that re-points its own hostname at the loopback address is refused by both layers, even though the browser treats its requests as same-origin and so never sends a CORS preflight.

#### Scenario: A rebound hostname reaches the backend

- **WHEN** a request arrives at the backend with a `Host` header of `evil.example`, with or without a port
- **THEN** it is refused before reaching a controller, and no list entry, transfer import, or export is read or written

#### Scenario: A rebound hostname reaches the frontend

- **WHEN** a request arrives at the frontend with a `Host` header of `evil.example`
- **THEN** the connection is closed with no response, and the request is neither proxied to `/api/` nor served any frontend file

#### Scenario: The documented access paths keep working

- **WHEN** the app is reached at `localhost` or `127.0.0.1` on the port `BACKEND_PORT` selects, under Docker Compose or run natively
- **THEN** the backend serves the request as before, and the frontend serves the app and proxies `/api/` as before

### Requirement: The frontend refuses to be framed

Every response the frontend serves SHALL carry `X-Frame-Options: DENY`, so that no other origin can embed the UI in a frame and overlay it to capture clicks. This SHALL hold for the SPA shell, for hashed assets, and for responses proxied through `/api/`, and SHALL NOT displace the cache-control behaviour those responses already have.

#### Scenario: The SPA shell is framed

- **WHEN** a response for `/` is served
- **THEN** it carries `X-Frame-Options: DENY` alongside its existing `Cache-Control: no-cache`

#### Scenario: A hashed asset is framed

- **WHEN** a response for a file under `/assets/` is served
- **THEN** it carries `X-Frame-Options: DENY` alongside its existing `Cache-Control: public, max-age=31536000, immutable`

#### Scenario: A proxied API response

- **WHEN** a response is proxied through `/api/`
- **THEN** it carries `X-Frame-Options: DENY`

#### Scenario: Another origin embeds the app

- **WHEN** a page on another origin loads the app in an `<iframe>`
- **THEN** the browser refuses to render the frame
