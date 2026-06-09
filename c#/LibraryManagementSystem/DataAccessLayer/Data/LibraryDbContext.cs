using System;
using System.Collections.Generic;
using LibraryManagementSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagementSystem.Data;

public partial class LibraryDbContext : DbContext
{
    public LibraryDbContext()
    {
    }

    public LibraryDbContext(DbContextOptions<LibraryDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Book> Books { get; set; }

    public virtual DbSet<BookCategory> BookCategories { get; set; }

    public virtual DbSet<BookCopy> BookCopies { get; set; }

    public virtual DbSet<Borrowing> Borrowings { get; set; }

    public virtual DbSet<FinePayment> FinePayments { get; set; }

    public virtual DbSet<Member> Members { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseNpgsql("Host=localhost;Database=library_management;Username=dhanushkeloth;Password=1234");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Book>(entity =>
        {
            entity.HasKey(e => e.BookId).HasName("Books_pkey");

            entity.Property(e => e.Author).HasMaxLength(100);
            entity.Property(e => e.Title).HasMaxLength(200);

            entity.HasOne(d => d.Category).WithMany(p => p.Books)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_Books_BookCategories");
        });

        modelBuilder.Entity<BookCategory>(entity =>
        {
            entity.HasKey(e => e.CategoryId).HasName("BookCategories_pkey");

            entity.Property(e => e.Name).HasMaxLength(50);
        });

        modelBuilder.Entity<BookCopy>(entity =>
        {
            entity.HasKey(e => e.BookCopyId).HasName("BookCopies_pkey");
            entity.Property(e => e.Status).HasConversion<int>();
            entity.HasIndex(e => e.SerialNumber, "BookCopies_SerialNumber_key").IsUnique();

            entity.Property(e => e.SerialNumber).HasMaxLength(50);

            entity.HasOne(d => d.Book).WithMany(p => p.BookCopies)
                .HasForeignKey(d => d.BookId)
                .HasConstraintName("FK_BookCopies_Books");
        });

        modelBuilder.Entity<Borrowing>(entity =>
        {
            entity.HasKey(e => e.BorrowingId).HasName("Borrowings_pkey");
            entity.Property(e => e.Status).HasConversion<int>();
            entity.HasIndex(e => new { e.MemberId, e.BookId }, "UK_Prevent_Duplicate_Active_Borrow")
                .IsUnique()
                .HasFilter("(\"Status\" = 1)");

            entity.Property(e => e.BorrowDate)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.DueDate).HasColumnType("timestamp without time zone");
            entity.Property(e => e.ReturnDate).HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.BookCopy).WithMany(p => p.Borrowings)
                .HasForeignKey(d => d.BookCopyId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_Borrowings_BookCopies");

            entity.HasOne(d => d.Book).WithMany(p => p.Borrowings)
                .HasForeignKey(d => d.BookId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_Borrowings_Books");

            entity.HasOne(d => d.Member).WithMany(p => p.Borrowings)
                .HasForeignKey(d => d.MemberId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_Borrowings_Members");
        });

        modelBuilder.Entity<FinePayment>(entity =>
        {
            entity.HasKey(e => e.FinePaymentId).HasName("FinePayments_pkey");

            entity.HasIndex(e => e.BorrowingId, "FinePayments_BorrowingId_key").IsUnique();

            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.PaymentDate).HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.Borrowing).WithOne(p => p.FinePayment)
                .HasForeignKey<FinePayment>(d => d.BorrowingId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_FinePayments_Borrowings");

            entity.HasOne(d => d.Member).WithMany(p => p.FinePayments)
                .HasForeignKey(d => d.MemberId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_FinePayments_Members");
        });

        modelBuilder.Entity<Member>(entity =>
        {
            entity.HasKey(e => e.MemberId).HasName("Members_pkey");

            entity.Property(e=>e.Membership).HasConversion<int>();
            entity.HasIndex(e => e.Email, "Members_Email_key").IsUnique();

            entity.HasIndex(e => e.Phone, "Members_Phone_key").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Phone).HasMaxLength(50);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
