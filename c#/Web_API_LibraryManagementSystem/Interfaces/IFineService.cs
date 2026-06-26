using System.Collections.Generic;
using LibraryManagementSystem.Models;

namespace LibraryManagementSystem.Interfaces
{
    public interface IFineService
    {
        decimal CheckPendingBalance(int memberId);
        bool ProcessFinePayment(int memberId, decimal amount);
        IEnumerable<FinePayment> ViewMemberFineHistory(int memberId);
        IEnumerable<FinePayment> ViewAllUnpaidFines();
    }
}