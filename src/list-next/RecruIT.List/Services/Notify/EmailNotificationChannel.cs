using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using RecruIT.List.Options;

namespace RecruIT.List.Services.Notify;

public sealed class EmailNotificationChannel(IOptions<NotifyMailerOptions> mailerOptions)
    : INotificationChannel
{
    public async Task SendAsync(
        string recipient,
        string subject,
        string htmlBody,
        CancellationToken ct = default
    )
    {
        var options = mailerOptions.Value;

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(options.From));
        message.To.Add(MailboxAddress.Parse(recipient));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(options.SmtpHost, options.SmtpPort, SecureSocketOptions.Auto, ct);
        if (!string.IsNullOrEmpty(options.SmtpUsername))
        {
            await client.AuthenticateAsync(options.SmtpUsername, options.SmtpPassword ?? "", ct);
        }
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }
}
