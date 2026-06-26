using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;
using ShipmentTrackingAPI.Data;
using ShipmentTrackingAPI.DTOs.Admin;
using ShipmentTrackingAPI.DTOs.Common;
using ShipmentTrackingAPI.Interfaces;
using ShipmentTrackingAPI.Models;
using ShipmentTrackingAPI.Models.Enums;
using ShipmentTrackingAPI.Models.Exceptions;
using ShipmentTrackingAPI.Repositories.RepoInterfaces;
using ShipmentTrackingAPI.Services;

namespace ShipmentTrackingAPI.Tests.Services;

/// <summary>
/// Full coverage tests for AdminService.
///
/// AdminService uses AppDbContext directly (no repository abstraction for
/// most queries). We use EF Core's in-memory provider so tests are
/// fast, hermetic, and require no real PostgreSQL instance.
///
/// COVERAGE MAP
/// ────────────
/// GetAllDriversAsync
///   ✓ returns all drivers when no status filter
///   ✓ filters by status correctly
///   ✓ clamps page below 1 to 1
///   ✓ clamps size above 50 to 50
///   ✓ clamps size below 1 to 1
///   ✓ pagination skip/take is correct
///   ✓ maps all DTO fields
///   ✓ null PhoneNumber mapped to "N/A"
///
/// GetDriverDetailAsync
///   ✓ found → maps all fields including ApprovedByName
///   ✓ not found → throws NotFoundException
///   ✓ approvedByNavigation null → ApprovedByName is null
///
/// UpdateDriverAccountStatusAsync
///   ✓ approve driver → sets ApprovedBy + ApprovedAt
///   ✓ suspend driver with no active delivery → succeeds, wipes OpStatus
///   ✓ delete driver → wipes OpStatus + deactivates user
///   ✓ suspend driver with active delivery → throws ConflictException
///   ✓ delete driver with active delivery → throws ConflictException
///   ✓ driver not found → throws NotFoundException
///   ✓ all active shipment statuses block suspension (Assigned/PickedUp/InTransit/Arrived)
///
/// GetAllShipmentsAsync
///   ✓ returns all shipments unfiltered
///   ✓ filters by status
///   ✓ filters by driverUserId
///   ✓ pagination correct
///   ✓ maps address lines correctly
///   ✓ null driver name → null in DTO
///
/// OverrideShipmentStatusAsync
///   ✓ sets new status + inserts audit event
///   ✓ delivered → sets DeliveredAt
///   ✓ cancelled → sets CancelledAt
///   ✓ failedDelivery → sets FailedAt
///   ✓ previously InTransit → publishes GPS stopped event
///   ✓ terminal override → sets active driver to Available
///   ✓ shipment not found → throws NotFoundException
///   ✓ reason text trimmed in audit event
///
/// GetDashboardMetricsAsync
///   ✓ counts per status correct
///   ✓ ActiveDeliveries = Assigned + PickedUp + InTransit + Arrived
///   ✓ DeliveriesToday counts only today's deliveries
///   ✓ DriversPendingApproval count
///   ✓ TotalActiveDrivers count
///   ✓ empty database returns all zeros
/// </summary>
[TestFixture]
public class AdminServiceTests
{
    private AppDbContext _ctx = null!;
    private Mock<IShipmentRepository> _shipmentRepo = null!;
    private Mock<IDriverRepository> _driverRepo = null!;
    private Mock<IUserRepository> _userRepo = null!;
    private Mock<IGpsSimulationChannel> _gpsChannel = null!;
    private AdminService _sut = null!;

    // ── Setup / Teardown ─────────────────────────────────────

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _ctx = new AppDbContext(options);
        _shipmentRepo = new Mock<IShipmentRepository>(MockBehavior.Loose);
        _driverRepo = new Mock<IDriverRepository>(MockBehavior.Loose);
        _userRepo = new Mock<IUserRepository>(MockBehavior.Loose);
        _gpsChannel = new Mock<IGpsSimulationChannel>(MockBehavior.Loose);

