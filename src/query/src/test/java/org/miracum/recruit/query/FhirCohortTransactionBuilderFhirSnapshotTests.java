package org.miracum.recruit.query;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.parser.IParser;
import java.time.Month;
import java.time.Year;
import java.util.ArrayList;
import org.approvaltests.Approvals;
import org.approvaltests.core.Options;
import org.approvaltests.scrubbers.GuidScrubber;
import org.approvaltests.scrubbers.RegExScrubber;
import org.approvaltests.scrubbers.Scrubbers;
import org.junit.jupiter.api.Test;
import org.miracum.recruit.query.models.CohortDefinition;
import org.miracum.recruit.query.models.Person;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.context.properties.EnableConfigurationProperties;
import org.springframework.boot.test.context.SpringBootTest;

@SpringBootTest(classes = {FhirSystems.class})
@EnableConfigurationProperties(value = {FhirSystems.class})
public class FhirCohortTransactionBuilderFhirSnapshotTests {
  private static final FhirContext fhirContext = FhirContext.forR4();
  private static final IParser fhirParser = fhirContext.newJsonParser().setPrettyPrint(true);

  private final FhirCohortTransactionBuilder sut;

  private final CohortDefinition testCohort;

  private final String FHIR_DATETIME_REGEX =
      "([0-9]([0-9]([0-9][1-9]|[1-9]0)|[1-9]00)|[1-9]000)(-(0[1-9]|1[0-2])(-(0[1-9]|[1-2][0-9]|3[0-1])(T([01][0-9]|2[0-3]):[0-5][0-9]:([0-5][0-9]|60)(\\.[0-9]{1,9})?)?)?(Z|(\\+|-)((0[0-9]|1[0-3]):[0-5][0-9]|14:00)?)?)?";

  @Autowired
  public FhirCohortTransactionBuilderFhirSnapshotTests(FhirSystems fhirSystems) {
    var mapper = new VisitToEncounterMapper(fhirSystems);
    sut = new FhirCohortTransactionBuilder(fhirSystems, 100, false, false, false, mapper);

    testCohort = new CohortDefinition();
    testCohort.setId(1L);
    testCohort.setName("Testcohort");
  }

  @Test
  void buildFromOmopCohort_withLargeCohort() {
    var persons = new ArrayList<Person>();

    for (int i = 0; i < 50; i++) {
      var person =
          Person.builder()
              .personId(i + 1L)
              .yearOfBirth(Year.of(1900 + i))
              .monthOfBirth(Month.FEBRUARY)
              .dayOfBirth(1)
              .gender("Female")
              .build();
      persons.add(person);
    }

    var bundle = sut.buildFromOmopCohort(testCohort, persons, persons.size());

    var fhirJson = fhirParser.encodeResourceToString(bundle);

    final var dateTimeScrubber =
        new RegExScrubber(
            "(\"date\": \")" + FHIR_DATETIME_REGEX, "\"date\": \"2000-01-01T11:11:11Z");
    var scrubber = Scrubbers.scrubAll(dateTimeScrubber, new GuidScrubber());
    Approvals.verify(fhirJson, new Options(scrubber).forFile().withExtension(".fhir.json"));
  }
}
