package org.miracum.recruit.query.routes;

import static net.logstash.logback.argument.StructuredArguments.kv;

import com.google.common.collect.Sets;
import java.net.MalformedURLException;
import java.net.URL;
import java.util.HashSet;
import java.util.Set;
import org.apache.camel.Body;
import org.apache.camel.Exchange;
import org.apache.camel.LoggingLevel;
import org.apache.camel.builder.RouteBuilder;
import org.apache.camel.http.base.HttpOperationFailedException;
import org.apache.camel.model.dataformat.JsonLibrary;
import org.miracum.recruit.query.CohortSelectorConfig;
import org.miracum.recruit.query.LabelExtractor;
import org.miracum.recruit.query.models.CohortDefinition;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;

@Component
public class WebApiRoute extends RouteBuilder {

  static final String GET_COHORT_DEFINITIONS = "direct:webApi.getCohortDefinitions";
  static final String GET_COHORT_DEFINITION = "direct:webApi.getCohortDefinition";
  static final String RUN_COHORT_GENERATION = "direct:webApi.runCohortGeneration";
  static final String GET_AUTH_TOKEN = "direct:authService";

  private static final Logger LOG = LoggerFactory.getLogger(WebApiRoute.class);
  private static final String HEADER_GENERATION_STATUS = "generationStatus";
  private final Set<String> matchLabels;
  private final LabelExtractor labelExtractor;

  @Value("${query.webapi.base-url}")
  private URL baseUrl;

  @Value("${atlas.dataSource}")
  private String dataSourceName;

  @Value("${query.webapi.auth.enabled}")
  private boolean isWebApiAuthEnabled;

  @Value("${query.webapi.auth.login-path}")
  private String webApiAuthLoginPath;

  @Autowired
  public WebApiRoute(CohortSelectorConfig selectorConfig, LabelExtractor labelExtractor) {
    this.matchLabels = selectorConfig.getMatchLabels();
    this.labelExtractor = labelExtractor;
  }

