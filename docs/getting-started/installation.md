# Quickstart

To get an initial impression of the app, you can quickly deploy it using
either Compose or Helm.

=== "Compose"

    Requires the [Docker CLI](https://docs.docker.com/get-started/get-docker/)
    with the Compose plugin version 5.6.0 or later on a machine with around
    16 GiB of memory.

    Download and extract the Compose files from the latest release:

    ```sh
    curl -fsSL https://github.com/open-recruit/recruit/releases/latest/download/recruit-compose.tgz | tar -xz
    ```

    Start all services, including demo fixtures:

    ```sh
    docker compose --project-name=recruit-demo \
        --env-file=compose/.demo.env \
        -f compose/compose.yaml \
        -f compose/compose.demo.yaml up --detach --wait
    ```

    The first start takes several minutes to download the images, pre-load the FHIR server with synthetic data,
    and register the same resources as Delta Lake tables for querying by Trino.

    Once the command exits, open the screening list at <http://127.0.0.1:32080>. Authentication is disabled in the
    demo, so there's no login. The query module checks for studies every minute, so the demo study's screening list
    shows up about a minute later.

    ??? info "What's included"

        Besides the two recruIT modules, the screening list (`list`) and the query module (`query-sql-on-fhir`),
        `compose.demo.yaml` starts everything they depend on:

        - a [HAPI FHIR](https://github.com/hapifhir/hapi-fhir-jpaserver-starter) server, pre-loaded with a dozen
          [Synthea](https://synthetichealth.github.io/synthea/) patients and a demo study
        - a lakehouse with the same patients as [Delta Lake](https://delta.io/) tables, made up of SeaweedFS as object
          storage, a Hive metastore, and [Trino](https://trino.io/) to query them
        - PostgreSQL for the FHIR server, the screening list, and the Hive metastore
        - [MailDev](https://github.com/maildev/maildev), a mock mail server that catches the notification emails,
          at <http://127.0.0.1:32025>

        For a production installation, all of these are replaced by your own services, see
        [Docker Compose](../deployment/compose.md#production-installation).

    ??? tip "Troubleshooting"

        Follow the query module's logs to see it pick up the studies and run their eligibility criteria:

        ```sh
        docker compose --project-name=recruit-demo logs --follow query-sql-on-fhir
        ```

        Check that every service is healthy:

        ```sh
        docker compose --project-name=recruit-demo \
            --env-file=compose/.demo.env \
            -f compose/compose.yaml \
            -f compose/compose.demo.yaml \
            -f compose/compose.probe.yaml run --rm health-probes
        ```

    To stop and remove everything, including demo data, run:

    ```sh
    docker compose --project-name=recruit-demo down --volumes
    ```

=== "Helm"

    Requires access to a Kubernetes cluster with around 16 GiB of memory, e.g. a local one created using
    [KinD](https://kind.sigs.k8s.io/), as well as [kubectl](https://kubernetes.io/docs/tasks/tools/) and
    [helm](https://helm.sh/docs/intro/install/).

    Create a basic KinD cluster:

    ```sh
    kind create cluster
    ```

    Install the chart into a new `recruit` namespace, with demo fixtures enabled:

    <!-- x-release-please-start-version -->

    ```sh
    helm upgrade --install -n recruit \
      --create-namespace \
      --render-subchart-notes \
      --version 0.0.0 \
      --wait \
      --timeout 30m \
      -f https://raw.githubusercontent.com/open-recruit/recruit/0.0.0/charts/recruit/values-demo.yaml \
      recruit-demo \
      oci://ghcr.io/open-recruit/recruit/charts/recruit
    ```

    <!-- x-release-please-end-version -->

    ??? example "Demo values"

        The [`values-demo.yaml`](https://github.com/open-recruit/recruit/blob/master/charts/recruit/values-demo.yaml)
        used above enables the query module and a sample study, and generates synthetic patients that are loaded
        into the FHIR server and queried by the query module via an embedded DuckDB, see
        [Querying FHIR resources with DuckDB](../trino/index.md#querying-fhir-resources-with-duckdb):

        ```yaml title="values-demo.yaml"
        --8<-- "recruit/values-demo.yaml"
        ```

    The install takes several minutes to download the images, generate synthetic patients, and load them into
    the FHIR server.

    Once the command exits, forward the screening list port:

    ```sh
    kubectl -n recruit port-forward svc/recruit-demo-list 32080:8080
    ```

    and open the screening list at <http://127.0.0.1:32080> to see the demo study. It might take a couple of minutes
    to appear.

    To remove everything again, run:

    ```sh
    helm uninstall -n recruit recruit-demo
    kubectl delete namespace recruit
    ```

!!! tip

    You can now either [create your own study](creating-your-first-study.md) or head straight to the production deployment
    guide using [Compose](../deployment/compose.md) or [Kubernetes](../deployment/kubernetes.md).
