package org.miracum.recruit.query.models;

import java.time.Month;
import java.time.Year;
import java.util.Set;
import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Data;
import lombok.NoArgsConstructor;
import org.springframework.data.annotation.Id;
import org.springframework.data.annotation.Transient;

@Data
@NoArgsConstructor
@AllArgsConstructor
@Builder
public class Person {
  @Id private Long personId;
  private String gender;
  private Year yearOfBirth;
  private Month monthOfBirth;
  private Integer dayOfBirth;
  private Long locationId;
  private String sourceId;
  private Integer genderConceptId;

  @Transient private Set<VisitOccurrence> visitOccurrences;
}
