FROM docker.io/library/gradle:7.6.0@sha256:adaf7d0ef536c90a1c4c6f4d4064a9c1099260e9f3bf0459f3a52a5893da513f AS build
WORKDIR /home/gradle/src
ENV GRADLE_USER_HOME /gradle

ARG OPENTELEMETRY_JAVA_AGENT_VERSION=1.21.0
RUN wget --quiet https://github.com/open-telemetry/opentelemetry-java-instrumentation/releases/download/v${OPENTELEMETRY_JAVA_AGENT_VERSION}/opentelemetry-javaagent.jar

COPY settings.gradle ./
COPY config/ config/
COPY buildSrc/ buildSrc/

ARG MODULE_NAME=query
COPY ${MODULE_NAME}/build.gradle ./${MODULE_NAME}/

RUN gradle clean build --no-daemon || true

COPY --chown=gradle:gradle . .

RUN gradle :${MODULE_NAME}:build -x test --info && \
    java -Djarmode=layertools -jar ${MODULE_NAME}/build/libs/*.jar extract --destination ${MODULE_NAME}/

FROM build AS test
ARG MODULE_NAME=query
RUN gradle :${MODULE_NAME}:test && \
    gradle jacocoTestReport

FROM gcr.io/distroless/java17-debian11:nonroot@sha256:9ed1f1775ffbaad2ab63c9812ec836a0e2def28b53752192b01bc53ad6195c6e
WORKDIR /app

COPY --from=build /home/gradle/src/opentelemetry-javaagent.jar ./opentelemetry-javaagent.jar

ARG MODULE_NAME=query
COPY --from=build /home/gradle/src/${MODULE_NAME}/dependencies/ ./
COPY --from=build /home/gradle/src/${MODULE_NAME}/spring-boot-loader/ ./
COPY --from=build /home/gradle/src/${MODULE_NAME}/application/ .

USER 65532:65532
EXPOSE 8080/tcp 8081/tcp
ENV SPRING_PROFILES_ACTIVE=prod
ENTRYPOINT ["java", "org.springframework.boot.loader.JarLauncher"]
