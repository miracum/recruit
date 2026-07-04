FROM docker.io/library/gradle:9.4.1-jdk25@sha256:8e41f079ca18bf00994f0ba7ca5cbfb7b0c2fdafd72bee98e6c3785bd9ac2939 AS build
SHELL ["/bin/bash", "-eo", "pipefail", "-c"]
WORKDIR /home/gradle/project

COPY . .

RUN --mount=type=cache,target=/home/gradle/.gradle/caches <<EOF
gradle clean ":tester:build" -x test --info
java -Djarmode=layertools -jar tester/build/libs/*.jar extract --destination "tester/"
EOF

FROM build AS test
ENV TZ="UTC"
RUN <<EOF
gradle ":tester:test"
gradle jacocoTestReport
EOF

FROM gcr.io/distroless/java25-debian13:nonroot@sha256:ace83a068839dbfb151b0d80693df23120f6d13f963427fde7e43d9a175fd54a
WORKDIR /app

COPY --from=build /home/gradle/project/tester/dependencies/ ./
COPY --from=build /home/gradle/project/tester/spring-boot-loader/ ./
COPY --from=build /home/gradle/project/tester/application/ ./

COPY --from=docker.io/rancher/kubectl:v1.36.0@sha256:05d2b313e2f397e0ade252136aed47abd72d56ead11d1b027ac70f66362c8495 /bin/kubectl /usr/bin/kubectl

COPY --from=fixtures chaos.yaml sample-list-bundle.json /tmp/

USER 65532:65532
ENTRYPOINT ["java", "org.springframework.boot.loader.launch.JarLauncher"]
