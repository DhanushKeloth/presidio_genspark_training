using Models;
namespace Interfaces
{

    public interface INotification
    {
        public void Send(User user, Notification notification);
    }

}