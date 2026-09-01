FROM docker.io/library/gradle:9.6.1-jdk25@sha256:e8aeffb8197b17151ce24607811f60e91125f0018f7a3b08dc504ba9168a9c4f AS build
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

FROM gcr.io/distroless/java25-debian13:nonroot@sha256:fce4a1d66284e8866c46113d9bdc286c46fb8c3c3f0a098f877034349e88debe
WORKDIR /app

COPY --from=build /home/gradle/project/tester/dependencies/ ./
COPY --from=build /home/gradle/project/tester/spring-boot-loader/ ./
COPY --from=build /home/gradle/project/tester/application/ ./

COPY --from=docker.io/rancher/kubectl:v1.36.2@sha256:06c7a7a9772737494ae1e0c3af90f1b5385630c147e47c1c7cea92f4bed55fbe /bin/kubectl /usr/bin/kubectl

COPY --from=build /home/gradle/project/tester/src/main/resources/ /tmp/

USER 65532:65532
ENTRYPOINT ["java", "org.springframework.boot.loader.launch.JarLauncher"]
