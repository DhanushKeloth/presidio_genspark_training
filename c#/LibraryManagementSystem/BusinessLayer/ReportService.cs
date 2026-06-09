using System;
using LibraryManagementSystem.Interfaces;
using LibraryManagementSystem.Models;

namespace LibraryManagementSystem.Services
{
    public class ReportService : IReportService
    {
        private readonly IReportRepository _reportRepo;
        private readonly IMemberRepository<int,Member> _memberRepo;

        public ReportService(IReportRepository reportRepo, IMemberRepository<int,Member> memberRepo)
        {
            _reportRepo = reportRepo;
            _memberRepo = memberRepo;
        }

        public void GenerateCurrentlyBorrowedReport()
        {
            var data = _reportRepo.GetCurrentlyBorrowedBooks();
            Console.WriteLine("\n==================================================================================");
            Console.WriteLine(string.Format(" {0,-10} | {1,-25} | {2,-20} | {3,-12}", "Copy ID", "Book Title", "Borrowed By", "Due Date"));
            Console.WriteLine("==================================================================================");
            foreach (var b in data)
            {
                Console.WriteLine(string.Format(" #{0,-9} | {1,-25} | {2,-20} | {3,-12:d}", 
                    b.BookCopyId, b.BookCopy?.Book?.Title, b.Member?.Name, b.DueDate.ToLocalTime()));
            }
        }

        public void GenerateOverdueReport()
        {
            var data = _reportRepo.GetOverdueBooks();
            Console.WriteLine("\n==================================================================================");
            Console.WriteLine(string.Format(" {0,-10} | {1,-25} | {2,-20} | {3,-12} | {4,-8}", "Copy ID", "Book Title", "Member Name", "Due Date", "Days Late"));
            Console.WriteLine("==================================================================================");
            foreach (var b in data)
            {
                int daysLate = (int)Math.Ceiling((DateTime.UtcNow - b.DueDate).TotalDays);
                Console.WriteLine(string.Format(" #{0,-9} | {1,-25} | {2,-20} | {3,-12:d} | {4,-8} days", 
                    b.BookCopyId, b.BookCopy?.Book?.Title, b.Member?.Name, b.DueDate.ToLocalTime(), daysLate));
            }
        }

        public void GeneratePendingFinesReport()
        {
            var data = _reportRepo.GetMembersWithPendingFines();
            Console.WriteLine("\n==================================================");
            Console.WriteLine(string.Format(" {0,-12} | {1,-20} | {2,-12}", "Member ID", "Member Name", "Total Fine"));
            Console.WriteLine("==================================================");
            foreach (dynamic x in data)
            {
                Console.WriteLine(string.Format(" #{0,-11} | {1,-20} | ₹{2,-11:F2}", x.MemberId, x.MemberName, x.TotalOwed));
            }
        }

        public void GenerateMostBorrowedReport()
        {
            var data = _reportRepo.GetMostBorrowedBooks(5);
            Console.WriteLine("\n=============================================================");
            Console.WriteLine(string.Format(" {0,-10} | {1,-35} | {2,-10}", "Book ID", "Book Title", "Times Loaned"));
            Console.WriteLine("=============================================================");
            foreach (dynamic x in data)
            {
                Console.WriteLine(string.Format(" #{0,-9} | {1,-35} | {2,-10} checkouts", x.BookId, x.Title, x.BorrowCount));
            }
        }

        public void GenerateCategoryAvailabilityReport()
        {
            var data = _reportRepo.GetAvailableBooksByCategory();
            Console.WriteLine("\n==========================================");
            Console.WriteLine(string.Format(" {0,-25} | {1,-12}", "Genre / Category", "In-Stock Count"));
            Console.WriteLine("==========================================");
            foreach (dynamic x in data)
            {
                Console.WriteLine(string.Format(" {0,-25} | {1,-12} copies", x.CategoryName, x.AvailableCount));
            }
        }

        public void GenerateMemberHistoryReport(int memberId)
        {
            var member = _memberRepo.GetById(memberId);
            if (member == null) { Console.WriteLine("Member profile not found."); return; }

            var data = _reportRepo.GetMemberBorrowingHistory(memberId);
            Console.WriteLine($"\nLending Audit Trail Ledger For: {member.Name} (ID: #{memberId})");
            Console.WriteLine("==================================================================================");
            Console.WriteLine(string.Format(" {0,-10} | {1,-30} | {2,-12} | {3,-12} | {4,-10}", "Copy ID", "Book Title", "Checkout Date", "Return Date", "Status"));
            Console.WriteLine("==================================================================================");
            foreach (var b in data)
            {
                string retStr = b.ReturnDate.HasValue ? b.ReturnDate.Value.ToLocalTime().ToShortDateString() : "ACTIVE LOAN";
                Console.WriteLine(string.Format(" #{0,-9} | {1,-30} | {2,-12:d} | {3,-12} | {4,-10}", 
                    b.BookCopyId, b.BookCopy?.Book?.Title, b.BorrowDate.ToLocalTime(), retStr, b.Status));
            }
        }
    }
}