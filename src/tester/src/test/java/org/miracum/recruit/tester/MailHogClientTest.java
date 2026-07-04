package org.miracum.recruit.tester;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import com.sun.net.httpserver.HttpServer;
import java.io.IOException;
import java.net.InetSocketAddress;
import java.net.URI;
import java.nio.charset.StandardCharsets;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import tools.jackson.databind.ObjectMapper;

class MailHogClientTest {

  private HttpServer server;
  private MailHogClient sut;
  private URI baseUrl;
  private String lastRequestMethod;

  @BeforeEach
  void setUp() throws IOException {
    server = HttpServer.create(new InetSocketAddress("localhost", 0), 0);
    server.createContext(
        "/api/v2/messages",
        exchange -> {
          lastRequestMethod = exchange.getRequestMethod();
          var body = "{\"total\": 3}".getBytes(StandardCharsets.UTF_8);
          exchange.sendResponseHeaders(200, body.length);
          exchange.getResponseBody().write(body);
          exchange.close();
        });
    server.createContext(
        "/api/v1/messages",
        exchange -> {
          lastRequestMethod = exchange.getRequestMethod();
          exchange.sendResponseHeaders(200, -1);
          exchange.close();
        });
    server.start();

    baseUrl = URI.create("http://localhost:" + server.getAddress().getPort() + "/api/");
    sut = new MailHogClient(new ObjectMapper());
  }

  @AfterEach
  void tearDown() {
    server.stop(0);
  }

  @Test
  void getMessages_returnsTotalFromResponseBody() throws Exception {
    var messages = sut.getMessages(baseUrl);

    assertThat(messages.total()).isEqualTo(3);
    assertThat(lastRequestMethod).isEqualTo("GET");
  }

  @Test
  void deleteMessages_sendsDeleteRequest() throws Exception {
    sut.deleteMessages(baseUrl);

    assertThat(lastRequestMethod).isEqualTo("DELETE");
  }

  @Test
  void getMessages_withErrorResponse_throwsException() throws IOException {
    server.stop(0);
    server = HttpServer.create(new InetSocketAddress("localhost", 0), 0);
    server.createContext(
        "/api/v2/messages",
        exchange -> {
          exchange.sendResponseHeaders(500, -1);
          exchange.close();
        });
    server.start();
    var errorBaseUrl = URI.create("http://localhost:" + server.getAddress().getPort() + "/api/");

    assertThatThrownBy(() -> sut.getMessages(errorBaseUrl)).isInstanceOf(IOException.class);
  }
}
