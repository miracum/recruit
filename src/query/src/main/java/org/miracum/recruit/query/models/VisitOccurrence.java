package org.miracum.recruit.query.models;

import java.time.LocalDate;
import java.time.LocalDateTime;
import java.util.Set;
import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Data;
import lombok.NoArgsConstructor;
import org.springframework.data.annotation.Id;
import org.springframework.data.annotation.Transient;
import org.springframework.data.relational.core.mapping.Embedded;
import org.springframework.data.relational.core.mapping.Embedded.OnEmpty;

@Data
@NoArgsConstructor
@AllArgsConstructor
@Builder
public class VisitOccurrence {
  @Id private Long visitOccurrenceId;
  private Long personId;
  private Integer visitConceptId;
  private LocalDate visitStartDate;
  private LocalDateTime visitStartDatetime;
  private LocalDate visitEndDate;
  private LocalDateTime visitEndDatetime;
  private Integer visitTypeConceptId;
  private Long careSiteId;
  private String visitSourceValue;
  private Integer visitSourceConceptId;
  private Integer dischargeToConceptId;

  // see the comments in routes/OmopRoute.java for why we aren't using
  // Spring Data JDBC's support for collections
  // @MappedCollection(idColumn = "visit_occurrence_id")
  @Transient private Set<VisitDetail> visitDetails;

  @Embedded(onEmpty = OnEmpty.USE_NULL)
  private CareSite careSite;
}
