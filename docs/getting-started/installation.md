# Quickstart

To get an initial impression of the app, you can quickly deploy it using
either Compose or Helm.

=== "Compose"

    Requires the [Docker CLI](https://docs.docker.com/get-started/get-docker/) with the Compose plugin.

    Download and extract the Compose files from the latest release:

    ```sh
    curl -fsSL https://github.com/open-recruit/recruit/releases/latest/download/recruit-compose.tgz | tar -xz
    ```

    Start all services with sample data and a pre-configured Keycloak for authentication:

    ```sh
    docker compose --project-name=recruit \
        --env-file=compose/.staging.env \
        -f compose/compose.yaml \
        -f compose/compose.staging.yaml up --detach
    ```

    Once everything is up, open the screening list at <http://recruit-list.127.0.0.1.nip.io> and log in
    as `uc1-admin` with password `admin`. Email notifications can be viewed at <http://maildev.127.0.0.1.nip.io>.

    To stop and remove everything again, run:

    ```sh
    docker compose --project-name=recruit \
        --env-file=compose/.staging.env \
        -f compose/compose.yaml \
        -f compose/compose.staging.yaml down --volumes
    ```

=== "Helm"

    Requires access to a Kubernetes cluster, e.g. a local one created using [KinD](https://kind.sigs.k8s.io/),
    as well as [kubectl](https://kubernetes.io/docs/tasks/tools/) and [helm](https://helm.sh/docs/intro/install/).

    Install the chart into a new `recruit` namespace:

    ```sh
    helm install -n recruit \
      --create-namespace \
      --render-subchart-notes \
      --wait \
      recruit oci://ghcr.io/open-recruit/recruit/charts/recruit
    ```

    Verify that all services are ready:

    ```sh
    helm test -n recruit recruit
    ```

    Follow the instructions printed after the installation to access the screening list.

    To remove everything again, run:

    ```sh
    helm uninstall -n recruit recruit
    kubectl delete namespace recruit
    ```

See these guides for a production deployment using your own data:

[Deploy to Kubernetes using Helm](../deployment/kubernetes.md){ .md-button }

[Deploy using Docker Compose](../deployment/docker-compose.md){ .md-button }

!!! success "Your first study"

    Once you've completed the installation, visit the [Creating your first study guide](./creating-your-first-study.md)
    to learn how to add a study to the system and view screening recommendations.

!!! tip "Kubernetes"

    If available, we recommend deploying to Kubernetes as it allows enabling advanced features including simplified configuration,
    observability, high-availability, and secure, mTLS-encrypted service-to-service communication via a service mesh.
