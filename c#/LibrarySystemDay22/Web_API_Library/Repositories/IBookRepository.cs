using System.Collections.Generic;
using LibraryManagementSystem.Models;

namespace LibraryManagementSystem.Interfaces
{
    public interface IBookRepository< T> where T : class
    {
        T AddBook(T item);
        IEnumerable<T> GetAllBooks();
        IEnumerable<T> SearchBooks(string query);
        public T? GetBookById(int bookId);
       
    }
}