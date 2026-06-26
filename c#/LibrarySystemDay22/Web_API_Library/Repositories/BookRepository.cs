using System.Collections.Generic;
using System.Linq;
using LibraryManagementSystem.Models;
using LibraryManagementSystem.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagementSystem.Repositories
{
    public class BookRepository : IBookRepository<Book>
    {
        private readonly LibraryDbContext _context;

        public BookRepository(LibraryDbContext context)
        {
            _context = context;
        }

        public Book AddBook(Book item)
        {
            _context.Books.Add(item);
            _context.SaveChanges();
            return item;
        }

        public IEnumerable<Book> GetAllBooks()
        {
            return _context.Books.Where(b => b.AvailableCopies > 0).ToList();
        }

        public IEnumerable<Book> SearchBooks(string query)
        {
            return _context.Books
                .Where(b => b.Title.Contains(query) || b.Author.Contains(query) || b.ISBN.Contains(query))
                .ToList();
        }

        public Book? GetBookById(int bookId)
        {
            return _context.Books.Find(bookId);
        }
    }
}