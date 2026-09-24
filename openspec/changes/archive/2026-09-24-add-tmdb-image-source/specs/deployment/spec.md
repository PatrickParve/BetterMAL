## MODIFIED Requirements

### Requirement: Secrets via gitignored .env
The system SHALL load secrets from a `.env` file at the repository root that is gitignored and never committed. The secrets are the MAL client ID and secret, the optional TMDB API key, and the database connection. MAL access and refresh tokens are not among them: they come from authorization and are stored in Postgres.

The TMDB API key SHALL be optional. When it is absent or empty, the backend SHALL start normally with TMDB access off (`tmdb-artwork`).

This SHALL hold on both run paths:
- **Docker Compose:** the backend receives these values from `.env` through Compose.
- **Native, Development environment:** the backend SHALL read the same `.env` itself, taking each value as Docker Compose would read it, so that one file configures both paths identically. It SHALL reach the database through the port Postgres is exposed on locally.

Outside the Development environment, the backend SHALL NOT read a `.env` file, even if one is present where it would look.

#### Scenario: Secrets stay out of the repo
- **WHEN** the repository is committed or made public
- **THEN** the `.env` file and its secrets are excluded by gitignore and not present in the repo

#### Scenario: A native run uses the same .env as Docker
- **WHEN** the backend is started natively in Development from its project folder, with a `.env` at the repository root and the Compose `postgres` service running
- **THEN** it uses that file's MAL client ID and secret and its TMDB API key, and connects to Postgres on `localhost` at `POSTGRES_PORT` with that file's database, user and password

#### Scenario: Docker passes the TMDB key through
- **WHEN** `.env` sets `TMDB_API_KEY` and the stack is started with Docker Compose
- **THEN** the backend container receives that key and TMDB access is on

#### Scenario: No TMDB key
- **WHEN** `.env` leaves `TMDB_API_KEY` empty or omits it
- **THEN** the backend starts normally, TMDB access is off, and everything else works as before

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

## ADDED Requirements

### Requirement: The custom id mapping file reaches the backend on both run paths
The system SHALL make the custom id-mapping file (`external-id-mapping`) available to the backend on both run paths:
- **Docker Compose:** the repository's `backend/custom` folder SHALL be copied into the backend image when it is built, where the container's non-root user can read it, so applying an edit needs a rebuild (`docker compose up -d --build`). It SHALL NOT be a bind mount of a host folder: that needs Docker Desktop to be allowed into the project's folder, macOS withholds that from `~/Documents` until it is granted, and a mount source the daemon cannot reach stops the backend from starting at all.
- **Native run:** the backend SHALL look for `custom/id-mapping.json` in its working directory and in the two folders above it, so that a run from the project folder finds `backend/custom/id-mapping.json`, and it SHALL apply an edit to that file within seconds, with no restart.

The file's location SHALL be overridable by configuration (`IdMapping:CustomFile`, or `IdMapping__CustomFile` in the environment).

#### Scenario: The container reads the file it was built with
- **WHEN** the Compose stack is built and started
- **THEN** the backend reads `/app/custom/id-mapping.json`, a copy of the repository's file as of the build, as its non-root user

#### Scenario: An edit in Docker takes a rebuild
- **WHEN** I edit the custom file and run `docker compose up -d --build`
- **THEN** the rebuilt backend applies the edit

#### Scenario: The stack does not depend on host file sharing
- **WHEN** Docker Desktop has not been given access to the folder the project is in
- **THEN** `docker compose up -d --build` still starts the backend

#### Scenario: Editing during a native run
- **WHEN** I edit the custom file while the backend runs natively
- **THEN** the backend applies the change within seconds, with no restart

#### Scenario: A native run finds the file
- **WHEN** the backend is started natively from its project folder
- **THEN** it reads `backend/custom/id-mapping.json`

#### Scenario: A configured location wins
- **WHEN** `IdMapping__CustomFile` names a file
- **THEN** that file is read instead of the default location
