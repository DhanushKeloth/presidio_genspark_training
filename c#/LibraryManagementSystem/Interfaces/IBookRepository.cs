using System.Collections.Generic;
using LibraryManagementSystem.Models;

namespace LibraryManagementSystem.Interfaces
{
    public interface IBookRepository< T> where T : class
    {
        T AddBook(T item);
        void AddMultipleCopy(BookCopy copy);
        IEnumerable<Book> GetAvailableBooks();
        IEnumerable<Book> SearchBooks(string query);
        public Book? GetBookById(int bookId);
        BookCopy? GetCopyById(int bookCopyId);
        void UpdateCopy(BookCopy copy);
    }
}