## Layer rules (affects every code change)

Four projects; dependencies point inward only:

- **Domain** — plain records and enums. No NuGet dependencies. No EF, no HttpClient.
- **Application** — use-case services and port interfaces. Depends only on Domain.
  Never reference Infrastructure from here.
- **Infrastructure** — adapters implementing the ports (HTTP fetcher, HTML parser,
  EF repository). Only place EF Core and Npgsql packages live.
- **Api** — composition root only. The sole project that references Infrastructure.

After any code change, verify no reference points outward (e.g. `dotnet build`
will catch circular references; grep `.csproj` for unexpected cross-references).

## Hard constraints (never violate without an ADR update)

- No third-party HTML parsing packages (no HtmlAgilityPack, AngleSharp, Selenium,
  Playwright). BCL only.
- EF Core entities (`*Entity`) must stay in Infrastructure. Never use Domain
  records as EF entities.
- The per-location `try/catch` in `SolicitorSearchService` is intentional —
  one location failing must not abort the whole run.
- Firm branch identity = normalise(FirmName) + Postcode (fallback Phone).
  Firm identity for multi-location grouping = normalise(FirmName) only.
  Keep consistent across `SolicitorSearchService`, `ReportBuilder`, `RunComparer`.

## Comments

Prefer self-documenting code. Add comments only for non-obvious *why*, not for
*what* the next line already says.

| Location | Use |
|----------|-----|
| Application ports (`I*` in Application) | `///` on the interface when IntelliSense on the contract helps |
| Public Infrastructure adapters | Optional one-line `///` on the type; no per-method docs unless warranted |
| `internal` helpers (parser, etc.) | `//` for brief gotchas, or no comment |
| Architecture / trade-off decisions | `docs/ADRs.md`, not long comment blocks in code |

Do **not** add: XML docs that restate the type or method name; per-method
`Returns null when…` boilerplate on internal helpers; decorative section banners;
`§` plan/spec cross-references; phase-roadmap placeholders in `Program.cs`.

**Keep** short comments for domain gotchas that code alone cannot convey — e.g.
live-site slug rules, HTML parser constraints, intentional error-handling
contracts (404 vs throw vs bubble), DI lifetime notes.

## Architecture decisions

See `docs/ADRs.md` for full rationale. Read it before modifying the parser
pipeline, the persistence model, or `SolicitorSearchService` orchestration.