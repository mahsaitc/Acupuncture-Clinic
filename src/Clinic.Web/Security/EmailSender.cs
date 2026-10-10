using System.Net;
using System.Net.Mail;

namespace Clinic.Web.Security;

/// <summary>
/// Sends the site's emails (for now only password reset links) through the SMTP server in "Email:Smtp"
/// (Host, Port, User, Password, From, EnableSsl). Without a host nothing is sent; on a development machine the
/// message is written to the log instead, so the reset can still be tried locally.
/// </summary>
public class EmailSender(IConfiguration config, IWebHostEnvironment env, ILogger<EmailSender> logger)
{
    public bool Configured => !string.IsNullOrWhiteSpace(config["Email:Smtp:Host"]);

    public virtual async Task SendAsync(string to, string subject, string body)
    {
        if (!Configured)
        {
            if (env.IsDevelopment())
            {
                logger.LogWarning("Email not configured. Message to {To}: {Subject}\n{Body}", to, subject, body);
            }
            else
            {
                logger.LogWarning("Email not configured; could not send \"{Subject}\" to a user.", subject);
            }
            return;
        }

        var smtp = config.GetSection("Email:Smtp");
        using var client = new SmtpClient(smtp["Host"], smtp.GetValue("Port", 587))
        {
            EnableSsl = smtp.GetValue("EnableSsl", true),
            Credentials = string.IsNullOrEmpty(smtp["User"]) ? null : new NetworkCredential(smtp["User"], smtp["Password"]),
        };
        using var message = new MailMessage(smtp["From"] ?? smtp["User"]!, to, subject, body);
        try
        {
            await client.SendMailAsync(message);
        }
        catch (SmtpException e)
        {
            logger.LogError(e, "Could not send \"{Subject}\".", subject);
        }
    }
}
