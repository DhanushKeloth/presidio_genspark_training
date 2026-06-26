using System;
using System.Collections.Generic;
using LibraryManagementSystem.Enums;

namespace LibraryManagementSystem.Models;

public partial class Member
{
    public int MemberId { get; set; }

    public string Name { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public MembershipType Membership { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<Borrowing> Borrowings { get; set; } = new List<Borrowing>();

    public virtual ICollection<FinePayment> FinePayments { get; set; } = new List<FinePayment>();
}
