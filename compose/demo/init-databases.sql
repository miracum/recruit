-- executed by the postgres image's entrypoint on first startup, i.e. only if the data directory is empty
CREATE DATABASE fhir;
CREATE DATABASE recruit;
CREATE DATABASE hive_metastore;
