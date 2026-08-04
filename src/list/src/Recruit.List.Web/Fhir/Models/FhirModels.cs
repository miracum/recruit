using System.Text.Json;
using System.Text.Json.Serialization;

namespace Recruit.List.Web.Fhir.Models;

public sealed class FhirBundle
{
    [JsonPropertyName("total")]
    public int? Total { get; set; }

    [JsonPropertyName("link")]
    public List<FhirBundleLink>? Link { get; set; }

    [JsonPropertyName("entry")]
    public List<FhirBundleEntry>? Entry { get; set; }
}

public sealed class FhirBundleLink
{
    [JsonPropertyName("relation")]
    public string? Relation { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

public sealed class FhirBundleEntry
{
    [JsonPropertyName("fullUrl")]
    public string? FullUrl { get; set; }

    [JsonPropertyName("resource")]
    public JsonElement Resource { get; set; }
}

public sealed class FhirCoding
{
    [JsonPropertyName("system")]
    public string? System { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("display")]
    public string? Display { get; set; }
}

public sealed class FhirCodeableConcept
{
    [JsonPropertyName("coding")]
    public List<FhirCoding>? Coding { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

public sealed class FhirIdentifier
{
    [JsonPropertyName("system")]
    public string? System { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("type")]
    public FhirCodeableConcept? Type { get; set; }
}

public sealed class FhirReference
{
    [JsonPropertyName("reference")]
    public string? Reference { get; set; }

    [JsonPropertyName("display")]
    public string? Display { get; set; }
}

public sealed class FhirExtension
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("valueString")]
    public string? ValueString { get; set; }
}

public sealed class FhirPeriod
{
    [JsonPropertyName("start")]
    public DateTimeOffset? Start { get; set; }

    [JsonPropertyName("end")]
    public DateTimeOffset? End { get; set; }
}

public sealed class FhirMeta
{
    [JsonPropertyName("lastUpdated")]
    public DateTimeOffset? LastUpdated { get; set; }
}

public sealed class FhirHumanName
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("family")]
    public string? Family { get; set; }

    [JsonPropertyName("given")]
    public List<string>? Given { get; set; }
}

public sealed class FhirPatient
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("identifier")]
    public List<FhirIdentifier>? Identifier { get; set; }

    [JsonPropertyName("name")]
    public List<FhirHumanName>? Name { get; set; }

    [JsonPropertyName("gender")]
    public string? Gender { get; set; }

    [JsonPropertyName("birthDate")]
    public string? BirthDate { get; set; }
}

public sealed class FhirResearchSubject
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("meta")]
    public FhirMeta? Meta { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("period")]
    public FhirPeriod? Period { get; set; }

    [JsonPropertyName("study")]
    public FhirReference? Study { get; set; }

    [JsonPropertyName("individual")]
    public FhirReference? Individual { get; set; }

    [JsonPropertyName("extension")]
    public List<FhirExtension>? Extension { get; set; }
}

public sealed class FhirResearchStudy
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("extension")]
    public List<FhirExtension>? Extension { get; set; }
}

public sealed class FhirOperationOutcome
{
    [JsonPropertyName("issue")]
    public List<FhirOperationOutcomeIssue>? Issue { get; set; }
}

public sealed class FhirOperationOutcomeIssue
{
    [JsonPropertyName("severity")]
    public string? Severity { get; set; }

    [JsonPropertyName("diagnostics")]
    public string? Diagnostics { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }
}
