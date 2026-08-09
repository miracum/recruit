namespace list.Models;

public enum PatientHistoryEntryKind
{
    StatusChange,
    Note,
}

/// <summary>One entry in a patient's combined status-change + note timeline, newest first.</summary>
public sealed class PatientHistoryEntryDto
{
    public required DateTimeOffset Time { get; init; }

    public required string Title { get; init; }

    public string? Description { get; init; }

    public required PatientHistoryEntryKind Kind { get; init; }
}
