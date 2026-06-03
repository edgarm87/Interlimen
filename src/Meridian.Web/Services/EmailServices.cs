using Resend;

namespace Meridian.Web.Services;

public class EmailService
{
    private readonly IResend _resend;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IResend resend, ILogger<EmailService> logger)
    {
        _resend = resend;
        _logger = logger;
    }

    public async Task SendConfirmationEmailAsync(string toEmail, string firstName, string confirmationLink)
    {
        try
        {
            var message = new EmailMessage
            {
                From = "INTERLIMEN <hello@interlimen.com>",
                To = { toEmail },
                Subject = "Confirm your INTERLIMEN account",
                HtmlBody = $"""
                    <div style="font-family:'Helvetica Neue',sans-serif; max-width:520px; margin:0 auto; padding:40px 20px; background:#f4f6f9;">
                        <div style="background:#fff; border-radius:10px; padding:40px; border:1px solid rgba(27,79,114,0.12);">
                            <div style="text-align:center; margin-bottom:32px;">
                                <span style="font-family:monospace; font-size:14px; font-weight:600; letter-spacing:0.2em; color:#1B4F72;">INTERLIMEN</span>
                            </div>
                            <h2 style="font-size:22px; font-weight:700; color:#1B4F72; margin-bottom:12px;">
                                Confirm your email address
                            </h2>
                            <p style="font-size:15px; color:#5a6a7e; line-height:1.7; margin-bottom:28px;">
                                Hi {firstName}, thanks for signing up. Click the button below to confirm your email and activate your 14-day free trial.
                            </p>
                            <a href="{confirmationLink}" 
                               style="display:block; text-align:center; background:#1B4F72; color:#fff; text-decoration:none; padding:14px 28px; border-radius:8px; font-size:15px; font-weight:600; margin-bottom:24px;">
                                Confirm Email Address
                            </a>
                            <p style="font-size:13px; color:#8a9ab0; line-height:1.6;">
                                If you didn't create an account, you can safely ignore this email. This link expires in 24 hours.
                            </p>
                            <hr style="border:none; border-top:1px solid rgba(27,79,114,0.10); margin:24px 0;" />
                            <p style="font-size:12px; color:#8a9ab0; text-align:center;">
                                INTERLIMEN · Legal Intelligence for Immigration Professionals
                            </p>
                        </div>
                    </div>
                """
            };

            await _resend.EmailSendAsync(message);
            _logger.LogInformation("Confirmation email sent to {Email}", toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send confirmation email to {Email}", toEmail);
            throw;
        }
    }
    
    public async Task SendNewUserNotificationAsync(string orgName, string firstName, string lastName, string email)
{
    try
    {
        var message = new EmailMessage
        {
            From = "INTERLIMEN <hello@interlimen.com>",
            To = { "edgarmora87@gmail.com" },
            Subject = $"New signup: {firstName} {lastName} — {orgName}",
            HtmlBody = $"""
                <div style="font-family:'Helvetica Neue',sans-serif; max-width:520px; margin:0 auto; padding:40px 20px; background:#f4f6f9;">
                    <div style="background:#fff; border-radius:10px; padding:40px; border:1px solid rgba(27,79,114,0.12);">
                        <div style="text-align:center; margin-bottom:32px;">
                            <span style="font-family:monospace; font-size:14px; font-weight:600; letter-spacing:0.2em; color:#1B4F72;">INTERLIMEN</span>
                        </div>
                        <h2 style="font-size:22px; font-weight:700; color:#1B4F72; margin-bottom:24px;">
                            New user signed up
                        </h2>
                        <table style="width:100%; font-size:14px; color:#5a6a7e; border-collapse:collapse;">
                            <tr style="border-bottom:1px solid rgba(27,79,114,0.08)">
                                <td style="padding:10px 0; font-weight:500; color:#1B4F72; width:140px;">Name</td>
                                <td style="padding:10px 0">{firstName} {lastName}</td>
                            </tr>
                            <tr style="border-bottom:1px solid rgba(27,79,114,0.08)">
                                <td style="padding:10px 0; font-weight:500; color:#1B4F72;">Organization</td>
                                <td style="padding:10px 0">{orgName}</td>
                            </tr>
                            <tr style="border-bottom:1px solid rgba(27,79,114,0.08)">
                                <td style="padding:10px 0; font-weight:500; color:#1B4F72;">Email</td>
                                <td style="padding:10px 0">{email}</td>
                            </tr>
                            <tr>
                                <td style="padding:10px 0; font-weight:500; color:#1B4F72;">Signed up</td>
                                <td style="padding:10px 0">{DateTime.UtcNow:MMM d, yyyy h:mm tt} UTC</td>
                            </tr>
                        </table>
                    </div>
                </div>
            """
        };

        await _resend.EmailSendAsync(message);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to send new user notification for {Email}", email);
    }
}
}