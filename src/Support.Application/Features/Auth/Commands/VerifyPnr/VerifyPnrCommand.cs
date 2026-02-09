namespace Support.Application.Features.Auth.Commands.VerifyPnr;

public class VerifyPnrCommand
{
    public string PNR { get; set; } = null!;
    public string LastName { get; set; } = null!;
}

public class VerifyPnrResult
{
    public string Token { get; set; } = null!;
    public string PassengerEmail { get; set; } = null!;
}
