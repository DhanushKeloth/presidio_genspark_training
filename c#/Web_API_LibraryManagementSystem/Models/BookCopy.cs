using System;
using System.Collections.Generic;
using LibraryManagementSystem.Enums;

namespace LibraryManagementSystem.Models;

public partial class BookCopy
{
    public int BookCopyId { get; set; }

    public int BookId { get; set; }

    public string SerialNumber { get; set; } = null!;

    public CopyStatus Status { get; set; }

    public virtual Book Book { get; set; } = null!;

    public virtual ICollection<Borrowing> Borrowings { get; set; } = new List<Borrowing>();
}
