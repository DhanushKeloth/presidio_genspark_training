using System;
using System.Collections.Generic;
using LibraryManagementSystem.Enums;

namespace LibraryManagementSystem.Models;

public class Member
{
    public int MemberId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public DateTime MembershipDate { get; set; } = DateTime.UtcNow;
}
