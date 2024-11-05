# Emails

## Introduction

Emails are often used as a backup/recovery mechanism in case users forgets
their password.

Remember, there are different factors that we can use for authentication.
The categories are something you:

<dl>
  <dt><b>Know</b></dt>
  <dd>password, pin, etc</dd>
  <dt><b>Have</b></dt>
  <dd>Security token, email etc</dd>
  <dt><b>Are</b></dt>
  <dd>Biometrics: fingerprint, facial recognition</dd>
</dl>

In many systems the primary factor of authentication is password (something
they know).
If that fails we need a different authentication mechanism.
Commonly email is used (something they have).

If the user forgets their password, then we can send an email with a code then
allows setting a new password.
We need to have the users email address verified beforehand, for it to be secure.
That is why you commonly get an email asking to confirm your email address when
signing up for a new online service.

## Templating

Emails can contain HTML - which is perfect for sending the previous mentioned
code.
The user just has to click a link in the email.

_As a side note.
There are huge difference in how different email clients render HTML.
So making a design that looks good across all clients is actually pretty
difficult._

We are not going to worry too much about how the emails look.
We just need a simple way to generate HTML containing some dynamic content,
such as a link with the reset code.
Also, we can't use JavaScript in emails, since clients doesn't support it (as
that would be a security risk).
So we can't use a SPA framework like React to render the document.
We need the HTML to be rendered fully on the server.

Since we have dynamic elements in the emails, we need a templating system.
ASP.NET comes with a server-side HTML templating system called Razor.
Razor allows you to embed bits of C# within an HTML file.
It works well for generating HTML for web-pages, but if you want to use Razor
in a different context, it requires some boilerplate.

RazorLight is a package that creates an abstraction for the boilerplate.
It gives a nice API for rendering Razor HTML to a string, which is easy to send
as an email.

Let's add the package.

```sh
dotnet add server/Api package RazorLight --version 2.3.1
```

We will store our Razor based email templates in `server/Api/Emails`.
The templates will have `.cshtml` as file extension.
We need these files need to be embedded within the DLL we get when compiling
the code.

To embed the templates, we need to make sure our `server/Api/Api.csproj`
contains `PreserveCompilationContext` and has a `EmbeddedResource` with a
`Includ` pattern matching the template files.

_Your `.csproj` should already contain it.
Just provided here for reference, in case you want to use it in a different
project._

```csproj
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <PreserveCompilationContext>true</PreserveCompilationContext>
  </PropertyGroup>

  ...

  <ItemGroup>
    <EmbeddedResource Include="Emails\*.cshtml" />
  </ItemGroup>

</Project>
```

Next, we need to implement a class that Identity can use for sending emails.

Add a new file with the following to `server/Api/Misc/AppEmailSender.cs`:

```cs
namespace Api.Misc;

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
```

_We'll add the missing implementation in a moment._

The `RenderTemplateAsync` renders the template with the given name.
The name corresponds to one of the files in `server/Api/Emails`, but without
the file extension (`.cshtml`).

