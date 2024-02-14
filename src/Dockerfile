FROM docker.io/library/gradle:8.6.0-jdk17@sha256:05a2d55e715fa98b1bb75a87c9589d349721137ae0825c4ee726907523a62af7 AS build
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

FROM gcr.io/distroless/java17-debian12:nonroot@sha256:33ef074660b7d18c6e4ba80de767867cef56905ed362e51f8d76fc737fdfe249
WORKDIR /app

COPY --from=build /home/gradle/src/opentelemetry-javaagent.jar ./opentelemetry-javaagent.jar

ARG MODULE_NAME=query
COPY --from=build /home/gradle/src/${MODULE_NAME}/dependencies/ ./
COPY --from=build /home/gradle/src/${MODULE_NAME}/spring-boot-loader/ ./
COPY --from=build /home/gradle/src/${MODULE_NAME}/application/ .

USER 65532:65532
EXPOSE 8080/tcp 8081/tcp
ENV SPRING_PROFILES_ACTIVE="prod"
ENTRYPOINT ["java", "org.springframework.boot.loader.JarLauncher"]
