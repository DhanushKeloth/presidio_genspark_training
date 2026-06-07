using Microsoft.EntityFrameworkCore.Storage;
using ShipmentTrackingAPI.Models;

namespace ShipmentTrackingAPI.Repositories.RepoInterfaces
{
    public interface IShipmentRepository : IRepository<Shipment>
    {
        // ==========================================
        // COMPLEX RETRIEVALS
        // ==========================================
        
        /// <summary>
        /// Retrieves a shipment with its nested Addresses, Items, and OTP Windows.
        /// </summary>
        Task<Shipment?> GetByIdWithAddressesAsync(int id);

        /// <summary>
        /// Pessimistic Row Lock (FOR UPDATE). 
        /// Crucial for preventing race conditions when two drivers click "Accept Job" simultaneously.
        /// </summary>
        Task<Shipment?> GetByIdWithLockAsync(int id, IDbContextTransaction tx);

        /// <summary>
        /// Lightweight lookup by tracking number. Does not include heavy navigation properties.
        /// </summary>
        Task<Shipment?> GetShipmentByTrackingNumberAsync(string trackingNumber);

        /// <summary>
        /// Highly optimized query for the GPS Simulation Service. 
        /// Called every 5 seconds. Must return quickly.
        /// </summary>
        Task<List<Shipment>> GetInTransitShipmentsAsync();

        // ==========================================
        // VALIDATIONS & CONSTRAINTS
        // ==========================================

        /// <summary>
        /// Ensures generated tracking numbers don't collide.
        /// </summary>
        Task<bool> IsTrackingNumberUniqueAsync(string trackingNumber);

        /// <summary>
        /// Idempotency check. Prevents accidental double-bookings if the user clicks "Submit" twice rapidly.
        /// Returns true if the same customer booked the same route in the last 60 seconds.
        /// </summary>
        Task<bool> IsDuplicateBookingAsync(int customerId, string pickupAddress, string dropoffAddress);

        /// <summary>
        /// Anti-abandonment guard check. 
        /// Returns true if the driver currently holds any shipment that is Assigned, PickedUp, or InTransit.
        /// </summary>
        Task<bool> HasActiveShipmentAsync(int driverId);

        // ==========================================
        // SUB-ENTITY MANAGEMENT (Events & OTPs)
        // ==========================================

        /// <summary>
        /// Adds a tracking timeline event.
        /// </summary>
        Task AddEventAsync(ShipmentEvent evt);

        /// <summary>
        /// Handles both creation and regeneration of OTP windows using PostgreSQL's ON CONFLICT logic.
        /// </summary>
        Task UpsertOtpWindowAsync(ShipmentOtpWindow window);
    }
}