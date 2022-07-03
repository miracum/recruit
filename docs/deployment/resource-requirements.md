# Resource requirements

## Memory limits

The `notify` and `query` module are Java Spring Boot applications with a fairly large memory footprint.
By default, the `-XX:MaxRAMPercentage` options is set to `85` inside the container. See for example
[here](https://support.cloudbees.com/hc/en-us/articles/204859670-Java-Heap-settings-Best-Practice#modernplatform)
and [here](https://focusedlabs.io/blog/the-no-nonsense-guide-to-jvm-14-memory-on-kubernetes-508m) for an explanation.

The `list` module is a NodeJS ExpressJS application with a significantly smaller footprint.

The staging Docker Compose deployment sets the container memory limit for `query` and `notify` to `512m` which is
sufficient to handle small and moderately-sized cohorts. The limit should be larger than `256m` to avoid
OOM-kills. The screening list has fairly consistent memory usage and a limit of `64m` is adequate.

Here's the output of `docker stats` after deploying and running the stack for a brief period:

```console
$ docker stats

CONTAINER ID   NAME                     CPU %     MEM USAGE / LIMIT   MEM %     NET I/O           BLOCK I/O   PIDS
6d97c2c07f6c   recruit-omopdb-1         0.00%     1.335GiB / 2GiB     66.74%    1.44MB / 77.1MB   0B / 0B     17
4401b85966cc   recruit-fhir-db-1        0.06%     57.08MiB / 512MiB   11.15%    572kB / 229kB     0B / 0B     17
6b7367e52147   recruit-keycloak-1       1.85%     567.6MiB / 1GiB     55.43%    24kB / 25.2kB     0B / 0B     156
7ab9b22280c1   recruit-maildev-1        1.14%     30.72MiB / 64MiB    48.00%    49.8kB / 2.79kB   0B / 0B     11
76a07054c5b6   recruit-ohdsi-webapi-1   0.09%     910.2MiB / 2GiB     44.44%    77MB / 1.33MB     0B / 0B     81
a41d06e5bf77   recruit-fhir-1           0.61%     1.174GiB / 2GiB     58.70%    310kB / 790kB     0B / 0B     97
76dc0480a374   recruit-ohdsi-atlas-1    0.00%     16.65MiB / 64MiB    26.02%    22.1kB / 0B       0B / 0B     25
959e994462bc   recruit-query-1          0.06%     311MiB / 512MiB     60.75%    238kB / 135kB     0B / 0B     39
782082ab5cfe   recruit-list-1           0.00%     42.26MiB / 64MiB    66.03%    22.1kB / 0B       0B / 0B     10
8b470b0610d5   recruit-notify-1         0.13%     257MiB / 512MiB     50.19%    154kB / 41.8kB    0B / 0B     41
```

The same limits may be set for the Kubernetes deployment as well using the `notify.resources`, `query.resources`,
and `list.resources` Helm chart values. See also [Resource Management for Pods and Containers
](https://kubernetes.io/docs/concepts/configuration/manage-resources-containers/).
