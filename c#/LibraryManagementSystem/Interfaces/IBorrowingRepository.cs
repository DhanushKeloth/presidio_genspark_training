using System.Collections.Generic;
using LibraryManagementSystem.Models;
using Microsoft.EntityFrameworkCore.Storage;

namespace LibraryManagementSystem.Interfaces
{
    public interface IBorrowingRepository<T> where T:class
    {
        T? Add(Borrowing borrowing);
        T? GetActiveByCopyId(int bookCopyId);
        int GetActiveCountByMemberId(int memberId);
        T? UpdateBorrowing(Borrowing borrowing);
        IDbContextTransaction BeginTransaction();
    }
}