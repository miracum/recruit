package org.miracum.recruit.query.health;

import java.net.URL;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.boot.actuate.health.Health;
import org.springframework.boot.actuate.health.HealthIndicator;
import org.springframework.stereotype.Component;
import org.springframework.web.reactive.function.client.WebClient;

@Component
public class WebApiHealthIndicator implements HealthIndicator {

  private final WebClient webClient;

  private final URL webApiBaseUrl;

  @Autowired
  public WebApiHealthIndicator(@Value("${query.webapi.base-url}") URL webApiBaseUrl) {
    this.webApiBaseUrl = webApiBaseUrl;
    this.webClient = WebClient.create(webApiBaseUrl.toString());
  }

  @Override
  public Health health() {
    try {
      webClient.get().uri("/info").retrieve();
    } catch (Exception exc) {
      return Health.down()
          .withDetail("baseUrl", webApiBaseUrl)
          .withDetail("error", exc.getMessage())
          .build();
    }

    return Health.up().withDetail("baseUrl", webApiBaseUrl).build();
  }
}
