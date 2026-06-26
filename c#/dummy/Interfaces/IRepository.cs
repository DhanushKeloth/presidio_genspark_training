namespace Interfaces
{
    public interface IRepository<K,T> where T: class
    {
        public T CreateUser(K key,T item);
        public T? GetUser(K key);
        public List<T>? GetUsers();
        public T? UpdateUser(K key,T item);
        public T? DeleteUser(K key);
        
    }
}