using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using ShipmentTrackingAPI.Hubs;
using ShipmentTrackingAPI.Hubs.DTOs;
using ShipmentTrackingAPI.Services;

namespace ShipmentTrackingAPI.Tests.Services;

/// <summary>
/// NUnit tests for TrackingService — covers every public method and all branches.
///
/// Mocking strategy:
///   IHubContext<TrackingHub, ITrackingClient>  → Mock<IHubContext<...>>
///   IHubClients<ITrackingClient>               → Mock<IHubClients<ITrackingClient>>
///   ITrackingClient                            → Mock<ITrackingClient>  (per-client / per-group proxy)
///   ILogger<TrackingService>                   → Mock<ILogger<TrackingService>>
///
/// All async hub calls return Task.CompletedTask unless we need to assert
/// specific arguments — in that case we use Callback().
/// </summary>
[TestFixture]
public class TrackingServiceTests
{
    // ── Mocks ────────────────────────────────────────────────────────────────

    private Mock<IHubContext<TrackingHub, ITrackingClient>> _hubContextMock = null!;
    private Mock<IHubClients<ITrackingClient>>              _hubClientsMock = null!;
    private Mock<ITrackingClient>                           _clientProxyMock = null!;
    private Mock<ILogger<TrackingService>>                  _loggerMock = null!;
    private TrackingService                                 _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _hubContextMock  = new Mock<IHubContext<TrackingHub, ITrackingClient>>();
        _hubClientsMock  = new Mock<IHubClients<ITrackingClient>>();
        _clientProxyMock = new Mock<ITrackingClient>();
        _loggerMock      = new Mock<ILogger<TrackingService>>();

