# fhir-constants

Generated Java constant classes for the canonical `CodeSystem`/`StructureDefinition`
(Profile)/`Extension` URLs defined by the recruIT FHIR Implementation Guide (`../../fhir/ig`), via
[`ig-codegen`](https://github.com/diz-uker/to-fhir/tree/main/ig-codegen).

`ig-codegen` itself is only needed at generation time, not at compile/runtime for this module - it's
on the `generateIgConstants` task's `buildscript` classpath (see `build.gradle`), not an
`api`/`implementation` dependency here, and scoped to this one build script rather than `buildSrc`
so it doesn't end up on every other module's build script classpath too. The generated code does
carry one real runtime dependency, though: `hapi-fhir-structures-r4` (`api` scope) — the
`CodeSystems.ScreeningListType` enum's `coding()` accessor returns a
`org.hl7.fhir.r4.model.Coding`.

## Regenerating

The recruIT IG isn't published to a FHIR package registry, so there's no `package.json`/package
cache to restore here (unlike `ig-codegen`'s own CLI entry point, which is built around that
model) — `generateIgConstants` (see `build.gradle`) scans `fhir/ig/fsh-generated/resources`
directly, via ig-codegen's lower-level `IgPackageScanner`/`JavaConstantsGenerator` API, the same
directory `sushi` builds locally from `fhir/ig/input/fsh`.

1. From the repo root, run `npx fsh-sushi fhir/ig` to (re-)generate
   `fhir/ig/fsh-generated/resources`.
2. Run `./gradlew :fhir-constants:generateIgConstants`.
3. Review the diff in `src/main/java`, commit it.

## Usage

```java
import recruit.Recruit;

String codeSystemUrl = Recruit.CodeSystems.screeningListType();
String profileUrl = Recruit.Profiles.screeningList();

// CodeSystems with inline concepts (content == "complete") also get an enum with a coding() accessor:
Coding coding = Recruit.CodeSystems.ScreeningListType.SCREENING_RECOMMENDATIONS.coding();
```

## Known limitation: `Extensions` factory methods aren't typed to their `value[x]`

`ig-codegen` determines an extension's `value[x]` shape from the `StructureDefinition.snapshot`.
`sushi` only emits a `differential`, not a `snapshot` (snapshot generation requires the full HL7 IG
Publisher, which this repo doesn't run as part of the regular build — see the commented-out "build
FHIR IG" step in `.github/workflows/build-docs.yaml`). Without a snapshot, every extension falls
back to the generic no-arg factory method, e.g.:

```java
Extension ext = Recruit.Extensions.screeningListBelongsToStudy();
ext.setValue(new Reference(researchStudy));
```

rather than the precisely-typed `screeningListBelongsToStudy(Reference value)` overload
`ig-codegen` would generate from a full snapshot. If the IG Publisher gets wired into the build
later, rerunning `generateIgConstants` against its snapshot-bearing output will pick up the more
specific overloads automatically.

## Updating the IG

Add or change FSH source under `fhir/ig/input/fsh`, then follow "Regenerating" above — review the
diff (added/removed/renamed constants), commit.
