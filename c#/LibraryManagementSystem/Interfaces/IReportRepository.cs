using System.Collections.Generic;
using LibraryManagementSystem.Models;

namespace LibraryManagementSystem.Interfaces
{
    public interface IReportRepository
    {
        IEnumerable<Borrowing> GetCurrentlyBorrowedBooks();
        IEnumerable<Borrowing> GetOverdueBooks();
        IEnumerable<object> GetMembersWithPendingFines(); // Returns projections of Member details + Fine sums
        IEnumerable<object> GetMostBorrowedBooks(int topCount);
        IEnumerable<object> GetAvailableBooksByCategory();
        IEnumerable<Borrowing> GetMemberBorrowingHistory(int memberId);
    }
}