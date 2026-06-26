using System;
using System.Collections.Generic;

namespace EFCoreTransactions.Models;

public partial class Account
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public decimal Balance { get; set; }
}
