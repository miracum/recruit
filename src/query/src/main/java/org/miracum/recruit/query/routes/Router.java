package org.miracum.recruit.query.routes;

import org.apache.camel.CamelContext;
import org.apache.camel.LoggingLevel;
import org.apache.camel.builder.RouteBuilder;
import org.apache.camel.component.micrometer.messagehistory.MicrometerMessageHistoryFactory;
import org.apache.camel.component.micrometer.routepolicy.MicrometerRoutePolicyFactory;
import org.apache.camel.spring.boot.CamelContextConfiguration;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.context.annotation.Bean;
import org.springframework.stereotype.Component;

@Component
public class Router extends RouteBuilder {

  public static final String DONE_COHORT_GENERATION = "direct:main.doneWithCohort";
  public static final String DONE_GET_PATIENTS = "direct:main.doneGetPatients";
  public static final String START_COHORT_GENERATION = "direct:main.startWithCohort";

  private final boolean shouldRunOnceAndExit;

  public Router(@Value("${query.run-once-and-exit}") boolean shouldRunOnceAndExit) {
    this.shouldRunOnceAndExit = shouldRunOnceAndExit;
  }

  @Bean
  public CamelContextConfiguration camelContextConfiguration() {

    return new CamelContextConfiguration() {
      @Override
      public void afterApplicationStart(CamelContext camelContext) {
        // not used
      }

      @Override
      public void beforeApplicationStart(CamelContext camelContext) {
        camelContext.addRoutePolicyFactory(new MicrometerRoutePolicyFactory());
        camelContext.setMessageHistoryFactory(new MicrometerMessageHistoryFactory());
      }
    };
  }

  @Override
  public void configure() {
    // Processing
    from(START_COHORT_GENERATION).to(OmopRoute.CLEAR_CACHE).to(WebApiRoute.GET_COHORT_DEFINITIONS);

    from(DONE_COHORT_GENERATION).to(OmopRoute.GET_PATIENTS);

    from(DONE_GET_PATIENTS).to(FhirRoute.CREATE_SCREENING_LIST);

    if (shouldRunOnceAndExit) {
      from("timer://runOnce?repeatCount=1")
          .log(LoggingLevel.INFO, "Running query module in one-shot mode")
          .to(START_COHORT_GENERATION)
          .log(LoggingLevel.INFO, "One-shot run completed")
          .onCompletion()
          .onFailureOnly()
          .log(LoggingLevel.INFO, "Run completed with failures")
          .process(
              ex -> {
                ex.getContext().shutdown();
                System.exit(1);
              })
          .end()
          .onCompletion()
          .onCompleteOnly()
          .log(LoggingLevel.INFO, "Run completed successfully")
          .process(
              ex -> {
                ex.getContext().shutdown();
                System.exit(0);
              })
          .end()
          .stop();
    } else {
      // Run from timer
      from("cron:getCohorts?schedule=0+{{query.schedule.unixCron}}")
          .autoStartup("{{query.schedule.enable}}")
          .log(LoggingLevel.INFO, "Running query module in scheduled mode")
          .to(START_COHORT_GENERATION);
    }
  }
}
