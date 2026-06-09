using System;
using System.Collections.Generic;

namespace LibraryManagementSystem.Models;

public partial class FinePayment
{
    public int FinePaymentId { get; set; }

    public int MemberId { get; set; }

    public int BorrowingId { get; set; }

    public decimal Amount { get; set; }

    public bool IsPaid { get; set; }

    public DateTime? PaymentDate { get; set; }

    public virtual Borrowing Borrowing { get; set; } = null!;

    public virtual Member Member { get; set; } = null!;
}
