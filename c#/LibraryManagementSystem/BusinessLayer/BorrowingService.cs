using System;
using System.Linq;
using LibraryManagementSystem.Interfaces;
using LibraryManagementSystem.Models;
using LibraryManagementSystem.Exceptions;
using LibraryManagementSystem.Enums;

namespace LibraryManagementSystem.Services
{
    public class BorrowingService : IBorrowingService
    {
        private readonly IBorrowingRepository<Borrowing> _borrowingRepo;
        private readonly IBookRepository<Book> _bookRepo;
        private readonly IMemberRepository<int, Member> _memberRepo;
        private readonly IFineRepository<int, FinePayment> _fineRepo;

        public BorrowingService(
            IBorrowingRepository<Borrowing> borrowingRepo,
            IBookRepository<Book> bookRepo,
            IMemberRepository<int, Member> memberRepo,
            IFineRepository<int, FinePayment> fineRepo)
        {
            _borrowingRepo = borrowingRepo;
            _bookRepo = bookRepo;
            _memberRepo = memberRepo;
            _fineRepo = fineRepo;
        }

        public bool BorrowBook(int memberId, int bookId)
        {
            var member = _memberRepo.GetById(memberId)
                ?? throw new RecordNotFoundException($"Member with ID {memberId} does not exist.");

            Book? masterBook = _bookRepo.GetBookById(bookId);
            if (masterBook == null)
            {
                throw new RecordNotFoundException($"Book profile ID {bookId} does not exist.");
            }

            // Extract the physical copy safely
            BookCopy? availableCopy = masterBook.BookCopies?.FirstOrDefault(c => c.Status == CopyStatus.Available);
            if (availableCopy == null)
            {
                throw new BusinessRuleException("Transaction Denied: No active copies of this book are currently available on the shelf.");
            }

            if (!member.IsActive)
            {
                throw new BusinessRuleException(" Transaction Denied: This membership account is currently suspended or inactive.");
            }

            // Verify fine limit check via the fine repository summary lookup
            decimal currentFineBalance = _fineRepo.GetTotalUnpaidFine(memberId);
            if (currentFineBalance > 500.00m)
            {
                throw new BusinessRuleException($"Transaction Denied: Account blocked due to excessive unpaid fines (Current: ₹{currentFineBalance}). Limit is ₹500.");
            }

            var activeBorrowCount = _borrowingRepo.GetActiveCountByMemberId(memberId);

            if (member.Borrowings != null && member.Borrowings.Any(b => b.ReturnDate == null && b.BookCopy?.BookId == bookId))
            {
                throw new BusinessRuleException(" Transaction Denied: Member already has an identical copy of this title checked out.");
            }

            int maxBooks = 2;
            int maxDays = 7;

            switch (member.Membership)
            {
                case MembershipType.Student:
                    maxBooks = 3;
                    maxDays = 10;
                    break;
                case MembershipType.Premium:
                    maxBooks = 5;
                    maxDays = 15;
                    break;
                case MembershipType.Basic:
                default:
                    maxBooks = 2;
                    maxDays = 7;
                    break;
            }

            if (activeBorrowCount >= maxBooks)
            {
                throw new BusinessRuleException($"Transaction Denied: Quota exceeded. {member.Membership} tiers are limited to {maxBooks} concurrent loans.");
            }

            using (var transaction = _borrowingRepo.BeginTransaction())
            {
                try
                {
                    DateTime now = DateTime.Now;

                    var newBorrow = new Borrowing
                    {
                        MemberId = memberId,
                        BookCopyId = availableCopy.BookCopyId, 
                        BookId = bookId,
                        BorrowDate = now,
                        DueDate = now.AddDays(maxDays),
                        ReturnDate = null,
                        Status = BorrowingStatus.Active,
                        Member = member,
                        BookCopy = availableCopy
                    };

                    availableCopy.Status = CopyStatus.Unavailable;
                    _bookRepo.UpdateCopy(availableCopy);
                    _borrowingRepo.Add(newBorrow);

                    transaction.Commit();
                    Console.WriteLine($"\n✅ Receipt window: Allowed {maxDays} days. Due back: {newBorrow.DueDate.ToShortDateString()}");
                    return true;
                }
                catch (Exception)
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        public bool ReturnBook(int bookCopyId)
        {
            var activeBorrowing = _borrowingRepo.GetActiveByCopyId(bookCopyId)
                ?? throw new RecordNotFoundException($"No active lending logs found for physical book copy barcode #{bookCopyId}.");

            var copy = _bookRepo.GetCopyById(bookCopyId);

            using (var transaction = _borrowingRepo.BeginTransaction())
            {
                try
                {
                    DateTime returnTime = DateTime.Now;
                    
                    activeBorrowing.ReturnDate = returnTime;
                    activeBorrowing.Status = BorrowingStatus.Returned; 

                    // Process fine calculations dynamically
                    if (returnTime > activeBorrowing.DueDate)
                    {
                        TimeSpan delayDuration = returnTime - activeBorrowing.DueDate;
                        int delayedDays = (int)Math.Ceiling(delayDuration.TotalDays);

                        if (delayedDays > 0)
                        {
                            decimal calculatedFine = delayedDays * 10.00m; 
                            var penaltyRecord = new FinePayment
                            {
                                MemberId = activeBorrowing.MemberId,
                                BorrowingId = activeBorrowing.BorrowingId,
                                Amount = calculatedFine, 
                                PaymentDate = returnTime,
                                IsPaid = false,
                            };
                            
                            _fineRepo.AddMember(penaltyRecord);
                            
                            Console.WriteLine($"\n⚠️ LATE RETURN DETECTED: Book is overdue by {delayedDays} day(s). Generated fine: ₹{calculatedFine}.");
                        }
                    }

                    _borrowingRepo.UpdateBorrowing(activeBorrowing);

                    if (copy != null)
                    {
                        copy.Status = CopyStatus.Available;
                        _bookRepo.UpdateCopy(copy);
                    }

                    transaction.Commit();
                    Console.WriteLine("Success: Book processed and re-shelved successfully.");
                    return true;
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    Console.WriteLine($"\nDATABASE ERROR: {ex.InnerException?.Message ?? ex.Message}"); 
                    throw;
                }
            }
        }
    }
}