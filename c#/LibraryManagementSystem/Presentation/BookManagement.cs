using System;
using System.Collections.Generic;
using System.Linq;
using LibraryManagementSystem.Interfaces;
using LibraryManagementSystem.Models;
using LibraryManagementSystem.Exceptions;
using LibraryManagementSystem.BusinessLayer;
using LibraryManagementSystem.Enums;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagementSystem.Presentation
{
    public class BookManagement
    {
        private readonly IBookService _bookService;

        public BookManagement(IBookService bookService)
        {
            _bookService = bookService;
        }

        public void Run()
        {
            while (true)
            {
                DisplayMenu();
                string choice = Console.ReadLine() ?? "";

                switch (choice)
                {
                    case "1":
                        HandleAddBook();
                        break;
                    case "2":
                        HandleAddCopies();
                        break;
                    case "3":
                        HandleViewAvailable();
                        break;
                    case "4":
                        HandleSearchBooks();
                        break;
                    case "5":
                        HandleDecommissionCopy();
                        break;
                    case "6":
                        Console.WriteLine("Returning to main engine controller...");
                        return;
                    default:
                        Console.WriteLine(" Invalid selection option. Please choose options 1-6.");
                        break;
                }
            }
        }

        private void DisplayMenu()
        {
            Console.WriteLine("\n=== CATALOG & BOOK MANAGEMENT ===");
            Console.WriteLine("1. Add New  Book ");
            Console.WriteLine("2. Add Copies to Existing Book Inventory");
            Console.WriteLine("3. View Active/Available Catalog Items");
            Console.WriteLine("4. Search Books Catalog");
            Console.WriteLine("5. Flag Book Copy as Unavailable");
            Console.WriteLine("6. Back to Main Menu");
            Console.Write("Select an option: ");
        }

        private void HandleAddBook()
        {
            string title = PromptUntilValid("Enter Book Title: ", BookValidator.ValidateTitle);
            string author = PromptUntilValid("Enter Author Name: ", BookValidator.ValidateAuthor);
            int categoryId = PromptForInteger("Enter Category ID Code: ");

            try
            {
                bool complete = _bookService.AddBook(title, author, categoryId);
                if (complete) Console.WriteLine("\n Master book record appended to catalog successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n Catalog Failure: {ex.Message}");
            }
        }

        private void HandleAddCopies()
        {
            int bookId = PromptForInteger("Enter Target Catalog Book ID: ");
            int count = PromptForInteger("Enter Quantity of Physical Copies to Add: ");

            try
            {
                bool complete = _bookService.AddMultipleCopies(bookId, count);
                if (complete) Console.WriteLine($"\n Successfully generated {count} inventory tracks for Book ID {bookId}.");
            }
            catch (LibraryException ex)
            {
                Console.WriteLine($"error cannot add book copy {ex.Message}");
            }
            catch (DbUpdateException ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine($"\n Database Error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"DB : {ex.InnerException.Message}");
                }
            }
        }

        private void HandleViewAvailable()
        {
            var books = _bookService.ViewAvailableBooks();
            PrintBookRecords(books, "--- Displaying Available Catalog Material ---");
        }

        private void HandleSearchBooks()
        {
            Console.Write("Enter your lookup parameter (Title or Author match context): ");
            string query = Console.ReadLine() ?? "";

            var books = _bookService.SearchBooks(query);
            PrintBookRecords(books, $"--- Search Matches matching criteria: '{query}' ---");
        }

        private void HandleDecommissionCopy()
        {
            int copyId = PromptForInteger("Enter Specific Physical Book Copy ID Code: ");

            try
            {
                bool complete = _bookService.MarkCopyAsUnavailable(copyId);
                if (complete) Console.WriteLine($"\n Physical book copy unit #{copyId} marked as Unavailable.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n Inventory Modification Blocked: {ex.Message}");
            }
        }

        private string PromptUntilValid(string promptText, Action<string> validationStep)
        {
            while (true)
            {
                Console.Write(promptText);
                string input = Console.ReadLine() ?? "";
                try
                {
                    validationStep(input);
                    return input;
                }
                catch (ValidException ex)
                {
                    Console.WriteLine($" {ex.Message} Try again.");
                }
            }
        }

        private int PromptForInteger(string promptText)
        {
            while (true)
            {
                Console.Write(promptText);
                if (int.TryParse(Console.ReadLine(), out int result) && result >= 0)
                {
                    return result;
                }
                Console.WriteLine(" Numeric formatting required. Provide a valid positive integer value.");
            }
        }

        private void PrintBookRecords(IEnumerable<Book> records, string headerLabel)
        {
            Console.WriteLine($"\n{headerLabel}");
            int itemCounter = 0;
            foreach (var b in records)
            {
                itemCounter++;
                int copyCount = b.BookCopies?.Count(c => c.Status == CopyStatus.Available) ?? 0;
                // Updated display format string removing ISBN reference
                Console.WriteLine($"ID: {b.BookId} | Title: {b.Title} | Author: {b.Author} [Copies Available: {copyCount}]");
            }
            if (itemCounter == 0) Console.WriteLine("No records matching the target request were found.");
        }
    }
}