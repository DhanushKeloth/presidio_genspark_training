using System.Runtime.InteropServices;
using NotificationSystem.Interfaces;

namespace NotificationSystem.Repositories
{
    internal class UserRepository<K,T>: IRepository<K,T> where T:class where K:notnull
    {
        private readonly Dictionary<K,T> _users = new Dictionary<K, T>();
         public T CreateUser(K key,T item)
        {
            if (_users.ContainsKey(key))
            {
                Console.WriteLine("user with the same key already exists");
                return _users[key];
            }
            _users.Add(key,item);
            return item;
        }
        public T? GetUser(K key)
        {
            if (_users.ContainsKey(key))
            {
                return _users[key];
            }
            return null;
        }
        public List<T>? GetUsers()
        {
            if(_users.Count==0) return null;
            var userslist = _users.Values.ToList();
            return userslist;   
        }
        public T? UpdateUser(K key,T item)
        {
            if(_users[key]==null)
                return null;
            _users[key]=item;
            return item;   
        }
        public T? DeleteUser(K key)
        {
            var user = _users[key];
            if (user == null)
            {
                return null;
            }
            return user;
        }
    }
}