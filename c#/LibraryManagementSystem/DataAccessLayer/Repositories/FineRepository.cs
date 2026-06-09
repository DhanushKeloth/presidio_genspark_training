using System;
using System.Collections.Generic;
using System.Linq;
using LibraryManagementSystem.Data;
using LibraryManagementSystem.Interfaces;
using LibraryManagementSystem.Models;

namespace LibraryManagementSystem.Repositories
{
    public class FineRepository : IFineRepository<int, FinePayment>
    {
        private readonly LibraryDbContext _context;

        public FineRepository(LibraryDbContext context)
        {
            _context = context;
        }

        public FinePayment? AddMember(FinePayment item)
        {
            _context.FinePayments.Add(item);
            _context.SaveChanges();
            return item;
        }

        public FinePayment? GetMemberById(int finePaymentId)
        {
            return _context.FinePayments.FirstOrDefault(f => f.FinePaymentId == finePaymentId);
        }

        public IEnumerable<FinePayment> GetHistoryByMemberId(int memberId)
        {
            return _context.FinePayments
                .Where(f => f.MemberId == memberId)
                .OrderByDescending(f => f.PaymentDate)
                .ToList();
        }

        public IEnumerable<FinePayment> GetUnpaidFines()
        {
            // Pulls active borrowing logs that currently carry a generated fine balance 
            // but haven't been settled yet. If you have an IsPaid flag on a Fine table, use that here.
            return _context.FinePayments
                .Where(f => !f.IsPaid)
                .ToList();
        }

        public FinePayment? UpdateMember(FinePayment item)
        {
            _context.FinePayments.Update(item);
            _context.SaveChanges();
            return item;
        }

       public decimal GetTotalUnpaidFine(int memberId)
        {
            //  Sum all outstanding fine amounts directly from the FinePayments table rows!
            return _context.FinePayments
                .Where(f => f.MemberId == memberId && !f.IsPaid)
                .Sum(f => f.Amount); // Assumes AmountPaid represents the fine value when IsPaid is false
        }
    }
}