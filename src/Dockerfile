FROM docker.io/library/gradle:9.2.1-jdk25@sha256:3dec978fd14b3dc1083dfbeedfab9dd9da3d23bbb1a5d1e8b618b88e1ea3d35d AS build
WORKDIR /home/gradle/project

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

FROM gcr.io/distroless/java25-debian13:nonroot@sha256:fa9bfc14924fa3b43d43944d93887155d19843b3aa45610b659496f928fe2a9c
WORKDIR /app

ARG MODULE_NAME=query
COPY --from=build /home/gradle/project/${MODULE_NAME}/dependencies/ ./
COPY --from=build /home/gradle/project/${MODULE_NAME}/spring-boot-loader/ ./
COPY --from=build /home/gradle/project/${MODULE_NAME}/application/ ./

USER 65532:65532
EXPOSE 8080/tcp 8081/tcp
ENV SPRING_PROFILES_ACTIVE="prod"
ENTRYPOINT ["java", "org.springframework.boot.loader.launch.JarLauncher"]
