package recruit;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;

import org.hl7.fhir.r4.model.Coding;
import org.hl7.fhir.r4.model.Extension;
import org.junit.jupiter.api.Test;

class RecruitSmokeTest {

  @Test
  void codeSystemUrlMatchesTheFshSource() {
    assertEquals(
        "https://miracum.github.io/recruit/fhir/CodeSystem/screening-list-type",
        Recruit.CodeSystems.screeningListType());
  }

  @Test
  void codingAccessorReturnsExpectedSystemAndCode() {
    Coding coding = Recruit.CodeSystems.ScreeningListType.SCREENING_RECOMMENDATIONS.coding();
    assertEquals(
        "https://miracum.github.io/recruit/fhir/CodeSystem/screening-list-type",
        coding.getSystem());
    assertEquals("screening-recommendations", coding.getCode());
  }

  @Test
  void profileUrlsMatchTheFshSource() {
    assertEquals(
        "https://miracum.github.io/recruit/fhir/StructureDefinition/research-study",
        Recruit.Profiles.researchStudy());
    assertEquals(
        "https://miracum.github.io/recruit/fhir/StructureDefinition/screening-list",
        Recruit.Profiles.screeningList());
  }

  @Test
  void extensionFactoryReturnsEmptyExtensionWithTheCanonicalUrl() {
    Extension extension = Recruit.Extensions.screeningListBelongsToStudy();
    assertEquals(
        "https://miracum.github.io/recruit/fhir/StructureDefinition/screening-list-belongs-to-study",
        extension.getUrl());
    assertTrue(extension.getValue() == null);
  }
}
