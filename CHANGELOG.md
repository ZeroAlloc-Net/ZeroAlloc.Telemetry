# Changelog

## [1.9.0](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/compare/v1.8.0...v1.9.0) (2026-10-01)


### Features

* metric conventions for histogram units, buckets, tags and elements ([#177](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/issues/177)) ([4067f95](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/4067f95515a42ab0ddc6612983fbe4e91b2a41af)), closes [#171](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/issues/171)
* span kind, error status, parameter name tokens and tags at start ([#178](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/issues/178)) ([f46f92c](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/f46f92c215d83d4f57ef36017f889734033666f2)), closes [#170](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/issues/170)
* stop generated awaits capturing the context, and version source and meter ([#179](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/issues/179)) ([f1a7be4](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/f1a7be4ecdb150b573d5fc10208b6bda863aaceb)), closes [#172](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/issues/172)


### Bug Fixes

* emit type parameters and constraints on generic proxy methods ([#174](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/issues/174)) ([1c90011](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/1c90011d332b5736011e663659312caa220181eb)), closes [#168](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/issues/168)
* every interface member shape compiles in the proxy or is reported ([#180](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/issues/180)) ([c8bc16f](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/c8bc16f46039917cfc6967ea6a2b95339e05a64a)), closes [#173](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/issues/173)


### Performance

* return the inner task when nothing listens to an awaitable method ([#176](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/issues/176)) ([df10380](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/df10380909cdf771f233a9e4ca7c4b762f0e5ba7))

## [1.8.0](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/compare/v1.7.0...v1.8.0) (2026-09-30)


### Features

* add [MetricTagFromResult] to tag metrics with values read from the result ([#164](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/issues/164)) ([a2b193d](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/a2b193d72569da3d068a9e882b0e26a0e1a6899a)), closes [#150](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/issues/150)


### Bug Fixes

* generate proxies for nested and generic instrumented interfaces ([#166](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/issues/166)) ([987f929](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/987f929cfbb764bdb49a4c75900503781e408fe4))
* name generated files after the instrumented interface's full name ([#163](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/issues/163)) ([b930747](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/b930747d26a50071860122eeeef2671aec7aa0cb)), closes [#161](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/issues/161)

## [1.7.0](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/compare/v1.6.4...v1.7.0) (2026-09-27)


### Features

* **generator:** add When, Unit and Description to Count and Histogram ([9aa8bca](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/9aa8bca73c2c0808eb72d8aca86b2782cfe742cc))
* **generator:** record metrics from the return value with CountFromResult and HistogramFromResult ([9aa8bca](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/9aa8bca73c2c0808eb72d8aca86b2782cfe742cc))
* **generator:** report bad member paths and non-bool When guards as ZTEL007 and ZTEL008 ([9aa8bca](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/9aa8bca73c2c0808eb72d8aca86b2782cfe742cc))
* **generator:** report ZTEL003 for TraceTagFromResult and TraceTagConstant outside Instrument types ([d6692b1](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/d6692b1e2b412ae2cb2910fc85066c3b543f4e57))
* **generator:** warn with ZTEL010 when a TraceTag member path does not resolve, and emit no tag ([d6692b1](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/d6692b1e2b412ae2cb2910fc85066c3b543f4e57))


### Bug Fixes

* **core:** use constructor arguments in the InstrumentAttribute example ([9aa8bca](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/9aa8bca73c2c0808eb72d8aca86b2782cfe742cc))
* **generator:** declare the catch variable only when the span reads it ([9aa8bca](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/9aa8bca73c2c0808eb72d8aca86b2782cfe742cc))
* **generator:** detect Task, ValueTask and task-like returns by symbol instead of by name ([9aa8bca](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/9aa8bca73c2c0808eb72d8aca86b2782cfe742cc))
* **generator:** escape activity source, span, metric and tag names in generated string literals ([9aa8bca](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/9aa8bca73c2c0808eb72d8aca86b2782cfe742cc))
* **generator:** keep null-safe access after a Value segment on a nullable value type ([9aa8bca](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/9aa8bca73c2c0808eb72d8aca86b2782cfe742cc))
* **generator:** key metric fields by instrument kind and emit valid, distinct identifiers ([9aa8bca](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/9aa8bca73c2c0808eb72d8aca86b2782cfe742cc))


### Performance

* make generator models value-equatable so incremental caching hits ([#155](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/issues/155)) ([77f6b7e](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/77f6b7e6f76b8c1caa146aacace3b4329627e777)), closes [#151](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/issues/151)

## [1.6.4](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/compare/v1.6.3...v1.6.4) (2026-09-26)


### Bug Fixes

* mark released analyzer rules and public api as shipped and automate the move ([#144](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/issues/144)) ([d08bd9a](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/d08bd9ab8089d462feeaec2cac5b651d840fa04e))

## [1.6.3](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/compare/v1.6.2...v1.6.3) (2026-09-20)


### Bug Fixes

* **ci:** pin the SDK floor at the .NET 10 GA band, not the newest patch ([#131](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/issues/131)) ([a984e0c](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/a984e0c7fcb34b042a496db05c5a7fa673f06b16))

## [1.6.2](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/compare/v1.6.1...v1.6.2) (2026-09-19)


### Bug Fixes

* **ci:** stamp the assembly version when publishing from a manifest ([#126](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/issues/126)) ([527a765](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/527a76517e895f3a08502113d8c3fb5cd43954a0))

## [1.6.1](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/compare/v1.6.0...v1.6.1) (2026-08-07)


### Bug Fixes

* **generator:** suppress EPS06 for Roslyn 4.14's larger pipeline struct ([#58](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/issues/58)) ([78df711](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/78df7116884497cea7441d48e187b858081a398f))

## [1.6.0](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/compare/v1.5.0...v1.6.0) (2026-08-07)


### Features

* **generator:** substitute {type} in span names per implementation ([#54](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/issues/54)) ([13c5bba](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/13c5bba31001966cd7185c023fdf6bd0d154f3e6)), closes [#53](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/issues/53)

## [1.5.0](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/compare/v1.4.2...v1.5.0) (2026-08-07)


### Features

* **generator:** add [TraceTagConstant] for compile-time constant tags ([#48](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/issues/48)) ([e9c363e](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/e9c363e068d98e42dc8b60cd08f2d42281ba1d1c)), closes [#36](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/issues/36)
* **generator:** add When to [TraceTagFromResult] for conditional tags ([#52](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/issues/52)) ([59e45ae](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/59e45ae6a11dd537fedc04da35915d4c6c8e1934)), closes [#37](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/issues/37)
* **generator:** let [TraceTag] read a member of an argument ([#51](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/issues/51)) ([9ae1819](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/9ae18190de77aff70ff5c35ba6ec36cd652524d8)), closes [#35](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/issues/35)


### Bug Fixes

* **generator:** make dotted member paths null-safe and compilable ([#46](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/issues/46)) ([56be404](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/56be4043b95f04c2dd30a2b260241ce6cdcd2f54))

## [1.4.2](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/compare/v1.4.1...v1.4.2) (2026-08-07)


### Bug Fixes

* **generator:** suppress EPC12 in the emitted proxy ([#42](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/issues/42)) ([08f1d44](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/08f1d446f602d7ab48a5eb43cf62cb29bb22346d)), closes [#38](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/issues/38)

## [1.4.1](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/compare/v1.4.0...v1.4.1) (2026-08-07)


### Bug Fixes

* **generator:** preserve nullable annotations in generated signatures ([#33](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/issues/33)) ([f648d03](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/f648d03720814e3d6502ffcf57eed2133033d243)), closes [#29](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/issues/29)

## [1.4.0](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/compare/v1.3.0...v1.4.0) (2026-08-07)


### Features

* **generator:** record span tags from arguments and results ([#31](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/issues/31)) ([79b6d59](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/commit/79b6d5919df99f0d93e77346d4344ff9ebd43013)), closes [#30](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/issues/30)

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
