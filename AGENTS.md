# AGENTS.md: ZeroAlloc.Telemetry

A source generator plus a small attribute library. `[Instrument]` on an interface makes the generator
emit a `<Name>Instrumented` proxy that wraps each call in an `Activity` span and `Meter` instruments.
Two NuGet packages ship: `ZeroAlloc.Telemetry` (attributes, `net10.0` and `net11.0`, bundles the
generator under `analyzers/dotnet/cs`) and `ZeroAlloc.Telemetry.Generator` (the same generator,
standalone, `netstandard2.0`).

## Layout

- `src/ZeroAlloc.Telemetry.Generator` is the product. Judge the hot path by the code it emits
  (`ProxyWriter.cs` and friends), not by the generator's own code.
- `tests/ZeroAlloc.Telemetry.Generator.Tests` runs the generator in-process and compares the output
  with snapshots in `Snapshots/*.verified.cs`. They are data, not code.
- `tests/ZeroAlloc.Telemetry.Generated.Tests` compiles the real generated proxies, so it holds the
  behaviour tests and the allocation gates. Add a hot-path test here, not in the generator tests.
- `benchmarks/` and `samples/ZeroAlloc.Telemetry.AotSmoke` do not ship. Neither is in a test run.

## Build and test

Prerequisites: `global.json` pins SDK 11.0.100 with `rollForward: latestFeature`. The library and the test
projects target `net10.0` and `net11.0`, so they need the .NET 10 and .NET 11 runtimes.

CI job `build` in `.github/workflows/ci.yml`:

```bash
dotnet tool restore
VERSION=$(dotnet dotnet-gitversion /output json /showvariable SemVer)
dotnet restore ZeroAlloc.Telemetry.slnx
dotnet build ZeroAlloc.Telemetry.slnx --configuration Release --no-restore
dotnet test ZeroAlloc.Telemetry.slnx --configuration Release --no-build --verbosity normal --logger "trx;LogFilePrefix=test-results" --collect "XPlat Code Coverage" --settings coverage.runsettings --results-directory TestResults
```

The job then runs the `coverage-gate` action of `ci-baseline`: merged Cobertura reports must reach 80
percent line coverage. These commands do not reproduce that gate. `codecov.yml` tolerates a 1 percent
drop on the project and checks patch coverage informationally.

CI job `aot-smoke`, with clang and zlib1g-dev installed:

```bash
dotnet restore samples/ZeroAlloc.Telemetry.AotSmoke/ZeroAlloc.Telemetry.AotSmoke.csproj
dotnet publish samples/ZeroAlloc.Telemetry.AotSmoke/ZeroAlloc.Telemetry.AotSmoke.csproj -r linux-x64 -c Release -o ./aot-out
./aot-out/ZeroAlloc.Telemetry.AotSmoke
```

CI workflow `benchmarks.yml`, job `smoke`, calls the shared `benchmark-smoke.yml` with
`project: benchmarks/ZeroAlloc.Telemetry.Benchmarks`. The `full` job (manual dispatch) is:

```bash
dotnet build benchmarks/ZeroAlloc.Telemetry.Benchmarks -c Release
dotnet run --project benchmarks/ZeroAlloc.Telemetry.Benchmarks -c Release --no-build -- --filter "${FILTER:-*}"
```

The benchmarks target `net10.0` only, so they need the .NET 10 runtime.

The `api-compat`, `release-tracking`, `lint-commits` and baseline jobs are shared workflows from
`ZeroAlloc-Net/.github` and `ci-baseline`. They do not run from a checkout.

Fast inner loop, none of it run by a CI job:

```bash
dotnet test tests/ZeroAlloc.Telemetry.Generated.Tests -c Release -f net11.0 --filter "FullyQualifiedName~NoListenerAllocationTests"
dotnet test tests/ZeroAlloc.Telemetry.Generator.Tests -c Release -f net11.0
SHA=$(git rev-parse --short HEAD); dotnet pack ZeroAlloc.Telemetry.slnx -c Release -p:Version=9.9.9-local.$SHA -o /tmp/telemetry-pack
```

A short benchmark run for a local check: `dotnet run --project benchmarks/ZeroAlloc.Telemetry.Benchmarks -c Release -- --filter '*InstrumentedProxy*' --job Dry --inProcess`.
It leaves a `BenchmarkDotNet.Artifacts` folder, which is git-ignored.

CI job `sdk10-consumer` needs a 10.0.1xx SDK; `tests/ZeroAlloc.Telemetry.Sdk10Smoke/global.json` pins
10.0.100 with `rollForward: latestPatch`, so a newer band cannot stand in for it. It builds and runs a
consumer of the packed packages with Roslyn 5.0:

