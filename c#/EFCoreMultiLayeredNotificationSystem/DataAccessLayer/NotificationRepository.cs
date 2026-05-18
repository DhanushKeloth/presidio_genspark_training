using System.Linq.Expressions;
using DataAccessLayer.Contexts;
using Interfaces;
using Models;
using Models.Exceptions;
using Npgsql;
namespace DataAccessLayer
{

    public class NotificationRepository<K, T> : INotificationRepository<K, T> where T : class
    {

        // private readonly List<T> _notifications = new List<T>();
        private readonly NotificationDbContext _context;
        public NotificationRepository(NotificationDbContext context)
        {
            _context=context;
        }
        public T AddNotification(T item)
        {
            try
            {
                _context.Set<T>().Add(item);
                _context.SaveChanges();
                Console.WriteLine("inserted notification into database");
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return item;
        }

        public List<T>? GetNotifications()
        {
            try
            {
                return _context.Set<T>().ToList();
            }catch(Exception ex)
            {
                Console.WriteLine("error fetching notifications",ex.Message);
                return new List<T>();
            }
            
           
        }

    }
}


// dotnet ef dbcontext scaffold "Host=localhost;Port=5432;Database=notification_db;Username=dhanushkeloth;Password=1234" Npgsql.EntityFrameworkCore.PostgreSQL -p DataAccessLayer/DataAccessLayer.csproj -s Presentation/Presentation.csproj -o Contexts -c NotificationDbContext --force