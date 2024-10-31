using System.Net;
using System.Net.Mail;
using DataAccess.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using RazorLight;
using Service;

namespace Api.Misc;

public record EmailModel(User User, string Email, string CodeOrLink);

public class AppEmailSender(IOptions<AppOptions> options, ILogger<AppEmailSender> logger)
    : IEmailSender<User>
{
    private readonly AppOptions _options = options.Value;
    private readonly ILogger _logger = logger;
    private readonly RazorLightEngine engine = new RazorLightEngineBuilder()
        .UseEmbeddedResourcesProject(typeof(AppEmailSender).Assembly)
        .UseMemoryCachingProvider()
        .UseOptions(new RazorLightOptions() { EnableDebugMode = true })
        .Build();
    
public async Task SendConfirmationLinkAsync(User user, string email, string confirmationLink) =>
    await RenderAndSend(
        email,
        "Confirm your email",
        "ConfirmationLink",
        new EmailModel(user, email, confirmationLink)
    );

public async Task SendPasswordResetCodeAsync(User user, string email, string resetCode) =>
    await RenderAndSend(
        email,
        "Password reset",
        "PasswordResetCode",
        new EmailModel(user, email, resetCode)
    );

public async Task SendPasswordResetLinkAsync(User user, string email, string resetLink) =>
    await RenderAndSend(
        email,
        "Password reset",
        "PasswordResetLink",
        new EmailModel(user, email, resetLink)
    );


    public async Task RenderAndSend<TModel>(
        string toEmail,
        string subject,
        string template,
        TModel model
    )
    {
        var message = await RenderTemplateAsync(template, model);
        await SendEmailAsync(toEmail, subject, message);
    }

    public async Task<string> RenderTemplateAsync<TModel>(string template, TModel model)
    {
        return await engine.CompileRenderAsync($"Api.Emails.{template}", model);
    }

    public async Task SendEmailAsync(string toEmail, string subject, string message)
    {
        try
        {
            using var client = new SmtpClient(_options.SmtpServer, _options.SmtpPort!.Value);
            client.UseDefaultCredentials = false;
            if (
                !string.IsNullOrWhiteSpace(_options.SmtpUsername)
                && !string.IsNullOrWhiteSpace(_options.SmtpPassword)
            )
            {
                client.Credentials = new NetworkCredential(
                    _options.SmtpUsername,
                    _options.SmtpPassword
                );
            }

            client.EnableSsl = _options.SmtpEnableSsl!.Value;

            var mailMessage = new MailMessage
            {
                From = new MailAddress(_options.SmtpSenderEmail),
                Subject = subject,
                Body = message,
                IsBodyHtml = true
            };
            mailMessage.To.Add(toEmail);

            await client.SendMailAsync(mailMessage);
        }
        catch (Exception e)
        {
            _logger.LogError(e, null);
        }
    }
}