The `SendEmailAsync` sends an email using the
[SMTP](https://en.wikipedia.org/wiki/Simple_Mail_Transfer_Protocol).
Settings for a test SMTP server running in Docker is already present in
`appsettings.Development.json`, and it got spun up when you did `docker compose
up`.

Identity uses an implementation of `IEmailSender` to send emails.

It requires 3 methods to be implemented.
So let's add those:

```cs
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
```

They use a `EmailModel` class to carry data to the template.
Add it to the top of the file, right below the `namespace` statement:

```cs
public record EmailModel(User User, string Email, string CodeOrLink);
```

The strings `"ConfirmationLink"`, `"PasswordResetCode"` and `"PasswordResetLink"`
refers to template files.
Let's add those:

`server/Api/Emails/ConfirmationLink.cshtml`

```cshtml
@using RazorLight
@inherits TemplatePage<Api.Misc.EmailModel>

<h1>Confirm your email</h1>
<h3>Hi @Model.User.UserName, thank you for signing up on our site!</h3>
<p>
    We need you to confirm your email address.
    To do that please click on the link below.
</p>
<a href="@Model.CodeOrLink">Confirm @Model.Email</a>
```

`server/Api/Emails/PasswordResetCode.cshtml`

```cshtml
@using RazorLight
@inherits TemplatePage<Api.Misc.EmailModel>

<h1>Password reset</h1>
<h3>Hi @Model.User.UserName!</h3>
<p>
    To reset your password we need you to enter the code show below.
</p>
<pre>@Model.CodeOrLink</pre>
```

`server/Api/Emails/PasswordResetLink.cshtml`

```cshtml
@using RazorLight
@inherits TemplatePage<Api.Misc.EmailModel>

<h1>Password reset</h1>
<h3>Hi @Model.User.UserName!</h3>
<p>
    To reset your password we need you to enter the code show below.
</p>
<a href="@Model.CodeOrLink">Confirm @Model.Email</a>
```

_Feel free to change the template text._

## Wire it up

To wire it up, you need to add the following `#region Securitty` in `Program.cs`:

```cs
builder.Services.AddSingleton<IEmailSender<User>, AppEmailSender>();
```

We also need to make some changes in `AuthController` to handle email
confirmation.

First we need to generate a token and send it in an email during registration.

Change the register method to:

```cs
[HttpPost]
[Route("register")]
[AllowAnonymous]
public async Task<RegisterResponse> Register(
    IOptions<AppOptions> options,
    [FromServices] UserManager<User> userManager,
    [FromServices] IEmailSender<User> emailSender,
    [FromServices] IValidator<RegisterRequest> validator,
    [FromBody] RegisterRequest data
)
{
    await validator.ValidateAndThrowAsync(data);

    var user = new User { UserName = data.Email, Email = data.Email };
    var result = await userManager.CreateAsync(user, data.Password);
    if (!result.Succeeded)
    {
        throw new ValidationError(
            result.Errors.ToDictionary(x => x.Code, x => new[] { x.Description })
        );
    }
    await userManager.AddToRoleAsync(user, Role.Reader);

    var token = await userManager.GenerateEmailConfirmationTokenAsync(user);

    var qs = new Dictionary<string, string?> { { "token", token }, { "email", user.Email } };
    var confirmationLink = new UriBuilder(options.Value.Address)
    {
        Path = "/api/auth/confirm",
        Query = QueryString.Create(qs).Value
    }.Uri.ToString();

    await emailSender.SendConfirmationLinkAsync(user, user.Email, confirmationLink);

    return new RegisterResponse(Email: user.Email, Name: user.UserName);
}
```

Second we need an endpoint to handle when the user open the confirmation link.

Add a new method to `AuthController`:

```cs
[HttpGet]
[Route("confirm")]
[AllowAnonymous]
public async Task<IResult> ConfirmEmail(
    [FromServices] UserManager<User> userManager,
    string token,
    string email
)
{
    var user = await userManager.FindByEmailAsync(email) ?? throw new AuthenticationError();
    var result = await userManager.ConfirmEmailAsync(user, token);
    if (!result.Succeeded)
        throw new AuthenticationError();
    return Results.Content("<h1>Email confirmed</h1>", "text/html", statusCode: 200);
}
```

## Try it

1. Open <http://localhost:5173/register>, fill out the form and submit.
2. Then open <http://localhost:1080/> to see the email.
3. Click the link in the email.
4. You should see a page that says "Email confirmed".

How it works.
The `docker-compose.yml` contains a service for
[MailCatcher](https://mailcatcher.me/), which is a simple SMTP server made for
debugging email sending.
It got a simple web interface on port 1080 that shows all emails that was sent
to it.

The configuration in `server/Api/appsettings.Development.json` contains
settings for the SMTP server, which is what `AppEmailSender` uses when sending
emails.

## Challenge

Implement password reset.

Create an endpoint to initiate the reset.
You can use `UserManager` to generate a reset token that you then send in an
email.

## Closing thoughts

MailCatcher is only really useful for local testing.

For a deployed application, you should use an email service instead.
Some options are [SendGrid](https://sendgrid.com/en-us/free),
[Mailgun](https://www.mailgun.com/pricing/) or you can even (for limited
volume) use a GMail account.
