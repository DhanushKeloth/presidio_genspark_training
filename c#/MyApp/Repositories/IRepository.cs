internal interface IRepository<K,T> where T: class
{
    public T Create(T item);
    public T? GetAccount(K key);
    public T? Update(K key,T item);
    // public T DeleteAccount(K key);
}