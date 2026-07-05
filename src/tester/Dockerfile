FROM docker.io/library/gradle:9.5.1-jdk25@sha256:b1220e480f72dd26482af4606e4bc541dda5e0ef350f90b51cf1c5092639ab61 AS build
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

COPY --from=docker.io/rancher/kubectl:v1.36.1@sha256:e430739d5eaef82b73e738ffa57019c8206c92b65ab3a18df766a1732b17c7f5 /bin/kubectl /usr/bin/kubectl

COPY --from=build /home/gradle/project/tester/src/main/resources/ /tmp/

USER 65532:65532
ENTRYPOINT ["java", "org.springframework.boot.loader.launch.JarLauncher"]
