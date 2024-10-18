using DataAccess.Entities;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Service;
using Service.Auth.Dto;

namespace Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    [HttpPost]
    [Route("login")]
    public async Task<LoginResponse> Login(
        [FromServices] SignInManager<User> signInManager,
        [FromServices] IValidator<LoginRequest> validator,
        [FromBody] LoginRequest data
    )
    {
        await validator.ValidateAndThrowAsync(data);
        var result = await signInManager.PasswordSignInAsync(
            data.Email,
            data.Password,
            false,
            true
        );
        if (!result.Succeeded)
            throw new AuthenticationError();
        return new LoginResponse();
    }

    [HttpPost]
    [Route("register")]
    public async Task<RegisterResponse> Register(
        IOptions<AppOptions> options,
        [FromServices] UserManager<User> userManager,
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
        return new RegisterResponse(Email: user.Email, Name: user.UserName);
    }

    [HttpPost]
    [Route("logout")]
    public async Task<IResult> Logout([FromServices] SignInManager<User> signInManager)
    {
        await signInManager.SignOutAsync();
        return Results.Ok();
    }

    [HttpGet]
    [Route("userinfo")]
    public async Task<AuthUserInfo> UserInfo([FromServices] UserManager<User> userManager)
    {
        var username = (HttpContext.User.Identity?.Name) ?? throw new AuthenticationError();
        var user = await userManager.FindByNameAsync(username) ?? throw new AuthenticationError();
        var roles = await userManager.GetRolesAsync(user);
        var isAdmin = roles.Contains(Role.Admin);
        var canPublish = roles.Contains(Role.Editor) || isAdmin;
        return new AuthUserInfo(username, isAdmin, canPublish);
    }
}