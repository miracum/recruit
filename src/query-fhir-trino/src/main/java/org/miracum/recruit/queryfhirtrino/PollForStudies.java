package org.miracum.recruit.queryfhirtrino;

import java.text.SimpleDateFormat;

@Component
public class PollForStudies {

  private static final Logger log = LoggerFactory.getLogger(PollForStudies.class);

  private static final SimpleDateFormat dateFormat = new SimpleDateFormat("HH:mm:ss");

  private JdbcTemplate jdbcTemplate;

  @Scheduled(fixedRate = 5000)
  public void reportCurrentTime() {
    log.info("The time is now {}", dateFormat.format(new Date()));
    // 1. get all FHIR ResearchStudy resources from the fhir server:
    //
    // http://recruit-fhir-server.127.0.0.1.nip.io/fhir/ResearchStudy?enrollment.code=trino-sql-encoded-eligibility-criteria&_include=ResearchStudy:enrollment&_include:iterate=Group:characteristic-reference
    // 2. for each ResearchStudy resource, get the associated FHIR Group resource, and the
    // assoaciated FHIR Library resource
    // 3. for each Library resource, fetch the SQL query
    // 4. finally, run this sql query against trino
    // 5. for each returned patient id/row, create a new FHIR ResearchSubject resource, referencing
    // the patient_id and the original ResearchStudy from above
    // 6. post a FHIR List referencing all the ResearchSubject resources

    // jdbcTemplate.query(
  }
}
