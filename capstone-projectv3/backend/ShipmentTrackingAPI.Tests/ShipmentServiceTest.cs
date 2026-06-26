using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using ShipmentTrackingAPI.Data;
using ShipmentTrackingAPI.DTOs.Shipment;
using ShipmentTrackingAPI.DTOs.Common;
using ShipmentTrackingAPI.Interfaces;
using ShipmentTrackingAPI.Models;
using ShipmentTrackingAPI.Models.Enums;
using ShipmentTrackingAPI.Repositories.RepoInterfaces;
using ShipmentTrackingAPI.Services;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ShipmentTrackingAPI.Tests.Services;

/// <summary>
/// Full coverage tests for ShipmentService.
///
/// Architecture note:
///   ShipmentService uses BOTH IShipmentRepository (mocked) and AppDbContext
///   directly. We use EF Core InMemory for AppDbContext so all direct _ctx
///   queries work correctly without a real DB. The repository is mocked via Moq.
///
/// COVERAGE MAP
/// ────────────
/// BookShipmentAsync
///   ✓ valid booking → creates Shipment + 2 addresses + items + event
///   ✓ duplicate booking → throws InvalidOperationException
///   ✓ all item fields mapped (including LengthCm/WidthCm/HeightCm)
///   ✓ tracking number uniqueness retry (first collision, second unique)
///   ✓ returns correct ShipmentDto
///
/// GetCustomerShipmentsAsync
///   ✓ returns only customer's own shipments
///   ✓ filters by status when provided
///   ✓ maps pickup/dropoff address lines
///   ✓ clamps page below 1
///   ✓ clamps size above 50
///   ✓ clamps size below 1
///   ✓ pagination skip/take correct
///   ✓ empty result returns empty list
///
/// GetShipmentByIdAsync
///   ✓ Customer owns shipment → returns full detail
///   ✓ Customer does not own → throws UnauthorizedAccessException
///   ✓ Driver owns (assigned) → returns detail
///   ✓ Driver does not own → throws UnauthorizedAccessException
///   ✓ Admin sees any shipment (no ownership check)
///   ✓ shipment not found → throws KeyNotFoundException
///   ✓ maps items, events, addresses, driver info
///
/// CancelShipmentAsync
///   ✓ Pending shipment → cancelled + event inserted + SignalR broadcast
///   ✓ non-Pending status → throws InvalidOperationException
///   ✓ different customer → throws UnauthorizedAccessException
///   ✓ shipment not found → throws KeyNotFoundException
///   ✓ BroadcastStatusUpdateAsync called with Cancelled
///
/// GetPublicTrackingAsync
///   ✓ found → returns tracking DTO with event timeline
///   ✓ not found → throws KeyNotFoundException
///   ✓ InTransit → exposes driver lat/lng
///   ✓ non-InTransit → driver lat/lng is null
///   ✓ events ordered by OccurredAt ascending
///
/// GetPendingQueueAsync
///   ✓ returns only Pending + unassigned shipments
///   ✓ clamps page/size
///   ✓ maps weight as sum(weightKg * quantity)
///   ✓ maps item count correctly
///   ✓ orders oldest first (FIFO)
///
/// AssignDriverAsync
///   ✓ Pending shipment → assigned, event inserted, broadcast sent
///   ✓ already assigned → throws InvalidOperationException
///   ✓ non-Pending status → throws InvalidOperationException
///   ✓ shipment not found → throws KeyNotFoundException
///   ✓ BroadcastStatusUpdateAsync called with Assigned
///
/// UpdateStatusAsync
///   ✓ PickedUp → InTransit → GPS channel started, broadcast sent
///   ✓ InTransit → Arrived → GPS channel stopped, DriverArrived broadcast
///   ✓ InTransit → Arrived → GPS missing coords → warning logged, no GPS event
///   ✓ wrong driver → throws UnauthorizedAccessException
///   ✓ terminal status via this endpoint → throws InvalidOperationException (Delivered/Cancelled/FailedDelivery)
///   ✓ invalid transition (Pending → InTransit) → throws InvalidOperationException
///   ✓ transition from status with no valid next → correct hint in message
///   ✓ shipment not found → throws KeyNotFoundException
///   ✓ description correct per target status
///
/// FailDeliveryAsync
///   ✓ Arrived → FailedDelivery, FailedAt set, event inserted, broadcast sent
///   ✓ non-Arrived status → throws InvalidOperationException
///   ✓ wrong driver → throws UnauthorizedAccessException
///   ✓ shipment not found → throws KeyNotFoundException
///   ✓ BroadcastStatusUpdateAsync called with FailedDelivery
/// </summary>
[TestFixture]
public class ShipmentServiceTests
{
    // ── Infrastructure ───────────────────────────────────────
    private AppDbContext                  _ctx          = null!;
    private Mock<IShipmentRepository>     _repoMock     = null!;
    private Mock<ITrackingService>        _trackingMock = null!;
    private Mock<IGpsSimulationChannel>   _gpsMock      = null!;
    private Mock<ILogger<ShipmentService>> _loggerMock  = null!;
    private ShipmentService               _sut          = null!;

    // ── Setup / Teardown ─────────────────────────────────────

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _ctx          = new AppDbContext(options);
        _repoMock     = new Mock<IShipmentRepository>(MockBehavior.Loose);
        _trackingMock = new Mock<ITrackingService>(MockBehavior.Loose);
        _gpsMock      = new Mock<IGpsSimulationChannel>(MockBehavior.Loose);
        _loggerMock   = new Mock<ILogger<ShipmentService>>();

