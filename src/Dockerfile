FROM docker.io/library/gradle:9.6.1-jdk25@sha256:934a520ae0cc1f46764c2e6e1f6510d2fcdf6a7e12328b6aee34192d14f171a2 AS deps
SHELL ["/bin/bash", "-eo", "pipefail", "-c"]
WORKDIR /home/gradle/project

COPY . .

# builds every module this Dockerfile is used for in one shared, MODULE_NAME-independent Gradle
# invocation, rather than repeating "gradle build" once per module (parameterized by MODULE_NAME)
# as before. docker-bake.hcl builds all 4 of these targets in one `docker buildx bake` invocation,
# and BuildKit runs independent targets *concurrently* by default - so a RUN instruction that still
# varied by MODULE_NAME meant 4 Gradle daemons racing to download the exact same plugins/dependencies
# at the same time, on one shared runner, instead of the single shared download this stage now does
# once for real (verified: before this change, spring-boot-gradle-plugin-4.1.0.jar was independently
# downloaded 4 times, a few seconds apart, in the same CI job).
RUN --mount=type=cache,target=/home/gradle/.gradle/caches <<EOF
gradle clean build -x test --info
EOF

FROM deps AS build
ARG MODULE_NAME=query
RUN <<EOF
java -Djarmode=tools -jar ${MODULE_NAME}/build/libs/*.jar extract --layers --launcher --force \
    --destination "${MODULE_NAME}/"
EOF

FROM build AS test
ENV TZ="UTC"
RUN --mount=type=cache,target=/home/gradle/.gradle/caches <<EOF
gradle ":${MODULE_NAME}:test"
gradle jacocoTestReport
EOF

FROM gcr.io/distroless/java25-debian13:nonroot@sha256:2ce7f9eb870273fcb76131cf1e3b0f3e4ef48ee86679166cae1d53fe035c47aa
WORKDIR /app

ARG MODULE_NAME=query
COPY --from=build /home/gradle/project/${MODULE_NAME}/dependencies/ ./
COPY --from=build /home/gradle/project/${MODULE_NAME}/spring-boot-loader/ ./
COPY --from=build /home/gradle/project/${MODULE_NAME}/module-dependencies/ ./
COPY --from=build /home/gradle/project/${MODULE_NAME}/application/ ./

USER 65532:65532
ENV SPRING_PROFILES_ACTIVE="prod"
ENTRYPOINT ["java", "org.springframework.boot.loader.launch.JarLauncher"]
