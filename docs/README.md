# Personix.CodeStyle

Shared code style rules for Personix .NET projects, delivered as a **global AnalyzerConfig**.

Referencing this package turns the agreed rules into **build errors**, so they hold in CI and on
every machine — instead of depending on whether each developer happens to have the same editor
settings. Changing a rule means bumping one package version, not editing nine repositories.

## Installation

```xml
<PackageReference Include="Personix.CodeStyle" Version="1.0.3" PrivateAssets="all" />
```

`PrivateAssets="all"` keeps the rules from flowing to consumers of your package — they govern your
build, not theirs. Put the reference in `Directory.Build.props` to cover every project in a
repository at once.

## What it enforces

| Rule | Severity | Meaning |
|---|---|---|
| `IDE0011` | **error** | Braces on every control-flow statement, including single-line `if`. |
| `IDE0161` | warning | File-scoped namespace declarations. |
| `IDE0005` | warning | No unused `using` directives. |
| `IDE0051` / `IDE0052` | warning | No unread private members. |
| `IDE0060` | suggestion | Unused parameters. |
| `IDE1006` | warning | Private field naming: `_underscore` on instance fields, `PascalCase` on `const` and `static readonly` ones. |

Braces are an error rather than a warning on purpose: the failure mode is a second statement added
under an unbraced `if`, which silently falls outside the condition and reads as if it did not.

Alongside the analyzer rules, the package fails the build on a handful of conventions that no
analyzer covers. All of them are errors, because each describes something that is either agreed or
not — there is no useful middle setting.

| Rule | Applies to | Meaning |
|---|---|---|
| `PERSONIX001` | packable projects | A published package must carry `docs/README.md`. The package is wired up as the NuGet readme automatically, so no csproj needs to set `PackageReadmeFile`. |
| `PERSONIX002` | test projects | FluentAssertions, AwesomeAssertions and NFluent are refused. FluentAssertions is commercially licensed from version 8. |
| `PERSONIX003` | **every project** | NSubstitute and FakeItEasy are refused. Moq is the test double library across Personix. |
| `PERSONIX004` | test projects | A test project must reference Shouldly. Banning the alternatives is not the same as having the agreed one. |

`PERSONIX003` deliberately ignores `IsTestProject`. Scoping it to test projects would leave the rule
silent exactly where an unwanted reference is least likely to be noticed — a helper or fixture
project that never set the flag.

## What it does not cover

Formatting that the editor applies as you type — indentation, encoding, line endings — comes from
`.editorconfig`, which MSBuild reads from disk and cannot be delivered in a package. Keep a small
`.editorconfig` in each repository for that. It changes rarely; this package holds the rules that
actually change.

## Overriding a rule

A repository-local `.editorconfig` takes precedence over this global config, so a project with a
genuine reason can relax a specific rule locally. That is intended — the package sets the default,
not an absolute.

## Licence

MIT — see [LICENSE](LICENSE).
