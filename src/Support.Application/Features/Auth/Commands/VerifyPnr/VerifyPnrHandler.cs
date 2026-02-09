using Support.Application.Common;
using Support.Application.Interfaces;

namespace Support.Application.Features.Auth.Commands.VerifyPnr;

public class VerifyPnrHandler
{
    private readonly IReservationProvider _reservationProvider;
    private readonly IJwtTokenService _jwtTokenService;

    public VerifyPnrHandler(IReservationProvider reservationProvider, IJwtTokenService jwtTokenService)
    {
        _reservationProvider = reservationProvider;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<Result<VerifyPnrResult>> Handle(VerifyPnrCommand request, CancellationToken cancellationToken)
    {
        var reservation = await _reservationProvider.GetReservationAsync(request.PNR, request.LastName, cancellationToken);

        if (reservation == null)
        {
            return Result<VerifyPnrResult>.Failure("Invalid PNR or last name");
        }

        var passenger = reservation.Passengers.FirstOrDefault(p =>
            p.LastName.Equals(request.LastName, StringComparison.OrdinalIgnoreCase));

        if (passenger == null)
        {
            return Result<VerifyPnrResult>.Failure("Passenger not found in reservation");
        }

        var token = _jwtTokenService.GeneratePassengerToken(request.PNR, request.LastName, passenger.Email);

        return Result<VerifyPnrResult>.Success(new VerifyPnrResult
        {
            Token = token,
            PassengerEmail = passenger.Email
        });
    }
}
