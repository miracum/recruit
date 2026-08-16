package org.miracum.recruit.supersetlibrarysync.superset;

import com.fasterxml.jackson.annotation.JsonIgnoreProperties;
import com.fasterxml.jackson.annotation.JsonProperty;
import java.io.IOException;
import java.net.URLEncoder;
import java.nio.charset.StandardCharsets;
import java.time.Duration;
import java.util.ArrayList;
import java.util.List;
import okhttp3.MediaType;
import okhttp3.OkHttpClient;
import okhttp3.Request;
import okhttp3.RequestBody;
import org.springframework.stereotype.Component;
import tools.jackson.databind.ObjectMapper;

/**
 * A minimal client for the parts of Superset's REST API this module needs: authenticating and
 * listing/fetching SQL Lab saved queries. Hand-rolled rather than generated from Superset's OpenAPI
 * spec - the only existing Java client for it (<a
 * href="https://github.com/suedschwede/superset-swagger-client">superset-swagger-client</a>) hasn't
 * been touched since 2020 and was generated against a years-old API version, and a fresh generated
 * client would cover a huge surface (charts, dashboards, ...) for the 3 endpoints actually used
 * here.
 */
@Component
public class SupersetClient {

  private static final MediaType JSON = MediaType.get("application/json; charset=utf-8");

  private final SupersetProperties properties;
  private final ObjectMapper objectMapper;
  private final OkHttpClient httpClient;

  // set once per process by ensureAuthenticated() - this module runs one sync cycle and exits, so
  // there's no reason to deal with refresh-token lifecycle management.
  private String accessToken;

  public SupersetClient(SupersetProperties properties, ObjectMapper objectMapper) {
    this.properties = properties;
    this.objectMapper = objectMapper;

    var timeout = Duration.ofSeconds(properties.timeoutInSeconds());
    this.httpClient =
        new OkHttpClient.Builder()
            .callTimeout(timeout)
            .connectTimeout(timeout)
            .readTimeout(timeout)
            .writeTimeout(timeout)
            .build();
  }

  /**
   * Every saved query the configured user can see, paginated. Note this is the lightweight list
   * representation - use {@link #getSavedQuery(long)} to get an individual query's full {@code
   * sql}, in case the list endpoint truncates it.
   */
  public List<SavedQuery> listSavedQueries() throws IOException {
    ensureAuthenticated();

    var savedQueries = new ArrayList<SavedQuery>();
    var pageNumber = 0;
    while (true) {
      var risonQuery = "(page:%d,page_size:%d)".formatted(pageNumber, properties.pageSize());
      var url = properties.url() + "/api/v1/saved_query/?q=" + encode(risonQuery);

      var responseBody = execute(authorizedRequest(url).get().build());
      var page = objectMapper.readValue(responseBody, SavedQueryListResponse.class);
      savedQueries.addAll(page.result());

      if (page.result().size() < properties.pageSize()) {
        break;
      }
      pageNumber++;
    }

    return savedQueries;
  }

  /** The full record for one saved query, including its complete {@code sql}. */
  public SavedQuery getSavedQuery(long id) throws IOException {
    ensureAuthenticated();

    var url = properties.url() + "/api/v1/saved_query/" + id;
    var responseBody = execute(authorizedRequest(url).get().build());
    return objectMapper.readValue(responseBody, SavedQueryGetResponse.class).result();
  }

  private void ensureAuthenticated() throws IOException {
    if (accessToken != null) {
      return;
    }

    var loginRequest = new LoginRequest(properties.username(), properties.password(), "db", false);
    var body = RequestBody.create(objectMapper.writeValueAsBytes(loginRequest), JSON);
    var request =
        new Request.Builder().url(properties.url() + "/api/v1/security/login").post(body).build();

    var responseBody = execute(request);
    accessToken = objectMapper.readValue(responseBody, LoginResponse.class).accessToken();
  }

  private Request.Builder authorizedRequest(String url) {
    return new Request.Builder().url(url).header("Authorization", "Bearer " + accessToken);
  }

  private String execute(Request request) throws IOException {
    try (var response = httpClient.newCall(request).execute()) {
      var responseBody = response.body() != null ? response.body().string() : "";
      if (!response.isSuccessful()) {
        throw new IOException(
            "Superset request to %s failed with status %d: %s"
                .formatted(request.url(), response.code(), responseBody));
      }
      return responseBody;
    }
  }

  private static String encode(String value) {
    return URLEncoder.encode(value, StandardCharsets.UTF_8);
  }

  private record LoginRequest(String username, String password, String provider, boolean refresh) {}

  @JsonIgnoreProperties(ignoreUnknown = true)
  private record LoginResponse(@JsonProperty("access_token") String accessToken) {}

  @JsonIgnoreProperties(ignoreUnknown = true)
  private record SavedQueryListResponse(int count, List<SavedQuery> result) {}

  @JsonIgnoreProperties(ignoreUnknown = true)
  private record SavedQueryGetResponse(SavedQuery result) {}
}
