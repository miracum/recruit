FROM docker.io/library/gradle:8.11.1-jdk21@sha256:7990a44ed0ad609ee740426d3becc69ae7d10a5ed14da7e354ad83cf7ef1d087 AS build
WORKDIR /home/gradle/project
ARG OPENTELEMETRY_JAVA_AGENT_VERSION=2.4.0
RUN wget --quiet "https://github.com/open-telemetry/opentelemetry-java-instrumentation/releases/download/v${OPENTELEMETRY_JAVA_AGENT_VERSION}/opentelemetry-javaagent.jar"

COPY --chown=gradle:gradle . .

ARG MODULE_NAME=query
RUN --mount=type=cache,target=/home/gradle/.gradle/caches <<EOF
gradle clean ":${MODULE_NAME}:build" -x test --info --no-daemon
gradle jacocoTestReport --no-daemon
java -Djarmode=layertools -jar ${MODULE_NAME}/build/libs/*.jar extract --destination "${MODULE_NAME}/"
EOF

FROM build AS test
ENV TZ="UTC"
RUN <<EOF
gradle ":${MODULE_NAME}:test"
gradle jacocoTestReport
EOF

FROM gcr.io/distroless/java21-debian12:nonroot@sha256:2985410a80560b788c15694d9dba8da051db5087f6e2a7cff64358650fdd91f5
WORKDIR /app

COPY --from=build /home/gradle/project/opentelemetry-javaagent.jar ./opentelemetry-javaagent.jar

ARG MODULE_NAME=query
COPY --from=build /home/gradle/project/${MODULE_NAME}/dependencies/ ./
COPY --from=build /home/gradle/project/${MODULE_NAME}/spring-boot-loader/ ./
COPY --from=build /home/gradle/project/${MODULE_NAME}/application/ ./

USER 65532:65532
EXPOSE 8080/tcp 8081/tcp
ENV SPRING_PROFILES_ACTIVE="prod"
ENTRYPOINT ["java", "org.springframework.boot.loader.launch.JarLauncher"]
