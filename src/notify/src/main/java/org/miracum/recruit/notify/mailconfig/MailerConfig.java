package org.miracum.recruit.notify.mailconfig;

import lombok.Data;
import org.springframework.boot.context.properties.ConfigurationProperties;
import org.springframework.context.annotation.Configuration;

@Configuration
@ConfigurationProperties(prefix = "notify.mailer")
@Data
public class MailerConfig {
  private String from;
  private String linkTemplate;
  private String subject;
}
