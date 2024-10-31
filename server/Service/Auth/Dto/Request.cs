using FluentValidation;

namespace Service.Auth.Dto;

public record RegisterRequest(string Email, string Password, string Name);

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email).EmailAddress().NotEmpty();
        RuleFor(x => x.Password).MinimumLength(6);
        RuleFor(x => x.Name).NotEmpty();
    }
}

public record LoginRequest(string Email, string Password);

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public record InitPasswordResetRequest(string Email);

public class InitPasswordResetRequestValidator : AbstractValidator<InitPasswordResetRequest>
{
    public InitPasswordResetRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}
public record PasswordResetRequest(string Email, string Token, string NewPassword);

public class PasswordResetRequestValidator : AbstractValidator<PasswordResetRequest>
{
    public PasswordResetRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty();
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty();
    }
}
