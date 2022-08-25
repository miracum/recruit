package org.miracum.recruit.notify.practitioner;

import static org.assertj.core.api.Assertions.assertThat;

import java.util.List;
import org.junit.jupiter.api.Test;
import org.miracum.recruit.notify.fhirserver.FhirSystemsConfig;
import org.miracum.recruit.notify.mailconfig.UserConfig;
import org.miracum.recruit.notify.mailconfig.UserConfig.Subscription;
import org.miracum.recruit.notify.mailconfig.UserConfig.Trial;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.context.properties.EnableConfigurationProperties;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.test.context.ActiveProfiles;

@ActiveProfiles("test")
@SpringBootTest(classes = {FhirSystemsConfig.class})
@EnableConfigurationProperties(value = {FhirSystemsConfig.class})
class PractitionerCreatorTest {

  @Autowired FhirSystemsConfig fhirSystemsConfig;

  @Test
  void create_withGivenSubscriptions_shouldSetPractitionerTelecomToIt() {
    var subscriptions =
        List.of(new Subscription("a@example.com", null), new Subscription("b@example.com", null));

    var trial = new Trial();
    trial.setSubscriptions(subscriptions);

    var config = new UserConfig();
    config.setTrials(List.of(trial));

    var sut = new PractitionerCreator(fhirSystemsConfig, config);

    var practitioners = sut.create();

    assertThat(practitioners)
        .hasSize(2)
        .anyMatch(p -> p.getTelecomFirstRep().getValue().equals("a@example.com"))
        .anyMatch(p -> p.getTelecomFirstRep().getValue().equals("b@example.com"));
  }

  @Test
  void create_withMultipleIdenticalEmailAddresses_shouldCreateOnlyDistinctPractitioners() {
    var subscriptions =
        List.of(
            new Subscription("a@example.com", null),
            new Subscription("b@example.com", null),
            new Subscription("a@example.com", null));

    var trial = new Trial();
    trial.setSubscriptions(subscriptions);

    var config = new UserConfig();
    config.setTrials(List.of(trial));

    var sut = new PractitionerCreator(fhirSystemsConfig, config);

    var practitioners = sut.create();

    assertThat(practitioners)
        .hasSize(2)
        .anyMatch(p -> p.getTelecomFirstRep().getValue().equals("a@example.com"))
        .anyMatch(p -> p.getTelecomFirstRep().getValue().equals("b@example.com"));
  }
}
