using System.Collections.Generic;
using LibraryManagementSystem.Interfaces;
using LibraryManagementSystem.Models;
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

        public Book AddBook(Book book)
        {
            // Validations from your business layer
            BookValidator.ValidateTitle(book.Title);
            BookValidator.ValidateAuthor(book.Author);
            BookValidator.ValidateCopies(book.AvailableCopies);
            var newBook = new Book
            {
                Title = book.Title,
                Author = book.Author,
                ISBN = book.ISBN,
                PublishedYear = book.PublishedYear,
                AvailableCopies = book.AvailableCopies
            };

            return _bookRepo.AddBook(newBook);
        }

        public IEnumerable<Book> GetBooks()
        {
            return _bookRepo.GetAllBooks();
        }

        public IEnumerable<Book> SearchBooks(string query)
        {
            var result= _bookRepo.SearchBooks(query);
            return result;
        }

        public Book? GetBookById(int bookId)
        {
            return _bookRepo.GetBookById(bookId);
        }
    }
}