  @Override
  public void configure() throws MalformedURLException {
    // general error handler
    errorHandler(
        defaultErrorHandler()
            .maximumRedeliveries(5)
            .redeliveryDelay(5000)
            .retryAttemptedLogLevel(LoggingLevel.WARN));

    // in case of a http exception then retry at most 3 times
    onException(HttpOperationFailedException.class)
        .maximumRedeliveries(3)
        .handled(true)
        .delay(10_000)
        .log(
            LoggingLevel.WARN,
            LOG,
            "HTTP error during request processing. Failing after retrying.");

    // @formatter:off
    // @spotless:off
    if (isWebApiAuthEnabled) {
      var authUrl = new URL(baseUrl.getProtocol(), baseUrl.getHost(), baseUrl.getPort(),
        baseUrl.getPath() + webApiAuthLoginPath, null);

      // via https://gist.github.com/rafaeltuelho/4d2449ac9b709fd29d79fa89acd8b48b
      from(GET_AUTH_TOKEN)
        .log(LoggingLevel.DEBUG, "Auth is enabled. Fetching token from " + authUrl)
        .setHeader(Exchange.HTTP_METHOD, constant("POST"))
        .setHeader(Exchange.CONTENT_TYPE)
          .simple("application/x-www-form-urlencoded")
        .setHeader("Accept")
          .simple("application/json")
        .setBody()
          .constant("login={{query.webapi.auth.username}}&password={{query.webapi.auth.password}}")
        .to(authUrl.toString())
          .convertBodyTo(String.class)
        .log(LoggingLevel.DEBUG, LOG, "response from token provider: ${body}")
        .choice()
          .when().simple("${header.CamelHttpResponseCode} == 200")
                 .setHeader("bearerToken").simple("${header.Bearer}")
          .endChoice()
          .otherwise()
            .log("Failed to authenticate as {{query.webapi.auth.username}} against " + authUrl);
    }

    // when running all cohorts
    from(GET_COHORT_DEFINITIONS)
        .choice()
          .when(constant(isWebApiAuthEnabled))
            .to(GET_AUTH_TOKEN)
            .setHeader("Authorization")
              .simple("Bearer ${header.bearerToken}")
        .end()
        .removeHeader("jwt")
        .setHeader(Exchange.HTTP_METHOD, constant("GET"))
        .to(baseUrl + "/cohortdefinition")
        .convertBodyTo(String.class)
        .log(LoggingLevel.DEBUG, LOG, "response from webapi: ${body}")
        .split()
        .jsonpathWriteAsString("$[*]") // foreach cohort.
        // https://camel.apache.org/components/latest/jsonpath-component.html
        .unmarshal()
        .json(JsonLibrary.Jackson, CohortDefinition.class) // Convert from json to
        // CohortDefinition-Object
        .to(RUN_COHORT_GENERATION)
        .end();

    // when running just one cohort
    from(GET_COHORT_DEFINITION)
    	.to(OmopRoute.CLEAR_CACHE)
        .choice()
          .when(constant(isWebApiAuthEnabled))
            .to(GET_AUTH_TOKEN)
            .setHeader("Authorization")
              .simple("Bearer ${header.bearerToken}")
        .end()
        .log("processing cohort: ${body}")
        .setHeader(Exchange.HTTP_METHOD, constant("GET"))
        .toD(baseUrl + "/cohortdefinition/${body}")
        .convertBodyTo(String.class)
        .unmarshal()
        // Convert from json to CohortDefinition-Object
        .json(JsonLibrary.Jackson, CohortDefinition.class)
        .log(LoggingLevel.DEBUG, LOG, "[cohort ${body.id}] response from webapi: ${body}")
        .process(
            ex -> {
              var body = (CohortDefinition) ex.getIn().getBody();
              ex.getIn().setBody(body);
            })
        .to(RUN_COHORT_GENERATION);

    // generate a cohort
    from(RUN_COHORT_GENERATION)
        .log("processing cohort: ${body}")
        .filter()
        .method(this, "isMatchingCohort")
        .log("cohort=${body.id} matches the selector labels")
        .setHeader(Exchange.HTTP_METHOD, constant("GET"))
        // needed otherwise ConvertException
        .setHeader("cohort", body())
        .setBody().simple("${null}")
        .toD(baseUrl + "/cohortdefinition/${header.cohort.id}/generate/" + dataSourceName)
        .setHeader(HEADER_GENERATION_STATUS, constant("PENDING"))
        // Check Status of generation and loop while still running
        .loopDoWhile(header(HEADER_GENERATION_STATUS).regex("PENDING|RUNNING"))
        .toD(baseUrl + "/cohortdefinition/${header.cohort.id}/info")
        .convertBodyTo(String.class)
        .setHeader(HEADER_GENERATION_STATUS, jsonpath("$.[0].status"))
        .log(
            LoggingLevel.INFO,
            log,
            "cohort=${header.cohort.id} current status=${header.generationStatus}")
        .delay(simple("${properties:atlas.cohortStatusCheckBackoffTime}"))
        .end()
        .choice()
        .when(header(HEADER_GENERATION_STATUS).isEqualTo("COMPLETE"))
          .setBody(header("cohort"))
        .to(Router.DONE_COHORT_GENERATION);
    // @spotless:on
    // @formatter:on
  }

  public boolean isMatchingCohort(@Body CohortDefinition definition) {
    log.info(
        "Checking if {} matches labels {}.",
        kv("cohort", definition.getId()),
        kv("matchLabels", matchLabels));
    if (matchLabels.isEmpty()) {
      // if no match labels are specified, simply accept all cohorts
      return true;
    }

    var allLabels = new HashSet<String>();
    allLabels.addAll(labelExtractor.extract(definition.getDescription()));
    allLabels.addAll(labelExtractor.extract(definition.getName()));

    // if there is at least some overlap between all labels extracted from
    // the cohort and the labels to match, i.e. the intersection between these
    // sets is not empty, then the cohort matches.
    return !Sets.intersection(allLabels, matchLabels).isEmpty();
  }
}
