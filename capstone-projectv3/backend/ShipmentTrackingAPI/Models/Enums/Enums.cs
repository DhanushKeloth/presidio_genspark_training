namespace ShipmentTrackingAPI.Models.Enums
{
    public enum UserRole { Customer, Driver, Admin }
    public enum DriverAccountStatus { PendingApproval, Active, Suspended, Deleted }
    public enum DriverOpStatus { Available, InTransit, Offline }
    public enum ShipmentStatus { Pending, Assigned, PickedUp, InTransit, Arrived, Delivered, Cancelled, FailedDelivery }
    public enum AddressType { Pickup, Dropoff }
    public enum OtpType { Pickup, Delivery }
}