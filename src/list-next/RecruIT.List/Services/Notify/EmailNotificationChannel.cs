using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using RecruIT.List.Options;

namespace RecruIT.List.Services.Notify;

public sealed class EmailNotificationChannel(IOptions<NotifyMailerOptions> mailerOptions)
    : INotificationChannel
{
    public async Task<INotificationBatch> BeginBatchAsync(CancellationToken ct = default)
    {
        var options = mailerOptions.Value;

        var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(
                options.SmtpHost,
                options.SmtpPort,
                SecureSocketOptions.Auto,
                ct
            );
            if (
                !string.IsNullOrEmpty(options.SmtpUsername)
                && client.AuthenticationMechanisms.Count > 0
            )
            {
                await client.AuthenticateAsync(options.SmtpUsername, options.SmtpPassword ?? "", ct);
            }
        }
        catch
        {
            client.Dispose();
            throw;
        }

        return new EmailBatch(client, options.From);
    }

    private sealed class EmailBatch(SmtpClient client, string from) : INotificationBatch
    {
        public async Task SendAsync(
            string recipient,
            string subject,
            string htmlBody,
            CancellationToken ct = default
        )
        {
            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(from));
            message.To.Add(MailboxAddress.Parse(recipient));
            message.Subject = subject;
            message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

            await client.SendAsync(message, ct);
        }

        public async ValueTask DisposeAsync()
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(true);
            }
            client.Dispose();
        }
    }
}
