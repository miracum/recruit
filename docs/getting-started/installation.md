# Quickstart

To get an initial impression of the app, you can quickly deploy it using
either Compose or Helm.

=== "Compose"

    Requires the [Docker CLI](https://docs.docker.com/get-started/get-docker/)
    with the Compose plugin version 5.6.0 or later.

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

    Once the command exits, you can open the screening list at <http://127.0.0.1:32080> and see the demo study.

    You can now either [create your own study](creating-your-first-study.md) or head straight to the
    production deployment guide using [Compose](../deployment/compose.md).

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

    Install the chart into a new `recruit` namespace, including demo fixtures:

    <!-- x-release-please-start-version -->

    ```sh
    helm install -n recruit \
      --create-namespace \
      --render-subchart-notes \
      --version 0.0.0-pr651 \
      --wait \
      --timeout 30m \
      recruit-demo \
      oci://ghcr.io/open-recruit/recruit/charts/recruit \
      -f - <<'EOF'
    --8<-- "recruit/values-demo.yaml"
    EOF
    ```

    <!-- x-release-please-end-version -->

    The install takes several minutes to download the images, generate synthetic patients, load them into
    the FHIR server, and register the same resources as Delta Lake tables for querying by Trino.

    Once the command exits, forward the screening list port:

    ```sh
    kubectl -n recruit port-forward svc/recruit-demo-list 32080:8080
    ```

    and open the screening list at <http://127.0.0.1:32080> and see the demo study.

    To remove everything again, run:

    ```sh
    helm uninstall -n recruit recruit-demo
    kubectl delete namespace recruit
    ```
