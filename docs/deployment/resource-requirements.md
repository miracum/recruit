# Resource requirements

## Memory limits

The `notify` and `query` module are Java Spring Boot applications with a fairly large memory footprint.
By default, the `-XX:MaxRAMPercentage` options is set to `85` inside the container. See for example
[here](https://support.cloudbees.com/hc/en-us/articles/204859670-Java-Heap-settings-Best-Practice#modernplatform)
and [here](https://focusedlabs.io/blog/the-no-nonsense-guide-to-jvm-14-memory-on-kubernetes-508m) for an explanation.

The `list` module is a NodeJS ExpressJS application with a significantly smaller footprint.

The staging Docker Compose deployment sets the container memory limit for `query` and `notify` to `512m` which is
sufficient to handle small and moderately sized cohorts. The limit should be larger than `256m` to avoid
OOM-kills. The screening list has fairly consistent memory usage and a limit of `128m` is reasonable.

Here's the output of `docker stats` after deploying and running the stack for a brief period:

```console
$ docker stats

CONTAINER ID   NAME                     CPU %     MEM USAGE / LIMIT   MEM %     NET I/O           BLOCK I/O   PIDS
82a98f752ebc   recruit-ohdsi-atlas-1    0.00%     16.68MiB / 64MiB    26.06%    25.3kB / 2.46kB   0B / 0B     25
a7297408bd00   recruit-query-1          0.06%     309.7MiB / 512MiB   60.50%    239kB / 135kB     0B / 0B     38
0d177572042b   recruit-notify-1         0.09%     254.2MiB / 512MiB   49.64%    154kB / 41.8kB    0B / 0B     42
f04d0d9d609f   recruit-list-1           0.00%     43.08MiB / 128MiB   33.66%    32.5kB / 11.1kB   0B / 0B     10
3674a31b2317   recruit-ohdsi-webapi-1   0.07%     1.079GiB / 4GiB     26.98%    332MB / 328MB     0B / 0B     81
e1b792d91d2a   recruit-fhir-1           0.20%     1.218GiB / 2GiB     60.88%    402kB / 7.18MB    0B / 0B     108
ab0e2da83cb2   recruit-traefik-1        0.00%     23.2MiB / 128MiB    18.12%    6.53MB / 6.52MB   0B / 0B     20
b89747f04bfb   recruit-maildev-1        0.00%     31.09MiB / 64MiB    48.57%    72.1kB / 13.4kB   0B / 0B     11
d3b4208c4c22   recruit-keycloak-1       10.74%    580.1MiB / 1GiB     56.65%    25.2kB / 25.9kB   0B / 0B     156
fefe3e6218f7   recruit-omopdb-1         99.80%    1.556GiB / 2GiB     77.79%    328MB / 332MB     0B / 0B     20
026e21b07821   recruit-fhir-db-1        0.01%     58.23MiB / 512MiB   11.37%    592kB / 246kB     0B / 0B     17
```

The same limits may be set for the Kubernetes deployment as well using the `notify.resources`, `query.resources`,
and `list.resources` Helm chart values. See also [Resource Management for Pods and Containers
](https://kubernetes.io/docs/concepts/configuration/manage-resources-containers/).
