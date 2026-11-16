---
name: bump-version
description: Prepare a new release of SONAR Easy Mode - collect changes since the last vX.Y.Z tag, bump version.txt, add a CHANGELOG.md entry and sync the feature list in media/nexus.md. Use when the user asks to bump the version, prepare/cut a release, or update the changelog.
argument-hint: "[major|minor|patch|X.Y.Z]"
---

# Bump version

Argument: `$ARGUMENTS` - `major`, `minor`, `patch` or an explicit `X.Y.Z`. Empty means infer it (step 2).

## 1. Collect changes since the last tag

```sh
git describe --tags --abbrev=0 --match "v[0-9]*.[0-9]*.[0-9]*"   # last tag, e.g. v1.0.0
git log <tag>..HEAD --format="%h %s%n%b" --reverse
git diff <tag>..HEAD --stat
git diff <tag>..HEAD -- README.md SonarEasyMode/Settings.cs
```

- If there are no commits since the tag, stop and say so.
- If the working tree is dirty, mention it: uncommitted changes are not part of the release unless the user says so.
- Commit subjects are short (`feat: ...`, `fix: ...`, `ref: ...`, `docs: ...`, `chore: ...`). Do not rely on them alone: read the README and `Settings.cs` diffs (new/renamed/removed config keys, changed defaults) and the code diff of anything unclear, so every entry describes the actual player-visible behavior.
- Ignore commits that do not affect players: `docs:`, `chore:`, banner/media, build tooling, research notes.

## 2. Pick the new version

Current version is in `version.txt` (single line, e.g. `1.0.0`). It is the single source of truth: the csproj, `Plugin.Version` and the zip name are generated from it.

If no argument was given, infer it and state the choice:
- **major**: a config key removed or renamed, or a change that breaks existing config files.
- **minor**: any new feature or new config key (`feat:`).
- **patch**: only fixes, tuning of defaults, refactors.

Write the new version to `version.txt` as one line with a trailing newline, nothing else.

## 3. Update CHANGELOG.md

Format is [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Insert the new section above the previous release, below the intro:

```markdown
## [X.Y.Z] - YYYY-MM-DD

### Added
### Changed
### Fixed
### Removed
```

- Use today's date. Drop empty subsections.
- One bullet per player-visible change, written for players, not developers: what it does and the config key in backticks with its default, e.g. ``- `AutoTagInterval` (default 4): while the sonar is on, ...``.
- Merge commits that belong to one change (a feature plus its follow-up fix inside the same release is just the feature).
- A changed default goes under **Changed** with old -> new value.
- Update the link references at the bottom of the file: add `[X.Y.Z]: https://github.com/greendwin/sonar-easy-mode/compare/v<prev>...vX.Y.Z`.

## 4. Sync media/nexus.md

`media/nexus.md` is the Nexus Mods page text. Make its feature list match what the mod does now, at default settings:

- Source of truth is the current `README.md` and `SonarEasyMode/Settings.cs` (defaults), not the changelog.
- Add every new player-visible feature, update changed numbers (e.g. "3x longer"), remove anything that no longer exists. Features that are off by default can be mentioned as optional.
- Keep the existing tone and structure: short plain sentences, no config key names in the feature bullets, no developer details. Keep the Install section accurate (BepInEx build requirement, config file name).
- Fix broken markup if you spot it (missing `##` on headings, stray pasted text).
- The file uses CRLF line endings; keep them.

## 5. Finish

Do not commit or tag unless the user asks. Show a short summary: old -> new version, the changelog section, what changed in `nexus.md`. Then offer the release commands matching the repo convention:

```sh
git commit -am "chore: version \`X.Y.Z\`"
git tag -a vX.Y.Z -m "vX.Y.Z"
make build   # dist/SonarEasyMode-X.Y.Z.zip
```