```bash
v=9.9.9-local.sha$(git rev-parse --short HEAD).$(date +%s).$$
dotnet pack src/ZeroAlloc.Telemetry -c Release -o artifacts -p:Version="$v"
dotnet pack src/ZeroAlloc.Telemetry.Generator -c Release -o artifacts -p:Version="$v"
cd tests/ZeroAlloc.Telemetry.Sdk10Smoke/Consumer
dotnet run -c Release -p:ZeroAllocTelemetryVersion="$v"
```

CI job `roslyn-floor` calls the shared `roslyn-floor.yml` from ZeroAlloc-Net/.github: it packs the
solution and fails when a packed DLL references Roslyn above 5.0.0.

## Conventions and gotchas

- The generator references `Microsoft.CodeAnalysis.CSharp` 5.0.0, the compiler of SDK band 10.0.1xx, the
  oldest supported SDK. A compiler older than the Roslyn the DLL references skips the generator with
  `CS9057`, and nothing is generated. The shared Renovate preset holds it below 5.1. If a newer Roslyn API
  is needed, change the code; raise the floor only together with the minimum SDK. The generator test
  project may use a newer Roslyn.
- **Packing needs `-p:Version`.** The csproj versions are the placeholder `0.0.0-local`, and
  `Directory.Build.props` fails `dotnet pack` with ZAVERSION001 or ZAVERSION002 without a version
  passed on the command line. Builds and tests do not need it. After `dotnet build` without a version,
  a `pack --no-build` fails ZAVERSION002.
- **Snapshots.** A changed generator output fails `Generator.Tests` and writes `*.received.cs` beside
  the snapshot. Review the diff, then accept it with `ZA_SNAPSHOT_UPDATE=1 dotnet test
  tests/ZeroAlloc.Telemetry.Generator.Tests`. Never hand-edit a `.verified.cs` file.
- **Tracked API surfaces.** New public API goes in `src/ZeroAlloc.Telemetry/PublicAPI.Unshipped.txt`
  (RS0016 and RS0017 are build errors). A new or changed generator diagnostic (`ZTELnnn`) goes in
  `src/ZeroAlloc.Telemetry.Generator/AnalyzerReleases.Unshipped.md`. Leave the `Shipped` files alone:
  the release PR moves entries there.
- **Warnings are errors** in every project (`TreatWarningsAsErrors`, with Meziantou, Roslynator and
  ZeroAlloc analyzers). The AOT smoke also turns IL2026, IL2067, IL2075, IL2091, IL3050 and IL3051
  into errors explicitly.
- **Exception to the org rule on benchmark frameworks:** the benchmark project targets only `net10.0`,
  not both LTS frameworks. Its numbers are same-run ratios on that one runtime.
- **What the 0 B claim covers.** With no listener on the `ActivitySource` and no enabled instrument, a
  `Task`, `ValueTask` or `ValueTask<T>` method returns the inner task as is and the tests assert 0 B,
  tagged metrics included (`NoListenerAllocationTests`, `MetricTagAllocationTests`,
  `PerElementMetricsTests`, `GenAiMetricsTests`). With an `ActivityListener` attached, a span allocates
  its `Activity` and tags, and no test promises otherwise. The one listener case asserted at 0 B is the
  per-element histogram with a metrics listener (`GenAiMetricsTests`).
- The generator targets `netstandard2.0` with `EnforceExtendedAnalyzerRules`: no APIs newer than that,
  and keep the incremental pipeline cacheable (`IncrementalCachingTests` guards it, models in
  `Models/` are value-equatable).

<!-- org-core:start sha256=8faf417fa86f11bd8ad9bc98dfa4683fba44e440291e84c6cb9b0ec3ca0c0e7e (generated from docker-nuc scripts/agents-md/org-core-zeroalloc.md; do not edit here) -->
## Org rules

These rules hold in every ZeroAlloc-Net repo. Each library makes two promises: it works under
NativeAOT and trimming, and its hot path allocates nothing. The 2026-10-11 sweep found 16 of 28 repos
allocating on the hot path, most with docs claiming 0 B.

### Hot path

The operation a consumer runs repeatedly: `Send`, a cache hit, `Validate`, `Map`, `Serialize`, a
resilience call, a state transition, an append, a worker poll, including the idle poll. Startup and
registration are not hot path. For a source generator, the hot path is the code it emits: judge the
snapshots and the generated output.

### Allocation traps the sweep found

