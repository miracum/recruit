package org.miracum.recruit.supersetlibrarysync.superset;

import static org.assertj.core.api.Assertions.assertThat;

import java.io.IOException;
import java.net.URI;
import okhttp3.mockwebserver.MockResponse;
import okhttp3.mockwebserver.MockWebServer;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import tools.jackson.databind.ObjectMapper;

class SupersetClientTest {

  private MockWebServer server;
  private ObjectMapper objectMapper;

  @BeforeEach
  void startServer() throws IOException {
    server = new MockWebServer();
    server.start();
    objectMapper = new ObjectMapper();
  }

  @AfterEach
  void stopServer() throws IOException {
    server.shutdown();
  }

  private SupersetClient newClient() {
    var properties =
        new SupersetProperties(
            URI.create(server.url("/").toString().replaceAll("/$", "")),
            "admin",
            "admin",
            null,
            "trino",
            100,
            10);
    return new SupersetClient(properties, objectMapper);
  }

  private void enqueueLogin() {
    server.enqueue(
        new MockResponse()
            .setResponseCode(200)
            .setHeader("Content-Type", "application/json")
            .setBody("{\"access_token\":\"the-token\",\"refresh_token\":\"ignored\"}"));
  }

  @Test
  void listSavedQueries_authenticatesFirst_thenSendsBearerToken() throws Exception {
    enqueueLogin();
    server.enqueue(
        new MockResponse()
            .setResponseCode(200)
            .setHeader("Content-Type", "application/json")
            .setBody("{\"count\":0,\"result\":[]}"));

    newClient().listSavedQueries();

    var loginRequest = server.takeRequest();
    assertThat(loginRequest.getPath()).isEqualTo("/api/v1/security/login");
    assertThat(loginRequest.getMethod()).isEqualTo("POST");

    var listRequest = server.takeRequest();
    assertThat(listRequest.getPath()).startsWith("/api/v1/saved_query/?q=");
    assertThat(listRequest.getHeader("Authorization")).isEqualTo("Bearer the-token");
  }

  @Test
  void listSavedQueries_onlyAuthenticatesOnce_acrossMultiplePages() throws Exception {
    enqueueLogin();
    // a full page (page_size below) followed by a short page ends pagination
    server.enqueue(
        new MockResponse()
            .setResponseCode(200)
            .setHeader("Content-Type", "application/json")
            .setBody(pageOf(2)));
    server.enqueue(
        new MockResponse()
            .setResponseCode(200)
            .setHeader("Content-Type", "application/json")
            .setBody(pageOf(1)));

    var properties =
        new SupersetProperties(
            URI.create(server.url("/").toString().replaceAll("/$", "")),
            "admin",
            "admin",
            null,
            "trino",
            2,
            10);
    var client = new SupersetClient(properties, objectMapper);

    var savedQueries = client.listSavedQueries();

    assertThat(savedQueries).hasSize(3);
    // 1 login + 2 list pages
    assertThat(server.getRequestCount()).isEqualTo(3);
  }

  @Test
  void getSavedQuery_unwrapsResultEnvelope() throws Exception {
    enqueueLogin();
    server.enqueue(
        new MockResponse()
            .setResponseCode(200)
            .setHeader("Content-Type", "application/json")
            .setBody(
                "{\"id\":42,\"result\":{\"id\":42,\"label\":\"My Query\",\"description\":null,"
                    + "\"schema\":\"public\",\"sql\":\"SELECT 1\",\"tags\":[]}}"));

    var savedQuery = newClient().getSavedQuery(42);

    assertThat(savedQuery.id()).isEqualTo(42);
    assertThat(savedQuery.label()).isEqualTo("My Query");
    assertThat(savedQuery.sql()).isEqualTo("SELECT 1");

    var request = server.takeRequest(); // login
    var getRequest = server.takeRequest();
    assertThat(getRequest.getPath()).isEqualTo("/api/v1/saved_query/42");
    assertThat(request.getMethod()).isEqualTo("POST");
  }

  @Test
  void request_withNonSuccessfulResponse_throwsIoException() {
    server.enqueue(new MockResponse().setResponseCode(401).setBody("Unauthorized"));

    var client = newClient();

    org.junit.jupiter.api.Assertions.assertThrows(IOException.class, client::listSavedQueries);
  }

  private static String pageOf(int size) {
    var builder = new StringBuilder("{\"count\":").append(size).append(",\"result\":[");
    for (var i = 0; i < size; i++) {
      if (i > 0) {
        builder.append(',');
      }
      builder
          .append("{\"id\":")
          .append(i)
          .append(",\"label\":\"q")
          .append(i)
          .append(
              "\",\"description\":null,\"schema\":\"public\",\"sql\":\"SELECT 1\",\"tags\":[]}");
    }
    return builder.append("]}").toString();
  }
}
