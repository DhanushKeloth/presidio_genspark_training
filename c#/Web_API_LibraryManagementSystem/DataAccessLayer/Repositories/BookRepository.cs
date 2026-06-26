using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using LibraryManagementSystem.Data;
using LibraryManagementSystem.Interfaces;
using LibraryManagementSystem.Models;

namespace LibraryManagementSystem.Repositories
{
    public class BookRepository : IBookRepository< Book>
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

        public void AddMultipleCopy(BookCopy copy)
        {
            try
            {
            _context.BookCopies.Add(copy);
            _context.SaveChanges();
                
            }catch(Exception ex)
            {
                Console.WriteLine("error in adding copies",ex.Message);
            }
        }

        public IEnumerable<Book> GetAvailableBooks()
        {
            return _context.Books
                .Include(b => b.BookCopies)
                .Where(b => b.BookCopies.Any(c => c.Status == Enums.CopyStatus.Available))
                .ToList();
        }

        public IEnumerable<Book> SearchBooks(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return _context.Books.ToList();

            string lowerQuery = query.ToLower();

            return _context.Books
                .Include(b => b.BookCopies)
                .Where(b => b.Title.ToLower().Contains(lowerQuery) || 
                            b.Author.ToLower().Contains(lowerQuery))
                .ToList();
        }

        public BookCopy? GetCopyById(int bookCopyId)
        {
            return _context.BookCopies.Find(bookCopyId);
        }
        public Book? GetBookById(int bookId)
        {
            return _context.Books
                .Include(b => b.BookCopies) // Joins the tables so copies are loaded into memory
                .FirstOrDefault(b => b.BookId == bookId);
        }

        public void UpdateCopy(BookCopy copy)
        {
            _context.BookCopies.Update(copy);
            _context.SaveChanges();
        }
    }
}