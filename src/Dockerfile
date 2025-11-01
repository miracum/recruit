FROM docker.io/library/gradle:9.1.0-jdk21@sha256:db12d4789367d4676ef3d8aa672685e0e451705776b53f7d81c953fba3b1d55b AS build
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

FROM gcr.io/distroless/java21-debian12:nonroot@sha256:5d281651dbf431f0fa9bc28bce5514a5594d66ccd2f9bf8602adccfc2518d4f9
WORKDIR /app

ARG MODULE_NAME=query
COPY --from=build /home/gradle/project/${MODULE_NAME}/dependencies/ ./
COPY --from=build /home/gradle/project/${MODULE_NAME}/spring-boot-loader/ ./
COPY --from=build /home/gradle/project/${MODULE_NAME}/application/ ./

USER 65532:65532
EXPOSE 8080/tcp 8081/tcp
ENV SPRING_PROFILES_ACTIVE="prod"
ENTRYPOINT ["java", "org.springframework.boot.loader.launch.JarLauncher"]
