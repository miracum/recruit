package org.miracum.recruit.notify.mailconfig;

import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.stream.Collectors;
import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Data;
import lombok.NoArgsConstructor;
import org.quartz.CronExpression;
import org.springframework.boot.context.properties.ConfigurationProperties;
import org.springframework.context.annotation.Configuration;

@Configuration
@ConfigurationProperties(prefix = "notify.rules")
@Data
public class UserConfig {
  private final Map<String, CronExpression> schedules = new HashMap<>();
  private List<Trial> trials;

  public List<Subscription> getSubscriptions() {
    return this.getTrials().stream()
        .flatMap(t -> t.getSubscriptions().stream())
        .collect(Collectors.toUnmodifiableList());
  }

  @Data
  public static class Trial {
    private String acronym;
    private List<Subscription> subscriptions;
  }

  @Data
  @Builder
  @NoArgsConstructor
  @AllArgsConstructor
  public static class Subscription {
    private String email;
    private String notify;
  }

  public List<Subscription> getSubscriptionsByAcronym(String acronym) {
    return this.getTrials().stream()
        .filter(t -> t.acronym.equals(acronym) || t.acronym.equals("*"))
        .flatMap(t -> t.getSubscriptions().stream())
        .collect(Collectors.toUnmodifiableList());
  }
}
