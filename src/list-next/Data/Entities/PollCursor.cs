namespace list.Data.Entities;

/// <summary>
/// One row per screening `List`, tracking how far the notification poller has progressed for it.
/// `LastSeenVersionId` doubles as an EF Core concurrency token (see AppDbContext.OnModelCreating):
/// when multiple `list-next` replicas race to advance the same list's cursor, only one `SaveChanges`
/// wins - the others get a DbUpdateConcurrencyException, which is the "another replica already
/// claimed this version transition" signal.
/// </summary>
public sealed class PollCursor
{
    /// <summary>FHIR List logical id - the primary key.</summary>
    public required string ListId { get; set; }

    public required string LastSeenVersionId { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
