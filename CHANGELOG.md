# Changelog

## [1.3.0](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/compare/v1.2.2...v1.3.0) (2026-05-13)


### Features

* **benchmarks:** add hand-written ActivitySource comparison ([#27](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/issues/27)) ([9479911](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/9479911e98eeafaa14252a59f5192fdd7db06167))

## [1.2.2](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/compare/v1.2.1...v1.2.2) (2026-05-12)


### Bug Fixes

* **readme:** absolute GitHub URLs so nuget.org links resolve ([#25](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/issues/25)) ([60cc1bc](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/60cc1bce9a71fa7a9c3fcae2d5ec759ca14f7c57))

## [1.2.1](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/compare/v1.2.0...v1.2.1) (2026-05-03)


### Bug Fixes

* **release-please:** drop pre-major flags (package is post-1.0) ([#20](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/issues/20)) ([cac7918](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/cac79183ecd970fdeeed071b9d920a9655544d7c))

## [1.2.0](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/compare/v1.1.3...v1.2.0) (2026-05-01)


### Features

* lock public API surface (PublicApiAnalyzers + api-compat gate) ([#19](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/issues/19)) ([c587080](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/c5870808d9ce2cfaeb07d7995bbcf22b5fbfa1d0))


### Bug Fixes

* restore generator package publishing (revert IsPackable=false) ([195bd07](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/195bd0706142fd27fab2cebf8640764936afe3ea))
* restore generator package publishing with correct packaging ([206a5d6](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/206a5d654e95ca9c5e7c2ae546fd2d999f81b8b2))

## [1.1.3](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/compare/v1.1.2...v1.1.3) (2026-04-30)


### Bug Fixes

* stop publishing broken stand-alone generator nupkg ([5ac7d83](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/5ac7d83465d9eb24eea393ca1a93e592d92b44e6))
* stop publishing broken stand-alone generator nupkg ([19c5ce9](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/19c5ce954995c42a3689eebc080efd2723f25807))

## [1.1.2](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/compare/v1.1.1...v1.1.2) (2026-04-29)


### Documentation

* **readme:** standardize 5-badge set ([09634c8](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/09634c868384d2c2ef5a595b2e2179bd2d3bba68))
* **readme:** standardize 5-badge set (NuGet/Build/License/AOT/Sponsors) ([7b701bd](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/7b701bdf5b8d4f90429a149f5b79e56b84449d3d))

## [1.1.1](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/compare/v1.1.0...v1.1.1) (2026-04-28)


### Documentation

* add GitHub Sponsors badge to README ([80937d0](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/80937d045777c404825ff6ce4d649e56a6e18250))
* add GitHub Sponsors badge to README ([c5de3fa](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/c5de3faac1b788c5a8abc1d22a1dacc0f68cc323))

## [1.1.0](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/compare/v1.0.0...v1.1.0) (2026-04-23)


### Features

* **generator:** add ZTEL001-003 diagnostics ([#8](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/issues/8)) ([d20a675](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/d20a675d8553a0788b54a85d5b10563a6d071239))


### Performance

* add BenchmarkDotNet project measuring instrumented-proxy overhead ([#6](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/issues/6)) ([6dabf88](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/6dabf8882a19fe633155ae381c200021ca4fedaa))


### Documentation

* add performance page covering instrumentation-proxy design and benchmark ([#9](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/issues/9)) ([10d2fca](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/10d2fca8c70703ac74a1a6038666fbf2ff6b376b))

## 1.0.0 (2026-04-17)


### Features

* add ZeroAlloc.Telemetry core attributes ([1906101](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/1906101c62e7fd3ed4c71a5612531a249c42ab5b))
* add ZeroAlloc.Telemetry.Generator skeleton and solution ([41ef2ab](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/41ef2ab70583bc610c3dde54fc06c0dc1a8c4ed5))
* generator emits Activity proxy for [Trace] methods ([cfff8e2](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/cfff8e2f7d5bff2ad04fde868f4766bdb4101a08))


### Documentation

* add ecosystem-style documentation (getting-started, attributes, source-generator, testing, aot) ([a14dded](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/a14dded56b5310973736c1da297fc40644416bcd))


### Tests

* add runtime behavior tests for generated proxy pattern ([4553667](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/4553667cc8b1c90fb292757f175f3bf775c93701))
* add snapshot tests for [Count] and [Histogram] generator output ([029e3ff](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/029e3ff8545b91460fbd90de2327ef70d2a49eaa))
