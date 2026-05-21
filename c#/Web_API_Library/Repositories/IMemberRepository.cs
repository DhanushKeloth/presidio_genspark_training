using System.Collections.Generic;
using LibraryManagementSystem.Models;

namespace LibraryManagementSystem.Interfaces
{
    public interface IMemberRepository<K, T> where T : class
    {
        public T? AddMember(T item);
        public T? GetById(K key);
        public IEnumerable<T> GetAll();
        public T? GetByContact(string contact); // key can be Email or Phone number
        public T? RemoveMember(K key);
    }
}