        // IHubContext.Clients → our mock clients collection
        _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);

        // Any group name → return the same proxy mock
        _hubClientsMock
            .Setup(c => c.Group(It.IsAny<string>()))
            .Returns(_clientProxyMock.Object);

        // Any specific connection → return the same proxy mock
        _hubClientsMock
            .Setup(c => c.Client(It.IsAny<string>()))
            .Returns(_clientProxyMock.Object);

        // All ITrackingClient methods return completed tasks by default
        _clientProxyMock.Setup(c => c.LocationUpdated(It.IsAny<LocationUpdatedDto>()))
                        .Returns(Task.CompletedTask);
        _clientProxyMock.Setup(c => c.StatusUpdated(It.IsAny<StatusUpdatedDto>()))
                        .Returns(Task.CompletedTask);
        _clientProxyMock.Setup(c => c.DriverArrived(It.IsAny<DriverArrivedDto>()))
                        .Returns(Task.CompletedTask);
        _clientProxyMock.Setup(c => c.ShipmentDelivered(It.IsAny<ShipmentDeliveredDto>()))
                        .Returns(Task.CompletedTask);
        _clientProxyMock.Setup(c => c.OtpRegenerated(It.IsAny<OtpRegeneratedDto>()))
                        .Returns(Task.CompletedTask);
        _clientProxyMock.Setup(c => c.PickupOtpGenerated(It.IsAny<OtpGeneratedDto>()))
                        .Returns(Task.CompletedTask);
        _clientProxyMock.Setup(c => c.DeliveryOtpGenerated(It.IsAny<OtpGeneratedDto>()))
                        .Returns(Task.CompletedTask);

        _sut = new TrackingService(_hubContextMock.Object, _loggerMock.Object);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  RegisterConnection / RemoveConnection
    // ════════════════════════════════════════════════════════════════════════

    [Test]
    public async Task RegisterConnection_ThenBroadcast_UsesCorrectConnectionId()
    {
        // Arrange
        const int    userId       = 42;
        const string connectionId = "conn-abc";
        _sut.RegisterConnection(userId, connectionId);

        // Act — PushOtpToSenderAsync uses the registered connectionId
        await _sut.PushOtpToSenderAsync(userId, "TRK001", "1234", DateTime.UtcNow.AddMinutes(15));

        // Assert — Client() was called with the registered connectionId
        _hubClientsMock.Verify(c => c.Client(connectionId), Times.Once);
        _clientProxyMock.Verify(c => c.PickupOtpGenerated(It.IsAny<OtpGeneratedDto>()), Times.Once);
    }

    [Test]
    public void RegisterConnection_OverwritesPreviousConnectionForSameUser()
    {
        // Arrange
        const int userId = 10;
        _sut.RegisterConnection(userId, "old-conn");

        // Act — second registration for same user
        _sut.RegisterConnection(userId, "new-conn");

        // Assert — no exception; verified implicitly by later OTP push using new conn
        Assert.DoesNotThrowAsync(async () =>
            await _sut.PushOtpToSenderAsync(userId, "TRK002", "5678", DateTime.UtcNow));

        _hubClientsMock.Verify(c => c.Client("new-conn"), Times.Once);
    }

    [Test]
    public void RemoveConnection_KnownConnection_CleansUpBothDictionaries()
    {
        // Arrange
        const int    userId       = 7;
        const string connectionId = "conn-7";
        _sut.RegisterConnection(userId, connectionId);

        // Act
        _sut.RemoveConnection(connectionId);

        // Assert — OTP push should now log warning and NOT call Client()
        Assert.DoesNotThrowAsync(async () =>
            await _sut.PushOtpToSenderAsync(userId, "TRK003", "0000", DateTime.UtcNow));

        _hubClientsMock.Verify(c => c.Client(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public void RemoveConnection_UnknownConnection_DoesNotThrow()
    {
        // Should silently no-op for connections that were never registered
        Assert.DoesNotThrow(() => _sut.RemoveConnection("ghost-conn"));
    }

    [Test]
    public void RemoveConnection_StaleConnectionId_DoesNotRemoveNewerConnection()
    {
        // Simulate: user reconnects (new connectionId) then old tab disconnects
        const int    userId      = 99;
        const string oldConn     = "conn-old";
        const string newConn     = "conn-new";

        _sut.RegisterConnection(userId, oldConn);
        _sut.RegisterConnection(userId, newConn);   // new tab wins

        // Old tab disconnects
        _sut.RemoveConnection(oldConn);

        // New conn should still be active — OTP push should reach newConn
        Assert.DoesNotThrowAsync(async () =>
            await _sut.PushOtpToSenderAsync(userId, "TRK099", "1111", DateTime.UtcNow));

        _hubClientsMock.Verify(c => c.Client(newConn), Times.Once);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  BroadcastLocationUpdateAsync
    // ════════════════════════════════════════════════════════════════════════

    [Test]
    public async Task BroadcastLocationUpdateAsync_CallsGroupWithCorrectName()
    {
        // Act
        await _sut.BroadcastLocationUpdateAsync("TRK-LOC", 12.345, 98.765);

        // Assert
        _hubClientsMock.Verify(c => c.Group("shipment-TRK-LOC"), Times.Once);
    }

    [Test]
    public async Task BroadcastLocationUpdateAsync_SendsCorrectDto()
    {
        LocationUpdatedDto? captured = null;
        _clientProxyMock
            .Setup(c => c.LocationUpdated(It.IsAny<LocationUpdatedDto>()))
            .Callback<LocationUpdatedDto>(dto => captured = dto)
            .Returns(Task.CompletedTask);

        await _sut.BroadcastLocationUpdateAsync("TRK100", 1.0, 2.0);

        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.TrackingNumber, Is.EqualTo("TRK100"));
        Assert.That(captured.Latitude,        Is.EqualTo(1.0));
        Assert.That(captured.Longitude,       Is.EqualTo(2.0));
        Assert.That(captured.Timestamp,       Is.LessThanOrEqualTo(DateTime.UtcNow));
    }

    // ════════════════════════════════════════════════════════════════════════
    //  BroadcastStatusUpdateAsync
    // ════════════════════════════════════════════════════════════════════════

    [Test]
    public async Task BroadcastStatusUpdateAsync_CallsGroupWithCorrectName()
    {
        await _sut.BroadcastStatusUpdateAsync("TRK-STS", "InTransit", "Moving");

        _hubClientsMock.Verify(c => c.Group("shipment-TRK-STS"), Times.Once);
    }

    [Test]
    public async Task BroadcastStatusUpdateAsync_SendsCorrectDto()
    {
        StatusUpdatedDto? captured = null;
        _clientProxyMock
            .Setup(c => c.StatusUpdated(It.IsAny<StatusUpdatedDto>()))
            .Callback<StatusUpdatedDto>(dto => captured = dto)
            .Returns(Task.CompletedTask);

        await _sut.BroadcastStatusUpdateAsync("TRK200", "PickedUp", "Parcel collected.");

        Assert.That(captured!.TrackingNumber, Is.EqualTo("TRK200"));
        Assert.That(captured.NewStatus,       Is.EqualTo("PickedUp"));
        Assert.That(captured.Description,     Is.EqualTo("Parcel collected."));
    }

    // ════════════════════════════════════════════════════════════════════════
    //  BroadcastDriverArrivedAsync
    // ════════════════════════════════════════════════════════════════════════

    [Test]
    public async Task BroadcastDriverArrivedAsync_CallsGroupAndSendsDto()
    {
        DriverArrivedDto? captured = null;
        _clientProxyMock
            .Setup(c => c.DriverArrived(It.IsAny<DriverArrivedDto>()))
            .Callback<DriverArrivedDto>(dto => captured = dto)
            .Returns(Task.CompletedTask);

        await _sut.BroadcastDriverArrivedAsync("TRK300", 13.0, 80.0);

        _hubClientsMock.Verify(c => c.Group("shipment-TRK300"), Times.Once);
        Assert.That(captured!.TrackingNumber, Is.EqualTo("TRK300"));
        Assert.That(captured.DriverLat,       Is.EqualTo(13.0));
        Assert.That(captured.DriverLng,       Is.EqualTo(80.0));
    }

    // ════════════════════════════════════════════════════════════════════════
    //  BroadcastDeliverySuccessAsync
    // ════════════════════════════════════════════════════════════════════════

    [Test]
    public async Task BroadcastDeliverySuccessAsync_CallsGroupAndSendsDto()
    {
        ShipmentDeliveredDto? captured = null;
        _clientProxyMock
            .Setup(c => c.ShipmentDelivered(It.IsAny<ShipmentDeliveredDto>()))
            .Callback<ShipmentDeliveredDto>(dto => captured = dto)
            .Returns(Task.CompletedTask);

        await _sut.BroadcastDeliverySuccessAsync("TRK400");

        _hubClientsMock.Verify(c => c.Group("shipment-TRK400"), Times.Once);
        Assert.That(captured!.TrackingNumber, Is.EqualTo("TRK400"));
        Assert.That(captured.DeliveredAt,     Is.LessThanOrEqualTo(DateTime.UtcNow));
    }

    // ════════════════════════════════════════════════════════════════════════
    //  BroadcastOtpRegeneratedAsync
    // ════════════════════════════════════════════════════════════════════════

    [Test]
    public async Task BroadcastOtpRegeneratedAsync_CallsGroupAndSendsDto()
    {
        OtpRegeneratedDto? captured = null;
        _clientProxyMock
            .Setup(c => c.OtpRegenerated(It.IsAny<OtpRegeneratedDto>()))
            .Callback<OtpRegeneratedDto>(dto => captured = dto)
            .Returns(Task.CompletedTask);

        var expires = DateTime.UtcNow.AddMinutes(15);
        await _sut.BroadcastOtpRegeneratedAsync("TRK500", "Pickup", expires);

        _hubClientsMock.Verify(c => c.Group("shipment-TRK500"), Times.Once);
        Assert.That(captured!.TrackingNumber, Is.EqualTo("TRK500"));
        Assert.That(captured.OtpType,         Is.EqualTo("Pickup"));
        Assert.That(captured.ExpiresAt,       Is.EqualTo(expires));
    }

    // ════════════════════════════════════════════════════════════════════════
    //  PushOtpToSenderAsync
    // ════════════════════════════════════════════════════════════════════════

    [Test]
    public async Task PushOtpToSenderAsync_UserConnected_PushesPickupOtpToConnection()
    {
        const int    userId = 1;
        const string connId = "conn-sender";
        _sut.RegisterConnection(userId, connId);

        OtpGeneratedDto? captured = null;
        _clientProxyMock
            .Setup(c => c.PickupOtpGenerated(It.IsAny<OtpGeneratedDto>()))
            .Callback<OtpGeneratedDto>(dto => captured = dto)
            .Returns(Task.CompletedTask);

        var expires = DateTime.UtcNow.AddMinutes(15);
        await _sut.PushOtpToSenderAsync(userId, "TRK-S1", "1234", expires);

        _hubClientsMock.Verify(c => c.Client(connId), Times.Once);
        Assert.That(captured!.OtpType,        Is.EqualTo("Pickup"));
        Assert.That(captured.TrackingNumber,  Is.EqualTo("TRK-S1"));
        Assert.That(captured.OtpCode,         Is.EqualTo("1234"));
        Assert.That(captured.ExpiresAt,       Is.EqualTo(expires));
    }

    [Test]
    public async Task PushOtpToSenderAsync_UserNotConnected_LogsWarningAndSkipsPush()
    {
        // User 999 was never registered
        await _sut.PushOtpToSenderAsync(999, "TRK-MISS", "0000", DateTime.UtcNow);

        // Hub Client() should never be called
        _hubClientsMock.Verify(c => c.Client(It.IsAny<string>()), Times.Never);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  PushOtpToRecipientAsync
    // ════════════════════════════════════════════════════════════════════════

    [Test]
    public async Task PushOtpToRecipientAsync_UserConnected_PushesDeliveryOtpToConnection()
    {
        const int    userId = 2;
        const string connId = "conn-recipient";
        _sut.RegisterConnection(userId, connId);

        OtpGeneratedDto? captured = null;
        _clientProxyMock
            .Setup(c => c.DeliveryOtpGenerated(It.IsAny<OtpGeneratedDto>()))
            .Callback<OtpGeneratedDto>(dto => captured = dto)
            .Returns(Task.CompletedTask);

        var expires = DateTime.UtcNow.AddMinutes(15);
        await _sut.PushOtpToRecipientAsync(userId, "TRK-R1", "5678", expires);

        _hubClientsMock.Verify(c => c.Client(connId), Times.Once);
        Assert.That(captured!.OtpType,        Is.EqualTo("Delivery"));
        Assert.That(captured.TrackingNumber,  Is.EqualTo("TRK-R1"));
        Assert.That(captured.OtpCode,         Is.EqualTo("5678"));
    }

    [Test]
    public async Task PushOtpToRecipientAsync_UserNotConnected_LogsWarningAndSkipsPush()
    {
        await _sut.PushOtpToRecipientAsync(888, "TRK-MISS2", "9999", DateTime.UtcNow);

        _hubClientsMock.Verify(c => c.Client(It.IsAny<string>()), Times.Never);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  GroupName helper (tested implicitly via group broadcasts)
    // ════════════════════════════════════════════════════════════════════════

    [Test]
    public async Task GroupName_AlwaysPrefixedWithShipmentDash()
    {
        await _sut.BroadcastLocationUpdateAsync("XYZ789", 0, 0);

        _hubClientsMock.Verify(c => c.Group("shipment-XYZ789"), Times.Once);
    }
}