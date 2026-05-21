using System.Collections.Generic;
using LibraryManagementSystem.Models;

namespace LibraryManagementSystem.Interfaces
{
    public interface IBookService
    {
        Book AddBook(Book item);
        IEnumerable<Book> GetBooks();
        IEnumerable<Book> SearchBooks(string query);
        Book? GetBookById(int bookId);
    }
}
