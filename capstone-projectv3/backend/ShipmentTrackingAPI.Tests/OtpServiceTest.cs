using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;
using ShipmentTrackingAPI.Data;
using ShipmentTrackingAPI.DTOs.Otp;
using ShipmentTrackingAPI.Interfaces;
using ShipmentTrackingAPI.Models;
using ShipmentTrackingAPI.Models.Enums;
using ShipmentTrackingAPI.Models.Exceptions;
using ShipmentTrackingAPI.Repositories.RepoInterfaces;
using ShipmentTrackingAPI.Services;
using ShipmentTrackingAPI.Services.Interfaces;

namespace ShipmentTrackingAPI.Tests.Services;

/// <summary>
/// NUnit tests for OtpService — 100% branch coverage.
///
/// DB strategy:
///   AppDbContext uses EF Core InMemory provider.
///   Each test gets a fresh database (unique name via TestContext.CurrentContext.Test.FullName).
///
/// Dependency mocks:
///   IShipmentRepository  → Mock<IShipmentRepository>
///   ITrackingService     → Mock<ITrackingService>
///
/// Abbreviations used in variable names:
///   sut  = System Under Test (OtpService)
///   trk  = tracking number
/// </summary>
[TestFixture]
public class OtpServiceTests
{
    // ── Shared mocks / SUT ────────────────────────────────────────────────────

    private Mock<IShipmentRepository> _repoMock     = null!;
    private Mock<ITrackingService>    _trackingMock = null!;
    private AppDbContext               _db           = null!;
    private OtpService                 _sut          = null!;

    // ── Well-known driver / customer ids ─────────────────────────────────────
    private const int DriverId    = 10;
    private const int CustomerId  = 20;
    private const int OtherDriver = 99;

    // ─────────────────────────────────────────────────────────────────────────
    //  SetUp / TearDown
    // ─────────────────────────────────────────────────────────────────────────

