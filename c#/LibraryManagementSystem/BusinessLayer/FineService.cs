using System;
using System.Collections.Generic;
using System.Linq;
using LibraryManagementSystem.Interfaces;
using LibraryManagementSystem.Models;
using LibraryManagementSystem.Exceptions;
using LibraryManagementSystem.Repositories;

namespace LibraryManagementSystem.Services
{
    public class FineService : IFineService
    {
        private readonly IFineRepository<int, FinePayment> _fineRepo;
        private readonly IMemberRepository<int,Member> _memberRepo;
        private readonly IBorrowingRepository<Borrowing> _borrowingRepo; 

        public FineService(
            IFineRepository<int, FinePayment> fineRepo, 
            IMemberRepository<int,Member> memberRepo,
            IBorrowingRepository<Borrowing> borrowingRepo)
        {
            _fineRepo = fineRepo;
            _memberRepo = memberRepo;
            _borrowingRepo = borrowingRepo;
        }

        public decimal CheckPendingBalance(int memberId)
        {
            var member = _memberRepo.GetById(memberId)
                ?? throw new RecordNotFoundException($"Member ID {memberId} does not exist.");

            return _fineRepo.GetTotalUnpaidFine(memberId);
        }

        public bool ProcessFinePayment(int memberId, decimal amount)
        {
            if (amount <= 0)
            {
                throw new BusinessRuleException("Payment Denied: Payment amount must be greater than ₹0.");
            }

            var member = _memberRepo.GetById(memberId)
                ?? throw new RecordNotFoundException($"Member ID {memberId} does not exist.");

            decimal currentBalance = _fineRepo.GetTotalUnpaidFine(memberId);

            if (currentBalance == 0)
            {
                throw new BusinessRuleException(" Account Clear: This member currently has no outstanding fine balances.");
            }

            if (amount > currentBalance)
            {
                throw new BusinessRuleException($"Payment Denied: Attempted to pay ₹{amount} on a total balance of ₹{currentBalance}.");
            }

            using (var transaction = _borrowingRepo.BeginTransaction())
            {
                try
                {
                    var unpaidFines = _fineRepo.GetHistoryByMemberId(memberId)
                        .Where(f => !f.IsPaid)
                        .OrderBy(f => f.PaymentDate) 
                        .ToList();

                    decimal remainingPaymentToApply = amount;

                    foreach (var fine in unpaidFines)
                    {
                        if (remainingPaymentToApply <= 0) break;
                        if (remainingPaymentToApply >= fine.Amount)
                        {
                            remainingPaymentToApply -= fine.Amount;
                            fine.IsPaid = true; 
                            _fineRepo.UpdateMember(fine);
                        }
                        else
                        {
                            fine.Amount -= remainingPaymentToApply;
                            remainingPaymentToApply = 0;
                            _fineRepo.UpdateMember(fine);
                        }
                    }

                    transaction.Commit();
                    Console.WriteLine($"\n💰 Cashier Window: Successfully processed payment of ₹{amount}. Remaining Balance: ₹{currentBalance - amount}");
                    return true;
                }
                catch (Exception)
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        public IEnumerable<FinePayment> ViewMemberFineHistory(int memberId)
        {
            var member = _memberRepo.GetById(memberId)
                ?? throw new RecordNotFoundException($"Member ID {memberId} does not exist.");

            return _fineRepo.GetHistoryByMemberId(memberId);
        }

        public IEnumerable<FinePayment> ViewAllUnpaidFines()
        {
            return _fineRepo.GetUnpaidFines();
        }
    }
}