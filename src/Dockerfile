FROM docker.io/library/gradle:8.6.0-jdk17@sha256:27ed98487dd9c155d555955084dfd33f32d9f7ac5a90a79b1323ab002a1a8b6e AS build
WORKDIR /home/gradle/src
ENV GRADLE_USER_HOME="/gradle"

ARG OPENTELEMETRY_JAVA_AGENT_VERSION=1.30.0
RUN wget --quiet "https://github.com/open-telemetry/opentelemetry-java-instrumentation/releases/download/v${OPENTELEMETRY_JAVA_AGENT_VERSION}/opentelemetry-javaagent.jar"

COPY settings.gradle ./
COPY config/ config/
COPY buildSrc/ buildSrc/

ARG MODULE_NAME=query
COPY ${MODULE_NAME}/build.gradle ./${MODULE_NAME}/

RUN gradle clean build --no-daemon || true

COPY --chown=gradle:gradle . .

RUN gradle ":${MODULE_NAME}:build" -x test --info && \
    java -Djarmode=layertools -jar ${MODULE_NAME}/build/libs/*.jar extract --destination "${MODULE_NAME}/"

FROM build AS test
ENV TZ="UTC"
RUN gradle ":${MODULE_NAME}:test" && \
    gradle jacocoTestReport

FROM gcr.io/distroless/java17-debian12:nonroot@sha256:9401092582fef8d81c60b7d3f48a671c522759bc3a36a43536e38d8c8528054d
WORKDIR /app

COPY --from=build /home/gradle/src/opentelemetry-javaagent.jar ./opentelemetry-javaagent.jar

ARG MODULE_NAME=query
COPY --from=build /home/gradle/src/${MODULE_NAME}/dependencies/ ./
COPY --from=build /home/gradle/src/${MODULE_NAME}/spring-boot-loader/ ./
COPY --from=build /home/gradle/src/${MODULE_NAME}/application/ .

USER 65532:65532
EXPOSE 8080/tcp 8081/tcp
ENV SPRING_PROFILES_ACTIVE="prod"
ENTRYPOINT ["java", "org.springframework.boot.loader.launch.JarLauncher"]
