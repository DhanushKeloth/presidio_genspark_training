namespace ShipmentTrackingAPI.DTOs.Common
{
    
   public class PaginatedResponse<T>
    {
        public List<T> Data { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int Size { get; set; }
      }
}