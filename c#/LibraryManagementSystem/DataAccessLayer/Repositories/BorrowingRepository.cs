using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using LibraryManagementSystem.Data;
using LibraryManagementSystem.Interfaces;
using LibraryManagementSystem.Models;

namespace LibraryManagementSystem.Repositories
{
    public class BorrowingRepository : IBorrowingRepository< Borrowing>
    {
        private readonly LibraryDbContext _context;

        public BorrowingRepository(LibraryDbContext context)
        {
            _context = context;
        }

        // Maps to: T? Add(Borrowing borrowing);    
        public Borrowing? Add(Borrowing borrowing)
        {
            _context.Borrowings.Add(borrowing);
            _context.SaveChanges();
            return borrowing;
        }

        // Maps to: T? GetActiveByCopyId(int bookCopyId);
        public Borrowing? GetActiveByCopyId(int bookCopyId)
        {
            return _context.Borrowings
                .FirstOrDefault(b => b.BookCopyId == bookCopyId && b.ReturnDate == null);
        }

        // Maps to: int GetActiveCountByMemberId(int memberId);
        public int GetActiveCountByMemberId(int memberId)
        {
            return _context.Borrowings
                .Count(b => b.MemberId == memberId && b.ReturnDate == null);
        }

        // Maps to: T? UpdateBorrowing(Borrowing borrowing);
        public Borrowing? UpdateBorrowing(Borrowing borrowing)
        {
            _context.Borrowings.Update(borrowing);
            _context.SaveChanges();
            return borrowing;
        }

        // Maps to: IDbContextTransaction BeginTransaction();
        public IDbContextTransaction BeginTransaction()
        {
            return _context.Database.BeginTransaction();
        }
    }
}