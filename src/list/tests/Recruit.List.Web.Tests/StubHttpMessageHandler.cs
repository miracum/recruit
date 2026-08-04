using System.Net;

namespace Recruit.List.Web.Tests;

/// <summary>A minimal HttpMessageHandler stub that serves canned responses and records every request made against it.</summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responders = new();

    public List<HttpRequestMessage> Requests { get; } = [];

    /// <summary>
    /// The body of each request, captured eagerly at send-time. Reading
    /// <see cref="HttpRequestMessage.Content"/> after <c>SendAsync</c> returns is unsafe --
    /// callers typically wrap the request in a <c>using</c> and dispose it (along with its
    /// content) right after -- so assertions on the request body must use this instead.
    /// </summary>
    public List<string?> RequestBodies { get; } = [];

    public void Enqueue(Func<HttpRequestMessage, HttpResponseMessage> responder) =>
        _responders.Enqueue(responder);

    public void Enqueue(
        HttpStatusCode statusCode,
        string content,
        string mediaType = "application/fhir+json"
    ) =>
        Enqueue(_ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, System.Text.Encoding.UTF8, mediaType),
        });

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        Requests.Add(request);
        RequestBodies.Add(
            request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken)
        );

        if (_responders.Count == 0)
        {
            throw new InvalidOperationException(
                $"No stubbed response left for request: {request.Method} {request.RequestUri}"
            );
        }

        return _responders.Dequeue()(request);
    }
}
