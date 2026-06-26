using System.Collections.Generic;
using LibraryManagementSystem.Models;

namespace LibraryManagementSystem.Interfaces
{
    public interface IBookService
    {
        bool AddBook(string title, string author,  int categoryId);
        bool AddMultipleCopies(int bookId, int count);
        IEnumerable<Book> ViewAvailableBooks();
        IEnumerable<Book> SearchBooks(string query);
        bool MarkCopyAsUnavailable(int bookCopyId);
    }
}