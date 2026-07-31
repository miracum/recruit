FROM docker.io/library/gradle:9.6.1-jdk25@sha256:934a520ae0cc1f46764c2e6e1f6510d2fcdf6a7e12328b6aee34192d14f171a2 AS build
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

FROM gcr.io/distroless/java25-debian13:nonroot@sha256:94dc1090200950a4bdc530a3c3dc4094b3a2cfbeb3ae3b77d1abe41641efecb0
WORKDIR /app

ARG MODULE_NAME=query
COPY --from=build /home/gradle/project/${MODULE_NAME}/dependencies/ ./
COPY --from=build /home/gradle/project/${MODULE_NAME}/spring-boot-loader/ ./
COPY --from=build /home/gradle/project/${MODULE_NAME}/application/ ./

USER 65532:65532
EXPOSE 8080/tcp 8081/tcp
ENV SPRING_PROFILES_ACTIVE="prod"
ENTRYPOINT ["java", "org.springframework.boot.loader.launch.JarLauncher"]
