using ShipmentTrackingAPI.DTOs.Shipment;
using ShipmentTrackingAPI.Models.Enums;
using ShipmentTrackingAPI.DTOs.Common;
namespace ShipmentTrackingAPI.Interfaces
{
    public interface IShipmentService
    {
        // 1. CUSTOMER ACTIONS
        Task<ShipmentDto> BookShipmentAsync(int customerId, BookShipmentRequestDto req);
        
        Task<PaginatedResponse<ShipmentSummaryDto>> GetCustomerShipmentsAsync(int customerId, ShipmentQueryParams q);
        
        Task<ShipmentDetailDto> GetShipmentByIdAsync(int id, int requesterId, UserRole role);
        
        //  Customer cancels before driver assignment
        Task<ShipmentDto> CancelShipmentAsync(int shipmentId, int customerId); 

        // 2. PUBLIC ACTIONS
        Task<PublicTrackingDto> GetPublicTrackingAsync(string trackingNumber);

        // 3. DRIVER ACTIONS (Job Management)
        Task<PaginatedResponse<PendingJobDto>> GetPendingQueueAsync(int driverId, int page, int size);
        // Handles the pessimistic row lock
        Task<ShipmentDto> AssignDriverAsync(int shipmentId, int driverId); 
        
        // Validates via the state machine guard
        Task<ShipmentDto> UpdateStatusAsync(int shipmentId, int driverId, ShipmentStatus newStatus);
        
        //  Driver triggers failure at dropoff
        Task<ShipmentDto> FailDeliveryAsync(int shipmentId, int driverId, string reason); 

        //get quote of the shipment returns the cost only
        Task<ShipmentQuoteResponseDto> GetShipmentQuoteAsync(BookShipmentRequestDto req);

        
    }
}