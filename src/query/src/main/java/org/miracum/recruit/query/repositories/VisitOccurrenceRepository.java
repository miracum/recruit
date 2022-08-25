package org.miracum.recruit.query.repositories;

import java.util.Collection;
import java.util.Set;
import org.miracum.recruit.query.models.VisitOccurrence;
import org.springframework.data.jdbc.repository.query.Query;
import org.springframework.data.repository.CrudRepository;

public interface VisitOccurrenceRepository extends CrudRepository<VisitOccurrence, Long> {

  // unfortunately we can't encode the "JOIN" within the method name so we have to manually
  // specify the entire SQL statement.
  @Query(
      """
          SELECT visit_occurrence.*, care_site.care_site_name
          FROM visit_occurrence
          LEFT JOIN care_site ON visit_occurrence.care_site_id = care_site.care_site_id
          WHERE visit_occurrence.person_id = :personId
          ORDER BY visit_start_date DESC
          LIMIT 5
      """)
  Set<VisitOccurrence> findFirst5ByPersonIdOrderByVisitStartDateDesc(Long personId);

  Set<VisitOccurrence> findFirst5ByPersonIdInOrderByVisitStartDateDesc(Collection<Long> personIds);
}
