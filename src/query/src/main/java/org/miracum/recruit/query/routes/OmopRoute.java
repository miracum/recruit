package org.miracum.recruit.query.routes;

import java.time.Month;
import java.time.Year;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import org.apache.camel.LoggingLevel;
import org.apache.camel.builder.RouteBuilder;
import org.miracum.recruit.query.models.Person;
import org.miracum.recruit.query.repositories.VisitDetailRepository;
import org.miracum.recruit.query.repositories.VisitOccurrenceRepository;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;

@Component
public class OmopRoute extends RouteBuilder {

  static final String GET_PATIENT_IDS = "direct:omop.getPatientIds";
  static final String GET_PATIENTS = "direct:omop.getPatients";
  static final String CLEAR_CACHE = "direct:omop.clearCache";
  private static final Logger logger = LoggerFactory.getLogger(OmopRoute.class);
  private final VisitOccurrenceRepository visitOccurrenceRepository;
  private final VisitDetailRepository visitDetailRepository;
  // catch SQL params from application.yml
  @Value("${query.excludePatientParameters.demographics}")
  private boolean excludePatientParams;

  @Autowired
  public OmopRoute(
      VisitOccurrenceRepository visitOccurrenceRepository,
      VisitDetailRepository visitDetailRepository) {
    this.visitOccurrenceRepository = visitOccurrenceRepository;
    this.visitDetailRepository = visitDetailRepository;
  }

  /**
   * Create SQL-String to request data from OMOP DB Parameter to be requested can be set in
   * application.yml
   *
   * @param cohortId Cohort of which data has to be requested
   * @return SQL-String
   */
  private String buildSQLString(String cohortId) {
    StringBuilder sqlRequest =
        new StringBuilder(
            "sql:SELECT {{omop.cdmSchema}}.person.person_id, {{omop.cdmSchema}}.person.person_source_value");

    // Check if params should be requested
    if (!this.excludePatientParams) {
      sqlRequest.append(
          ", {{omop.cdmSchema}}.concept.concept_name, {{omop.cdmSchema}}.concept.vocabulary_id");
      sqlRequest.append(", {{omop.cdmSchema}}.person.year_of_birth");
      sqlRequest.append(", {{omop.cdmSchema}}.person.month_of_birth");
      sqlRequest.append(", {{omop.cdmSchema}}.person.day_of_birth");
    }
    sqlRequest.append(" FROM {{omop.resultsSchema}}.cohort");
    sqlRequest.append(
        " INNER JOIN {{omop.cdmSchema}}.person ON {{omop.resultsSchema}}.cohort.subject_id={{omop.cdmSchema}}.person.person_id");

    // Join is only necessary for gender
    if (!this.excludePatientParams) {
      sqlRequest.append(
          " LEFT JOIN {{omop.cdmSchema}}.concept ON {{omop.cdmSchema}}.concept.concept_id={{omop.cdmSchema}}.person.gender_concept_id");
    }
    sqlRequest.append(" WHERE {{omop.resultsSchema}}.cohort.cohort_definition_id=" + cohortId);
    sqlRequest.append(" ORDER BY {{omop.resultsSchema}}.cohort.cohort_start_date DESC");
    sqlRequest.append(" LIMIT {{query.cohortSizeThreshold}};");
    return sqlRequest.toString();
  }

  @Override
  public void configure() {

    // @formatter:off
    // gets number of persons in cohort as cohortSize in header
    // gets the CohortDefinition in the body
    from(GET_PATIENTS)
        .to(
            "sql:SELECT count(*) from {{omop.resultsSchema}}.cohort where {{omop.resultsSchema}}.cohort.cohort_definition_id = :#${body.id};")
        .process(
            ex -> {
              @SuppressWarnings("unchecked")
              var result = (List<Map<String, Object>>) ex.getIn().getBody();
              ex.getIn().setHeader("cohortSize", result.get(0).get("count"));
            })
        .log(buildSQLString("${header.cohort.id}"))
        .toD(buildSQLString("${header.cohort.id}"))
        .process(
            ex -> {
              @SuppressWarnings("unchecked")
              var result = (List<Map<String, Object>>) ex.getIn().getBody();
              var patients = new ArrayList<Person>();
              for (Map<String, Object> row : result) {
                Person patient = new Person();
                patient.setPersonId((long) (int) row.get("person_id"));

                if (row.get("year_of_birth") != null) {
                  patient.setYearOfBirth(Year.of((int) row.get("year_of_birth")));
                }
                if (row.get("month_of_birth") != null) {
                  patient.setMonthOfBirth(Month.of(((int) row.get("month_of_birth"))));
                }
                if (row.get("day_of_birth") != null) {
                  patient.setDayOfBirth((int) row.get("day_of_birth"));
                }
                if (row.get("person_source_value") != null) {
                  patient.setSourceId((String) row.get("person_source_value"));
                }
                if (row.get("vocabulary_id") != null
                    && (row.get("vocabulary_id")).equals("Gender")) {
                  patient.setGender((String) row.getOrDefault("concept_name", null));
                }

                // for the purpose of recruitment support, we only really care about the most recent
                // visit of a patient. However, since location data may not yet be available for
                // that visit, we fetch the 5 most recent visits as a heuristic hoping one of them
                // contains location/ward information.
                var visitOccurrences =
                    visitOccurrenceRepository.findFirst5ByPersonIdOrderByVisitStartDateDesc(
                        patient.getPersonId());

                // from a performance-perspective this isn't ideal as in total
                // we have to create at most n * 5 + 1 DB queries, where n is the number of
                // patients. We could use the "In" modifier to fetch all visit details for a given
                // visit occurrence at once if it turns out to be too slow.
                for (var visitOccurrence : visitOccurrences) {
                  var visitDetails =
                      visitDetailRepository
                          .findTop5ByVisitOccurrenceIdOrderByVisitDetailStartDateDesc(
                              visitOccurrence.getVisitOccurrenceId());
                  visitOccurrence.setVisitDetails(visitDetails);
                }

                patient.setVisitOccurrences(visitOccurrences);

                patients.add(patient);
              }
              ex.getIn().setBody(patients);
            })
        .to("log:?level=DEBUG&showBody=true")
        .log(
            LoggingLevel.DEBUG,
            logger,
            "[Cohort ${header.cohort.id}] found ${body.size()} patient(s) for cohort id ${header.cohort.id}")
        .to(Router.DONE_GET_PATIENTS);
    // @formatter:on

    from(CLEAR_CACHE)
        .log("clear cohort cache")
        .to("sql:TRUNCATE TABLE ohdsi.generation_cache CONTINUE IDENTITY RESTRICT;")
        .log("done clearing cohort cache");
  }
}
