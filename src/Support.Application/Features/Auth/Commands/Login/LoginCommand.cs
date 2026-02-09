namespace Support.Application.Features.Auth.Commands.Login;

public class LoginCommand
{
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
}

public class LoginResult
{
    public string Token { get; set; } = null!;
    public string UserName { get; set; } = null!;
    public string Role { get; set; } = null!;
}
