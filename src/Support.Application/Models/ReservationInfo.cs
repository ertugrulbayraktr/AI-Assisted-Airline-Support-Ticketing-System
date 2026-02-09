namespace Support.Application.Models;

public class ReservationInfo
{
    public string PNR { get; set; } = null!;
    public List<Passenger> Passengers { get; set; } = new();
    public List<FlightSegment> Segments { get; set; } = new();
    public string FlightStatus { get; set; } = null!; // on-time, delayed, cancelled
    public string? BaggageStatus { get; set; }
    public FareRules FareRules { get; set; } = null!;
}

public class Passenger
{
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Email { get; set; } = null!;
}

public class FlightSegment
{
    public string FlightNumber { get; set; } = null!;
    public string Departure { get; set; } = null!;
    public string Arrival { get; set; } = null!;
    public DateTime DepartureTime { get; set; }
    public DateTime ArrivalTime { get; set; }
}

public class FareRules
{
    public bool IsRefundable { get; set; }
    public bool IsChangeable { get; set; }
    public decimal? CancellationFee { get; set; }
}
