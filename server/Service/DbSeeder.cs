using DataAccess;
using DataAccess.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Service;

public class DbSeeder
{
    private readonly ILogger<DbSeeder> logger;
    private readonly AppDbContext context;
    private readonly UserManager<User> userManager;
    private readonly RoleManager<IdentityRole> roleManager;

    public DbSeeder(
        ILogger<DbSeeder> logger,
        AppDbContext context,
        UserManager<User> userManager,
        RoleManager<IdentityRole> roleManager
    )
    {
        this.logger = logger;
        this.context = context;
        this.userManager = userManager;
        this.roleManager = roleManager;
    }

    public async Task SeedAsync()
    {
        await context.Database.EnsureCreatedAsync();

        await CreateRoles(Role.Admin, Role.Editor, Role.Reader);
        await CreateUser(username: "admin@example.com", password: "S3cret!", role: Role.Admin);
        await CreateUser(username: "editor@example.com", password: "S3cret!", role: Role.Editor);
        await CreateUser(username: "othereditor@example.com", password: "S3cret!", role: Role.Editor);
        await CreateUser(username: "reader@example.com", password: "S3cret!", role: Role.Reader);

        var author = await userManager.FindByNameAsync("editor@example.com");
        if (!context.Posts.Any(p => p.Title == "First post"))
        {
            context.Posts.Add(
                new Post
                {
                    Title = "First post",
                    Content =
                        @"## Hello Python
Have you ever wondered how to make a hello-world application in Python?

The answer is simply:
```py
print('Hello World!')
````

                    ",
                    AuthorId = author!.Id,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    PublishedAt = DateTime.UtcNow
                }
            );
        }
        await context.SaveChangesAsync();
    }

    private async Task CreateRoles(params string[] roles)
    {
        foreach (var role in roles)
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    async Task CreateUser(string username, string password, string role)
    {
        if (await userManager.FindByNameAsync(username) != null) return;
        var user = new User
        {
            UserName = username,
            Email = username,
            EmailConfirmed = true
        };
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                logger.LogWarning("{Code}: {Description}", error.Code, error.Description);
            }
        }
        await userManager.AddToRoleAsync(user!, role!);
    }

}