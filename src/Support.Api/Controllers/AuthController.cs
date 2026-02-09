using Microsoft.AspNetCore.Mvc;
using Support.Application.Features.Auth.Commands.Login;
using Support.Application.Features.Auth.Commands.VerifyPnr;

namespace Support.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly LoginHandler _loginHandler;
    private readonly VerifyPnrHandler _verifyPnrHandler;

    public AuthController(LoginHandler loginHandler, VerifyPnrHandler verifyPnrHandler)
    {
        _loginHandler = loginHandler;
        _verifyPnrHandler = verifyPnrHandler;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginCommand command)
    {
        var result = await _loginHandler.Handle(command, HttpContext.RequestAborted);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(new { error = result.ErrorMessage });
    }

    [HttpPost("passenger/verify-pnr")]
    public async Task<IActionResult> VerifyPnr([FromBody] VerifyPnrCommand command)
    {
        var result = await _verifyPnrHandler.Handle(command, HttpContext.RequestAborted);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(new { error = result.ErrorMessage });
    }
}
