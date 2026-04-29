# Changelog

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
