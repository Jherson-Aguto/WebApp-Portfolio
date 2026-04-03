using Resend;

namespace BlazorPortfolio.Services;

public class EmailService(IConfiguration config, ILogger<EmailService> logger)
{
    public async Task SendPasswordResetAsync(string toEmail, string resetLink)
    {
        var apiKey = config["Resend:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning("Resend:ApiKey is not configured — password reset email not sent to {Email}", toEmail);
            throw new InvalidOperationException("Email service is not configured. Please set Resend:ApiKey.");
        }

        // On Resend free tier (no custom domain), you can only send to your
        // verified Resend account email. Use Resend:OverrideTo to force all
        // emails to that address regardless of what's in the DB.
        var overrideTo = config["Resend:OverrideTo"];
        var actualTo   = !string.IsNullOrWhiteSpace(overrideTo) ? overrideTo : toEmail;

        var from = config["Resend:FromAddress"] ?? "onboarding@resend.dev";

        IResend resend = ResendClient.Create(apiKey);

        await resend.EmailSendAsync(new EmailMessage
        {
            From     = from,
            To       = actualTo,
            Subject  = "Reset your Portfolio Admin password",
            HtmlBody = $"""
                <div style="font-family: sans-serif; max-width: 600px; margin: auto;">
                    <h2>Password Reset Request</h2>
                    <p>We received a request to reset your admin password. Click the button below to proceed:</p>
                    <a href="{resetLink}" style="background-color: #000; color: #fff; padding: 12px 24px; text-decoration: none; border-radius: 5px; display: inline-block;">
                        Reset Password
                    </a>
                    <p style="margin-top: 20px; font-size: 12px; color: #666;">
                        This link will expire in <strong>30 minutes</strong>. If you didn't request this, no further action is needed.
                    </p>
                </div>
                """
        });

        logger.LogInformation("Password reset email sent to {ActualTo} (requested for {Email})", actualTo, toEmail);
    }
}
