using System;
using LibraryManagementSystem.Interfaces;
using LibraryManagementSystem.Exceptions;

namespace LibraryManagementSystem.Presentation
{
    public class BorrowManagement
    {
        private readonly IBorrowingService _borrowingService;

        public BorrowManagement(IBorrowingService borrowingService)
        {
            _borrowingService = borrowingService;
        }

        public void Run()
        {
            while (true)
            {
                Console.WriteLine("\n===  BORROW MANAGEMENT  ===");
                Console.WriteLine("1. Borrow / Issue a Book");
                Console.WriteLine("2. Return / Re-shelve a Book Copy");
                Console.WriteLine("3. Back to Main Dashboard");
                Console.Write("Select an option: ");

                string choice = Console.ReadLine() ?? "";
                switch (choice)
                {
                    case "1":
                        HandleBorrow();
                        break;
                    case "2":
                        HandleReturn();
                        break;
                    case "3":
                        return; // Exits loop, goes back to main program menu
                    default:
                        Console.WriteLine(" Selection error. Choose a valid option (1-3).");
                        break;
                }
            }
        }

        private void HandleBorrow()
        {
            Console.WriteLine("\n--- Issue New Book Loan ---");
            int memberId = PromptForInteger("Enter Cardholder Member ID: ");
            int bookId = PromptForInteger("Enter Master Book ID to find copy: ");

            try
            {
                // Calls the business logic service we built
                bool complete = _borrowingService.BorrowBook(memberId, bookId);
                if (complete)
                {
                    Console.WriteLine("\n✅ Success: System issued book! Physical copy has been flagged as checked out.");
                }
            }
            catch (LibraryException ex)
            {
                // Cleanly catches your custom rule exemptions (e.g. quota limits, inactive status)
                Console.WriteLine($"\n Rule Validation Blocked: {ex.Message}");
            }
            catch (Exception ex)
            {
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"🔍 Exact Database Error: {ex.InnerException.Message}");
                }
                Console.WriteLine($"\n Fatal Processing Defect: {ex.Message}");
            }
        }

        private void HandleReturn()
        {
            Console.WriteLine("\n--- Process Book Return ---");
            int copyId = PromptForInteger("Scan/Enter the Physical Book Copy ID: ");

            try
            {
                bool complete = _borrowingService.ReturnBook(copyId);
                if (complete)
                {
                    Console.WriteLine("\n✅ Success: Return log saved! Physical copy status is back to 'Available'.");
                }
            }
            catch (LibraryException ex)
            {
                Console.WriteLine($"\n Return processing failure: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n Fatal Processing Defect: {ex.Message}");
            }
        }

        private int PromptForInteger(string message)
        {
            while (true)
            {
                Console.Write(message);
                if (int.TryParse(Console.ReadLine(), out int result) && result > 0)
                {
                    return result;
                }
                Console.WriteLine(" Invalid input. Please enter a valid positive integer number.");
            }
        }
    }
}