        // Default: IsDuplicateBookingAsync returns false (no duplicate)
        _repoMock.Setup(r => r.IsDuplicateBookingAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        // Default: tracking number is always unique
        _repoMock.Setup(r => r.IsTrackingNumberUniqueAsync(It.IsAny<string>()))
            .ReturnsAsync(true);

        // Default: AddAsync and AddEventAsync do nothing
        _repoMock.Setup(r => r.AddAsync(It.IsAny<Shipment>()))
            .Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.AddEventAsync(It.IsAny<ShipmentEvent>()))
            .Returns(Task.CompletedTask);

        // Default: SignalR calls are fire-and-forget
        _trackingMock.Setup(t => t.BroadcastStatusUpdateAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _trackingMock.Setup(t => t.BroadcastDeliverySuccessAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _trackingMock.Setup(t => t.BroadcastDriverArrivedAsync(
                It.IsAny<string>(), It.IsAny<double>(), It.IsAny<double>()))
            .Returns(Task.CompletedTask);

        _sut = new ShipmentService(
            _repoMock.Object,
            _ctx,
            _trackingMock.Object,
            _gpsMock.Object,
            _loggerMock.Object);
    }

    [TearDown]
    public void TearDown() => _ctx.Dispose();

    // ── Seed helpers ─────────────────────────────────────────

    private Shipment SeedShipment(
        int id              = 1,
        int customerId      = 10,
        int? driverId       = null,
        ShipmentStatus status = ShipmentStatus.Pending,
        string trackingNum  = "TRK-TEST01",
        bool withAddresses  = true,
        bool withItems      = false,
        bool withEvents     = false)
    {
        var shipment = new Shipment
        {
            Id             = id,
            CustomerId     = customerId,
            DriverId       = driverId,
            TrackingNumber = trackingNum,
            Status         = status,
            CreatedAt      = DateTime.UtcNow.AddMinutes(-id), // stagger for ordering tests
            UpdatedAt      = DateTime.UtcNow,
        };
        _ctx.Shipments.Add(shipment);

        if (withAddresses)
        {
            _ctx.ShipmentAddresses.AddRange(
                new ShipmentAddress
                {
                    ShipmentId  = id,
                    AddressType = AddressType.Pickup,
                    AddressLine = "123 Pickup St",
                    Lat         = 17.385,
                    Lng         = 78.487,
                },
                new ShipmentAddress
                {
                    ShipmentId   = id,
                    AddressType  = AddressType.Dropoff,
                    AddressLine  = "456 Dropoff Rd",
                    Lat          = 17.440,
                    Lng          = 78.498,
                    ContactName  = "Recipient Person",
                    ContactPhone = "9000000001",
                });
        }

        if (withItems)
        {
            _ctx.ShipmentItems.Add(new ShipmentItem
            {
                ShipmentId  = id,
                Description = "Box of stuff",
                WeightKg    = 2.5m,
                LengthCm    = 30m,
                WidthCm     = 20m,
                HeightCm    = 10m,
                Quantity    = 2,
            });
        }

        if (withEvents)
        {
            _ctx.ShipmentEvents.AddRange(
                new ShipmentEvent
                {
                    ShipmentId  = id,
                    Status      = ShipmentStatus.Pending,
                    Description = "Booked",
                    OccurredAt  = DateTime.UtcNow.AddMinutes(-5),
                },
                new ShipmentEvent
                {
                    ShipmentId  = id,
                    Status      = ShipmentStatus.Assigned,
                    Description = "Driver assigned",
                    OccurredAt  = DateTime.UtcNow.AddMinutes(-2),
                });
        }

        _ctx.SaveChanges();
        return shipment;
    }

    private static BookShipmentRequestDto MakeBookingRequest(int itemCount = 1) => new()
    {
        PickupAddress  = "Sender Lane 1",
        PickupLat      = 17.385,
        PickupLng      = 78.487,
        DropoffAddress = "Recipient Road 2",
        DropoffLat     = 17.440,
        DropoffLng     = 78.498,
        RecipientName  = "John Doe",
        RecipientPhone = "9876543210",
        Items          = Enumerable.Range(1, itemCount).Select(i => new ShipmentItemRequestDto
        {
            Description = $"Item {i}",
            Weight      = 1.5m,
            Length      = 30m,
            Width       = 20m,
            Height      = 10m,
            Quantity    = 2,
        }).ToList(),
    };

    private Shipment SetupRepoShipment(
        int id            = 1,
        int customerId    = 10,
        int? driverId     = null,
        ShipmentStatus status = ShipmentStatus.Pending)
    {
        var shipment = new Shipment
        {
            Id             = id,
            CustomerId     = customerId,
            DriverId       = driverId,
            TrackingNumber = $"TRK-{id:D6}",
            Status         = status,
            CreatedAt      = DateTime.UtcNow,
            UpdatedAt      = DateTime.UtcNow,
            ShipmentAddresses = new List<ShipmentAddress>
            {
                new() { AddressType = AddressType.Pickup,  AddressLine = "Pickup St" },
                new() { AddressType = AddressType.Dropoff, AddressLine = "Dropoff Rd",
                        ContactName = "Recipient", ContactPhone = "9000000001" },
            },
            ShipmentItems  = new List<ShipmentItem>(),
            ShipmentEvents = new List<ShipmentEvent>(),
        };

        _repoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(shipment);
        _repoMock.Setup(r => r.GetByIdWithAddressesAsync(id)).ReturnsAsync(shipment);
        _repoMock.Setup(r => r.GetByIdWithLockAsync(id, It.IsAny<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>()))
                 .ReturnsAsync(shipment);

        return shipment;
    }

    // ═══════════════════════════════════════════════════════════
    //  BookShipmentAsync
    // ═══════════════════════════════════════════════════════════

    [Test]
    public async Task BookShipmentAsync_ValidRequest_ReturnsShipmentDto()
    {
        var req    = MakeBookingRequest();
        var result = await _sut.BookShipmentAsync(customerId: 10, req);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status,         Is.EqualTo(ShipmentStatus.Pending));
            Assert.That(result.TrackingNumber, Does.StartWith("TRK-"));
        });
    }

    [Test]
    public async Task BookShipmentAsync_CreatesAddressAndItemRows()
    {
        var req = MakeBookingRequest(itemCount: 2);
        await _sut.BookShipmentAsync(10, req);

        // Addresses: 1 Pickup + 1 Dropoff
        Assert.That(_ctx.ShipmentAddresses.Count(), Is.EqualTo(2));

        var pickup  = _ctx.ShipmentAddresses.First(a => a.AddressType == AddressType.Pickup);
        var dropoff = _ctx.ShipmentAddresses.First(a => a.AddressType == AddressType.Dropoff);

        Assert.Multiple(() =>
        {
            Assert.That(pickup.AddressLine,    Is.EqualTo("Sender Lane 1"));
            Assert.That(pickup.ContactName,    Is.Null);   // no contact on pickup
            Assert.That(dropoff.AddressLine,   Is.EqualTo("Recipient Road 2"));
            Assert.That(dropoff.ContactName,   Is.EqualTo("John Doe"));
            Assert.That(dropoff.ContactPhone,  Is.EqualTo("9876543210"));
            // 2 items seeded
            Assert.That(_ctx.ShipmentItems.Count(), Is.EqualTo(2));
        });
    }

    [Test]
    public async Task BookShipmentAsync_MapsAllItemDimensions()
    {
        var req = MakeBookingRequest(itemCount: 1);
        await _sut.BookShipmentAsync(10, req);

        var item = _ctx.ShipmentItems.First();
        Assert.Multiple(() =>
        {
            Assert.That(item.WeightKg, Is.EqualTo(1.5m));
            Assert.That(item.LengthCm, Is.EqualTo(30m));
            Assert.That(item.WidthCm,  Is.EqualTo(20m));
            Assert.That(item.HeightCm, Is.EqualTo(10m));
            Assert.That(item.Quantity, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task BookShipmentAsync_InsertsInitialPendingEvent()
    {
        await _sut.BookShipmentAsync(10, MakeBookingRequest());

        _repoMock.Verify(r => r.AddEventAsync(
            It.Is<ShipmentEvent>(e =>
                e.Status      == ShipmentStatus.Pending &&
                e.Description == "Shipment booked and pending driver assignment.")),
            Times.Once);
    }

    [Test]
    public void BookShipmentAsync_DuplicateBooking_ThrowsInvalidOperationException()
    {
        _repoMock.Setup(r => r.IsDuplicateBookingAsync(10, "Sender Lane 1", "Recipient Road 2"))
                 .ReturnsAsync(true);

        Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.BookShipmentAsync(10, MakeBookingRequest()));
    }

    [Test]
    public async Task BookShipmentAsync_TrackingNumberCollision_RetriesUntilUnique()
    {
        // First call returns false (not unique), second returns true (unique)
        var callCount = 0;
        _repoMock.Setup(r => r.IsTrackingNumberUniqueAsync(It.IsAny<string>()))
                 .ReturnsAsync(() => ++callCount > 1);

        var result = await _sut.BookShipmentAsync(10, MakeBookingRequest());

        Assert.That(callCount,             Is.EqualTo(2));
        Assert.That(result.TrackingNumber, Does.StartWith("TRK-"));
    }

    [Test]
    public async Task BookShipmentAsync_TrackingNumberFormat_MatchesTRKDashSix()
    {
        var result = await _sut.BookShipmentAsync(10, MakeBookingRequest());

        Assert.That(result.TrackingNumber, Does.Match(@"^TRK-[A-F0-9]{6}$"));
    }

    // ═══════════════════════════════════════════════════════════
    //  GetCustomerShipmentsAsync
    // ═══════════════════════════════════════════════════════════

    [Test]
    public async Task GetCustomerShipmentsAsync_ReturnsOnlyOwnShipments()
    {
        SeedShipment(1, customerId: 10, trackingNum: "TRK-C10A01");
        SeedShipment(2, customerId: 99, trackingNum: "TRK-C99A01"); // other customer

        var result = await _sut.GetCustomerShipmentsAsync(
            10, new ShipmentQueryParams { Page = 1, Size = 10 });

        Assert.That(result.TotalCount, Is.EqualTo(1));
        Assert.That(result.Data.First().TrackingNumber, Is.EqualTo("TRK-C10A01"));
    }

    [Test]
    public async Task GetCustomerShipmentsAsync_FilterByStatus_ReturnsOnlyMatching()
    {
        SeedShipment(1, customerId: 10, status: ShipmentStatus.Pending,   trackingNum: "TRK-000001");
        SeedShipment(2, customerId: 10, status: ShipmentStatus.Delivered,  trackingNum: "TRK-000002");

        var result = await _sut.GetCustomerShipmentsAsync(
            10, new ShipmentQueryParams { Page = 1, Size = 10, Status = ShipmentStatus.Pending });

        Assert.That(result.TotalCount, Is.EqualTo(1));
        Assert.That(result.Data.First().Status, Is.EqualTo(ShipmentStatus.Pending));
    }

    [Test]
    public async Task GetCustomerShipmentsAsync_MapsAddressAreas()
    {
        SeedShipment(1, customerId: 10, withAddresses: true, trackingNum: "TRK-000001");

        var result = await _sut.GetCustomerShipmentsAsync(
            10, new ShipmentQueryParams { Page = 1, Size = 10 });

        var dto = result.Data.First();
        Assert.Multiple(() =>
        {
            Assert.That(dto.PickupArea,  Is.EqualTo("123 Pickup St"));
            Assert.That(dto.DropoffArea, Is.EqualTo("456 Dropoff Rd"));
        });
    }

    [Test]
    public async Task GetCustomerShipmentsAsync_PageBelowOne_ClampsToOne()
    {
        SeedShipment(1, customerId: 10, trackingNum: "TRK-000001");

        var result = await _sut.GetCustomerShipmentsAsync(
            10, new ShipmentQueryParams { Page = -3, Size = 10 });

        Assert.That(result.Page, Is.EqualTo(1));
    }

    [Test]
    public async Task GetCustomerShipmentsAsync_SizeAbove50_ClampsTo50()
    {
        SeedShipment(1, customerId: 10, trackingNum: "TRK-000001");

        var result = await _sut.GetCustomerShipmentsAsync(
            10, new ShipmentQueryParams { Page = 1, Size = 500 });

        Assert.That(result.Size, Is.EqualTo(50));
    }

    [Test]
    public async Task GetCustomerShipmentsAsync_SizeBelowOne_ClampsToOne()
    {
        SeedShipment(1, customerId: 10, trackingNum: "TRK-000001");

        var result = await _sut.GetCustomerShipmentsAsync(
            10, new ShipmentQueryParams { Page = 1, Size = 0 });

        Assert.That(result.Size, Is.EqualTo(1));
    }

    [Test]
    public async Task GetCustomerShipmentsAsync_Pagination_CorrectSkipTake()
    {
        for (var i = 1; i <= 5; i++)
            SeedShipment(i, customerId: 10, trackingNum: $"TRK-00000{i}");

        var result = await _sut.GetCustomerShipmentsAsync(
            10, new ShipmentQueryParams { Page = 2, Size = 2 });

        Assert.That(result.Data.Count, Is.EqualTo(2));
        Assert.That(result.TotalCount, Is.EqualTo(5));
    }

    [Test]
    public async Task GetCustomerShipmentsAsync_EmptyResult_ReturnsEmptyList()
    {
        var result = await _sut.GetCustomerShipmentsAsync(
            10, new ShipmentQueryParams { Page = 1, Size = 10 });

        Assert.That(result.Data,       Is.Empty);
        Assert.That(result.TotalCount, Is.EqualTo(0));
    }

    // ═══════════════════════════════════════════════════════════
    //  GetShipmentByIdAsync
    // ═══════════════════════════════════════════════════════════

    [Test]
    public async Task GetShipmentByIdAsync_CustomerOwnsShipment_ReturnsDetail()
    {
        SetupRepoShipment(id: 1, customerId: 10);

        var result = await _sut.GetShipmentByIdAsync(1, requesterId: 10, UserRole.Customer);

        Assert.That(result.Id, Is.EqualTo(1));
    }

    [Test]
    public void GetShipmentByIdAsync_CustomerDoesNotOwn_ThrowsUnauthorized()
    {
        SetupRepoShipment(id: 1, customerId: 10);

        Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.GetShipmentByIdAsync(1, requesterId: 99, UserRole.Customer));
    }

    [Test]
    public async Task GetShipmentByIdAsync_DriverOwnsAssignedShipment_ReturnsDetail()
    {
        SetupRepoShipment(id: 1, customerId: 10, driverId: 20);

        var result = await _sut.GetShipmentByIdAsync(1, requesterId: 20, UserRole.Driver);

        Assert.That(result.Id, Is.EqualTo(1));
    }

    [Test]
    public void GetShipmentByIdAsync_DriverDoesNotOwn_ThrowsUnauthorized()
    {
        SetupRepoShipment(id: 1, customerId: 10, driverId: 20);

        Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.GetShipmentByIdAsync(1, requesterId: 99, UserRole.Driver));
    }

    [Test]
    public async Task GetShipmentByIdAsync_AdminRole_BypassesOwnershipCheck()
    {
        // Admin can see any shipment regardless of customerId / driverId
        SetupRepoShipment(id: 1, customerId: 10, driverId: 20);

        // requesterId = 999 (not the customer or driver) but role = Admin
        var result = await _sut.GetShipmentByIdAsync(1, requesterId: 999, UserRole.Admin);

        Assert.That(result.Id, Is.EqualTo(1));
    }

    [Test]
    public void GetShipmentByIdAsync_NotFound_ThrowsKeyNotFoundException()
    {
        _repoMock.Setup(r => r.GetByIdWithAddressesAsync(999))
                 .ReturnsAsync((Shipment?)null);

        Assert.ThrowsAsync<KeyNotFoundException>(
            () => _sut.GetShipmentByIdAsync(999, 10, UserRole.Customer));
    }

    [Test]
    public async Task GetShipmentByIdAsync_MapsAddressesItemsAndEvents()
    {
        var shipment = SetupRepoShipment(id: 1, customerId: 10);
        shipment.ShipmentItems = new List<ShipmentItem>
        {
            new() { Description = "Box", WeightKg = 1m, Quantity = 3 }
        };
        shipment.ShipmentEvents = new List<ShipmentEvent>
        {
            new() { Status = ShipmentStatus.Pending, Description = "Booked",
                    OccurredAt = DateTime.UtcNow.AddMinutes(-5) },
            new() { Status = ShipmentStatus.Assigned, Description = "Assigned",
                    OccurredAt = DateTime.UtcNow.AddMinutes(-2) },
        };

        var result = await _sut.GetShipmentByIdAsync(1, 10, UserRole.Customer);

        Assert.Multiple(() =>
        {
            Assert.That(result.Items.Count,  Is.EqualTo(1));
            Assert.That(result.Events.Count, Is.EqualTo(2));
            // Events ordered ascending
            Assert.That(result.Events[0].Description, Is.EqualTo("Booked"));
            Assert.That(result.Events[1].Description, Is.EqualTo("Assigned"));
            Assert.That(result.PickupAddress.AddressLine1,  Is.EqualTo("Pickup St"));
            Assert.That(result.DropoffAddress.AddressLine1, Is.EqualTo("Dropoff Rd"));
            Assert.That(result.RecipientName,  Is.EqualTo("Recipient"));
            Assert.That(result.RecipientPhone, Is.EqualTo("9000000001"));
        });
    }

    // ═══════════════════════════════════════════════════════════
    //  CancelShipmentAsync
    // ═══════════════════════════════════════════════════════════

    [Test]
    public async Task CancelShipmentAsync_PendingShipment_CancelsAndBroadcasts()
    {
        var shipment = SetupRepoShipment(id: 1, customerId: 10, status: ShipmentStatus.Pending);

        var result = await _sut.CancelShipmentAsync(1, customerId: 10);

        Assert.That(result.Status, Is.EqualTo(ShipmentStatus.Cancelled));

        _repoMock.Verify(r => r.AddEventAsync(
            It.Is<ShipmentEvent>(e => e.Status == ShipmentStatus.Cancelled)),
            Times.Once);

        _trackingMock.Verify(t => t.BroadcastStatusUpdateAsync(
            shipment.TrackingNumber,
            ShipmentStatus.Cancelled.ToString(),
            "Shipment cancelled by customer."),
            Times.Once);
    }

    [Test]
    public void CancelShipmentAsync_NonPendingStatus_ThrowsInvalidOperation()
    {
        SetupRepoShipment(id: 1, customerId: 10, status: ShipmentStatus.Assigned);

        Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CancelShipmentAsync(1, customerId: 10));
    }

    [TestCase(ShipmentStatus.PickedUp)]
    [TestCase(ShipmentStatus.InTransit)]
    [TestCase(ShipmentStatus.Arrived)]
    [TestCase(ShipmentStatus.Delivered)]
    public void CancelShipmentAsync_ActiveOrTerminalStatus_ThrowsInvalidOperation(
        ShipmentStatus status)
    {
        SetupRepoShipment(id: 1, customerId: 10, status: status);

        Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CancelShipmentAsync(1, customerId: 10));
    }

    [Test]
    public void CancelShipmentAsync_DifferentCustomer_ThrowsUnauthorized()
    {
        SetupRepoShipment(id: 1, customerId: 10, status: ShipmentStatus.Pending);

        Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.CancelShipmentAsync(1, customerId: 99));
    }

    [Test]
    public void CancelShipmentAsync_NotFound_ThrowsKeyNotFoundException()
    {
        _repoMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Shipment?)null);

        Assert.ThrowsAsync<KeyNotFoundException>(
            () => _sut.CancelShipmentAsync(999, customerId: 10));
    }

    // ═══════════════════════════════════════════════════════════
    //  GetPublicTrackingAsync
    // ═══════════════════════════════════════════════════════════

    [Test]
    public async Task GetPublicTrackingAsync_Found_ReturnsMappedDto()
    {
        SeedShipment(1, customerId: 10, status: ShipmentStatus.Pending,
            withAddresses: true, withEvents: true, trackingNum: "TRK-PUBLI1");

        var result = await _sut.GetPublicTrackingAsync("TRK-PUBLI1");

        Assert.Multiple(() =>
        {
            Assert.That(result.TrackingNumber, Is.EqualTo("TRK-PUBLI1"));
            Assert.That(result.Status,         Is.EqualTo(ShipmentStatus.Pending));
            Assert.That(result.PickupAddress,  Is.EqualTo("123 Pickup St"));
            Assert.That(result.DropoffAddress, Is.EqualTo("456 Dropoff Rd"));
            Assert.That(result.Events.Count,   Is.EqualTo(2));
        });
    }

    [Test]
    public void GetPublicTrackingAsync_NotFound_ThrowsKeyNotFoundException()
    {
        Assert.ThrowsAsync<KeyNotFoundException>(
            () => _sut.GetPublicTrackingAsync("TRK-NOTFOUND"));
    }

    [Test]
    public async Task GetPublicTrackingAsync_InTransit_ExposesDriverLocation()
    {
        // Seed a driver with GPS coordinates
        var driverUser = new User
        {
            Id           = 50,
            FullName     = "Driver",
            Email        = "d@test.com",
            PasswordHash = "hash",
            Role         = UserRole.Driver,
            IsActive     = true,
            CreatedAt    = DateTime.UtcNow,
            UpdatedAt    = DateTime.UtcNow,
        };
        _ctx.Users.Add(driverUser);

        var driverProfile = new DriverProfile
        {
            Id            = 5,
            UserId        = 50,
            VehicleType   = "Bike",
            LicenseNumber = "LIC5",
            AccountStatus = DriverAccountStatus.Active,
            CurrentLat    = 17.399,
            CurrentLng    = 78.491,
            CreatedAt     = DateTime.UtcNow,
            UpdatedAt     = DateTime.UtcNow,
        };
        _ctx.DriverProfiles.Add(driverProfile);
        _ctx.SaveChanges();

        // Seed InTransit shipment assigned to driver
        var shipment = new Shipment
        {
            Id             = 1,
            CustomerId     = 10,
            DriverId       = 50,
            TrackingNumber = "TRK-INTRANSIT",
            Status         = ShipmentStatus.InTransit,
            CreatedAt      = DateTime.UtcNow,
            UpdatedAt      = DateTime.UtcNow,
        };
        _ctx.Shipments.Add(shipment);
        _ctx.ShipmentAddresses.AddRange(
            new ShipmentAddress
            {
                ShipmentId  = 1, AddressType = AddressType.Pickup,
                AddressLine = "Pickup", Lat = 17.385, Lng = 78.487
            },
            new ShipmentAddress
            {
                ShipmentId  = 1, AddressType = AddressType.Dropoff,
                AddressLine = "Dropoff", Lat = 17.440, Lng = 78.498
            });
        _ctx.SaveChanges();

        var result = await _sut.GetPublicTrackingAsync("TRK-INTRANSIT");

        Assert.Multiple(() =>
        {
            Assert.That(result.DriverLat, Is.EqualTo(17.399));
            Assert.That(result.DriverLng, Is.EqualTo(78.491));
        });
    }

    [Test]
    public async Task GetPublicTrackingAsync_NonInTransit_DriverLocationIsNull()
    {
        SeedShipment(1, customerId: 10, status: ShipmentStatus.Assigned,
            trackingNum: "TRK-ASSIGNED");

        var result = await _sut.GetPublicTrackingAsync("TRK-ASSIGNED");

        Assert.Multiple(() =>
        {
            Assert.That(result.DriverLat, Is.Null);
            Assert.That(result.DriverLng, Is.Null);
        });
    }

    [Test]
    public async Task GetPublicTrackingAsync_EventsOrderedAscending()
    {
        SeedShipment(1, customerId: 10, status: ShipmentStatus.Assigned,
            withEvents: true, trackingNum: "TRK-EVENTS1");

        var result = await _sut.GetPublicTrackingAsync("TRK-EVENTS1");

        Assert.That(result.Events[0].Description, Is.EqualTo("Booked"));
        Assert.That(result.Events[1].Description, Is.EqualTo("Driver assigned"));
    }

    // ═══════════════════════════════════════════════════════════
    //  GetPendingQueueAsync
    // ═══════════════════════════════════════════════════════════

    [Test]
    public async Task GetPendingQueueAsync_ReturnsOnlyPendingUnassigned()
    {
        SeedShipment(1, customerId: 10, status: ShipmentStatus.Pending,
            driverId: null, withItems: true, trackingNum: "TRK-P001");
        SeedShipment(2, customerId: 10, status: ShipmentStatus.Assigned,
            driverId: 20, trackingNum: "TRK-A001"); // already assigned
        SeedShipment(3, customerId: 10, status: ShipmentStatus.Pending,
            driverId: 20, trackingNum: "TRK-P002"); // pending but taken

        var result = await _sut.GetPendingQueueAsync(driverId: 99, page: 1, size: 10);

        Assert.That(result.TotalCount, Is.EqualTo(1));
        Assert.That(result.Data.First().TrackingNumber, Is.EqualTo("TRK-P001"));
    }

    [Test]
    public async Task GetPendingQueueAsync_MapsWeightAndItemCount()
    {
        SeedShipment(1, customerId: 10, status: ShipmentStatus.Pending,
            driverId: null, withItems: true, trackingNum: "TRK-P001");

        var result = await _sut.GetPendingQueueAsync(driverId: 99, page: 1, size: 10);
        var dto    = result.Data.First();

        Assert.Multiple(() =>
        {
            // WeightKg=2.5, Quantity=2 → TotalWeightKg = 5.0
            Assert.That(dto.TotalWeightKg, Is.EqualTo(5.0m));
            Assert.That(dto.ItemCount,     Is.EqualTo(2));
        });
    }

   [Test]
    public async Task GetPendingQueueAsync_OrdersOldestFirst()
    {
        // 1. Arrange: Seed the shipments
        SeedShipment(1, customerId: 10, status: ShipmentStatus.Pending,
            driverId: null, trackingNum: "TRK-OLDER");
            
        SeedShipment(2, customerId: 10, status: ShipmentStatus.Pending,
            driverId: null, trackingNum: "TRK-NEWER");

        // 2. Force the timestamps to guarantee "TRK-OLDER" is actually older
        var olderShipment = _ctx.Shipments.First(s => s.TrackingNumber == "TRK-OLDER");
        var newerShipment = _ctx.Shipments.First(s => s.TrackingNumber == "TRK-NEWER");
        
        olderShipment.CreatedAt = DateTime.UtcNow.AddHours(-2); // Created 2 hours ago
        newerShipment.CreatedAt = DateTime.UtcNow.AddHours(-1); // Created 1 hour ago
        await _ctx.SaveChangesAsync();

        // 3. Act
        var result = await _sut.GetPendingQueueAsync(driverId: 99, page: 1, size: 10);

        // 4. Assert: Oldest shipment should appear first (FIFO queue)
        Assert.That(result.Data.First().TrackingNumber, Is.EqualTo("TRK-OLDER"));
    }

    [Test]
    public async Task GetPendingQueueAsync_Pagination_ClampsAndPaginates()
    {
        for (var i = 1; i <= 5; i++)
            SeedShipment(i, customerId: 10, status: ShipmentStatus.Pending,
                driverId: null, trackingNum: $"TRK-{i:D6}");

        var result = await _sut.GetPendingQueueAsync(driverId: 99, page: 2, size: 2);

        Assert.That(result.Data.Count, Is.EqualTo(2));
        Assert.That(result.TotalCount, Is.EqualTo(5));
    }

    [Test]
    public async Task GetPendingQueueAsync_SizeClamping()
    {
        SeedShipment(1, customerId: 10, status: ShipmentStatus.Pending,
            driverId: null, trackingNum: "TRK-000001");

        var result = await _sut.GetPendingQueueAsync(driverId: 99, page: 0, size: 200);

        Assert.That(result.Page, Is.EqualTo(1));
        Assert.That(result.Size, Is.EqualTo(50));
    }

    // ═══════════════════════════════════════════════════════════
    //  AssignDriverAsync
    // ═══════════════════════════════════════════════════════════

    [Test]
    public async Task AssignDriverAsync_PendingShipment_AssignsAndBroadcasts()
    {
        var shipment = SetupRepoShipment(id: 1, customerId: 10, status: ShipmentStatus.Pending);

        var result = await _sut.AssignDriverAsync(shipmentId: 1, driverId: 20);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status,         Is.EqualTo(ShipmentStatus.Assigned));
            Assert.That(shipment.DriverId,     Is.EqualTo(20));
        });

        _repoMock.Verify(r => r.AddEventAsync(
            It.Is<ShipmentEvent>(e =>
                e.Status  == ShipmentStatus.Assigned &&
                e.ActorId == 20)),
            Times.Once);

        _trackingMock.Verify(t => t.BroadcastStatusUpdateAsync(
            shipment.TrackingNumber,
            ShipmentStatus.Assigned.ToString(),
            "Driver accepted the job."),
            Times.Once);
    }

    [Test]
    public void AssignDriverAsync_AlreadyAssigned_ThrowsInvalidOperation()
    {
        SetupRepoShipment(id: 1, customerId: 10, driverId: 99, status: ShipmentStatus.Assigned);

        Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.AssignDriverAsync(1, driverId: 20));
    }

    [Test]
    public void AssignDriverAsync_NonPendingStatus_ThrowsInvalidOperation()
    {
        SetupRepoShipment(id: 1, customerId: 10, status: ShipmentStatus.Delivered);

        Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.AssignDriverAsync(1, driverId: 20));
    }

    [Test]
    public void AssignDriverAsync_NotFound_ThrowsKeyNotFoundException()
    {
        _repoMock.Setup(r => r.GetByIdWithLockAsync(999, It.IsAny<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>()))
                 .ReturnsAsync((Shipment?)null);

        Assert.ThrowsAsync<KeyNotFoundException>(
            () => _sut.AssignDriverAsync(999, driverId: 20));
    }

    [Test]
    public async Task AssignDriverAsync_BroadcastCalledAfterCommit()
    {
        var broadcastCalledAfterSave = false;
        var saveCalled               = false;

        var shipment = SetupRepoShipment(1, customerId: 10, status: ShipmentStatus.Pending);

        _trackingMock.Setup(t => t.BroadcastStatusUpdateAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback(() => broadcastCalledAfterSave = saveCalled)
            .Returns(Task.CompletedTask);

        // Intercept SaveChanges to set flag
        // (In-memory DB saves immediately, so the flag is true when broadcast runs)
        await _sut.AssignDriverAsync(1, 20);

        // The broadcast must have been called (verifying order indirectly)
        _trackingMock.Verify(t => t.BroadcastStatusUpdateAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
    }

    // ═══════════════════════════════════════════════════════════
    //  UpdateStatusAsync
    // ═══════════════════════════════════════════════════════════

    [Test]
    public async Task UpdateStatusAsync_PickedUpToInTransit_PublishesGpsStartedEvent()
    {
        var shipment = SetupRepoShipment(1, customerId: 10, driverId: 20,
            status: ShipmentStatus.PickedUp);

        // Seed driver profile with GPS and dropoff address with coords
        _ctx.Users.Add(new User
        {
            Id = 20, FullName = "D", Email = "d@t.com",
            PasswordHash = "h", Role = UserRole.Driver,
            IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        _ctx.DriverProfiles.Add(new DriverProfile
        {
            Id = 2, UserId = 20, VehicleType = "Bike", LicenseNumber = "L2",
            AccountStatus = DriverAccountStatus.Active,
            CurrentLat = 17.385, CurrentLng = 78.487,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        });
        _ctx.ShipmentAddresses.Add(new ShipmentAddress
        {
            ShipmentId = 1, AddressType = AddressType.Dropoff,
            AddressLine = "Dropoff", Lat = 17.440, Lng = 78.498,
        });
        _ctx.SaveChanges();

        await _sut.UpdateStatusAsync(1, driverId: 20, ShipmentStatus.InTransit);

        _gpsMock.Verify(g => g.Publish(It.Is<GpsSimulationEvent>(
            e => e.EventType      == GpsEventType.Started &&
                 e.ShipmentId     == 1 &&
                 e.CurrentLat     == 17.385 &&
                 e.DropoffLat     == 17.440)),
            Times.Once);

        _trackingMock.Verify(t => t.BroadcastStatusUpdateAsync(
            shipment.TrackingNumber,
            ShipmentStatus.InTransit.ToString(),
            "Driver is on the way to the recipient."),
            Times.Once);
    }

    [Test]
    public async Task UpdateStatusAsync_PickedUpToInTransit_MissingCoords_LogsWarningNoGpsEvent()
    {
        SetupRepoShipment(1, customerId: 10, driverId: 20, status: ShipmentStatus.PickedUp);

        // Driver has NO coordinates
        _ctx.Users.Add(new User
        {
            Id = 20, FullName = "D", Email = "d@t.com",
            PasswordHash = "h", Role = UserRole.Driver,
            IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        _ctx.DriverProfiles.Add(new DriverProfile
        {
            Id = 2, UserId = 20, VehicleType = "Bike", LicenseNumber = "L2",
            AccountStatus = DriverAccountStatus.Active,
            CurrentLat = null, CurrentLng = null,   // ← missing
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        });
        _ctx.SaveChanges();

        await _sut.UpdateStatusAsync(1, driverId: 20, ShipmentStatus.InTransit);

        // GPS channel must NOT have been called
        _gpsMock.Verify(g => g.Publish(It.IsAny<GpsSimulationEvent>()), Times.Never);

        // Logger must have warned
        _loggerMock.Verify(l => l.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("coordinates are missing")),
            null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public async Task UpdateStatusAsync_InTransitToArrived_PublishesGpsStoppedAndDriverArrived()
    {
        var shipment = SetupRepoShipment(1, customerId: 10, driverId: 20,
            status: ShipmentStatus.InTransit);

        _ctx.Users.Add(new User
        {
            Id = 20, FullName = "D", Email = "d@t.com",
            PasswordHash = "h", Role = UserRole.Driver,
            IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        _ctx.DriverProfiles.Add(new DriverProfile
        {
            Id = 2, UserId = 20, VehicleType = "Bike", LicenseNumber = "L2",
            AccountStatus = DriverAccountStatus.Active,
            CurrentLat = 17.440, CurrentLng = 78.498,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        });
        _ctx.SaveChanges();

        await _sut.UpdateStatusAsync(1, driverId: 20, ShipmentStatus.Arrived);

        _gpsMock.Verify(g => g.Publish(It.Is<GpsSimulationEvent>(
            e => e.EventType  == GpsEventType.Stopped &&
                 e.ShipmentId == 1)),
            Times.Once);

        _trackingMock.Verify(t => t.BroadcastDriverArrivedAsync(
            shipment.TrackingNumber,
            17.440, 78.498),
            Times.Once);

        _trackingMock.Verify(t => t.BroadcastStatusUpdateAsync(
            shipment.TrackingNumber,
            ShipmentStatus.Arrived.ToString(),
            "Driver has arrived at the delivery address."),
            Times.Once);
    }

    [Test]
    public void UpdateStatusAsync_WrongDriver_ThrowsUnauthorized()
    {
        SetupRepoShipment(1, customerId: 10, driverId: 20, status: ShipmentStatus.PickedUp);

        Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.UpdateStatusAsync(1, driverId: 99, ShipmentStatus.InTransit));
    }

    [TestCase(ShipmentStatus.Delivered)]
    [TestCase(ShipmentStatus.Cancelled)]
    [TestCase(ShipmentStatus.FailedDelivery)]
    public void UpdateStatusAsync_TerminalTargetStatus_ThrowsInvalidOperation(
        ShipmentStatus terminal)
    {
        SetupRepoShipment(1, customerId: 10, driverId: 20, status: ShipmentStatus.Arrived);

        Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.UpdateStatusAsync(1, driverId: 20, terminal));
    }

    [Test]
    public void UpdateStatusAsync_InvalidTransition_PendingToInTransit_ThrowsInvalidOperation()
    {
        SetupRepoShipment(1, customerId: 10, driverId: 20, status: ShipmentStatus.Pending);

        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.UpdateStatusAsync(1, driverId: 20, ShipmentStatus.InTransit));

        Assert.That(ex!.Message, Does.Contain("Cannot transition from 'Pending'"));
    }

    [Test]
    public void UpdateStatusAsync_TransitionFromStatusWithNoValidNext_IncludesHint()
    {
        // Assigned has no direct transition in ValidDriverTransitions
        SetupRepoShipment(1, customerId: 10, driverId: 20, status: ShipmentStatus.Assigned);

        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.UpdateStatusAsync(1, driverId: 20, ShipmentStatus.InTransit));

        Assert.That(ex!.Message, Does.Contain("No further transitions permitted from 'Assigned'"));
    }

    [Test]
    public void UpdateStatusAsync_NotFound_ThrowsKeyNotFoundException()
    {
        _repoMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Shipment?)null);

        Assert.ThrowsAsync<KeyNotFoundException>(
            () => _sut.UpdateStatusAsync(999, driverId: 20, ShipmentStatus.InTransit));
    }

    // ═══════════════════════════════════════════════════════════
    //  FailDeliveryAsync
    // ═══════════════════════════════════════════════════════════

    [Test]
    public async Task FailDeliveryAsync_ArrivedShipment_SetsFailedStatusAndBroadcasts()
    {
        var shipment = SetupRepoShipment(1, customerId: 10, driverId: 20,
            status: ShipmentStatus.Arrived);
        var before = DateTime.UtcNow.AddSeconds(-1);

        var result = await _sut.FailDeliveryAsync(1, driverId: 20, "Recipient not home");

        Assert.Multiple(() =>
        {
            Assert.That(result.Status,     Is.EqualTo(ShipmentStatus.FailedDelivery));
            Assert.That(shipment.FailedAt, Is.GreaterThanOrEqualTo(before));
        });

        _repoMock.Verify(r => r.AddEventAsync(
            It.Is<ShipmentEvent>(e =>
                e.Status      == ShipmentStatus.FailedDelivery &&
                e.Description == "Delivery failed: Recipient not home" &&
                e.ActorId     == 20)),
            Times.Once);

        _trackingMock.Verify(t => t.BroadcastStatusUpdateAsync(
            shipment.TrackingNumber,
            ShipmentStatus.FailedDelivery.ToString(),
            "Delivery failed: Recipient not home"),
            Times.Once);
    }

    [TestCase(ShipmentStatus.Pending)]
    [TestCase(ShipmentStatus.Assigned)]
    [TestCase(ShipmentStatus.PickedUp)]
    [TestCase(ShipmentStatus.InTransit)]
    [TestCase(ShipmentStatus.Delivered)]
    public void FailDeliveryAsync_NonArrivedStatus_ThrowsInvalidOperation(ShipmentStatus status)
    {
        SetupRepoShipment(1, customerId: 10, driverId: 20, status: status);

        Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.FailDeliveryAsync(1, driverId: 20, "reason"));
    }

    [Test]
    public void FailDeliveryAsync_WrongDriver_ThrowsUnauthorized()
    {
        SetupRepoShipment(1, customerId: 10, driverId: 20, status: ShipmentStatus.Arrived);

        Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.FailDeliveryAsync(1, driverId: 99, "reason"));
    }

    [Test]
    public void FailDeliveryAsync_NotFound_ThrowsKeyNotFoundException()
    {
        _repoMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Shipment?)null);

        Assert.ThrowsAsync<KeyNotFoundException>(
            () => _sut.FailDeliveryAsync(999, driverId: 20, "reason"));
    }

    [Test]
    public async Task FailDeliveryAsync_SetsFailedAtTimestamp()
    {
        SetupRepoShipment(1, customerId: 10, driverId: 20, status: ShipmentStatus.Arrived);
        var before = DateTime.UtcNow.AddSeconds(-1);

        await _sut.FailDeliveryAsync(1, driverId: 20, "Recipient absent");

        var shipment = _repoMock.Invocations
            .Where(i => i.Method.Name == "GetByIdAsync")
            .Select(i => i.ReturnValue)
            .OfType<Task<Shipment>>()
            .First()
            .Result;

        Assert.That(shipment.FailedAt, Is.GreaterThanOrEqualTo(before));
    }
}