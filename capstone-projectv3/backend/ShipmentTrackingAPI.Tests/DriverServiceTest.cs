using NUnit.Framework;
using Moq;
using ShipmentTrackingAPI.Services;
using ShipmentTrackingAPI.DTOs.Driver;
using ShipmentTrackingAPI.Models;
using ShipmentTrackingAPI.Models.Enums;
using ShipmentTrackingAPI.Models.Exceptions;
using ShipmentTrackingAPI.Repositories.RepoInterfaces;

namespace ShipmentTrackingAPI.Tests.Services;

/// <summary>
/// Full coverage tests for DriverService.
///
/// COVERAGE MAP
/// ────────────
/// GetMyProfileAsync
///   ✓ profile found → returns correctly mapped DTO
///   ✓ profile not found → throws NotFoundException
///   ✓ all DTO fields mapped correctly
///
/// UpdateOpStatusAsync
///   ✓ Active driver → Available (no active shipment) → succeeds
///   ✓ Active driver → Offline → succeeds, clears GPS
///   ✓ Active driver → Available with coordinates → sets GPS
///   ✓ Active driver → Available without coordinates → GPS unchanged
///   ✓ Active driver → Offline → wipes CurrentLat/CurrentLng to null
///   ✓ PendingApproval account → throws ForbiddenException
///   ✓ Suspended account → throws ForbiddenException
///   ✓ Deleted account → throws ForbiddenException
///   ✓ newStatus = InTransit → throws ConflictException (cannot self-set)
///   ✓ Available but has active shipment → throws ConflictException
///   ✓ Available and no active shipment → HasActiveShipmentAsync not called for Offline
///   ✓ UpdateAsync + SaveAsync both called
///   ✓ UpdatedAt timestamp is set
///   ✓ profile not found → throws NotFoundException
/// </summary>
[TestFixture]
public class DriverServiceTest
{
    private Mock<IDriverRepository>   _driverRepoMock   = null!;
    private Mock<IShipmentRepository> _shipmentRepoMock = null!;
    private DriverService             _sut              = null!;

    // ── Shared test data ─────────────────────────────────────

    private static DriverProfile MakeActiveProfile(int userId = 1) => new()
    {
        Id            = 10,
        UserId        = userId,
        VehicleType   = "Bike",
        VehicleNumber = "TS-09-AB-1234",
        LicenseNumber = "LIC123456",
        AccountStatus = DriverAccountStatus.Active,
        OpStatus      = DriverOpStatus.Offline,
        CurrentLat    = null,
        CurrentLng    = null,
        UpdatedAt     = DateTime.UtcNow,
        CreatedAt     = DateTime.UtcNow.AddDays(-30),
        User = new User
        {
            Id       = userId,
            FullName = "Test Driver",
            Email    = "driver@test.com",
        }
    };

    private static DriverProfile MakeProfileWithStatus(
        DriverAccountStatus accountStatus,
        int userId = 1)
    {
        var profile = MakeActiveProfile(userId);
        profile.AccountStatus = accountStatus;
        return profile;
    }

    // ── Setup ────────────────────────────────────────────────

    [SetUp]
    public void SetUp()
    {
        _driverRepoMock   = new Mock<IDriverRepository>(MockBehavior.Strict);
        _shipmentRepoMock = new Mock<IShipmentRepository>(MockBehavior.Strict);
        _sut = new DriverService(
            _driverRepoMock.Object,
            _shipmentRepoMock.Object);
    }

    // ═══════════════════════════════════════════════════════════
    //  GetMyProfileAsync
    // ═══════════════════════════════════════════════════════════

