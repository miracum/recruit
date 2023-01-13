package org.miracum.recruit.query.models;

import java.time.LocalDate;
import java.time.LocalDateTime;
import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Data;
import lombok.NoArgsConstructor;
import org.springframework.data.annotation.Id;
import org.springframework.data.relational.core.mapping.Embedded;
import org.springframework.data.relational.core.mapping.Embedded.OnEmpty;

@Data
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class VisitDetail {
  @Id private Long visitDetailId;
  private Long personId;
  private LocalDate visitDetailStartDate;
  private LocalDateTime visitDetailStartDatetime;
  private LocalDate visitDetailEndDate;
  private LocalDateTime visitDetailEndDatetime;
  private Integer visitDetailTypeConceptId;
  private Long careSiteId;
  private String visitDetailSourceValue;
  private Long visitOccurrenceId;

  @Embedded(onEmpty = OnEmpty.USE_NULL)
  private CareSite careSite;
}
