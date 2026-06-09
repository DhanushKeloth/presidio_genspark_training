using System.Collections.Generic;
using System.Linq;
using LibraryManagementSystem.Interfaces;
using LibraryManagementSystem.Models;
using LibraryManagementSystem.Exceptions;
using LibraryManagementSystem.BusinessLayer;

namespace LibraryManagementSystem.Services
{
    public class BookService : IBookService
    {
        private readonly IBookRepository<Book> _bookRepo;

        public BookService(IBookRepository<Book> bookRepo)
        {
            _bookRepo = bookRepo;
        }

        public bool AddBook(string title, string author, int categoryId)
        {
            BookValidator.ValidateTitle(title);
            BookValidator.ValidateAuthor(author);




            var newBook = new Book
            {
                Title = title,
                Author = author,
                CategoryId = categoryId
            };

            var savedBook = _bookRepo.AddBook(newBook);
            return savedBook != null;
        }

        public bool AddMultipleCopies(int bookId, int count)
        {
            if (count <= 0)
            {
                throw new ValidException("Count must be at least 1 copy.");
            }

            // Verify target book entity exists
            var targetBook = _bookRepo.SearchBooks("").FirstOrDefault(b => b.BookId == bookId);
            if (targetBook == null)
            {
                throw new RecordNotFoundException($"Book reference ID {bookId} does not exist.");
            }

            // Execute loop block to populate identical copy records
            for (int i = 0; i < count; i++)
            {
                // Generates a quick 4-character unique suffix
                string uniqueSuffix = Guid.NewGuid().ToString().Substring(0, 4).ToUpper();

                var newCopy = new BookCopy
                {
                    BookId = bookId,
                    Status = Enums.CopyStatus.Available,
                    // ADD THIS LINE: Populate the mandatory database column
                    SerialNumber = $"SN-{bookId}-{i + 1}-{uniqueSuffix}"
                };
                _bookRepo.AddMultipleCopy(newCopy);
            }
            return true;
        }

        public IEnumerable<Book> ViewAvailableBooks()
        {
            return _bookRepo.GetAvailableBooks();
        }

        public IEnumerable<Book> SearchBooks(string query)
        {
            return _bookRepo.SearchBooks(query);
        }

        public bool MarkCopyAsUnavailable(int bookCopyId)
        {


            var copy = _bookRepo.GetCopyById(bookCopyId);
            if (copy == null)
            {
                throw new RecordNotFoundException($"Inventory book copy tracking ID {bookCopyId} was not found.");
            }

            copy.Status = Enums.CopyStatus.Unavailable;


            _bookRepo.UpdateCopy(copy);
            return true;
        }
    }
}