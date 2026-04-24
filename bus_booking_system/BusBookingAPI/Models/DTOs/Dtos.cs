namespace BusBookingAPI.Models.DTOs;

// ---- Auth DTOs ----

public class UserRegisterRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class OperatorRegisterRequest
{
    public string CompanyName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? GstNumber { get; set; }
    public string? Address { get; set; }
    public string Password { get; set; } = string.Empty;
}

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

// ---- Bus Search DTOs ----

public class BusSearchRequest
{
    public string Source { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public DateOnly JourneyDate { get; set; }
    public string? BusType { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class BusSearchResult
{
    public Guid BusId { get; set; }
    public string OperatorName { get; set; } = string.Empty;
    public string BusType { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
    public TimeOnly DepartureTime { get; set; }
    public TimeOnly ArrivalTime { get; set; }
    public decimal? EstimatedHours { get; set; }
    public int TotalSeats { get; set; }
    public int AvailableSeats { get; set; }
    public decimal PricePerSeat { get; set; }
    public List<string> Amenities { get; set; } = [];
    public string BoardingLocation { get; set; } = string.Empty;
    public string DroppingLocation { get; set; } = string.Empty;
    public string SourceCity { get; set; } = string.Empty;
    public string DestinationCity { get; set; } = string.Empty;
}

public class PagedResult<T>
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<T> Results { get; set; } = [];
}

// ---- Bus Detail DTOs ----

public class SeatDto
{
    public Guid SeatId { get; set; }
    public string SeatNumber { get; set; } = string.Empty;
    public string SeatType { get; set; } = string.Empty;
    public short Row { get; set; }
    public short Col { get; set; }
    public string Status { get; set; } = "available"; // available | locked | booked
}

public class SeatLayoutDto
{
    public List<SeatDto> Seats { get; set; } = [];
}

public class BusDetailResponse
{
    public Guid BusId { get; set; }
    public string OperatorName { get; set; } = string.Empty;
    public string BusType { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
    public List<string> Amenities { get; set; } = [];
    public TimeOnly DepartureTime { get; set; }
    public TimeOnly ArrivalTime { get; set; }
    public string BoardingLocation { get; set; } = string.Empty;
    public string DroppingLocation { get; set; } = string.Empty;
    public decimal PricePerSeat { get; set; }
    public string SourceCity { get; set; } = string.Empty;
    public string DestinationCity { get; set; } = string.Empty;
    public SeatLayoutDto SeatLayout { get; set; } = new();
}

// ---- Seat Lock DTOs ----

public class SeatLockRequest
{
    public Guid BusId { get; set; }
    public DateOnly JourneyDate { get; set; }
    public List<Guid> SeatIds { get; set; } = [];
}

public class SeatLockResponse
{
    public DateTime LockExpiry { get; set; }
    public List<Guid> LockedSeats { get; set; } = [];
}

public class SeatUnlockRequest
{
    public Guid BusId { get; set; }
    public DateOnly JourneyDate { get; set; }
    public List<Guid> SeatIds { get; set; } = [];
}

// ---- Booking DTOs ----

public class PassengerRequest
{
    public Guid SeatId { get; set; }
    public string PassengerName { get; set; } = string.Empty;
    public short PassengerAge { get; set; }
    public string PassengerGender { get; set; } = string.Empty;
}

public class PaymentRequest
{
    public string Method { get; set; } = "dummy";
    public decimal Amount { get; set; }
}

public class CreateBookingRequest
{
    public Guid BusId { get; set; }
    public DateOnly JourneyDate { get; set; }
    public List<PassengerRequest> Passengers { get; set; } = [];
    public PaymentRequest Payment { get; set; } = new();
}

public class CreateBookingResponse
{
    public Guid BookingId { get; set; }
    public string BookingStatus { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string? TicketDownloadUrl { get; set; }
}

public class PassengerDto
{
    public string Name { get; set; } = string.Empty;
    public short Age { get; set; }
    public string Gender { get; set; } = string.Empty;
    public string SeatNumber { get; set; } = string.Empty;
}

public class BookingHistoryItem
{
    public Guid BookingId { get; set; }
    public string SourceCity { get; set; } = string.Empty;
    public string DestinationCity { get; set; } = string.Empty;
    public DateOnly JourneyDate { get; set; }
    public List<string> SeatNumbers { get; set; } = [];
    public string Status { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string? TicketUrl { get; set; }
}

public class CancelBookingRequest
{
    public string? CancellationReason { get; set; }
}

// ---- Operator DTOs ----

public class AddBusRequest
{
    public string RegistrationNumber { get; set; } = string.Empty;
    public string BusType { get; set; } = string.Empty;
    public short TotalSeats { get; set; }
    public Guid RouteId { get; set; }
    public TimeOnly DepartureTime { get; set; }
    public TimeOnly ArrivalTime { get; set; }
    public string BoardingLocation { get; set; } = string.Empty;
    public string DroppingLocation { get; set; } = string.Empty;
    public decimal PricePerSeat { get; set; }
    public List<string> Amenities { get; set; } = [];
    public SeatLayoutInput SeatLayout { get; set; } = new();
}

public class SeatLayoutInput
{
    public int Rows { get; set; }
    public int Cols { get; set; }
    public List<SeatInput> Layout { get; set; } = [];
}

public class SeatInput
{
    public string SeatNumber { get; set; } = string.Empty;
    public short Row { get; set; }
    public short Col { get; set; }
    public string Type { get; set; } = "seater";
}

public class UpdateBusRequest
{
    public string? BusType { get; set; }
    public Guid? RouteId { get; set; }
    public TimeOnly? DepartureTime { get; set; }
    public TimeOnly? ArrivalTime { get; set; }
    public string? BoardingLocation { get; set; }
    public string? DroppingLocation { get; set; }
    public decimal? PricePerSeat { get; set; }
    public List<string>? Amenities { get; set; }
}

public class UpdateBusStatusRequest
{
    public string Status { get; set; } = string.Empty;
}

public class OperatorBookingItem
{
    public Guid BookingId { get; set; }
    public DateOnly JourneyDate { get; set; }
    public string BusRegistration { get; set; } = string.Empty;
    public List<PassengerDto> Passengers { get; set; } = [];
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
}

// ---- Admin DTOs ----

public class OperatorListItem
{
    public Guid OperatorId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class OperatorDetailResponse : OperatorListItem
{
    public string? GstNumber { get; set; }
    public string? Address { get; set; }
    public string? RejectionReason { get; set; }
    public List<BusListItem> Buses { get; set; } = [];
}

public class BusListItem
{
    public Guid BusId { get; set; }
    public string RegistrationNumber { get; set; } = string.Empty;
    public string BusType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class UpdateOperatorStatusRequest
{
    public string Action { get; set; } = string.Empty; // approve | reject | disable
    public string? Reason { get; set; }
}

public class CreateRouteRequest
{
    public string SourceCity { get; set; } = string.Empty;
    public string DestinationCity { get; set; } = string.Empty;
    public decimal? DistanceKm { get; set; }
    public decimal? EstimatedHours { get; set; }
}

public class UpdateRouteRequest
{
    public string? SourceCity { get; set; }
    public string? DestinationCity { get; set; }
    public decimal? DistanceKm { get; set; }
    public decimal? EstimatedHours { get; set; }
}

public class RouteDto
{
    public Guid RouteId { get; set; }
    public string SourceCity { get; set; } = string.Empty;
    public string DestinationCity { get; set; } = string.Empty;
    public decimal? DistanceKm { get; set; }
    public decimal? EstimatedHours { get; set; }
    public bool IsActive { get; set; }
}

public class AdminDashboardResponse
{
    public int TotalBookings { get; set; }
    public decimal TotalRevenue { get; set; }
    public double CancellationRate { get; set; }
    public List<RevenueByOperator> RevenueByOperator { get; set; } = [];
    public List<RevenueByRoute> RevenueByRoute { get; set; } = [];
    public List<BookingsByDate> BookingsByDate { get; set; } = [];
}

public class RevenueByOperator
{
    public Guid OperatorId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
}

public class RevenueByRoute
{
    public Guid RouteId { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
}

public class BookingsByDate
{
    public DateOnly Date { get; set; }
    public int Count { get; set; }
    public decimal Revenue { get; set; }
}

// ---- Error DTO ----

public class ErrorResponse
{
    public int Status { get; set; }
    public string Error { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public List<FieldError>? Details { get; set; }
    public string TraceId { get; set; } = string.Empty;
}

public class FieldError
{
    public string Field { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
