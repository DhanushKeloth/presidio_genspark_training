using Microsoft.EntityFrameworkCore;
using ShipmentTrackingAPI.DTOs.Common;
using ShipmentTrackingAPI.DTOs.Shipment;
using ShipmentTrackingAPI.Models;
using ShipmentTrackingAPI.Models.Enums;
using ShipmentTrackingAPI.Repositories.RepoInterfaces; // Assuming this is your Repo interface namespace
using ShipmentTrackingAPI.Data;       // Assuming this is your DbContext namespace
using SwiftParcelAPI.Services.Interfaces;

namespace ShipmentTrackingAPI.Services
{
    public class ShipmentService : IShipmentService
    {
        private readonly IShipmentRepository _repo;
        private readonly AppDbContext _ctx; // Required for explicit transaction control

        public ShipmentService(IShipmentRepository repo, AppDbContext ctx)
        {
            _repo = repo;
            _ctx = ctx;
        }

        #region 1. CUSTOMER ACTIONS

        public async Task<ShipmentDto> BookShipmentAsync(int customerId, BookShipmentRequestDto req)
        {
            // 1. Idempotency Check (Prevents double-billing/booking)
            if (await _repo.IsDuplicateBookingAsync(customerId, req.PickupAddress, req.DropoffAddress))
            {
                throw new InvalidOperationException("A duplicate booking was detected. Please wait a moment before trying again.");
            }

            // 2. Generate Unique Tracking Number
            string trackingNumber;
            do
            {
                trackingNumber = $"TRK-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 6).ToUpper()}";
            } 
            while (!await _repo.IsTrackingNumberUniqueAsync(trackingNumber));

            // 3. Begin Transaction to ensure all or nothing
                using var tx = await _ctx.Database.BeginTransactionAsync();
    try
    {
        // 1. Core Shipment is now much leaner!
        var shipment = new Shipment
        {
            CustomerId = customerId,
            TrackingNumber = trackingNumber,
            Status = ShipmentStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _repo.AddAsync(shipment); 

        // 2. Map Addresses (Recipient Info goes to Dropoff!)
        var pickup = new ShipmentAddress 
        { 
            Shipment = shipment, 
            AddressType = AddressType.Pickup, 
            AddressLine = req.PickupAddress,
            Lat = req.PickupLat,
            Lng = req.PickupLng
            // If you track SenderName/Phone, it would go here
        };
        
        var dropoff = new ShipmentAddress 
        { 
            Shipment = shipment, 
            AddressType = AddressType.Dropoff, 
            AddressLine = req.DropoffAddress,
            Lat = req.DropoffLat,
            Lng = req.DropoffLng,
            
            // Assuming your ShipmentAddress model has properties like this:
            ContactName = req.RecipientName, 
            ContactPhone = req.RecipientPhone 
        };
        
        await _ctx.Set<ShipmentAddress>().AddRangeAsync(pickup, dropoff);

        // 3. Map Items (Weight belongs here!)
        var items = req.Items.Select(i => new ShipmentItem
        {
            Shipment = shipment,
            Description = i.Description,
            WeightKg = i.Weight,       // Weight per item
            Quantity = i.Quantity
        }).ToList();
        
        await _ctx.Set<ShipmentItem>().AddRangeAsync(items);
                // Initial Event
                var initialEvent = new ShipmentEvent
                {
                    Shipment = shipment,
                    Status = ShipmentStatus.Pending,
                    Description = "Shipment booked and pending driver assignment.",
                    OccurredAt = DateTime.UtcNow
                };
                await _repo.AddEventAsync(initialEvent);

                await _ctx.SaveChangesAsync();
                await tx.CommitAsync();

                return new ShipmentDto
                {
                    Id = shipment.Id,
                    TrackingNumber = shipment.TrackingNumber,
                    Status = shipment.Status,
                    CreatedAt = shipment.CreatedAt
                };
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        public async Task<PaginatedResponse<ShipmentSummaryDto>> GetCustomerShipmentsAsync(int customerId, ShipmentQueryParams q)
        {
            var query = _ctx.Shipments
                .Include(s => s.ShipmentAddresses)
                .Where(s => s.CustomerId == customerId);

            if (q.Status.HasValue)
            {
                query = query.Where(s => s.Status == q.Status.Value);
            }

            var totalCount = await query.CountAsync();
            var shipments = await query
                .OrderByDescending(s => s.CreatedAt)
                .Skip((q.Page - 1) * q.Size)
                .Take(q.Size)
                .ToListAsync();

            var items = shipments.Select(s => new ShipmentSummaryDto
            {
                Id = s.Id,
                TrackingNumber = s.TrackingNumber,
                Status = s.Status,
                CreatedAt = s.CreatedAt,
                PickupArea = s.ShipmentAddresses.FirstOrDefault(a => a.AddressType == AddressType.Pickup)?.AddressLine ?? "N/A",
                DropoffArea = s.ShipmentAddresses.FirstOrDefault(a => a.AddressType == AddressType.Dropoff)?.AddressLine ?? "N/A"
            }).ToList();

            return new PaginatedResponse<ShipmentSummaryDto>
            {
                Data = items,
                TotalCount = totalCount,
                Page = q.Page,
                Size = q.Size
            };
        }

        public async Task<ShipmentDetailDto> GetShipmentByIdAsync(int id, int requesterId, UserRole role)
        {
            var shipment = await _repo.GetByIdWithAddressesAsync(id)
                ?? throw new KeyNotFoundException("Shipment not found.");

            // Strict Authorization Bounds
            if (role == UserRole.Customer && shipment.CustomerId != requesterId)
                throw new UnauthorizedAccessException("Access denied.");
            if (role == UserRole.Driver && shipment.DriverId != requesterId)
                throw new UnauthorizedAccessException("Access denied.");

            var pickup = shipment.ShipmentAddresses.FirstOrDefault(a => a.AddressType == AddressType.Pickup);
            var dropoff = shipment.ShipmentAddresses.FirstOrDefault(a => a.AddressType == AddressType.Dropoff);

            return new ShipmentDetailDto
            {
                Id = shipment.Id,
                TrackingNumber = shipment.TrackingNumber,
                Status = shipment.Status,
                CreatedAt = shipment.CreatedAt,
                UpdatedAt = shipment.UpdatedAt,
                CustomerName = shipment.Customer?.FullName ?? "Unknown",
                RecipientName = dropoff?.ContactName ?? "Unknown", 
                RecipientPhone = dropoff?.ContactPhone ?? "Unknown",
                DriverId = shipment.DriverId,
                DriverName = shipment.Driver?.FullName,
                DriverPhone = shipment.Driver?.DriverProfileUser?.PhoneNumber,
                VehicleNumber = shipment.Driver?.DriverProfileUser?.VehicleNumber,
                
                PickupAddress = new ShipmentAddressDto
                {
                    AddressLine1 = pickup?.AddressLine ?? "",
                    Latitude = pickup?.Lat,
                    Longitude = pickup?.Lng
                },
                DropoffAddress = new ShipmentAddressDto
                {
                    AddressLine1 = dropoff?.AddressLine ?? "",
                    Latitude = dropoff?.Lat,
                    Longitude = dropoff?.Lng
                },
                
                Items = shipment.ShipmentItems.Select(i => new ShipmentItemDto
                {
                    Description = i.Description,
                    Weight = i.WeightKg,
                    Quantity = i.Quantity
                }).ToList(),

                Events = shipment.ShipmentEvents.OrderByDescending(e => e.OccurredAt).Select(e => new ShipmentEventDto
                {
                    Status = e.Status,
                    Description = e.Description,
                    Timestamp = e.OccurredAt
                }).ToList()
            };
        }

        public async Task<ShipmentDto> CancelShipmentAsync(int shipmentId, int customerId)
        {
            var shipment = await _repo.GetByIdAsync(shipmentId) ?? throw new KeyNotFoundException();

            if (shipment.CustomerId != customerId)
                throw new UnauthorizedAccessException("You can only cancel your own shipments.");

            if (shipment.Status != ShipmentStatus.Pending)
                throw new InvalidOperationException($"Cannot cancel shipment. Current status is {shipment.Status}.");

            shipment.Status = ShipmentStatus.Cancelled;
            shipment.UpdatedAt = DateTime.UtcNow;

            await _repo.AddEventAsync(new ShipmentEvent
            {
                ShipmentId = shipment.Id,
                Status = ShipmentStatus.Cancelled,
                Description = "Shipment cancelled by customer.",
                OccurredAt = DateTime.UtcNow
            });

            await _ctx.SaveChangesAsync();

            return new ShipmentDto { Id = shipment.Id, TrackingNumber = shipment.TrackingNumber, Status = shipment.Status, CreatedAt = shipment.CreatedAt };
        }

        #endregion

        #region 2. PUBLIC ACTIONS

        public async Task<PublicTrackingDto> GetPublicTrackingAsync(string trackingNumber)
        {
            var shipment = await _repo.GetShipmentByTrackingNumberAsync(trackingNumber) 
                ?? throw new KeyNotFoundException("Invalid tracking number.");

            // Re-fetch events and addresses here because GetByTrackingNumberAsync was built to be ultra-lightweight.
            var details = await _ctx.Shipments
                .AsNoTracking()
                .Include(s => s.ShipmentAddresses)
                .Include(s => s.ShipmentEvents)
                .Include(s => s.Driver).ThenInclude(d => d.DriverProfileUser)
                .FirstOrDefaultAsync(s => s.Id == shipment.Id);

            var pickup = details!.ShipmentAddresses.FirstOrDefault(a => a.AddressType == AddressType.Pickup);
            var dropoff = details.ShipmentAddresses.FirstOrDefault(a => a.AddressType == AddressType.Dropoff);

            return new PublicTrackingDto
            {
                TrackingNumber = details.TrackingNumber,
                Status = details.Status,
                PickupAddress = pickup?.AddressLine ?? "N/A",
                DropoffAddress = dropoff?.AddressLine ?? "N/A",
                DriverLat = details.Driver?.DriverProfileUser?.CurrentLat,
                DriverLng = details.Driver?.DriverProfileUser?.CurrentLng,
                Events = details.ShipmentEvents.OrderByDescending(e => e.OccurredAt).Select(e => new ShipmentEventDto
                {
                    Status = e.Status,
                    Description = e.Description,
                    Timestamp = e.OccurredAt
                }).ToList()
            };
        }

        #endregion

        #region 3. DRIVER ACTIONS

        public async Task<PaginatedResponse<PendingJobDto>> GetPendingQueueAsync(int driverId, int page, int size)
        {
            var query = _ctx.Shipments
                .AsNoTracking()
                .Include(s => s.ShipmentAddresses)
                .Include(s => s.ShipmentItems)
                .Where(s => s.Status == ShipmentStatus.Pending && s.DriverId == null);

            var totalCount = await query.CountAsync();
            var shipments = await query
                .OrderBy(s => s.CreatedAt) // Oldest pending jobs first
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync();

            var items = shipments.Select(s => new PendingJobDto
            {
                TrackingNumber = s.TrackingNumber,
                PickupArea = s.ShipmentAddresses.FirstOrDefault(a => a.AddressType == AddressType.Pickup)?.AddressLine ?? "N/A",
                DropoffArea = s.ShipmentAddresses.FirstOrDefault(a => a.AddressType == AddressType.Dropoff)?.AddressLine ?? "N/A",
                TotalWeightKg = s.ShipmentItems.Sum(i => i.WeightKg),
                ItemCount = s.ShipmentItems.Sum(i => i.Quantity),
                CreatedAt = s.CreatedAt
            }).ToList();

            return new PaginatedResponse<PendingJobDto> { Data = items, TotalCount = totalCount, Page = page, Size = size };
        }

        public async Task<ShipmentDto> AssignDriverAsync(int shipmentId, int driverId)
        {
            using var tx = await _ctx.Database.BeginTransactionAsync();
            try
            {
                // This calls your raw SQL pessimistic lock! Driver B will wait here if Driver A is locking it.
                var shipment = await _repo.GetByIdWithLockAsync(shipmentId, tx) 
                    ?? throw new KeyNotFoundException();

                if (shipment.DriverId != null || shipment.Status != ShipmentStatus.Pending)
                    throw new InvalidOperationException("This shipment is no longer available.");

                shipment.DriverId = driverId;
                shipment.Status = ShipmentStatus.Assigned;
                shipment.UpdatedAt = DateTime.UtcNow;

                await _repo.AddEventAsync(new ShipmentEvent
                {
                    ShipmentId = shipment.Id,
                    Status = ShipmentStatus.Assigned,
                    Description = "Driver accepted the job.",
                    OccurredAt = DateTime.UtcNow
                });

                await _ctx.SaveChangesAsync();
                await tx.CommitAsync();

                return new ShipmentDto { Id = shipment.Id, TrackingNumber = shipment.TrackingNumber, Status = shipment.Status, CreatedAt = shipment.CreatedAt };
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        public async Task<ShipmentDto> UpdateStatusAsync(int shipmentId, int driverId, ShipmentStatus newStatus)
        {
            var shipment = await _repo.GetByIdAsync(shipmentId) ?? throw new KeyNotFoundException();

            if (shipment.DriverId != driverId)
                throw new UnauthorizedAccessException("You can only update your own assigned shipments.");

            // Simple State Machine validation to prevent skipping steps
            if (newStatus <= shipment.Status)
                throw new InvalidOperationException($"Cannot revert or set status to {newStatus} from {shipment.Status}.");

            shipment.Status = newStatus;
            shipment.UpdatedAt = DateTime.UtcNow;

            await _repo.AddEventAsync(new ShipmentEvent
            {
                ShipmentId = shipment.Id,
                Status = newStatus,
                Description = $"Status updated to {newStatus}.",
                OccurredAt = DateTime.UtcNow
            });

            await _ctx.SaveChangesAsync();

            return new ShipmentDto { Id = shipment.Id, TrackingNumber = shipment.TrackingNumber, Status = shipment.Status, CreatedAt = shipment.CreatedAt };
        }

        public async Task<ShipmentDto> FailDeliveryAsync(int shipmentId, int driverId, string reason)
        {
            var shipment = await _repo.GetByIdAsync(shipmentId) ?? throw new KeyNotFoundException();

            if (shipment.DriverId != driverId)
                throw new UnauthorizedAccessException();

            shipment.Status = ShipmentStatus.FailedDelivery;
            shipment.UpdatedAt = DateTime.UtcNow;

            await _repo.AddEventAsync(new ShipmentEvent
            {
                ShipmentId = shipment.Id,
                Status = ShipmentStatus.FailedDelivery,
                Description = $"Delivery Failed: {reason}",
                OccurredAt = DateTime.UtcNow
            });

            await _ctx.SaveChangesAsync();

            return new ShipmentDto { Id = shipment.Id, TrackingNumber = shipment.TrackingNumber, Status = shipment.Status, CreatedAt = shipment.CreatedAt };
        }

        #endregion
    }
}