    [Test]
    public async Task GetMyProfileAsync_ProfileExists_ReturnsMappedDto()
    {
        // Arrange
        var profile = MakeActiveProfile(userId: 7);
        _driverRepoMock.Setup(r => r.GetByUserIdAsync(7))
                       .ReturnsAsync(profile);

        // Act
        var result = await _sut.GetMyProfileAsync(7);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.UserId,        Is.EqualTo(7));
            Assert.That(result.FullName,      Is.EqualTo("Test Driver"));
            Assert.That(result.Email,         Is.EqualTo("driver@test.com"));
            Assert.That(result.VehicleType,   Is.EqualTo("Bike"));
            Assert.That(result.VehicleNumber, Is.EqualTo("TS-09-AB-1234"));
            Assert.That(result.LicenseNumber, Is.EqualTo("LIC123456"));
            Assert.That(result.AccountStatus, Is.EqualTo(DriverAccountStatus.Active));
            Assert.That(result.OpStatus,      Is.EqualTo(DriverOpStatus.Offline));
            Assert.That(result.CurrentLat,    Is.Null);
            Assert.That(result.CurrentLng,    Is.Null);
        });
    }

    [Test]
    public void GetMyProfileAsync_ProfileNotFound_ThrowsNotFoundException()
    {
        // Arrange
        _driverRepoMock.Setup(r => r.GetByUserIdAsync(99))
                       .ReturnsAsync((DriverProfile?)null);

        // Act & Assert
        Assert.ThrowsAsync<NotFoundException>(
            () => _sut.GetMyProfileAsync(99));
    }

    [Test]
    public async Task GetMyProfileAsync_MapsCreatedAt()
    {
        // Arrange
        var profile    = MakeActiveProfile();
        var expectedDt = profile.CreatedAt;
        _driverRepoMock.Setup(r => r.GetByUserIdAsync(1)).ReturnsAsync(profile);

        // Act
        var result = await _sut.GetMyProfileAsync(1);

        // Assert
        Assert.That(result.CreatedAt, Is.EqualTo(expectedDt));
    }

    // ═══════════════════════════════════════════════════════════
    //  UpdateOpStatusAsync — Account status guards
    // ═══════════════════════════════════════════════════════════

    [Test]
    public void UpdateOpStatusAsync_PendingApprovalAccount_ThrowsForbiddenException()
    {
        // Arrange
        var profile = MakeProfileWithStatus(DriverAccountStatus.PendingApproval);
        _driverRepoMock.Setup(r => r.GetByUserIdAsync(1)).ReturnsAsync(profile);

        var req = new UpdateOpStatusRequestDto { NewStatus = DriverOpStatus.Available };

        // Act & Assert
        Assert.ThrowsAsync<ForbiddenException>(
            () => _sut.UpdateOpStatusAsync(1, req));
    }

    [Test]
    public void UpdateOpStatusAsync_SuspendedAccount_ThrowsForbiddenException()
    {
        // Arrange
        var profile = MakeProfileWithStatus(DriverAccountStatus.Suspended);
        _driverRepoMock.Setup(r => r.GetByUserIdAsync(1)).ReturnsAsync(profile);

        var req = new UpdateOpStatusRequestDto { NewStatus = DriverOpStatus.Available };

        // Act & Assert
        Assert.ThrowsAsync<ForbiddenException>(
            () => _sut.UpdateOpStatusAsync(1, req));
    }

    [Test]
    public void UpdateOpStatusAsync_DeletedAccount_ThrowsForbiddenException()
    {
        // Arrange
        var profile = MakeProfileWithStatus(DriverAccountStatus.Deleted);
        _driverRepoMock.Setup(r => r.GetByUserIdAsync(1)).ReturnsAsync(profile);

        var req = new UpdateOpStatusRequestDto { NewStatus = DriverOpStatus.Available };

        // Act & Assert
        Assert.ThrowsAsync<ForbiddenException>(
            () => _sut.UpdateOpStatusAsync(1, req));
    }

    [Test]
    public void UpdateOpStatusAsync_ProfileNotFound_ThrowsNotFoundException()
    {
        // Arrange
        _driverRepoMock.Setup(r => r.GetByUserIdAsync(99))
                       .ReturnsAsync((DriverProfile?)null);

        var req = new UpdateOpStatusRequestDto { NewStatus = DriverOpStatus.Available };

        // Act & Assert
        Assert.ThrowsAsync<NotFoundException>(
            () => _sut.UpdateOpStatusAsync(99, req));
    }

    // ═══════════════════════════════════════════════════════════
    //  UpdateOpStatusAsync — InTransit self-set guard
    // ═══════════════════════════════════════════════════════════

    [Test]
    public void UpdateOpStatusAsync_SetInTransit_ThrowsConflictException()
    {
        // Arrange — active account, but trying to set InTransit directly
        var profile = MakeActiveProfile();
        _driverRepoMock.Setup(r => r.GetByUserIdAsync(1)).ReturnsAsync(profile);

        var req = new UpdateOpStatusRequestDto { NewStatus = DriverOpStatus.InTransit };

        // Act & Assert
        var ex = Assert.ThrowsAsync<ConflictException>(
            () => _sut.UpdateOpStatusAsync(1, req));

        Assert.That(ex!.Message, Does.Contain("InTransit status is set automatically"));
    }

    // ═══════════════════════════════════════════════════════════
    //  UpdateOpStatusAsync — Available with active shipment guard
    // ═══════════════════════════════════════════════════════════

    [Test]
    public void UpdateOpStatusAsync_SetAvailable_WithActiveShipment_ThrowsConflictException()
    {
        // Arrange
        var profile = MakeActiveProfile();
        _driverRepoMock.Setup(r => r.GetByUserIdAsync(1)).ReturnsAsync(profile);
        _shipmentRepoMock.Setup(r => r.HasActiveShipmentAsync(1))
                         .ReturnsAsync(true);

        var req = new UpdateOpStatusRequestDto { NewStatus = DriverOpStatus.Available };

        // Act & Assert
        var ex = Assert.ThrowsAsync<ConflictException>(
            () => _sut.UpdateOpStatusAsync(1, req));

        Assert.That(ex!.Message, Does.Contain("active shipment in progress"));
    }

    [Test]
    public async Task UpdateOpStatusAsync_SetAvailable_NoActiveShipment_Succeeds()
    {
        // Arrange
        var profile = MakeActiveProfile();
        _driverRepoMock.Setup(r => r.GetByUserIdAsync(1)).ReturnsAsync(profile);
        _shipmentRepoMock.Setup(r => r.HasActiveShipmentAsync(1))
                         .ReturnsAsync(false);
        _driverRepoMock.Setup(r => r.UpdateAsync(profile)).Returns(Task.CompletedTask);
        _driverRepoMock.Setup(r => r.SaveAsync()).Returns(Task.CompletedTask);

        var req = new UpdateOpStatusRequestDto { NewStatus = DriverOpStatus.Available };

        // Act
        var result = await _sut.UpdateOpStatusAsync(1, req);

        // Assert
        Assert.That(result.OpStatus, Is.EqualTo(DriverOpStatus.Available));
        _driverRepoMock.Verify(r => r.UpdateAsync(profile), Times.Once);
        _driverRepoMock.Verify(r => r.SaveAsync(),          Times.Once);
    }

    // ═══════════════════════════════════════════════════════════
    //  UpdateOpStatusAsync — GPS coordinate handling
    // ═══════════════════════════════════════════════════════════

    [Test]
    public async Task UpdateOpStatusAsync_SetAvailableWithCoordinates_SetsGps()
    {
        // Arrange
        var profile = MakeActiveProfile();
        _driverRepoMock.Setup(r => r.GetByUserIdAsync(1)).ReturnsAsync(profile);
        _shipmentRepoMock.Setup(r => r.HasActiveShipmentAsync(1)).ReturnsAsync(false);
        _driverRepoMock.Setup(r => r.UpdateAsync(profile)).Returns(Task.CompletedTask);
        _driverRepoMock.Setup(r => r.SaveAsync()).Returns(Task.CompletedTask);

        var req = new UpdateOpStatusRequestDto
        {
            NewStatus  = DriverOpStatus.Available,
            CurrentLat = 17.385,
            CurrentLng = 78.487,
        };

        // Act
        await _sut.UpdateOpStatusAsync(1, req);

        // Assert — GPS set on the profile
        Assert.Multiple(() =>
        {
            Assert.That(profile.CurrentLat, Is.EqualTo(17.385));
            Assert.That(profile.CurrentLng, Is.EqualTo(78.487));
        });
    }

    [Test]
    public async Task UpdateOpStatusAsync_SetAvailableWithoutCoordinates_GpsUnchanged()
    {
        // Arrange
        var profile = MakeActiveProfile();
        profile.CurrentLat = 17.000;
        profile.CurrentLng = 78.000;

        _driverRepoMock.Setup(r => r.GetByUserIdAsync(1)).ReturnsAsync(profile);
        _shipmentRepoMock.Setup(r => r.HasActiveShipmentAsync(1)).ReturnsAsync(false);
        _driverRepoMock.Setup(r => r.UpdateAsync(profile)).Returns(Task.CompletedTask);
        _driverRepoMock.Setup(r => r.SaveAsync()).Returns(Task.CompletedTask);

        var req = new UpdateOpStatusRequestDto
        {
            NewStatus  = DriverOpStatus.Available,
            CurrentLat = null,   // no coordinates provided
            CurrentLng = null,
        };

        // Act
        await _sut.UpdateOpStatusAsync(1, req);

        // Assert — GPS NOT updated (no coordinates in request)
        Assert.Multiple(() =>
        {
            Assert.That(profile.CurrentLat, Is.EqualTo(17.000));
            Assert.That(profile.CurrentLng, Is.EqualTo(78.000));
        });
    }

    [Test]
    public async Task UpdateOpStatusAsync_SetOffline_WipesGpsCoordinates()
    {
        // Arrange — driver has GPS set from a previous session
        var profile = MakeActiveProfile();
        profile.CurrentLat = 17.385;
        profile.CurrentLng = 78.487;

        _driverRepoMock.Setup(r => r.GetByUserIdAsync(1)).ReturnsAsync(profile);
        _driverRepoMock.Setup(r => r.UpdateAsync(profile)).Returns(Task.CompletedTask);
        _driverRepoMock.Setup(r => r.SaveAsync()).Returns(Task.CompletedTask);

        var req = new UpdateOpStatusRequestDto { NewStatus = DriverOpStatus.Offline };

        // Act
        await _sut.UpdateOpStatusAsync(1, req);

        // Assert — GPS wiped for privacy on clock-out
        Assert.Multiple(() =>
        {
            Assert.That(profile.CurrentLat, Is.Null);
            Assert.That(profile.CurrentLng, Is.Null);
            Assert.That(profile.OpStatus,   Is.EqualTo(DriverOpStatus.Offline));
        });
    }

    [Test]
    public async Task UpdateOpStatusAsync_SetOffline_DoesNotCheckActiveShipment()
    {
        // Arrange — setting Offline should skip the active-shipment check entirely
        var profile = MakeActiveProfile();
        _driverRepoMock.Setup(r => r.GetByUserIdAsync(1)).ReturnsAsync(profile);
        _driverRepoMock.Setup(r => r.UpdateAsync(profile)).Returns(Task.CompletedTask);
        _driverRepoMock.Setup(r => r.SaveAsync()).Returns(Task.CompletedTask);

        var req = new UpdateOpStatusRequestDto { NewStatus = DriverOpStatus.Offline };

        // Act
        await _sut.UpdateOpStatusAsync(1, req);

        // Assert — HasActiveShipmentAsync never called for Offline
        _shipmentRepoMock.Verify(
            r => r.HasActiveShipmentAsync(It.IsAny<int>()),
            Times.Never);
    }

    // ═══════════════════════════════════════════════════════════
    //  UpdateOpStatusAsync — UpdatedAt and persistence
    // ═══════════════════════════════════════════════════════════

    [Test]
    public async Task UpdateOpStatusAsync_SetsUpdatedAtTimestamp()
    {
        // Arrange
        var profile = MakeActiveProfile();
        var before  = DateTime.UtcNow.AddSeconds(-1);

        _driverRepoMock.Setup(r => r.GetByUserIdAsync(1)).ReturnsAsync(profile);
        _driverRepoMock.Setup(r => r.UpdateAsync(profile)).Returns(Task.CompletedTask);
        _driverRepoMock.Setup(r => r.SaveAsync()).Returns(Task.CompletedTask);

        var req = new UpdateOpStatusRequestDto { NewStatus = DriverOpStatus.Offline };

        // Act
        await _sut.UpdateOpStatusAsync(1, req);

        // Assert
        Assert.That(profile.UpdatedAt, Is.GreaterThanOrEqualTo(before));
    }

    [Test]
    public async Task UpdateOpStatusAsync_CallsUpdateAndSaveInOrder()
    {
        // Arrange
        var profile   = MakeActiveProfile();
        var callOrder = new List<string>();

        _driverRepoMock.Setup(r => r.GetByUserIdAsync(1)).ReturnsAsync(profile);
        _driverRepoMock.Setup(r => r.UpdateAsync(profile))
                       .Callback(() => callOrder.Add("update"))
                       .Returns(Task.CompletedTask);
        _driverRepoMock.Setup(r => r.SaveAsync())
                       .Callback(() => callOrder.Add("save"))
                       .Returns(Task.CompletedTask);

        var req = new UpdateOpStatusRequestDto { NewStatus = DriverOpStatus.Offline };

        // Act
        await _sut.UpdateOpStatusAsync(1, req);

        // Assert — update must precede save
        Assert.That(callOrder, Is.EqualTo(new[] { "update", "save" }));
    }

    [Test]
    public async Task UpdateOpStatusAsync_ReturnsMappedDto_WithNewStatus()
    {
        // Arrange
        var profile = MakeActiveProfile();
        _driverRepoMock.Setup(r => r.GetByUserIdAsync(1)).ReturnsAsync(profile);
        _shipmentRepoMock.Setup(r => r.HasActiveShipmentAsync(1)).ReturnsAsync(false);
        _driverRepoMock.Setup(r => r.UpdateAsync(profile)).Returns(Task.CompletedTask);
        _driverRepoMock.Setup(r => r.SaveAsync()).Returns(Task.CompletedTask);

        var req = new UpdateOpStatusRequestDto { NewStatus = DriverOpStatus.Available };

        // Act
        var result = await _sut.UpdateOpStatusAsync(1, req);

        // Assert — returned DTO reflects new status and identity fields
        Assert.Multiple(() =>
        {
            Assert.That(result.OpStatus,      Is.EqualTo(DriverOpStatus.Available));
            Assert.That(result.UserId,        Is.EqualTo(1));
            Assert.That(result.FullName,      Is.EqualTo("Test Driver"));
            Assert.That(result.AccountStatus, Is.EqualTo(DriverAccountStatus.Active));
        });
    }

    // ═══════════════════════════════════════════════════════════
    //  UpdateOpStatusAsync — all non-Active account statuses via
    //  parameterised test to ensure every enum value is covered
    // ═══════════════════════════════════════════════════════════

    [TestCase(DriverAccountStatus.PendingApproval)]
    [TestCase(DriverAccountStatus.Suspended)]
    [TestCase(DriverAccountStatus.Deleted)]
    public void UpdateOpStatusAsync_NonActiveAccountStatus_AlwaysThrowsForbidden(
        DriverAccountStatus accountStatus)
    {
        // Arrange
        var profile = MakeProfileWithStatus(accountStatus);
        _driverRepoMock.Setup(r => r.GetByUserIdAsync(1)).ReturnsAsync(profile);

        var req = new UpdateOpStatusRequestDto { NewStatus = DriverOpStatus.Available };

        // Act & Assert
        Assert.ThrowsAsync<ForbiddenException>(
            () => _sut.UpdateOpStatusAsync(1, req));
    }

    [TestCase(DriverAccountStatus.PendingApproval)]
    [TestCase(DriverAccountStatus.Suspended)]
    [TestCase(DriverAccountStatus.Deleted)]
    public void UpdateOpStatusAsync_NonActiveAccount_NeverCallsRepository_BeyondProfileLookup(
        DriverAccountStatus accountStatus)
    {
        // Arrange
        var profile = MakeProfileWithStatus(accountStatus);
        _driverRepoMock.Setup(r => r.GetByUserIdAsync(1)).ReturnsAsync(profile);

        var req = new UpdateOpStatusRequestDto { NewStatus = DriverOpStatus.Offline };

        // Act
        Assert.ThrowsAsync<ForbiddenException>(() => _sut.UpdateOpStatusAsync(1, req));

        // Assert — no update or save should have been attempted
        _driverRepoMock.Verify(r => r.UpdateAsync(It.IsAny<DriverProfile>()), Times.Never);
        _driverRepoMock.Verify(r => r.SaveAsync(),                            Times.Never);
    }
}