# dotnet-claude-kit in EvaGest

How to use [dotnet-claude-kit](https://github.com/codewithmukesh/dotnet-claude-kit)
(v0.12.0) in this repository: what is installed, what to use it for, and which
parts to ignore because they clash with EvaGest's own rules.

> **`CLAUDE.md` wins.** The kit targets ASP.NET Core web APIs on .NET 10. EvaGest
> is a WPF desktop app on .NET 9 with SQLite. Whenever a kit skill or agent says
> something different from [`CLAUDE.md`](../CLAUDE.md) or `docs/plan/`, follow
> `CLAUDE.md`.

---

## 1. What is installed

| Piece | Where | Scope |
|---|---|---|
| Plugin `dotnet-claude-kit@dotnet-claude-kit` (47 skills, 10 agents, hooks, MCP config) | [`.claude/settings.json`](../.claude/settings.json) → `extraKnownMarketplaces` + `enabledPlugins` | **Project**: committed, so anyone who opens the repo with Claude Code is offered it |
| Roslyn MCP server `cwm-roslyn-navigator` | .NET global tool (`%USERPROFILE%\.dotnet\tools`) | User machine |
| Plugin files | `%USERPROFILE%\.claude\plugins\cache\dotnet-claude-kit\…` | User machine |

**Deliberately not installed:** the kit's 10 "rules" (`.claude/rules/*.md`).
They are always-loaded and several contradict this project. See §6.

### Install on another machine

```powershell
dotnet tool install -g CWM.RoslynNavigator
```

Then open the repo with Claude Code. It reads `.claude/settings.json` and offers
to install the marketplace and plugin. Accept. Or do it by hand:

```powershell
claude plugin marketplace add codewithmukesh/dotnet-claude-kit --scope project
claude plugin install dotnet-claude-kit@dotnet-claude-kit --scope project
```

**Restart Claude Code after installing.** Skills, agents, hooks and the MCP
server load at session start.

### Check it works

```powershell
claude plugin list                 # dotnet-claude-kit … Scope: project, ✔ enabled
cwm-roslyn-navigator --help        # should log "Discovered solution: …\EvaGest.slnx"
```

Inside a session, `/mcp` should list `cwm-roslyn-navigator` as connected.

### Update / remove

```powershell
claude plugin update dotnet-claude-kit@dotnet-claude-kit
dotnet tool update -g CWM.RoslynNavigator

claude plugin uninstall dotnet-claude-kit@dotnet-claude-kit --scope project
dotnet tool uninstall -g CWM.RoslynNavigator
```

---

## 2. The useful part: Roslyn MCP tools

The navigator opens `EvaGest.slnx` and answers questions about the code
semantically, so Claude reads one method instead of a whole file. It finds the
solution by itself, no configuration needed. You don't call these tools. Claude
does. But you can ask for them by name to steer it.

| Tool | Good EvaGest use |
|---|---|
| `find_symbol`, `get_symbol_source`, `get_file_outline` | "Show me `VatCalculator.ComputeByRate`" without reading the whole file |
| `find_references`, `find_callers` | "Who calls `SaleService.Void`?" |
| `analyze_change_impact` | Before touching `Money`, `GridHelper` or anything on the money path: blast radius by project and file |
| `get_diagnostics` | Quick compiler/analyzer check after an edit (still run `dotnet build`/`dotnet test` before calling anything done) |
| `find_dead_code` | Candidates only. Bindings in XAML and `[RelayCommand]` source-generated members can look unused when they aren't |
| `detect_antipatterns` | `async void`, `.Result`/`.Wait()` (CLAUDE.md forbids these too) |
| `get_di_registrations` | Check lifetimes in `App.Configure` (page VMs transient, `MainWindowViewModel` singleton) |
| `get_project_graph`, `detect_circular_dependencies` | App ↔ Tests reference sanity |
| `get_test_coverage_map` | Rough guess at which services lack tests (matches by naming) |
| `resolve_stack_trace` | Paste a trace from the Serilog log file → file:line in our code |
| `get_nuget_packages` | Package inventory, no network |

Tools that mean nothing here: `get_endpoint_map` (no HTTP endpoints).

Example prompts:

```
Use analyze_change_impact on VatCalculator.ComputeByRate before we change it.
Resolve this stack trace from the log against the solution: <paste>
List DI registrations and flag any captive dependencies.
```

---

## 3. Slash commands worth using

Type them in a Claude Code session. Two names collide with Claude Code's own
built-ins, so use the namespaced form for those:
`/dotnet-claude-kit:code-review` and `/dotnet-claude-kit:plan`.

| Command | Use in EvaGest | Notes |
|---|---|---|
| `/verify` | Before calling a change done: build → analyzers → antipatterns → tests → format → diff review | Matches CLAUDE.md's "warning-free build, run all tests" |
| `/build-fix` | Build broken or tests red after a refactor: bounded fix loop | Read every fix it makes to money/VAT code. Never let it "fix" a test by changing an expected amount |
| `/dotnet-claude-kit:code-review` | Review the working diff using Roslyn blast radius | Built-in `/code-review` is also available, use either |
| `/health-check` | Occasional A–F report card: diagnostics, dead code, coverage | Ignore points it docks for missing web/API patterns |
| `/outdated` | Stale NuGet packages, CVEs, license traps | Stay on .NET 9 packages unless the project decides to move |
| `/security-scan` | Secrets and vulnerable packages | Most OWASP/auth/CORS checks don't apply to an offline desktop app |
| `/migrate` | EF Core migrations with a rollback plan | Migrations run from the `EvaGest/` folder (`dotnet ef migrations add <Name>`). Back up the user's `.db` before applying |
| `/de-sloppify` | Cleanup pass: unused usings, warnings, `sealed`, TODOs | Review the diff. It must not strip the explanatory comments CLAUDE.md asks for |
| `/spec` | Turn a vague request from the shop into a written spec | Saves to `docs/specs/`. Spec language in `docs/plan/` is Catalan, so decide which you want |
| `/checkpoint` | Commit + handoff note mid-session | Makes a commit, so only when you want one |
| `/wrap-up` | End-of-session handoff to `.claude/handoff.md` | Optional |
| `/arch-check` | Only meaningful with an architecture it knows | EvaGest is layered MVVM (see CLAUDE.md), not VSA/Clean/DDD. Tell it the rules from CLAUDE.md §Architecture explicitly |

**Do not run:**

- `/dotnet-init`: generates a new `CLAUDE.md` and would overwrite ours.
- `/scaffold`: generates web-API feature slices (endpoints, Result pattern,
  pagination). Wrong shape for a WPF page/dialog.
- `/tdd` as-is: it pushes WebApplicationFactory + Testcontainers. Our tests use
  SQLite in-memory and the WPF fixture. If you use it, say "follow
  `EvaGest.Tests/Infra` and CLAUDE.md §Tests".

---

## 4. Agents

Claude picks them automatically from your wording, or ask by name ("use the
ef-core-specialist agent to …").

| Agent | Useful here? |
|---|---|
| `ef-core-specialist` | **Yes**: queries in Services, migrations, SQLite quirks |
| `code-reviewer` | **Yes**: read-only reviews |
| `build-error-resolver` | **Yes**: compile errors |
| `refactor-cleaner` | Yes, with review. Works in an isolated worktree |
| `test-engineer` | With care. Defaults to Testcontainers, so point it at our fixtures |
| `performance-analyst` | Sometimes: slow reports or grid loads |
| `dotnet-architect` | Rarely. Architecture is already fixed by `docs/plan/capa-mvvm.md` |
| `api-designer`, `devops-engineer`, `security-auditor` | Little use: no API, no deploy pipeline, offline app |

---

## 5. Hooks that now run automatically

These come with the plugin and run on every session in this repo:

| Hook | When | Effect |
|---|---|---|
| `pre-bash-guard` | Before any Bash command | **Blocks** `git push --force`, `git reset --hard`, `git clean -f`, `git checkout .`, and `rm -rf` outside `bin/obj/node_modules/TestResults/.vs` |
| `post-edit-format` | After Claude edits a `.cs` file | Runs `dotnet format <project> --include <file>` |
| `post-scaffold-restore` | After Claude edits a `.csproj` | Runs `dotnet restore` |

Things to know:

- **No `.editorconfig` in this repo**, so `dotnet format` uses its defaults and
  may change whitespace/usings in a file Claude touches. Diffs can show more
  than the logical change. If that gets noisy, add an `.editorconfig` that
  matches the current style, or disable the plugin's hooks (`/hooks`).
- `jq` isn't installed. The hooks fall back to `sed`, which works but is less
  robust. Optional: `winget install jqlang.jq`.
- The format hook runs on the whole project the first time and can take a few
  seconds per edit.

---

## 6. Where the kit disagrees with EvaGest

The kit's skills (`testing`, `error-handling`, `modern-csharp`, …) load on
demand and can nudge Claude toward these. Our rules stay:

| Kit says | EvaGest does | Source |
|---|---|---|
| Testcontainers, no in-memory DB | Real **SQLite in-memory** in `Services/` and `Scenarios/` tests, WPF fixture in `Views/` | CLAUDE.md §Tests |
| `Money(decimal Amount, …)` records in examples | Money is **`int` cents**, VAT is **`int` basis points**. Never `decimal` | CLAUDE.md §Money |
| Result pattern, ProblemDetails | `Error*` properties on ViewModels, global `DispatcherUnhandledException` | CLAUDE.md §Error handling |
| Inject `ILogger` / Serilog bootstrap for ASP.NET | Static `Serilog.Log`, no injected logger | CLAUDE.md §Logging |
| `TimeProvider` over `DateTime.Now` | Not adopted. Existing code uses `DateTime.Now`. Don't migrate it unprompted | — |
| Conventional commits (`feat:`, `fix:`) | Plain imperative sentences ("Refuse VAT rates with more than two decimals") | git log |
| Comment only the non-obvious | **Comment well**, explain what non-trivial code does | CLAUDE.md §Planned refactor |
| .NET 10 / C# 14, latest 10.x packages | **.NET 9** (`net9.0-windows`) | `EvaGest.csproj` |
| Register everything in DI | Dialog ViewModels are built with `new`, on purpose | CLAUDE.md §Conventions |
| Minimal APIs, endpoints, auth, Docker, Aspire | Out of scope: offline single-machine desktop app | CLAUDE.md §Scope |

Agreements worth keeping: no repository layer over EF Core, async all the way
(no `.Result`/`.Wait()`), `sealed` where sensible, parameterised SQL.

If you ever want the kit's rules anyway, copy only what fits from the plugin
cache's `.claude/rules/` into `.claude/rules/` and edit them first. Don't copy
`testing.md`, `error-handling.md`, `git-workflow.md` or `packages.md` unchanged.

---

## 7. Typical workflow

1. Describe the change. Claude uses Roslyn tools to find the code cheaply.
2. Before touching money, the grid or client keys: ask for `analyze_change_impact`.
3. Implement. The format hook tidies each `.cs` edit.
4. Add/adjust tests (CLAUDE.md requires them for money, overlap, client keys, grid).
5. `/verify` (or `dotnet build EvaGest.slnx` + `dotnet test EvaGest.slnx`).
6. `/dotnet-claude-kit:code-review` on the diff.
7. Log any bug found in `bugs.csv`.

Full upstream guide:
<https://codewithmukesh.com/blog/dotnet-claude-kit-guide/>
