# Development

See <https://miracum.github.io/recruit/development/contributing/> for
detailed instructions on working with the source code in this folder.

## Regenerating FHIR IG constants

Two modules generate typed constant classes (`CodeSystem`/`StructureDefinition`
(Profile)/`Extension` URLs, plus `NamingSystem` unique IDs) from the recruIT FHIR Implementation
Guide (`../fhir/ig`), via [`ig-codegen`](https://github.com/diz-uker/to-fhir/tree/main/ig-codegen)
(Java) / [`ig-codegen-cs`](https://github.com/diz-uker/to-fhir/tree/main/ig-codegen-cs) (C#):

- [`fhir-constants`](fhir-constants/README.md) - Java, consumed by `query-sql-on-fhir`.
- [`list-next/fhir-constants-cs`](list-next/fhir-constants-cs/README.md) - C#, consumed by
  `list-next`.

Both scan `fhir/ig/fsh-generated/resources` directly (rather than going through either tool's own
package.json-driven CLI) since the recruIT IG isn't published to a FHIR package registry - sushi's
own local output is the only copy. That means both regeneration steps below share the same first
step:

1. From the repo root, run `npx fsh-sushi fhir/ig --snapshot` to (re-)generate
   `fhir/ig/fsh-generated/resources`.
2. Regenerate one or both languages:
   - Java: `./gradlew :fhir-constants:generateIgConstants`
   - C#: `dotnet build list-next/fhir-constants-cs/fhir-constants-cs.csproj -t:GenerateIgConstants`
3. Review the diff (added/removed/renamed constants), commit it.

See the two module READMEs linked above for the generated API shape and usage examples in each
language.
