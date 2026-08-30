# Trino SQL-based Query Module

!!! warning

    This is an experimental feature that is still under development. Expect breaking changes at any time.

As of version `10.2.0`, it is possible to use recruIT without requiring the OHDSI OMOP stack.
Instead, cohorts can be defined using SQL that is directly executed against FHIR resources.
The prerequisite is that FHIR resources were previously encoded as Delta Lake tables using [Pathling](https://pathling.csiro.au/)
which can then be queried using [Trino](https://trino.io/). The same resources need to exist both as tables and inside a FHIR server
in order for the query module to correctly link them.

The advantage is that no semantic transformation from mapping FHIR resources to the OMOP CDM is necessary.

To create the necessary tables, you can install a Pathling server and use the [`$import` operation](https://pathling.csiro.au/docs/server/operations/import)
to import FHIR bulk exports. You can find a demo setup for this approach at <https://github.com/bzkf/trino-on-fhir> as well.
If you're using Kafka, then <https://github.com/bzkf/fhir-to-lakehouse> is another way.

Because the return values of an SQL query can be arbitrary, the `query-sql-on-fhir` module assumes that one of the result columns is called `patient_id`
and contains the FHIR ID of the Patient ressource satisfying the eligibility criteria.

The `query-sql-on-fhir` module is part of both the compose-based setup and the Helm chart deployment but needs to be enabled using `--profile=trino`
for compose, and `query-sql-on-fhir.enabled=true` for the chart. Both assume that Trino and the required Delta Lake tables are already available.
The best way to try it out with sample data is following [the development setup](../development/contributing.md#setup-for-the-trino-sql-based-query-module).

The diagram below shows the changes compared to the default setup using OMOP:

![recruIT with Trino Architecture](../_img/diagrams/recruit-components-with-trino.svg)

Instead of using ATLAS, trial metadata is stored by creating FHIR ResearchStudy resources directly.
These studies then reference the elgibility critera as a FHIR Library which includes the SQL query that
encodes the study eligibility criteria.

![FHIR resource relationships](../_img/diagrams/clinfhir-recruit-trino.png)

## SQL Query Encoding

The eligibility criteria's SQL query is encoded using the
[SQLQuery](https://build.fhir.org/ig/FHIR/sql-on-fhir-v2/StructureDefinition-SQLQuery.html) profile
from the [SQL on FHIR v2](https://build.fhir.org/ig/FHIR/sql-on-fhir-v2/index.html) implementation
guide: a FHIR `Library` resource whose `content` carries the Base64-encoded SQL, and whose
`relatedArtifact` entries (if any) reference the `ViewDefinition` resources ("tables") that the
query depends on.

`query-sql-on-fhir` decides how to execute the query depending on the Library's content:

- If the Library has **no** `relatedArtifact` entries (i.e. it doesn't depend on any
  `ViewDefinition`) **and** it has a `content` entry with `contentType` set to
  `application/sql;dialect=trino`, the SQL is executed directly against the configured Trino
  database.
- Otherwise (the query depends on one or more `ViewDefinition`s, or no `trino`-dialect content is
  present), the whole Library resource is sent as the `queryResource` input parameter to the
  [`$sqlquery-run`](https://build.fhir.org/ig/FHIR/sql-on-fhir-v2/OperationDefinition-SQLQueryRun.html)
  operation of a configured sql-on-fhir server. For the development setup, this is a
  [Pathling](https://pathling.csiro.au/) server, configured via the `sql-on-fhir.url` /
  `SQL_ON_FHIR_URL` property.
