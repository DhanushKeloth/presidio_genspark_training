using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Runtime.InteropServices;
using DataAccessLayer.Contexts;
using Interfaces;
using Models;
using Models.Exceptions;
using Npgsql;

namespace DataAccessLayer
{
    public class UserRepository<K, T> : IRepository<K, T> where T : class where K : notnull
    {
        private readonly NotificationDbContext _context;
        public UserRepository(NotificationDbContext context)
        {
            _context = context;
        }
        // private readonly Dictionary<K, T> _users = new Dictionary<K, T>();
        public T CreateUser(K key, T item)
        {
            if (item is not User user)
            {
                throw new InvalidOperationException("item is not a valid user object");
            }
            try
            {
                _context.Set<T>().Add(item);
                _context.SaveChanges();
                Console.WriteLine("inserted the user values successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return item;
        }
        public T? GetUser(K key)
        {
            var item = _context.Set<T>().Find(key);
            if (item == null)
            {
                throw new UserNotFoundException($"user not found with key {key}");
            }
            return item;
        }
        public List<T>? GetUsers()
        {
            try
            {

                var usersList = _context.Set<T>().ToList();
                if (usersList.Count == 0)
                {
                    throw new InvalidOperationException("no users found");
                }
                return usersList;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return new List<T>();
            }
        }
        public T? UpdateUser(K key, T item)
        {
            try
            {
                _context.Set<T>().Update(item);
                int updatedrows = _context.SaveChanges();
                if (updatedrows > 0)
                {
                    Console.WriteLine($"updated the user with id {key}");
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return item;
        }
        public T? DeleteUser(K key)
        {
            T? userToDelete = GetUser(key);
            if (userToDelete == null)
            {
                throw new UserNotFoundException($"user with id {key} not found");

            }
            try
            {
                _context.Set<T>().Remove(userToDelete);
                int deletedrows = _context.SaveChanges();
                if (deletedrows > 0)
                {
                    Console.WriteLine($"deleted user with id {key}");
                }
            }catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return userToDelete;
        }
    }
}