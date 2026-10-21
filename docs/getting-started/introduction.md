# Introduction

recruIT is a software to assist with the recruitment of patients for clinical trials.

It regularly queries a patient health data repository for potential candidates and displays them on
a list to support manual screening.

It supports querying databases via either [Trino](https://trino.io/) or [DuckDB](https://duckdb.org/).
Eligibility criteria are represented using SQL, encoded as [SQL-on-FHIR SQLQuery Library resources](https://build.fhir.org/ig/FHIR/sql-on-fhir-v2/StructureDefinition-SQLQuery.html).

Internally, the modules communicate using HL7 FHIR® via a central FHIR server.

[Get Started 🚀](installation.md){ .md-button .md-button--primary}

## Architecture

Fundamentally, the design of the application is based on the publication [Design and Multicentric Implementation of a Generic Software Architecture for Patient Recruitment Systems Re-Using Existing HIS Tools and Routine Patient Data](https://dx.doi.org/10.4338%2FACI-2013-07-RA-0047) by Trinczek et al.
Of the 5 presented modules, recruIT implements the notification module (`notify`), the screening list module (`list`),
and the query module (`query`). The patient data module can be any database queryable via either Trino or DuckDB.
