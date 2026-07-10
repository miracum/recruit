FROM docker.io/library/gradle:9.6.0-jdk25@sha256:e3905233ae349e72016daf8a0e19f085a1dd89ded8ec88b3d8335d3fd0b350f4 AS build
SHELL ["/bin/bash", "-eo", "pipefail", "-c"]
WORKDIR /home/gradle/project

COPY . .

RUN --mount=type=cache,target=/home/gradle/.gradle/caches <<EOF
gradle clean ":tester:build" -x test --info
java -Djarmode=tools -jar tester/build/libs/*.jar extract --layers --launcher --force \
    --destination "tester/"
EOF

FROM build AS test
ENV TZ="UTC"
RUN <<EOF
gradle ":tester:test"
gradle jacocoTestReport
EOF

FROM gcr.io/distroless/java25-debian13:nonroot@sha256:dade01b669efd3bea3977f73cc196c56f1ee678a71ec8305f84ec15fd5a23c8d
WORKDIR /app

COPY --from=build /home/gradle/project/tester/dependencies/ ./
COPY --from=build /home/gradle/project/tester/spring-boot-loader/ ./
COPY --from=build /home/gradle/project/tester/application/ ./

COPY --from=docker.io/rancher/kubectl:v1.36.2@sha256:06c7a7a9772737494ae1e0c3af90f1b5385630c147e47c1c7cea92f4bed55fbe /bin/kubectl /usr/bin/kubectl

COPY --from=build /home/gradle/project/tester/src/main/resources/ /tmp/

USER 65532:65532
ENTRYPOINT ["java", "org.springframework.boot.loader.launch.JarLauncher"]
