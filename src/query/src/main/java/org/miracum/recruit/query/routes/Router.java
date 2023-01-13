package org.miracum.recruit.query.routes;

import org.apache.camel.CamelContext;
import org.apache.camel.LoggingLevel;
import org.apache.camel.builder.RouteBuilder;
import org.apache.camel.component.micrometer.messagehistory.MicrometerMessageHistoryFactory;
import org.apache.camel.component.micrometer.routepolicy.MicrometerRoutePolicyFactory;
import org.apache.camel.spring.boot.CamelContextConfiguration;
import org.springframework.context.annotation.Bean;
import org.springframework.stereotype.Component;

@Component
public class Router extends RouteBuilder {

  public static final String DONE_COHORT_GENERATION = "direct:main.doneWithCohort";
  public static final String DONE_GET_PATIENTS = "direct:main.doneGetPatients";
  public static final String START_COHORT_GENERATION = "direct:main.startWithCohort";

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

    // run via REST
    rest("/run")
        // run all cohorts
        .post()
        .route()
        .log(LoggingLevel.INFO, "Run Query module from external call")
        .process(
            ex -> {
              var template = ex.getContext().createProducerTemplate();
              template.asyncSendBody(START_COHORT_GENERATION, null);
            })
        .transform()
        .constant("Successfully started Query Module")
        .endRest()
        // run a cohort from the omop cohort-id
        .post("/{cohortId}")
        .route()
        .log(LoggingLevel.INFO, "Run cohort ${header.cohortId} in query module from external call")
        .process(
            ex -> {
              var template = ex.getContext().createProducerTemplate();
              template.asyncSendBody(
                  WebApiRoute.GET_COHORT_DEFINITION, ex.getIn().getHeader("cohortId"));
            })
        .transform()
        .simple("Successfully started Query Module for cohort ${header.cohortId}")
        .endRest();

    // Run from timer
    from("cron:getCohorts?schedule=0+{{query.schedule.unixCron}}")
        .autoStartup("{{query.schedule.enable}}")
        .to(START_COHORT_GENERATION);

    // Processing
    from(START_COHORT_GENERATION).to(OmopRoute.CLEAR_CACHE).to(WebApiRoute.GET_COHORT_DEFINITIONS);

    from(DONE_COHORT_GENERATION).to(OmopRoute.GET_PATIENTS);

    from(DONE_GET_PATIENTS).to(FhirRoute.CREATE_SCREENING_LIST);
  }
}
