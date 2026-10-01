FROM docker.io/library/gradle:9.7.0-jdk25@sha256:35b2b4054977b95348bdd126e808e245fc1e28e56537b51cfd90c7f355b5a105 AS build
SHELL ["/bin/bash", "-eo", "pipefail", "-c"]
WORKDIR /home/gradle/project

COPY . .

ARG MODULE_NAME=query
RUN --mount=type=cache,target=/home/gradle/.gradle/caches <<EOF
gradle clean ":${MODULE_NAME}:build" -x test --info
java -Djarmode=tools -jar ${MODULE_NAME}/build/libs/*.jar extract --layers --launcher --force \
    --destination "${MODULE_NAME}/"
EOF

FROM build AS test
ENV TZ="UTC"
RUN <<EOF
gradle ":${MODULE_NAME}:test"
gradle jacocoTestReport
EOF

FROM gcr.io/distroless/java25-debian13:nonroot@sha256:2ce7f9eb870273fcb76131cf1e3b0f3e4ef48ee86679166cae1d53fe035c47aa
WORKDIR /app

ARG MODULE_NAME=query
COPY --from=build /home/gradle/project/${MODULE_NAME}/dependencies/ ./
COPY --from=build /home/gradle/project/${MODULE_NAME}/spring-boot-loader/ ./
COPY --from=build /home/gradle/project/${MODULE_NAME}/application/ ./

USER 65532:65532
EXPOSE 8080/tcp 8081/tcp
ENV SPRING_PROFILES_ACTIVE="prod"
ENTRYPOINT ["java", "org.springframework.boot.loader.launch.JarLauncher"]
