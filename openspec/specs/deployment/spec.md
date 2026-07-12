# deployment Specification

## Purpose
TBD - created by archiving change bootstrap-anime-tracker. Update Purpose after archive.
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
The system SHALL load secrets (MAL client ID/secret, tokens, DB connection) from a `.env` file that is gitignored and never committed.

#### Scenario: Secrets stay out of the repo
- **WHEN** the repository is committed or made public
- **THEN** the `.env` file and its secrets are excluded by gitignore and not present in the repo

