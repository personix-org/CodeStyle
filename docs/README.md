# Personix.CodeStyle

Shared code style rules for Personix .NET projects, delivered as a **global AnalyzerConfig**.

Referencing this package turns the agreed rules into **build errors**, so they hold in CI and on
every machine — instead of depending on whether each developer happens to have the same editor
settings. Changing a rule means bumping one package version, not editing nine repositories.

## Installation

```xml
<PackageReference Include="Personix.CodeStyle" Version="1.1.0" PrivateAssets="all" />
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
| `IDE0022` | silent | Block bodies for methods — a rule for what the IDE generates, not for what is already written. |
| `PERSONIX005` | warning | A setter used only while the object is being created should be `init`. |
| `PERSONIX006` | warning | An `init` property without a default that every creation site sets should be `required`. |

Braces are an error rather than a warning on purpose: the failure mode is a second statement added
under an unbraced `if`, which silently falls outside the condition and reads as if it did not.

`IDE0022` is silent for the opposite reason. It exists so that implementing an interface or base
member produces a method with a body to fill in rather than an expression to unwrap first. Whether
a finished method reads better as one or the other is a judgement per method, so nothing is
underlined and code cleanup leaves settled code alone.

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

### PERSONIX005

A property with a `set` accessor is reported when every write to it in the project happens while the
object is being created: in an object initializer, in a `with` expression, or in a constructor of the
declaring or a derived type. Changing `set` to `init` then compiles unchanged and states what the
code already relies on.

Any other write keeps the property quiet — an assignment after construction, a compound assignment,
`++`, `??=`, deconstruction, a write inside a lambda, or a nested initializer such as
`new Team { Lead = { Name = "Ann" } }`. So does a setter that nothing in the project writes, since
such a property is usually filled by a serializer or a binder.

### PERSONIX006

An `init` property is reported when it has no default value, no constructor assigns it, and every
`new` of its type in the project sets it. Adding `required` then compiles unchanged and makes the
compiler hold the next creation site to the same rule. A `with` expression is not a creation site,
and creating a derived type counts as creating the base type.

System.Text.Json honours `required`: after the change, deserializing JSON that lacks the property
throws instead of leaving the default. Check that before adding the modifier to a type that is also
read from JSON.

### Scope of both rules

The analyzer sees one project at a time. Neither rule applies to static, abstract, virtual or
overriding properties, to interface implementations, or to positional record parameters.

In a packable project, a property whose setter is visible outside the assembly is skipped, because
consumers of the package may write it. A project that is not packable has no such consumers, so its
public properties are analysed as well.

That leaves one blind spot in layered solutions. A type declared in one project and created or
mutated in another is judged only by what its own project does with it. A Domain type that
Infrastructure creates is never reported under PERSONIX006, and a Domain property that Application
mutates can be reported under PERSONIX005 even though the change would not compile. In that case,
suppress the diagnostic on the property with `[SuppressMessage("Design", "PERSONIX005")]`.

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
