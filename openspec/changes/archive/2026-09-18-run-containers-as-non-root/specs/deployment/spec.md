## ADDED Requirements

### Requirement: Application containers run as non-root

Both application containers SHALL run their application process as a non-root user, so that code executing inside a container cannot write the application's own files. This is defence in depth: it does not prevent a compromise, it caps what one can do.

The backend image SHALL declare a non-root user in its runtime stage, using the non-root user its base image already provides rather than creating one. The published application files SHALL remain readable by that user and SHALL NOT be writable by it.

The frontend image SHALL be built on an unprivileged nginx image whose master process and worker processes both run as a non-root user. Because a non-root process cannot bind a privileged port, the frontend SHALL listen on container port 8080, and the Compose port mapping SHALL publish that port. The host port SHALL continue to come from `FRONTEND_PORT`, defaulting to 5173, so the app is reached exactly as before. The served static files SHALL remain readable by the nginx user and SHALL NOT be writable by it.

The frontend's nginx configuration SHALL NOT be modifiable from inside the running container, and the directory nginx loads configuration from SHALL NOT accept new files from it. Because nginx includes that directory by wildcard, a writable directory would otherwise let code running in the container add a second configuration and have it loaded, relaxing the hostname filtering and framing behaviour described below. Any build step needing elevated privileges to establish this SHALL apply only during the image build, leaving no root-owned process at runtime.

Adopting the unprivileged image SHALL NOT change what the frontend serves. The loopback-only hostname filtering and the `X-Frame-Options: DENY` behaviour that the "Only loopback hostnames are served" and "The frontend refuses to be framed" requirements describe SHALL hold unchanged on the new port.

This requirement applies to the container images only. It places no constraint on a native Development run, which is not containerised.

#### Scenario: The backend process is not root

- **WHEN** the backend container is running
- **THEN** its application process runs as the base image's built-in non-root user, not as root

#### Scenario: The backend cannot overwrite its own binaries

- **WHEN** the backend's application process attempts to write into its published application directory
- **THEN** the write is denied, while every file that directory holds remains readable to it

#### Scenario: The backend still serves and migrates

- **WHEN** the stack is brought up and the backend container starts as that non-root user
- **THEN** it applies its database migrations, starts its background services, and answers API requests exactly as it did as root

#### Scenario: No nginx process is root

- **WHEN** the frontend container is running
- **THEN** both the nginx master process and its worker processes run as a non-root user

#### Scenario: The frontend cannot overwrite what it serves

- **WHEN** the nginx user attempts to write into the directory holding the built SPA
- **THEN** the write is denied, while every file it serves remains readable to it

#### Scenario: The frontend cannot rewrite or extend its own configuration

- **WHEN** the nginx user attempts to modify its configuration file, or to create a new configuration file in the directory nginx loads them from
- **THEN** both are denied, so the hostname filtering and framing rules cannot be relaxed from inside the container

#### Scenario: The frontend is reached on the same host port

- **WHEN** the frontend listens on container port 8080 and Compose publishes it with `FRONTEND_PORT` unset
- **THEN** the app is reached at `http://localhost:5173` as before, and a `FRONTEND_PORT` set to another value publishes it there instead

#### Scenario: Hostname filtering and framing survive the port change

- **WHEN** the unprivileged frontend serves the SPA shell, a hashed asset, and a proxied `/api/` response on its new port
- **THEN** each response carries `X-Frame-Options: DENY` alongside its existing cache-control behaviour, and a request carrying a `Host` other than `localhost` or `127.0.0.1` still has its connection closed with no response
