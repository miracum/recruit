variable "REGISTRY" {
  default = "ghcr.io/miracum/recruit"
}

variable "TAG" {
  default = "latest"
}

group "default" {
  targets = [
    "query", "query-test",
    "notify", "notify-test",
    "list", "list-test",
    "list-next",
    "query-sql-on-fhir", "query-sql-on-fhir-test",
    "superset-library-sync", "superset-library-sync-test",
  ]
}

# the 4 Java modules all share the same context/Dockerfile (src/Dockerfile), differing only in
# which module's jar gets extracted - building them together in one bake invocation is what lets
# BuildKit share the COPY . . layer and the RUN --mount=type=cache Gradle dependency cache across
# them, since separate `docker build` invocations (one per module, one per CI job/runner) never
# shared that cache mount to begin with.

target "query" {
  context = "src"
  args    = { MODULE_NAME = "query" }
  tags    = ["${REGISTRY}/query:${TAG}"]
}

target "query-test" {
  inherits = ["query"]
  target   = "test"
  tags     = ["${REGISTRY}/query-test:${TAG}"]
}

target "notify" {
  context = "src"
  args    = { MODULE_NAME = "notify" }
  tags    = ["${REGISTRY}/notify:${TAG}"]
}

target "notify-test" {
  inherits = ["notify"]
  target   = "test"
  tags     = ["${REGISTRY}/notify-test:${TAG}"]
}

target "query-sql-on-fhir" {
  context = "src"
  args    = { MODULE_NAME = "query-sql-on-fhir" }
  tags    = ["${REGISTRY}/query-sql-on-fhir:${TAG}"]
}

target "query-sql-on-fhir-test" {
  inherits = ["query-sql-on-fhir"]
  target   = "test"
  tags     = ["${REGISTRY}/query-sql-on-fhir-test:${TAG}"]
}

target "superset-library-sync" {
  context = "src"
  args    = { MODULE_NAME = "superset-library-sync" }
  tags    = ["${REGISTRY}/superset-library-sync:${TAG}"]
}

target "superset-library-sync-test" {
  inherits = ["superset-library-sync"]
  target   = "test"
  tags     = ["${REGISTRY}/superset-library-sync-test:${TAG}"]
}

target "list" {
  context = "src/list"
  tags    = ["${REGISTRY}/list:${TAG}"]
}

target "list-test" {
  inherits = ["list"]
  target   = "test"
  tags     = ["${REGISTRY}/list-test:${TAG}"]
}

# no test stage in src/list-next/Dockerfile, so no list-next-test target.
target "list-next" {
  context = "src/list-next"
  tags    = ["${REGISTRY}/list-next:${TAG}"]
}