    [SetUp]
    public void SetUp()
    {
        // Fresh in-memory DB per test
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(TestContext.CurrentContext.Test.FullName)
            .Options;
        _db = new AppDbContext(options);

        _repoMock     = new Mock<IShipmentRepository>();
        _trackingMock = new Mock<ITrackingService>();

        // All tracking calls are fire-and-forget in tests
        _trackingMock.Setup(t => t.PushOtpToSenderAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);
        _trackingMock.Setup(t => t.PushOtpToRecipientAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);
        _trackingMock.Setup(t => t.BroadcastStatusUpdateAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _trackingMock.Setup(t => t.BroadcastDeliverySuccessAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _trackingMock.Setup(t => t.BroadcastOtpRegeneratedAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);

        _sut = new OtpService(_repoMock.Object, _trackingMock.Object, _db);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    // ─────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static Shipment BuildShipment(
        int              id,
        ShipmentStatus   status,
        int              driverId   = DriverId,
        int              customerId = CustomerId,
        string           trk        = "TRK-001") =>
        new()
        {
            Id             = id,
            TrackingNumber = trk,
            Status         = status,
            DriverId       = driverId,
            CustomerId     = customerId,
        };

    /// <summary>
    /// Wires up the repository mock AND inserts the shipment into the in-memory DB.
    ///
    /// Why both?
    ///   - The mock satisfies GetByIdWithAddressesAsync (which loads addresses via
    ///     a custom repository query that EF InMemory can't replicate).
    ///   - The DB insert is required so that EF's change-tracker can satisfy
    ///     _ctx.Shipments.Update(shipment) inside VerifyOtpAsync without throwing
    ///     DbUpdateConcurrencyException ("entity does not exist in store").
    ///
    /// We detach the entity after inserting so that the service's own Update() call
    /// does not conflict with the already-tracked instance.
    /// </summary>
    private void SetupRepo(Shipment? shipment)
    {
        _repoMock
            .Setup(r => r.GetByIdWithAddressesAsync(It.IsAny<int>()))
            .ReturnsAsync(shipment);

        if (shipment == null) return;

        _repoMock
            .Setup(r => r.UpsertOtpWindowAsync(It.IsAny<ShipmentOtpWindow>()))
            .Returns(Task.CompletedTask);

        // Seed into the in-memory store so EF Update() can find the row.
        _db.Shipments.Add(shipment);
        _db.SaveChanges();

        // Detach so the service can re-attach and call Update() without a
        // "already tracked" InvalidOperationException.
        _db.Entry(shipment).State = EntityState.Detached;
    }

    private async Task SeedOtpWindowAsync(
        int      shipmentId,
        OtpType  otpType,
        string   code         = "1234",
        short      attemptCount = 0,
        bool     expired      = false,
        bool     verified     = false)
    {
        var expiresAt = expired
            ? DateTime.UtcNow.AddMinutes(-1)   // already expired
            : DateTime.UtcNow.AddMinutes(15);

        _db.ShipmentOtpWindows.Add(new ShipmentOtpWindow
        {
            ShipmentId   = shipmentId,
            OtpType      = otpType,
            OtpCode      = verified ? null : code,
            ExpiresAt    = verified ? null : expiresAt,
            AttemptCount = attemptCount,
            GeneratedAt  = DateTime.UtcNow.AddMinutes(-1),
            VerifiedAt   = verified ? DateTime.UtcNow : null,
        });
        await _db.SaveChangesAsync();
    }

    // ════════════════════════════════════════════════════════════════════════
    //  RequestOtpAsync
    // ════════════════════════════════════════════════════════════════════════

    [Test]
    public async Task RequestOtpAsync_ValidPickupRequest_ReturnsWindowDtoAndPushesToSender()
    {
        // Arrange
        var shipment = BuildShipment(1, ShipmentStatus.Assigned);
        SetupRepo(shipment);

        // Act
        var result = await _sut.RequestOtpAsync(1, OtpType.Pickup, DriverId);

        // Assert
        Assert.That(result.OtpType,      Is.EqualTo("Pickup"));
        Assert.That(result.AttemptCount, Is.EqualTo(0));
        Assert.That(result.ExpiresAt,    Is.GreaterThan(DateTime.UtcNow));

        _trackingMock.Verify(t =>
            t.PushOtpToSenderAsync(CustomerId, "TRK-001", It.IsAny<string>(), It.IsAny<DateTime>()),
            Times.Once);
    }

    [Test]
    public async Task RequestOtpAsync_ValidDeliveryRequest_PushesToRecipient()
    {
        var shipment = BuildShipment(2, ShipmentStatus.Arrived);
        SetupRepo(shipment);

        var result = await _sut.RequestOtpAsync(2, OtpType.Delivery, DriverId);

        Assert.That(result.OtpType, Is.EqualTo("Delivery"));

        _trackingMock.Verify(t =>
            t.PushOtpToRecipientAsync(CustomerId, "TRK-001", It.IsAny<string>(), It.IsAny<DateTime>()),
            Times.Once);
    }

    [Test]
    public void RequestOtpAsync_ShipmentNotFound_ThrowsNotFoundException()
    {
        SetupRepo(null);

        Assert.ThrowsAsync<NotFoundException>(
            () => _sut.RequestOtpAsync(999, OtpType.Pickup, DriverId));
    }

    [Test]
    public void RequestOtpAsync_WrongDriver_ThrowsForbiddenException()
    {
        var shipment = BuildShipment(3, ShipmentStatus.Assigned);
        SetupRepo(shipment);

        Assert.ThrowsAsync<ForbiddenException>(
            () => _sut.RequestOtpAsync(3, OtpType.Pickup, OtherDriver));
    }

    [Test]
    public void RequestOtpAsync_PickupOtp_WrongStatus_ThrowsConflictException()
    {
        // Pickup requires Assigned; give it PickedUp instead
        var shipment = BuildShipment(4, ShipmentStatus.PickedUp);
        SetupRepo(shipment);

        Assert.ThrowsAsync<ConflictException>(
            () => _sut.RequestOtpAsync(4, OtpType.Pickup, DriverId));
    }

    [Test]
    public void RequestOtpAsync_DeliveryOtp_WrongStatus_ThrowsConflictException()
    {
        // Delivery requires Arrived; give it InTransit
        var shipment = BuildShipment(5, ShipmentStatus.InTransit);
        SetupRepo(shipment);

        Assert.ThrowsAsync<ConflictException>(
            () => _sut.RequestOtpAsync(5, OtpType.Delivery, DriverId));
    }

    [Test]
    public async Task RequestOtpAsync_GeneratedCode_IsFourDigitNumericString()
    {
        string? capturedCode = null;
        var shipment = BuildShipment(6, ShipmentStatus.Assigned);
        SetupRepo(shipment);

        _trackingMock
            .Setup(t => t.PushOtpToSenderAsync(
                It.IsAny<int>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<DateTime>()))
            .Callback<int, string, string, DateTime>((_, _, code, _) => capturedCode = code)
            .Returns(Task.CompletedTask);

        await _sut.RequestOtpAsync(6, OtpType.Pickup, DriverId);

        Assert.That(capturedCode, Has.Length.EqualTo(4));
        Assert.That(int.TryParse(capturedCode, out _), Is.True);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  VerifyOtpAsync
    // ════════════════════════════════════════════════════════════════════════

    [Test]
    public async Task VerifyOtpAsync_CorrectPickupCode_TransitionsToPickedUpAndBroadcasts()
    {
        var shipment = BuildShipment(10, ShipmentStatus.Assigned, trk: "TRK-PICKUP");
        SetupRepo(shipment);
        await SeedOtpWindowAsync(10, OtpType.Pickup, "1234");

        var result = await _sut.VerifyOtpAsync(10, OtpType.Pickup, "1234", DriverId);

        Assert.That(result.Success,    Is.True);
        Assert.That(result.NewStatus,  Is.EqualTo(ShipmentStatus.PickedUp));

        _trackingMock.Verify(t =>
            t.BroadcastStatusUpdateAsync("TRK-PICKUP", "PickedUp", It.IsAny<string>()),
            Times.Once);

        // PickedUpAt should be set in DB
        var saved = await _db.Shipments.FindAsync(10);
        Assert.That(saved!.PickedUpAt, Is.Not.Null);
    }

    [Test]
    public async Task VerifyOtpAsync_CorrectDeliveryCode_TransitionsToDeliveredAndBroadcasts()
    {
        var shipment = BuildShipment(11, ShipmentStatus.Arrived, trk: "TRK-DEL");
        SetupRepo(shipment);
        await SeedOtpWindowAsync(11, OtpType.Delivery, "5678");

        var result = await _sut.VerifyOtpAsync(11, OtpType.Delivery, "5678", DriverId);

        Assert.That(result.Success,   Is.True);
        Assert.That(result.NewStatus, Is.EqualTo(ShipmentStatus.Delivered));

        _trackingMock.Verify(t => t.BroadcastDeliverySuccessAsync("TRK-DEL"), Times.Once);

        var saved = await _db.Shipments.FindAsync(11);
        Assert.That(saved!.DeliveredAt, Is.Not.Null);
    }

    [Test]
    public async Task VerifyOtpAsync_CorrectCode_ClearsOtpCodeFromDb()
    {
        var shipment = BuildShipment(12, ShipmentStatus.Assigned);
        SetupRepo(shipment);
        await SeedOtpWindowAsync(12, OtpType.Pickup, "9999");

        await _sut.VerifyOtpAsync(12, OtpType.Pickup, "9999", DriverId);

        var window = await _db.ShipmentOtpWindows
            .FirstAsync(w => w.ShipmentId == 12 && w.OtpType == OtpType.Pickup);

        Assert.That(window.OtpCode,    Is.Null);
        Assert.That(window.ExpiresAt,  Is.Null);
        Assert.That(window.VerifiedAt, Is.Not.Null);
    }

    [Test]
    public async Task VerifyOtpAsync_IncorrectCode_ReturnsFalseAndDecrementsAttempts()
    {
        var shipment = BuildShipment(13, ShipmentStatus.Assigned);
        SetupRepo(shipment);
        await SeedOtpWindowAsync(13, OtpType.Pickup, "1111");

        var result = await _sut.VerifyOtpAsync(13, OtpType.Pickup, "0000", DriverId);

        Assert.That(result.Success,           Is.False);
        Assert.That(result.RemainingAttempts, Is.EqualTo(2));
    }

    [Test]
    public async Task VerifyOtpAsync_ThirdFailedAttempt_ThrowsRateLimitException()
    {
        var shipment = BuildShipment(14, ShipmentStatus.Assigned);
        SetupRepo(shipment);
        await SeedOtpWindowAsync(14, OtpType.Pickup, "1111", attemptCount: 2);

        Assert.ThrowsAsync<RateLimitException>(
            () => _sut.VerifyOtpAsync(14, OtpType.Pickup, "0000", DriverId));
    }

    [Test]
    public async Task VerifyOtpAsync_AlreadyLocked_ThrowsRateLimitExceptionImmediately()
    {
        var shipment = BuildShipment(15, ShipmentStatus.Assigned);
        SetupRepo(shipment);
        // Seed with attempt_count = 3 → IsLocked = true
        await SeedOtpWindowAsync(15, OtpType.Pickup, "1111", attemptCount: 3);

        Assert.ThrowsAsync<RateLimitException>(
            () => _sut.VerifyOtpAsync(15, OtpType.Pickup, "1111", DriverId));
    }

    [Test]
    public async Task VerifyOtpAsync_ExpiredWindow_ThrowsBadRequestException()
    {
        var shipment = BuildShipment(16, ShipmentStatus.Assigned);
        SetupRepo(shipment);
        await SeedOtpWindowAsync(16, OtpType.Pickup, "1234", expired: true);

        Assert.ThrowsAsync<BadRequestException>(
            () => _sut.VerifyOtpAsync(16, OtpType.Pickup, "1234", DriverId));
    }

    [Test]
    public async Task VerifyOtpAsync_AlreadyVerified_ThrowsConflictException()
    {
        var shipment = BuildShipment(17, ShipmentStatus.PickedUp);
        SetupRepo(shipment);
        await SeedOtpWindowAsync(17, OtpType.Pickup, verified: true);

        Assert.ThrowsAsync<ConflictException>(
            () => _sut.VerifyOtpAsync(17, OtpType.Pickup, "1234", DriverId));
    }

    [Test]
    public void VerifyOtpAsync_NoWindowExists_ThrowsBadRequestException()
    {
        var shipment = BuildShipment(18, ShipmentStatus.Assigned);
        SetupRepo(shipment);
        // No OTP window seeded at all

        Assert.ThrowsAsync<BadRequestException>(
            () => _sut.VerifyOtpAsync(18, OtpType.Pickup, "1234", DriverId));
    }

    [Test]
    public void VerifyOtpAsync_ShipmentNotFound_ThrowsNotFoundException()
    {
        SetupRepo(null);

        Assert.ThrowsAsync<NotFoundException>(
            () => _sut.VerifyOtpAsync(999, OtpType.Pickup, "1234", DriverId));
    }

    [Test]
    public void VerifyOtpAsync_WrongDriver_ThrowsForbiddenException()
    {
        var shipment = BuildShipment(19, ShipmentStatus.Assigned);
        SetupRepo(shipment);

        Assert.ThrowsAsync<ForbiddenException>(
            () => _sut.VerifyOtpAsync(19, OtpType.Pickup, "1234", OtherDriver));
    }

    [Test]
    public async Task VerifyOtpAsync_CorrectCode_TrimsWhitespaceFromSubmittedCode()
    {
        var shipment = BuildShipment(20, ShipmentStatus.Assigned);
        SetupRepo(shipment);
        await SeedOtpWindowAsync(20, OtpType.Pickup, "4321");

        // Submit code with surrounding spaces — should still match
        var result = await _sut.VerifyOtpAsync(20, OtpType.Pickup, "  4321  ", DriverId);

        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task VerifyOtpAsync_PickupSuccess_AddsAuditEvent()
    {
        var shipment = BuildShipment(21, ShipmentStatus.Assigned);
        SetupRepo(shipment);
        await SeedOtpWindowAsync(21, OtpType.Pickup, "2222");

        await _sut.VerifyOtpAsync(21, OtpType.Pickup, "2222", DriverId);

        var @event = await _db.ShipmentEvents
            .FirstOrDefaultAsync(e => e.ShipmentId == 21);

        Assert.That(@event,          Is.Not.Null);
        Assert.That(@event!.Status,  Is.EqualTo(ShipmentStatus.PickedUp));
        Assert.That(@event.ActorId,  Is.EqualTo(DriverId));
    }

    [Test]
    public async Task VerifyOtpAsync_DeliverySuccess_AddsAuditEvent()
    {
        var shipment = BuildShipment(22, ShipmentStatus.Arrived);
        SetupRepo(shipment);
        await SeedOtpWindowAsync(22, OtpType.Delivery, "3333");

        await _sut.VerifyOtpAsync(22, OtpType.Delivery, "3333", DriverId);

        var @event = await _db.ShipmentEvents
            .FirstOrDefaultAsync(e => e.ShipmentId == 22);

        Assert.That(@event!.Status, Is.EqualTo(ShipmentStatus.Delivered));
    }

    // ════════════════════════════════════════════════════════════════════════
    //  RegenerateOtpAsync
    // ════════════════════════════════════════════════════════════════════════

    [Test]
    public async Task RegenerateOtpAsync_LockedWindow_ResetsAndPushesToSender()
    {
        var shipment = BuildShipment(30, ShipmentStatus.Assigned, trk: "TRK-REGEN");
        SetupRepo(shipment);
        await SeedOtpWindowAsync(30, OtpType.Pickup, "0000", attemptCount: 3); // locked

        var result = await _sut.RegenerateOtpAsync(30, OtpType.Pickup, DriverId);

        Assert.That(result.AttemptCount, Is.EqualTo(0));
        Assert.That(result.ExpiresAt,    Is.GreaterThan(DateTime.UtcNow));

        _trackingMock.Verify(t =>
            t.PushOtpToSenderAsync(CustomerId, "TRK-REGEN", It.IsAny<string>(), It.IsAny<DateTime>()),
            Times.Once);

        _trackingMock.Verify(t =>
            t.BroadcastOtpRegeneratedAsync("TRK-REGEN", "Pickup", It.IsAny<DateTime>()),
            Times.Once);
    }

    [Test]
    public async Task RegenerateOtpAsync_ExpiredWindow_ResetsAndPushesToRecipient()
    {
        var shipment = BuildShipment(31, ShipmentStatus.Arrived, trk: "TRK-REGEN2");
        SetupRepo(shipment);
        await SeedOtpWindowAsync(31, OtpType.Delivery, "1111", expired: true);

        var result = await _sut.RegenerateOtpAsync(31, OtpType.Delivery, DriverId);

        Assert.That(result.OtpType, Is.EqualTo("Delivery"));

        _trackingMock.Verify(t =>
            t.PushOtpToRecipientAsync(CustomerId, "TRK-REGEN2", It.IsAny<string>(), It.IsAny<DateTime>()),
            Times.Once);
    }

    [Test]
    public async Task RegenerateOtpAsync_WindowStillValid_ThrowsConflictException()
    {
        var shipment = BuildShipment(32, ShipmentStatus.Assigned);
        SetupRepo(shipment);
        // Active, not locked, not expired → regeneration must be refused
        await SeedOtpWindowAsync(32, OtpType.Pickup, "5555", attemptCount: 1);

        Assert.ThrowsAsync<ConflictException>(
            () => _sut.RegenerateOtpAsync(32, OtpType.Pickup, DriverId));
    }

    [Test]
    public void RegenerateOtpAsync_NoWindowExists_ThrowsBadRequestException()
    {
        var shipment = BuildShipment(33, ShipmentStatus.Assigned);
        SetupRepo(shipment);
        // No window seeded

        Assert.ThrowsAsync<BadRequestException>(
            () => _sut.RegenerateOtpAsync(33, OtpType.Pickup, DriverId));
    }

    [Test]
    public void RegenerateOtpAsync_ShipmentNotFound_ThrowsNotFoundException()
    {
        SetupRepo(null);

        Assert.ThrowsAsync<NotFoundException>(
            () => _sut.RegenerateOtpAsync(999, OtpType.Pickup, DriverId));
    }

    [Test]
    public void RegenerateOtpAsync_WrongDriver_ThrowsForbiddenException()
    {
        var shipment = BuildShipment(34, ShipmentStatus.Assigned);
        SetupRepo(shipment);

        Assert.ThrowsAsync<ForbiddenException>(
            () => _sut.RegenerateOtpAsync(34, OtpType.Pickup, OtherDriver));
    }

    [Test]
    public async Task RegenerateOtpAsync_ResetsAttemptCount_AndUpdatesGeneratedAt()
    {
        var shipment = BuildShipment(35, ShipmentStatus.Assigned);
        SetupRepo(shipment);
        await SeedOtpWindowAsync(35, OtpType.Pickup, "9000", attemptCount: 3);

        await _sut.RegenerateOtpAsync(35, OtpType.Pickup, DriverId);

        var window = await _db.ShipmentOtpWindows
            .FirstAsync(w => w.ShipmentId == 35 && w.OtpType == OtpType.Pickup);

        Assert.That(window.AttemptCount, Is.EqualTo(0));
        Assert.That(window.VerifiedAt,   Is.Null);
        Assert.That(window.OtpCode,      Is.Not.Null);
        Assert.That(window.OtpCode,      Has.Length.EqualTo(4));
    }
}