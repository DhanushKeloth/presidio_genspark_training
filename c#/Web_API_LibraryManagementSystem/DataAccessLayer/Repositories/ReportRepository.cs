using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using LibraryManagementSystem.Interfaces;

namespace LibraryManagementSystem.Repositories
{
    public class ReportRepository : IReportRepository
    {
        private readonly LibraryDbContext _context;

        public ReportRepository(LibraryDbContext context)
        {
            _context = context;
        }

        // 1. Books currently borrowed (ReturnDate is null)
        public IEnumerable<Borrowing> GetCurrentlyBorrowedBooks()
        {
            return _context.Borrowings
                .Include(b => b.Member)
                .Include(b => b.BookCopy)
                .ThenInclude(c => c.Book)
                .Where(b => b.ReturnDate == null)
                .ToList();
        }

        // 2. Overdue books (ReturnDate is null AND DueDate < Now)
        public IEnumerable<Borrowing> GetOverdueBooks()
        {
            return _context.Borrowings
                .Include(b => b.Member)
                .Include(b => b.BookCopy)
                .ThenInclude(c => c.Book)
                .Where(b => b.ReturnDate == null && b.DueDate < DateTime.UtcNow)
                .ToList();
        }

        // 3. Members with pending fines
        public IEnumerable<object> GetMembersWithPendingFines()
        {
            return _context.FinePayments
                .Where(f => !f.IsPaid)
                .GroupBy(f => f.MemberId)
                .Select(g => new
                {
                    MemberId = g.Key,
                    MemberName = _context.Members.Where(m => m.MemberId == g.Key).Select(m => m.Name).FirstOrDefault(),
                    TotalOwed = g.Sum(f => f.Amount)
                })
                .Where(x => x.TotalOwed > 0)
                .ToList();
        }

        // 4. Most borrowed books (Ranked by loan transaction counts)
        public IEnumerable<object> GetMostBorrowedBooks(int topCount = 5)
        {
            return _context.Borrowings
                .GroupBy(b => b.BookId)
                .Select(g => new
                {
                    BookId = g.Key,
                    Title = _context.Books.Where(b => b.BookId == g.Key).Select(b => b.Title).FirstOrDefault(),
                    BorrowCount = g.Count()
                })
                .OrderByDescending(x => x.BorrowCount)
                .Take(topCount)
                .ToList();
        }

        // 5. Available books grouped by category
        public IEnumerable<object> GetAvailableBooksByCategory()
        {
            // Grouping individual physical book copies that are marked "Available"
            return _context.BookCopies
                .Where(c => c.Status == Enums.CopyStatus.Available)
                .GroupBy(c => c.Book.Category) // Assumes your Book model has a 'Category' string/enum
                .Select(g => new
                {
                    CategoryName = g.Key.ToString(),
                    AvailableCount = g.Count()
                })
                .ToList();
        }

        // 6. Complete borrowing history of a specific member
        public IEnumerable<Borrowing> GetMemberBorrowingHistory(int memberId)
        {
            return _context.Borrowings
                .Include(b => b.BookCopy)
                .ThenInclude(c => c.Book)
                .Where(b => b.MemberId == memberId)
                .OrderByDescending(b => b.BorrowDate)
                .ToList();
        }
    }
}