## MODIFIED Requirements

### Requirement: A file this build cannot read is refused by name
Before writing or fetching anything, the system SHALL refuse a file when any of the following holds. It SHALL say which one, in words:
- **Not an export file.** It is not a JSON object, or it has no integer `formatVersion`.
- **A list backup.** Its `kind` is `"listBackup"` (see `list-backup`). The refusal SHALL say that the file is a backup of my list, and that a list backup is never imported. This SHALL be checked as soon as the file is known to be a JSON object, before its `formatVersion` is read, so a list backup is never described as newer, damaged or not an export file.
- **A newer format.** Its `formatVersion` is newer than the formats this build reads. The refusal SHALL name the file's format and the format this build reads. This SHALL be checked before the rest of the file's shape, so a newer file is never described as damaged.
- **Damaged.** A member the format requires is missing or of the wrong kind. The refusal SHALL name that member.
- **From this device.** Its `device.id` is this database's own device identifier.

Members the format does not define SHALL be ignored.

A refusal SHALL write nothing and fetch nothing. It SHALL leave the previous import's report in place.

#### Scenario: A newer format
- **WHEN** I import a file whose `formatVersion` is 2 on a build that reads format 1
- **THEN** the import is refused, saying the file is format 2 and this build reads format 1, and nothing changes

#### Scenario: Not an export file
- **WHEN** I import a JSON file that has no `formatVersion`
- **THEN** the import is refused as not an export file

#### Scenario: A list backup
- **WHEN** I import a list backup
- **THEN** the import is refused, saying that the file is a backup of my list and is never imported, and nothing changes

#### Scenario: A damaged file
- **WHEN** an export file's activity record has no `id`
- **THEN** the import is refused, naming that member, and nothing from the file is applied

#### Scenario: A file from this device
- **WHEN** I import a file this device exported
- **THEN** the import is refused, saying the file was exported from this device

#### Scenario: A member the format does not define
- **WHEN** a format-1 file carries a member the format does not define
- **THEN** that member is ignored and the import proceeds
