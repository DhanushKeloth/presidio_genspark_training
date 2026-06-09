using System;
using System.Collections.Generic;
using LibraryManagementSystem.Enums;

namespace LibraryManagementSystem.Models;

public partial class Borrowing
{
    public int BorrowingId { get; set; }

    public int MemberId { get; set; }

    public int BookCopyId { get; set; }

    public int BookId { get; set; }

    public DateTime BorrowDate { get; set; }

    public DateTime DueDate { get; set; }

    public DateTime? ReturnDate { get; set; }

    public BorrowingStatus Status { get; set; }

    public virtual Book Book { get; set; } = null!;

    public virtual BookCopy BookCopy { get; set; } = null!;

    public virtual FinePayment? FinePayment { get; set; }

    public virtual Member Member { get; set; } = null!;
}
