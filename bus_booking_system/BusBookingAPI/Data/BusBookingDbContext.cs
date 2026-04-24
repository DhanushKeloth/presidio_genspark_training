using BusBookingAPI.Models.Entities;
using Microsoft.EntityFrameworkCore;
using RouteEntity = BusBookingAPI.Models.Entities.Route;

namespace BusBookingAPI.Data;

public class BusBookingDbContext : DbContext
{
    public BusBookingDbContext(DbContextOptions<BusBookingDbContext> options) : base(options) { }

    public DbSet<Admin> Admins => Set<Admin>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Operator> Operators => Set<Operator>();
    public DbSet<RouteEntity> Routes => Set<RouteEntity>();
    public DbSet<Bus> Buses => Set<Bus>();
    public DbSet<Seat> Seats => Set<Seat>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingDetail> BookingDetails => Set<BookingDetail>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<SeatLock> SeatLocks => Set<SeatLock>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // --- Enums stored as strings in PostgreSQL ---
        modelBuilder.Entity<Operator>()
            .Property(o => o.Status)
            .HasColumnType("operator_status");

        modelBuilder.Entity<Bus>()
            .Property(b => b.Status)
            .HasColumnType("bus_status");

        modelBuilder.Entity<Booking>()
            .Property(b => b.Status)
            .HasColumnType("booking_status");

        modelBuilder.Entity<Payment>()
            .Property(p => p.PaymentStatus)
            .HasColumnType("payment_status");

        modelBuilder.Entity<Payment>()
            .Property(p => p.PaymentMethod)
            .HasColumnType("payment_method_enum");

        modelBuilder.Entity<Seat>()
            .Property(s => s.SeatType)
            .HasColumnType("seat_type_enum");

        // --- Unique indexes ---
        modelBuilder.Entity<SeatLock>()
            .HasIndex(sl => new { sl.SeatId, sl.JourneyDate })
            .IsUnique();

        modelBuilder.Entity<BookingDetail>()
            .HasIndex(bd => new { bd.BookingId, bd.SeatId })
            .IsUnique();

        modelBuilder.Entity<RouteEntity>()
            .HasIndex(r => new { r.SourceCity, r.DestinationCity })
            .IsUnique();

        // --- JSONB columns ---
        modelBuilder.Entity<Bus>()
            .Property(b => b.Amenities)
            .HasColumnType("jsonb");

        modelBuilder.Entity<Bus>()
            .Property(b => b.SeatLayout)
            .HasColumnType("jsonb");

        // --- Relationships ---
        modelBuilder.Entity<Booking>()
            .HasOne(b => b.Payment)
            .WithOne(p => p.Booking)
            .HasForeignKey<Payment>(p => p.BookingId);

        modelBuilder.Entity<Operator>()
            .HasOne(o => o.ApprovedByAdmin)
            .WithMany()
            .HasForeignKey(o => o.ApprovedBy)
            .IsRequired(false);

        modelBuilder.Entity<RouteEntity>()
            .HasOne(r => r.CreatedByAdmin)
            .WithMany()
            .HasForeignKey(r => r.CreatedBy);
    }
}
