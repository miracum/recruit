package org.miracum.recruit.notify.mailconfig;

import java.text.ParseException;
import org.quartz.CronExpression;
import org.springframework.boot.context.properties.ConfigurationPropertiesBinding;
import org.springframework.core.convert.converter.Converter;
import org.springframework.lang.NonNull;
import org.springframework.stereotype.Component;

@Component
@ConfigurationPropertiesBinding
public class CronExpressionConverter implements Converter<String, CronExpression> {

  @Override
  public CronExpression convert(@NonNull String source) {
    try {
      return new CronExpression(source);
    } catch (ParseException e) {
      throw new RuntimeException("Parsing CRON expression from config failed", e);
    }
  }
}