| Pattern | Instead |
|---|---|
| `async ValueTask` that only awaits and returns | `return new ValueTask<T>(inner.XAsync(ct));` when the method only forwards a `Task<T>` (return a forwarded `ValueTask<T>` directly). The wrapper stores a synchronous throw in the returned task; the direct return lets it, and the cancellation status of the callee, reach the caller. Keep the `async` wrapper when callers depend on either |
| `Task.WhenAll` over tasks that already completed | skip the await only when every task has `IsCompletedSuccessfully` (`Status == TaskStatus.RanToCompletion` on `netstandard2.0`); otherwise `await Task.WhenAll`, which also reports faults of completed tasks |
| `TagList` or log `params object[]` built before `Enabled`/`IsEnabled` | check first, or `LoggerMessage.Define` |
| event args allocated before checking for subscribers | copy the handler to a local, check, then allocate |
| `new Serializer()` / `new Utf8JsonWriter()` per call | cache stateless instances, reuse writers with `Reset` |
| `GetString()` / `ToString()` round-trips in JSON converters | parse and format through spans |
| `ToArray()` / `ToList()` copies between layers | pass `ReadOnlyMemory<T>` or spans, copy once at most |
| LINQ in emitted code | emit `for` loops |
| `yield` iterator behind a public `GetEnumerator` | public struct enumerator, from the first release |
| `Enum.IsDefined(typeof(E), v)` with a boxed value, `decimal.GetBits(v)` to read the scale | `Enum.IsDefined<E>(v)` when `v` has type `E` (.NET 5 or later, so not on .NET Framework, `netstandard2.0`, `netstandard2.1` or .NET Core 3.1); `v.Scale` for a scale read only (.NET 7 or later, never for the coefficient or sign). Keep the old call on older targets |
| configuration parsed or read per call | parse once and cache |
| an `AsyncLocal` write per call | pass state explicitly |
| a capturing lambda or method-group delegate per call | cache the delegate in a field, or a static lambda |
| a per-key object that is never removed | remove on release; otherwise it is a leak |

Inherent allocations are fine when documented: the consumer's own result, an exception on a failure
path, a one-time cache fill.

### Tests and benchmarks

- Every hot path has an allocation gate test (`AllocationGate` from ZeroAlloc.TestHelpers) that measures
  the whole public call, including any state setup.
- Budgets are exact. If a path allocates, assert that number and comment what it is.
- Test the generated code, not a hand-written stand-in, and cover every shape: each attribute, nested
  collections, and the asynchronous case with a fake that returns pending tasks.
- Benchmarks run in CI through the shared `benchmark-smoke.yml` and target the library's LTS
  frameworks. The benchmark body allocates nothing besides the library itself. Run locally with
  `--inProcess`.
- Publish exact allocations and same-run ratios against a `Baseline`, never absolute timings. Run with
  `--launchCount 3`, pin CPUs with `taskset`, give the spread, and label every table.

### NativeAOT and trimming

- Packable runtime projects, and projects consumers publish, set `IsAotCompatible=true` on modern
  target frameworks. Analyzers and source generators target `netstandard2.0`.
- No reflection or runtime code generation on a supported path. An opt-in reflection fallback carries
  honest `[RequiresUnreferencedCode]` / `[RequiresDynamicCode]`. Every suppression must be true;
  remove dead ones.
- The AOT smoke sample publishes with `TreatWarningsAsErrors=true`. If a third-party warning really
  blocks it, use a narrow, justified suppression for that warning. `WarningsNotAsErrors` selects codes,
  not assemblies, so it would also downgrade the same warning from generated code.
- The smoke runs the hot path of every package in the repo. A package without a smoke has unknown AOT
  status. Adapters run against real services in CI service containers; publish-only is a last resort.
  App and template smokes make authenticated requests.

### Docs and shipping

- Docs match measurements. After a hot-path change, re-measure and update `docs/performance.md` and the
  README. Never write "0 B" without a test that asserts it.
- New public API is `feat:`, even for a performance change. Public API changes are additive: never add
  an optional parameter to a shipped signature. A breaking end state goes in a `next-major` issue.
- A behaviour change gets a `## Behaviour change` section in the PR body.
- Conventional Commits with release-please. Subjects start lowercase, body lines stay within 100
  characters, and a body never contains nested parentheses: release-please drops the whole commit.
- Local packs use `9.9.9-local.<sha>`. After a release, verify the package on NuGet.
- Fix review findings in the PR; file an issue only for what cannot go in it.

Full rules, template wiring lessons and the reasons behind each rule: https://github.com/ZeroAlloc-Net/.github/blob/main/AGENTS.md
<!-- org-core:end -->
