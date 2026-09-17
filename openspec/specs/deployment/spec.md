# deployment Specification

## Purpose
The deployment capability governs the three-service Docker Compose stack — auto-restarting with the machine, its durable Postgres data volume, and secrets read from a single gitignored .env — and a native Development run that reads that same .env directly rather than through Compose. Both run the backend on the one port set by BACKEND_PORT.
## Requirements
### Requirement: Docker Compose three-service stack
The system SHALL be deployable via Docker Compose with separate Postgres, backend, and frontend services.

#### Scenario: Bringing up the stack
- **WHEN** `docker compose up` is run
- **THEN** the Postgres, backend, and frontend services all start and the app is reachable locally

### Requirement: Auto-restart with the machine
The system SHALL configure each service with `restart: unless-stopped` so the app comes back up automatically when the PC boots and Docker starts, and shuts down cleanly with the PC.

#### Scenario: Recovery after reboot
- **WHEN** the PC reboots and Docker starts
- **THEN** each service restarts automatically unless it was explicitly stopped

### Requirement: Durable Postgres data volume
The system SHALL use a named volume for the Postgres data directory so data survives container rebuilds.

#### Scenario: Data survives a rebuild
- **WHEN** the containers are rebuilt
- **THEN** the Postgres data persists via the named volume

### Requirement: Secrets via gitignored .env
The system SHALL load secrets (the MAL client ID and secret, and the database connection) from a `.env` file at the repository root that is gitignored and never committed. MAL access and refresh tokens are not among them: they come from authorization and are stored in Postgres.

This SHALL hold on both run paths. Under Docker Compose, the backend receives these values from `.env` through Compose. Run natively in the Development environment, the backend SHALL read the same `.env` itself, taking each value as Docker Compose would read it, so that one file configures both paths identically. Run natively, it SHALL reach the database through the port Postgres is exposed on locally.

Outside the Development environment, the backend SHALL NOT read a `.env` file, even if one is present where it would look.

#### Scenario: Secrets stay out of the repo
- **WHEN** the repository is committed or made public
- **THEN** the `.env` file and its secrets are excluded by gitignore and not present in the repo

#### Scenario: A native run uses the same .env as Docker
- **WHEN** the backend is started natively in Development from its project folder, with a `.env` at the repository root and the Compose `postgres` service running
- **THEN** it uses that file's MAL client ID and secret, and connects to Postgres on `localhost` at `POSTGRES_PORT` with that file's database, user and password

#### Scenario: A value is read the way Docker Compose reads it
- **WHEN** a value in `.env` is followed by a whitespace-separated `# comment`, or is wrapped in one pair of quotes
- **THEN** a native run uses the value without the comment or quotes, the same value Docker Compose passes to the containers

#### Scenario: A native run with no .env
- **WHEN** the backend is started natively in Development and there is no `.env` at the repository root
- **THEN** it starts using its own Development database settings, which match `.env.example`'s defaults, and connecting to MAL stays unavailable until credentials are set

#### Scenario: A malformed port in .env
- **WHEN** the backend is started natively in Development and `.env` sets `BACKEND_PORT` or `POSTGRES_PORT` to something other than a whole number
- **THEN** it fails at startup with an error that names that key, without showing the value

#### Scenario: Outside Development, .env is never read
- **WHEN** the backend runs in any environment other than Development, including the Docker image's Production environment, and a `.env` file is present where a native run would look
- **THEN** that file is not read, and the backend's configuration is unchanged by it

### Requirement: One backend port, set by BACKEND_PORT
The system SHALL take the backend's host port from `BACKEND_PORT` in `.env`, and SHALL use 5050 when it is unset or empty. That one port SHALL be where the backend is reached on `localhost`, whether it runs under Docker Compose or natively. It SHALL also be the port in the OAuth redirect URI `http://localhost:{port}/callback`, and the port the frontend dev server proxies `/api` calls to.

#### Scenario: The default port
- **WHEN** `.env` does not set `BACKEND_PORT`, and the backend runs under Docker Compose or natively
- **THEN** the backend is reached at `http://localhost:5050` and the OAuth redirect URI is `http://localhost:5050/callback`

#### Scenario: A chosen port applies everywhere on a native run
- **WHEN** `.env` sets `BACKEND_PORT` to a port other than 5050, and the backend and the frontend dev server run natively
- **THEN** the backend listens on that port, the OAuth redirect URI uses it, and the dev server proxies `/api` to it

#### Scenario: A chosen port applies everywhere under Docker
- **WHEN** `.env` sets `BACKEND_PORT` to a port other than 5050, and the stack runs under Docker Compose
- **THEN** the backend is published on that port and the OAuth redirect URI uses it

