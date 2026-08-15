# fhir-constants-cs

Generated C# constant classes for the canonical `CodeSystem`/`StructureDefinition`
(Profile)/`Extension` URLs defined by the recruIT FHIR Implementation Guide (`../../../fhir/ig`), via
[`ig-codegen-cs`](https://github.com/diz-uker/to-fhir/tree/main/ig-codegen-cs) - the C# port of the
same [`ig-codegen`](https://github.com/diz-uker/to-fhir/tree/main/ig-codegen) library that produces
`../../fhir-constants` (the Java equivalent, consumed by `query-sql-on-fhir`).

Lives nested under `list-next/` rather than as a `src/`-level sibling module: `list-next`'s
container build only uses `src/list-next/` as its Docker build context (unlike the Gradle modules,
which share the whole `src/` tree), so a project reference to a sibling directory outside that
context wouldn't resolve during `docker build`.

`io.github.diz-uker.ig-codegen-core` is only needed at generation time, not at compile/runtime -
it's a `PackageReference` of the `Generate` console project (see `Generate/Generate.csproj`), not
of this library itself, so it doesn't end up on `list-next`'s dependency graph. The generated code
does carry one real runtime dependency, though: `Hl7.Fhir.R4` - the
`ScreeningListType`/`EligibilityObservationCategory` enums' `Coding()` accessors return a
`Hl7.Fhir.Model.Coding`, and `Extensions.ScreeningListBelongsToStudy(...)` returns a
`Hl7.Fhir.Model.Extension`.

Not published as a NuGet package - referenced directly via `ProjectReference` from `list.csproj`,
the only current C# consumer of recruit's own IG.

## Regenerating

The recruIT IG isn't published to a FHIR package registry, so there's no restorable package
cache here (unlike `ig-codegen`'s own CLI entry point, which is built around that model, and which
`../../fhir-ig-constants-cs` in the diz-uker/to-fhir repo uses for the externally-published MII
Kerndatensatz) - `GenerateIgConstants` (see `fhir-constants-cs.csproj`) instead runs a small local
console project (`Generate/`) that scans `fhir/ig/fsh-generated/resources` directly, via
`io.github.diz-uker.ig-codegen-core`'s lower-level `IgPackageScanner`/`CSharpConstantsGenerator`
API - the same directory `sushi` builds locally from `fhir/ig/input/fsh`.

1. From the repo root, run `npx fsh-sushi fhir/ig --snapshot` to (re-)generate
   `fhir/ig/fsh-generated/resources`.
2. Run `dotnet build src/list-next/fhir-constants-cs/fhir-constants-cs.csproj -t:GenerateIgConstants`.
3. Review the diff in `src/`, commit it.

## Usage

```csharp
using Recruit;

string codeSystemUrl = Recruit.CodeSystems.Urls.ScreeningListType;
string profileUrl = Recruit.Profiles.ScreeningList;

// CodeSystems with inline concepts also get an enum with a Coding() accessor:
Coding coding = Recruit.CodeSystems.ScreeningListType.ScreeningRecommendations.Coding();

// Extensions get a factory method typed to their value[x] (sushi's --snapshot flag is what makes
// this possible - see "Regenerating" above):
Extension ext = Recruit.Extensions.ScreeningListBelongsToStudy(new ResourceReference(researchStudyUrl));
```

## Updating the IG

Add or change FSH source under `fhir/ig/input/fsh`, then follow "Regenerating" above - review the
diff (added/removed/renamed constants), commit.
