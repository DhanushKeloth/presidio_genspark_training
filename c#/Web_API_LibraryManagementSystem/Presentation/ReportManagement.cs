using System;
using LibraryManagementSystem.Interfaces;

namespace LibraryManagementSystem.Presentation
{
    public class ReportManagement
    {
        private readonly IReportService _reportService;

        public ReportManagement(IReportService reportService)
        {
            _reportService = reportService;
        }

        public void DisplayReportMenu()
        {
            while (true)
            {
                Console.Clear();
                
                Console.WriteLine("==================================================");
                Console.WriteLine(" 1. Books Currently Borrowed");
                Console.WriteLine(" 2. Overdue Books Tracker");
                Console.WriteLine(" 3. Members with Pending Fines Summary");
                Console.WriteLine(" 4. Top 5 Most Borrowed Titles");
                Console.WriteLine(" 5. Available Book Stock Counts by Category");
                Console.WriteLine(" 6. Individual Member Lending History Audit");
                Console.WriteLine(" 7. Return to Main Control Panel");
                Console.WriteLine("==================================================");
                Console.Write("Enter the choice");

                string choice = Console.ReadLine();
                Console.Clear();
                Console.WriteLine($"--- Report Output: Option {choice} ---");

                try
                {
                    switch (choice)
                    {
                        case "1":
                            _reportService.GenerateCurrentlyBorrowedReport();
                            break;
                        case "2":
                            _reportService.GenerateOverdueReport();
                            break;
                        case "3":
                            _reportService.GeneratePendingFinesReport();
                            break;
                        case "4":
                            _reportService.GenerateMostBorrowedReport();
                            break;
                        case "5":
                            _reportService.GenerateCategoryAvailabilityReport();
                            break;
                        case "6":
                            Console.Write("Enter Member ID to query: ");
                            if (int.TryParse(Console.ReadLine(), out int mid))
                                _reportService.GenerateMemberHistoryReport(mid);
                            else
                                Console.WriteLine("Invalid ID format.");
                            break;
                        case "7":
                            return;
                        default:
                            Console.WriteLine(" Invalid choice. Select an option from 1 to 7.");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to render report dataset: {ex.Message}");
                }

                Console.WriteLine("\nPress any key to return to reports menu...");
                Console.ReadKey();
            }
        }
    }
}