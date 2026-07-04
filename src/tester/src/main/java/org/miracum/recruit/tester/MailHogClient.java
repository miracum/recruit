package org.miracum.recruit.tester;

import com.fasterxml.jackson.databind.ObjectMapper;
import java.io.IOException;
import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import org.springframework.stereotype.Component;

@Component
public class MailHogClient {

  private final HttpClient httpClient = HttpClient.newHttpClient();
  private final ObjectMapper objectMapper;

  public MailHogClient(ObjectMapper objectMapper) {
    this.objectMapper = objectMapper;
  }

  public MailHogMessages getMessages(URI mailhogApiBaseUrl)
      throws IOException, InterruptedException {
    var request = HttpRequest.newBuilder(mailhogApiBaseUrl.resolve("v2/messages")).GET().build();
    var response = httpClient.send(request, HttpResponse.BodyHandlers.ofString());

    if (response.statusCode() >= 400) {
      throw new IOException(
          "Getting messages from MailHog failed with status " + response.statusCode());
    }

    return objectMapper.readValue(response.body(), MailHogMessages.class);
  }

  public void deleteMessages(URI mailhogApiBaseUrl) throws IOException, InterruptedException {
    var request = HttpRequest.newBuilder(mailhogApiBaseUrl.resolve("v1/messages")).DELETE().build();
    var response = httpClient.send(request, HttpResponse.BodyHandlers.discarding());

    if (response.statusCode() >= 400) {
      throw new IOException(
          "Deleting messages from MailHog failed with status " + response.statusCode());
    }
  }

  public record MailHogMessages(int total) {}
}