        _sut = new AdminService(
            _shipmentRepo.Object,
            _driverRepo.Object,
            _userRepo.Object,
            _ctx,
            _gpsChannel.Object);
    }

    [TearDown]
    public void TearDown() => _ctx.Dispose();

    // ── Seed helpers ─────────────────────────────────────────

    private User SeedUser(int id, string name = "Driver User", string email = "driver@test.com")
    {
        var user = new User
        {
            Id = id,
            FullName = name,
            Email = email,
            PasswordHash = "hash",
            Role = UserRole.Driver,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _ctx.Users.Add(user);
        _ctx.SaveChanges();
        return user;
    }

    private DriverProfile SeedDriver(
        int profileId,
        int userId,
        DriverAccountStatus status = DriverAccountStatus.PendingApproval,
        DriverOpStatus? opStatus = null,
        string? phone = "9876543210")
    {
        var user = _ctx.Users.Find(userId) ?? SeedUser(userId);
        var profile = new DriverProfile
        {
            Id = profileId,
            UserId = userId,
            VehicleType = "Bike",
            VehicleNumber = "TS-01-AB-1234",
            LicenseNumber = $"LIC{profileId}",
            AccountStatus = status,
            OpStatus = opStatus,
            PhoneNumber = phone,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _ctx.DriverProfiles.Add(profile);
        _ctx.SaveChanges();
        return profile;
    }

    private Shipment SeedShipment(
        int id,
        int customerId,
        int? driverUserId,
        ShipmentStatus status,
        DateTime? deliveredAt = null)
    {
        var shipment = new Shipment
        {
            Id = id,
            TrackingNumber = $"TRK-{id:D6}",
            CustomerId = customerId,
            DriverId = driverUserId,
            Status = status,
            DeliveredAt = deliveredAt,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _ctx.Shipments.Add(shipment);

        _ctx.ShipmentAddresses.AddRange(
            new ShipmentAddress
            {
                ShipmentId = id,
                AddressType = AddressType.Pickup,
                AddressLine = "123 Pickup St",
            },
            new ShipmentAddress
            {
                ShipmentId = id,
                AddressType = AddressType.Dropoff,
                AddressLine = "456 Dropoff Rd",
            });

        _ctx.SaveChanges();
        return shipment;
    }

    // ═══════════════════════════════════════════════════════════
    //  GetAllDriversAsync
    // ═══════════════════════════════════════════════════════════

    [Test]
    public async Task GetAllDriversAsync_NoFilter_ReturnsAllDrivers()
    {
        SeedDriver(1, userId: 101, status: DriverAccountStatus.Active);
        SeedDriver(2, userId: 102, status: DriverAccountStatus.PendingApproval);

        var result = await _sut.GetAllDriversAsync(null, 1, 10);

        Assert.That(result.TotalCount, Is.EqualTo(2));
        Assert.That(result.Data.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task GetAllDriversAsync_StatusFilter_ReturnsOnlyMatching()
    {
        SeedDriver(1, userId: 101, status: DriverAccountStatus.Active);
        SeedDriver(2, userId: 102, status: DriverAccountStatus.PendingApproval);
        SeedDriver(3, userId: 103, status: DriverAccountStatus.PendingApproval);

        var result = await _sut.GetAllDriversAsync(DriverAccountStatus.PendingApproval, 1, 10);

        Assert.That(result.TotalCount, Is.EqualTo(2));
        Assert.That(result.Data.All(d => d.AccountStatus == DriverAccountStatus.PendingApproval));
    }

    [Test]
    public async Task GetAllDriversAsync_PageBelowOne_ClampsToOne()
    {
        SeedDriver(1, userId: 101);

        var result = await _sut.GetAllDriversAsync(null, page: -5, size: 10);

        Assert.That(result.Page, Is.EqualTo(1));
    }

    [Test]
    public async Task GetAllDriversAsync_SizeAbove50_ClampsTo50()
    {
        SeedDriver(1, userId: 101);

        var result = await _sut.GetAllDriversAsync(null, page: 1, size: 200);

        Assert.That(result.Size, Is.EqualTo(50));
    }

    [Test]
    public async Task GetAllDriversAsync_SizeBelowOne_ClampsToOne()
    {
        SeedDriver(1, userId: 101);

        var result = await _sut.GetAllDriversAsync(null, page: 1, size: 0);

        Assert.That(result.Size, Is.EqualTo(1));
    }

    [Test]
    public async Task GetAllDriversAsync_MapsAllDtoFields()
    {
        var user = SeedUser(101, "John Driver", "john@test.com");
        SeedDriver(1, userId: 101, status: DriverAccountStatus.Active, phone: "9876543210");

        var result = await _sut.GetAllDriversAsync(null, 1, 10);
        var dto = result.Data.First();

        Assert.Multiple(() =>
        {
            Assert.That(dto.Id, Is.EqualTo(1));
            Assert.That(dto.FullName, Is.EqualTo("John Driver"));
            Assert.That(dto.Email, Is.EqualTo("john@test.com"));
            Assert.That(dto.PhoneNumber, Is.EqualTo("9876543210"));
            Assert.That(dto.AccountStatus, Is.EqualTo(DriverAccountStatus.Active));
            Assert.That(dto.VehicleType, Is.EqualTo("Bike"));
        });
    }

    [Test]
    public async Task GetAllDriversAsync_NullPhone_MapsToNA()
    {
        SeedDriver(1, userId: 101, phone: null);

        var result = await _sut.GetAllDriversAsync(null, 1, 10);

        Assert.That(result.Data.First().PhoneNumber, Is.EqualTo("N/A"));
    }

    [Test]
    public async Task GetAllDriversAsync_Pagination_RespectsSkipTake()
    {
        SeedDriver(1, userId: 101);
        SeedDriver(2, userId: 102);
        SeedDriver(3, userId: 103);

        var result = await _sut.GetAllDriversAsync(null, page: 2, size: 2);

        Assert.That(result.Data.Count, Is.EqualTo(1));
        Assert.That(result.TotalCount, Is.EqualTo(3));
    }

    // ═══════════════════════════════════════════════════════════
    //  GetDriverDetailAsync
    // ═══════════════════════════════════════════════════════════

    [Test]
    public async Task GetDriverDetailAsync_Found_MapsAllFields()
    {
        var adminUser = SeedUser(200, "Admin User", "admin@test.com");
        SeedUser(101, "John Driver", "john@test.com");
        var profile = SeedDriver(1, userId: 101, status: DriverAccountStatus.Active);

        // Set approval fields manually
        profile.ApprovedBy = 200;
        profile.ApprovedAt = DateTime.UtcNow;
        profile.ApprovedByNavigation = adminUser;
        _ctx.SaveChanges();

        var result = await _sut.GetDriverDetailAsync(1);

        Assert.Multiple(() =>
        {
            Assert.That(result.Id, Is.EqualTo(1));
            Assert.That(result.FullName, Is.EqualTo("John Driver"));
            Assert.That(result.AccountStatus, Is.EqualTo(DriverAccountStatus.Active));
            Assert.That(result.LicenseNumber, Is.EqualTo("LIC1"));
            Assert.That(result.ApprovedByName, Is.EqualTo("Admin User"));
        });
    }

    [Test]
    public void GetDriverDetailAsync_NotFound_ThrowsNotFoundException()
    {
        Assert.ThrowsAsync<NotFoundException>(
            () => _sut.GetDriverDetailAsync(999));
    }

    [Test]
    public async Task GetDriverDetailAsync_NoApproval_ApprovedByNameIsNull()
    {
        SeedUser(101);
        SeedDriver(1, userId: 101, status: DriverAccountStatus.PendingApproval);

        var result = await _sut.GetDriverDetailAsync(1);

        Assert.That(result.ApprovedByName, Is.Null);
    }

    // ═══════════════════════════════════════════════════════════
    //  UpdateDriverAccountStatusAsync
    // ═══════════════════════════════════════════════════════════

    [Test]
    public async Task UpdateDriverAccountStatusAsync_Approve_SetsApprovalFields()
    {
        SeedUser(101);
        SeedDriver(1, userId: 101, status: DriverAccountStatus.PendingApproval);
        var before = DateTime.UtcNow.AddSeconds(-1);

        await _sut.UpdateDriverAccountStatusAsync(
            adminId: 999,
            driverProfileId: 1,
            newStatus: DriverAccountStatus.Active);

        var updated = _ctx.DriverProfiles.Find(1)!;
        Assert.Multiple(() =>
        {
            Assert.That(updated.AccountStatus, Is.EqualTo(DriverAccountStatus.Active));
            Assert.That(updated.ApprovedBy, Is.EqualTo(999));
            Assert.That(updated.ApprovedAt, Is.GreaterThanOrEqualTo(before));
        });
    }

    [Test]
    public async Task UpdateDriverAccountStatusAsync_Suspend_NoActiveDelivery_Succeeds()
    {
        SeedUser(101);
        var profile = SeedDriver(1, userId: 101,
            status: DriverAccountStatus.Active,
            opStatus: DriverOpStatus.Available);

        await _sut.UpdateDriverAccountStatusAsync(
            adminId: 999,
            driverProfileId: 1,
            newStatus: DriverAccountStatus.Suspended);

        var updated = _ctx.DriverProfiles.Find(1)!;
        Assert.Multiple(() =>
        {
            Assert.That(updated.AccountStatus, Is.EqualTo(DriverAccountStatus.Suspended));
            Assert.That(updated.OpStatus, Is.Null);
        });
    }

    [Test]
    public async Task UpdateDriverAccountStatusAsync_Delete_DeactivatesUser()
    {
        var user = SeedUser(101);
        SeedDriver(1, userId: 101, status: DriverAccountStatus.Active);

        await _sut.UpdateDriverAccountStatusAsync(
            adminId: 999,
            driverProfileId: 1,
            newStatus: DriverAccountStatus.Deleted);

        var updatedUser = _ctx.Users.Find(101)!;
        var updatedProfile = _ctx.DriverProfiles.Find(1)!;

        Assert.Multiple(() =>
        {
            Assert.That(updatedUser.IsActive, Is.False);
            Assert.That(updatedProfile.AccountStatus, Is.EqualTo(DriverAccountStatus.Deleted));
            Assert.That(updatedProfile.OpStatus, Is.Null);
        });
    }

    [TestCase(ShipmentStatus.Assigned)]
    [TestCase(ShipmentStatus.PickedUp)]
    [TestCase(ShipmentStatus.InTransit)]
    [TestCase(ShipmentStatus.Arrived)]
    public void UpdateDriverAccountStatusAsync_Suspend_WithActiveDelivery_ThrowsConflict(
        ShipmentStatus activeStatus)
    {
        var customerUser = SeedUser(200, "Customer", "cust@test.com");
        SeedUser(101);
        SeedDriver(1, userId: 101, status: DriverAccountStatus.Active);
        SeedShipment(1, customerId: 200, driverUserId: 101, status: activeStatus);

        Assert.ThrowsAsync<ConflictException>(() =>
            _sut.UpdateDriverAccountStatusAsync(999, 1, DriverAccountStatus.Suspended));
    }

    [TestCase(ShipmentStatus.Assigned)]
    [TestCase(ShipmentStatus.PickedUp)]
    [TestCase(ShipmentStatus.InTransit)]
    [TestCase(ShipmentStatus.Arrived)]
    public void UpdateDriverAccountStatusAsync_Delete_WithActiveDelivery_ThrowsConflict(
        ShipmentStatus activeStatus)
    {
        var customerUser = SeedUser(200, "Customer", "cust@test.com");
        SeedUser(101);
        SeedDriver(1, userId: 101, status: DriverAccountStatus.Active);
        SeedShipment(1, customerId: 200, driverUserId: 101, status: activeStatus);

        Assert.ThrowsAsync<ConflictException>(() =>
            _sut.UpdateDriverAccountStatusAsync(999, 1, DriverAccountStatus.Deleted));
    }

    [Test]
    public void UpdateDriverAccountStatusAsync_DriverNotFound_ThrowsNotFoundException()
    {
        Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.UpdateDriverAccountStatusAsync(999, 9999, DriverAccountStatus.Active));
    }

    [Test]
    public async Task UpdateDriverAccountStatusAsync_TerminalShipments_DoNotBlockSuspend()
    {
        var customerUser = SeedUser(200, "Customer", "cust@test.com");
        SeedUser(101);
        SeedDriver(1, userId: 101, status: DriverAccountStatus.Active);
        // Delivered shipment — terminal, should NOT block suspension
        SeedShipment(1, customerId: 200, driverUserId: 101, status: ShipmentStatus.Delivered);

        // Should NOT throw
        await _sut.UpdateDriverAccountStatusAsync(999, 1, DriverAccountStatus.Suspended);

        var updated = _ctx.DriverProfiles.Find(1)!;
        Assert.That(updated.AccountStatus, Is.EqualTo(DriverAccountStatus.Suspended));
    }

    // ═══════════════════════════════════════════════════════════
    //  GetAllShipmentsAsync
    // ═══════════════════════════════════════════════════════════

    [Test]
    public async Task GetAllShipmentsAsync_NoFilter_ReturnsAll()
    {
        SeedUser(1, "Customer A", "a@test.com");
        SeedShipment(1, customerId: 1, driverUserId: null, status: ShipmentStatus.Pending);
        SeedShipment(2, customerId: 1, driverUserId: null, status: ShipmentStatus.Delivered);

        var result = await _sut.GetAllShipmentsAsync(null, null, 1, 10);

        Assert.That(result.TotalCount, Is.EqualTo(2));
    }

    [Test]
    public async Task GetAllShipmentsAsync_FilterByStatus_ReturnsOnlyMatching()
    {
        SeedUser(1, "Customer A", "a@test.com");
        SeedShipment(1, customerId: 1, driverUserId: null, status: ShipmentStatus.Pending);
        SeedShipment(2, customerId: 1, driverUserId: null, status: ShipmentStatus.Delivered);

        var result = await _sut.GetAllShipmentsAsync(ShipmentStatus.Pending, null, 1, 10);

        Assert.That(result.TotalCount, Is.EqualTo(1));
        Assert.That(result.Data.First().Status, Is.EqualTo(ShipmentStatus.Pending));
    }

    [Test]
    public async Task GetAllShipmentsAsync_FilterByDriverUserId_ReturnsOnlyMatching()
    {
        SeedUser(1, "Customer", "c@test.com");
        SeedUser(101, "Driver1", "d1@test.com");
        SeedUser(102, "Driver2", "d2@test.com");

        SeedShipment(1, customerId: 1, driverUserId: 101, status: ShipmentStatus.Assigned);
        SeedShipment(2, customerId: 1, driverUserId: 102, status: ShipmentStatus.Assigned);
        SeedShipment(3, customerId: 1, driverUserId: 101, status: ShipmentStatus.Delivered);

        var result = await _sut.GetAllShipmentsAsync(null, driverUserId: 101, 1, 10);

        Assert.That(result.TotalCount, Is.EqualTo(2));
        Assert.That(result.Data.All(s => s.DriverName != null));
    }

    [Test]
    public async Task GetAllShipmentsAsync_MapsAddressLines()
    {
        SeedUser(1, "Customer", "c@test.com");
        SeedShipment(1, customerId: 1, driverUserId: null, status: ShipmentStatus.Pending);

        var result = await _sut.GetAllShipmentsAsync(null, null, 1, 10);
        var dto = result.Data.First();

        Assert.Multiple(() =>
        {
            Assert.That(dto.PickupArea, Is.EqualTo("123 Pickup St"));
            Assert.That(dto.DropoffArea, Is.EqualTo("456 Dropoff Rd"));
        });
    }

    [Test]
    public async Task GetAllShipmentsAsync_NullDriver_DriverNameIsNull()
    {
        SeedUser(1, "Customer", "c@test.com");
        SeedShipment(1, customerId: 1, driverUserId: null, status: ShipmentStatus.Pending);

        var result = await _sut.GetAllShipmentsAsync(null, null, 1, 10);

        Assert.That(result.Data.First().DriverName, Is.Null);
    }

    [Test]
    public async Task GetAllShipmentsAsync_Pagination_RespectsSkipTake()
    {
        SeedUser(1, "Customer", "c@test.com");
        SeedShipment(1, customerId: 1, driverUserId: null, status: ShipmentStatus.Pending);
        SeedShipment(2, customerId: 1, driverUserId: null, status: ShipmentStatus.Pending);
        SeedShipment(3, customerId: 1, driverUserId: null, status: ShipmentStatus.Pending);

        var result = await _sut.GetAllShipmentsAsync(null, null, page: 2, size: 2);

        Assert.That(result.Data.Count, Is.EqualTo(1));
        Assert.That(result.TotalCount, Is.EqualTo(3));
    }

    // ═══════════════════════════════════════════════════════════
    //  OverrideShipmentStatusAsync
    // ═══════════════════════════════════════════════════════════

    [Test]
    public async Task OverrideShipmentStatusAsync_SetsStatusAndInsertsEvent()
    {
        SeedUser(1, "Customer", "c@test.com");
        SeedShipment(1, customerId: 1, driverUserId: null, status: ShipmentStatus.Pending);

        await _sut.OverrideShipmentStatusAsync(
            adminId: 999,
            shipmentId: 1,
            newStatus: ShipmentStatus.Cancelled,
            reason: "Test cancellation");

        var shipment = _ctx.Shipments.Find(1)!;
        var evt = _ctx.ShipmentEvents.First(e => e.ShipmentId == 1);

        Assert.Multiple(() =>
        {
            Assert.That(shipment.Status, Is.EqualTo(ShipmentStatus.Cancelled));
            Assert.That(evt.ActorId, Is.EqualTo(999));
            Assert.That(evt.Description, Does.Contain("Test cancellation"));
            Assert.That(evt.Description, Does.Contain("Pending"));
            Assert.That(evt.Description, Does.Contain("Cancelled"));
        });
    }

    [Test]
    public async Task OverrideShipmentStatusAsync_Delivered_SetsDeliveredAt()
    {
        SeedUser(1, "Customer", "c@test.com");
        SeedShipment(1, customerId: 1, driverUserId: null, status: ShipmentStatus.Arrived);
        var before = DateTime.UtcNow.AddSeconds(-1);

        await _sut.OverrideShipmentStatusAsync(999, 1, ShipmentStatus.Delivered, "Force deliver");

        var shipment = _ctx.Shipments.Find(1)!;
        Assert.That(shipment.DeliveredAt, Is.GreaterThanOrEqualTo(before));
    }

    [Test]
    public async Task OverrideShipmentStatusAsync_AlreadyDelivered_DoesNotOverwriteDeliveredAt()
    {
        SeedUser(1, "Customer", "c@test.com");
        var originalDeliveredAt = DateTime.UtcNow.AddHours(-2);
        SeedShipment(1, customerId: 1, driverUserId: null,
            status: ShipmentStatus.Delivered,
            deliveredAt: originalDeliveredAt);

        await _sut.OverrideShipmentStatusAsync(999, 1, ShipmentStatus.Delivered, "Re-override");

        var shipment = _ctx.Shipments.Find(1)!;
        // DeliveredAt already set — should not overwrite
        Assert.That(shipment.DeliveredAt!.Value,
            Is.EqualTo(originalDeliveredAt).Within(TimeSpan.FromSeconds(1)));
    }

    [Test]
    public async Task OverrideShipmentStatusAsync_Cancelled_SetsCancelledAt()
    {
        SeedUser(1, "Customer", "c@test.com");
        SeedShipment(1, customerId: 1, driverUserId: null, status: ShipmentStatus.Pending);
        var before = DateTime.UtcNow.AddSeconds(-1);

        await _sut.OverrideShipmentStatusAsync(999, 1, ShipmentStatus.Cancelled, "reason");

        var shipment = _ctx.Shipments.Find(1)!;
        Assert.That(shipment.CancelledAt, Is.GreaterThanOrEqualTo(before));
    }

    [Test]
    public async Task OverrideShipmentStatusAsync_FailedDelivery_SetsFailedAt()
    {
        SeedUser(1, "Customer", "c@test.com");
        SeedShipment(1, customerId: 1, driverUserId: null, status: ShipmentStatus.Arrived);
        var before = DateTime.UtcNow.AddSeconds(-1);

        await _sut.OverrideShipmentStatusAsync(999, 1, ShipmentStatus.FailedDelivery, "reason");

        var shipment = _ctx.Shipments.Find(1)!;
        Assert.That(shipment.FailedAt, Is.GreaterThanOrEqualTo(before));
    }

    [Test]
    public async Task OverrideShipmentStatusAsync_FromInTransit_PublishesGpsStoppedEvent()
    {
        SeedUser(1, "Customer", "c@test.com");
        SeedShipment(1, customerId: 1, driverUserId: null, status: ShipmentStatus.InTransit);

        await _sut.OverrideShipmentStatusAsync(999, 1, ShipmentStatus.Cancelled, "reason");

        _gpsChannel.Verify(g => g.Publish(It.Is<GpsSimulationEvent>(
            e => e.EventType == GpsEventType.Stopped &&
                 e.ShipmentId == 1 &&
                 e.TrackingNumber == "TRK-000001")),
            Times.Once);
    }

    [Test]
    public async Task OverrideShipmentStatusAsync_NotFromInTransit_DoesNotPublishGpsEvent()
    {
        SeedUser(1, "Customer", "c@test.com");
        SeedShipment(1, customerId: 1, driverUserId: null, status: ShipmentStatus.Pending);

        await _sut.OverrideShipmentStatusAsync(999, 1, ShipmentStatus.Cancelled, "reason");

        _gpsChannel.Verify(g => g.Publish(It.IsAny<GpsSimulationEvent>()), Times.Never);
    }

    [TestCase(ShipmentStatus.Delivered)]
    [TestCase(ShipmentStatus.Cancelled)]
    [TestCase(ShipmentStatus.FailedDelivery)]
    public async Task OverrideShipmentStatusAsync_TerminalOverride_SetsActiveDriverAvailable(ShipmentStatus terminalStatus)
    {
        // Arrange
        SeedUser(1, "Customer", "c@test.com");
        SeedUser(101);
        SeedDriver(1, userId: 101, status: DriverAccountStatus.Active,
            opStatus: DriverOpStatus.InTransit);
        SeedShipment(1, customerId: 1, driverUserId: 101, status: ShipmentStatus.Arrived);

        var activeProfile = _ctx.DriverProfiles.Find(1)!;
        _driverRepo.Setup(r => r.GetByUserIdAsync(101)).ReturnsAsync(activeProfile);
        _driverRepo.Setup(r => r.UpdateAsync(It.IsAny<DriverProfile>()))
                   .Returns(Task.CompletedTask);

        // Act - Pass the terminalStatus variable from the TestCase here!
        await _sut.OverrideShipmentStatusAsync(999, 1, terminalStatus, "force terminal override");

        // Assert
        _driverRepo.Verify(r => r.UpdateAsync(It.Is<DriverProfile>(
            p => p.OpStatus == DriverOpStatus.Available)),
            Times.Once);
    }
    [Test]
    public async Task OverrideShipmentStatusAsync_AlreadyCancelled_DoesNotOverwriteCancelledAt()
    {
        // 🎯 TARGETS: newStatus == Cancelled && !shipment.CancelledAt.HasValue (FALSE branch)
        SeedUser(1, "Customer", "c@test.com");
        var originalCancelledAt = DateTime.UtcNow.AddHours(-2);

        var shipment = SeedShipment(1, customerId: 1, driverUserId: null, status: ShipmentStatus.Cancelled);
        shipment.CancelledAt = originalCancelledAt;
        _ctx.SaveChanges();

        // Act: Admin forcefully overrides to Cancelled again
        await _sut.OverrideShipmentStatusAsync(999, 1, ShipmentStatus.Cancelled, "Re-cancel");

        // Assert: The original timestamp was preserved
        var updatedShipment = _ctx.Shipments.Find(1)!;
        Assert.That(updatedShipment.CancelledAt!.Value,
            Is.EqualTo(originalCancelledAt).Within(TimeSpan.FromSeconds(1)));
    }

    [Test]
    public async Task OverrideShipmentStatusAsync_AlreadyFailed_DoesNotOverwriteFailedAt()
    {
        // 🎯 TARGETS: newStatus == FailedDelivery && !shipment.FailedAt.HasValue (FALSE branch)
        SeedUser(1, "Customer", "c@test.com");
        var originalFailedAt = DateTime.UtcNow.AddHours(-2);

        var shipment = SeedShipment(1, customerId: 1, driverUserId: null, status: ShipmentStatus.FailedDelivery);
        shipment.FailedAt = originalFailedAt;
        _ctx.SaveChanges();

        // Act
        await _sut.OverrideShipmentStatusAsync(999, 1, ShipmentStatus.FailedDelivery, "Re-fail");

        // Assert
        var updatedShipment = _ctx.Shipments.Find(1)!;
        Assert.That(updatedShipment.FailedAt!.Value,
            Is.EqualTo(originalFailedAt).Within(TimeSpan.FromSeconds(1)));
    }

    [Test]
    public async Task OverrideShipmentStatusAsync_InTransitToInTransit_DoesNotStopGps()
    {
        // 🎯 TARGETS: previousStatus == InTransit && newStatus != InTransit (FALSE branch)
        SeedUser(1, "Customer", "c@test.com");
        SeedShipment(1, customerId: 1, driverUserId: null, status: ShipmentStatus.InTransit);

        // Act: Admin overrides from InTransit TO InTransit (same status)
        await _sut.OverrideShipmentStatusAsync(999, 1, ShipmentStatus.InTransit, "refreshing status");

        // Assert: The GPS stop event should NOT be published
        _gpsChannel.Verify(g => g.Publish(It.IsAny<GpsSimulationEvent>()), Times.Never);
    }
    [Test]
    public async Task OverrideShipmentStatusAsync_NonTerminalStatus_DoesNotEnterTerminalBlock()
    {
        SeedUser(1);
        SeedShipment(1, 1, null, ShipmentStatus.Pending);

        await _sut.OverrideShipmentStatusAsync(
            999,
            1,
            ShipmentStatus.Assigned,
            "move forward");

        // Just verify no driver repo interaction happened
        _driverRepo.Verify(
            x => x.GetByUserIdAsync(It.IsAny<int>()),
            Times.Never);
    }
    [Test]
    public async Task OverrideShipmentStatusAsync_DriverExistsButNotActive_DoesNotUpdate()
    {
        SeedUser(1);
        SeedUser(101);

        var driver = SeedDriver(
            1,
            101,
            DriverAccountStatus.Suspended);

        SeedShipment(1, 1, 101, ShipmentStatus.Arrived);

        _driverRepo.Setup(x => x.GetByUserIdAsync(101))
                   .ReturnsAsync(driver);

        await _sut.OverrideShipmentStatusAsync(
            999,
            1,
            ShipmentStatus.Delivered,
            "done");

        _driverRepo.Verify(
            x => x.UpdateAsync(It.IsAny<DriverProfile>()),
            Times.Never);
    }
    [Test]
    public async Task OverrideShipmentStatusAsync_DriverProfileNull_DoesNotUpdate()
    {
        SeedUser(1);
        SeedUser(101);

        SeedShipment(1, 1, 101, ShipmentStatus.Arrived);

        _driverRepo.Setup(x => x.GetByUserIdAsync(101))
                   .ReturnsAsync((DriverProfile?)null);

        await _sut.OverrideShipmentStatusAsync(
            999,
            1,
            ShipmentStatus.Delivered,
            "done");

        _driverRepo.Verify(
            x => x.UpdateAsync(It.IsAny<DriverProfile>()),
            Times.Never);
    }
    [Test]
    public async Task OverrideShipmentStatusAsync_ActiveDriver_NonTerminalStatus_DoesNotFetchDriver()
    {
        SeedUser(1);
        SeedUser(101);

        var driver = SeedDriver(
            1,
            101,
            DriverAccountStatus.Active);

        SeedShipment(
            1,
            1,
            101,
            ShipmentStatus.Pending);

        await _sut.OverrideShipmentStatusAsync(
            999,
            1,
            ShipmentStatus.Assigned,
            "moving");

        _driverRepo.Verify(
            x => x.GetByUserIdAsync(It.IsAny<int>()),
            Times.Never);
    }
    [Test]
    public async Task OverrideShipmentStatusAsync_TerminalStatus_WithNoDriver_DoesNotCallDriverRepo()
    {
        SeedUser(1, "Customer", "c@test.com");

        SeedShipment(
            1,
            customerId: 1,
            driverUserId: null,
            status: ShipmentStatus.Arrived);

        await _sut.OverrideShipmentStatusAsync(
            999,
            1,
            ShipmentStatus.Delivered,
            "completed");

        _driverRepo.Verify(
            x => x.GetByUserIdAsync(It.IsAny<int>()),
            Times.Never);
    }
    [Test]
    public void OverrideShipmentStatusAsync_ShipmentNotFound_ThrowsNotFoundException()
    {
        Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.OverrideShipmentStatusAsync(999, 9999, ShipmentStatus.Cancelled, "reason"));
    }

    [Test]
    public async Task OverrideShipmentStatusAsync_ReasonTrimmedInEventDescription()
    {
        SeedUser(1, "Customer", "c@test.com");
        SeedShipment(1, customerId: 1, driverUserId: null, status: ShipmentStatus.Pending);

        await _sut.OverrideShipmentStatusAsync(999, 1, ShipmentStatus.Cancelled, "  spaced reason  ");

        var evt = _ctx.ShipmentEvents.First(e => e.ShipmentId == 1);
        Assert.That(evt.Description, Does.Contain("spaced reason"));
        Assert.That(evt.Description, Does.Not.Contain("  spaced reason  "));
    }

    // ═══════════════════════════════════════════════════════════
    //  GetDashboardMetricsAsync
    // ═══════════════════════════════════════════════════════════

    [Test]
    public async Task GetDashboardMetricsAsync_EmptyDatabase_ReturnsAllZeros()
    {
        var result = await _sut.GetDashboardMetricsAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.TotalPendingShipments, Is.EqualTo(0));
            Assert.That(result.ActiveDeliveries, Is.EqualTo(0));
            Assert.That(result.DeliveriesToday, Is.EqualTo(0));
            Assert.That(result.DriversPendingApproval, Is.EqualTo(0));
            Assert.That(result.TotalActiveDrivers, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task GetDashboardMetricsAsync_CountsShipmentsByStatus()
    {
        SeedUser(1, "Customer", "c@test.com");
        SeedShipment(1, 1, null, ShipmentStatus.Pending);
        SeedShipment(2, 1, null, ShipmentStatus.Pending);
        SeedShipment(3, 1, null, ShipmentStatus.Assigned);
        SeedShipment(4, 1, null, ShipmentStatus.InTransit);

        var result = await _sut.GetDashboardMetricsAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.TotalPendingShipments, Is.EqualTo(2));
            // ActiveDeliveries = Assigned(1) + PickedUp(0) + InTransit(1) + Arrived(0)
            Assert.That(result.ActiveDeliveries, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task GetDashboardMetricsAsync_ActiveDeliveries_SumsAllActiveStatuses()
    {
        SeedUser(1, "Customer", "c@test.com");
        SeedShipment(1, 1, null, ShipmentStatus.Assigned);
        SeedShipment(2, 1, null, ShipmentStatus.PickedUp);
        SeedShipment(3, 1, null, ShipmentStatus.InTransit);
        SeedShipment(4, 1, null, ShipmentStatus.Arrived);

        var result = await _sut.GetDashboardMetricsAsync();

        Assert.That(result.ActiveDeliveries, Is.EqualTo(4));
    }

    [Test]
    public async Task GetDashboardMetricsAsync_DeliveriesToday_CountsOnlyToday()
    {
        SeedUser(1, "Customer", "c@test.com");

        // Today's delivery
        var todayShipment = SeedShipment(1, 1, null, ShipmentStatus.Delivered,
            deliveredAt: DateTime.UtcNow);

        // Yesterday's delivery — must NOT be counted
        var yesterdayShipment = SeedShipment(2, 1, null, ShipmentStatus.Delivered,
            deliveredAt: DateTime.UtcNow.AddDays(-1));

        var result = await _sut.GetDashboardMetricsAsync();

        Assert.That(result.DeliveriesToday, Is.EqualTo(1));
    }

    [Test]
    public async Task GetDashboardMetricsAsync_CountsDriversByAccountStatus()
    {
        SeedDriver(1, userId: 101, status: DriverAccountStatus.PendingApproval);
        SeedDriver(2, userId: 102, status: DriverAccountStatus.PendingApproval);
        SeedDriver(3, userId: 103, status: DriverAccountStatus.Active);

        var result = await _sut.GetDashboardMetricsAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.DriversPendingApproval, Is.EqualTo(2));
            Assert.That(result.TotalActiveDrivers, Is.EqualTo(1));
        });
    }
}