using Models;
using DataAccessLayer;
using Interfaces;
using Models.Exceptions;

using System.Text.RegularExpressions;

namespace BusinessLayer
{

    public class NotificationService
    {
        private readonly UserRepository<int, User> _userrepo;
        private readonly NotificationRepository<string, Notification> _notifrepo;
        private readonly IEnumerable<INotification> _senders;
        public NotificationService(
            UserRepository<int, User> userrepo,
            NotificationRepository<string, Notification> notifrepo,
            IEnumerable<INotification> senders)
        {
            _userrepo = userrepo;
            _notifrepo = notifrepo;
            _senders = senders;
        }
        public void SendNotification(INotification inotification, User user, Notification notification)
        {
            inotification.Send(user, notification);
        }
        public void ProcessNotification(int userId, string msg, string type)
        {
            if (string.IsNullOrWhiteSpace(msg))
            {
                throw new ValidException("message should not be empty");
            }
            if (msg.Length < 5)
            {
                throw new ValidException("message length should be atleast 5 characters");
            }
            var user = _userrepo.GetUser(userId) ?? throw new UserNotFoundException(userId);
            var sender = _senders.FirstOrDefault(s => s.GetType().Name.StartsWith(type, StringComparison.OrdinalIgnoreCase))
                         ?? throw new ValidException($"Notification type '{type}' is not supported.");

            if (type.ToLower() == "email")
            {
                EmailValidator.Validate(user.email);
            }
            if (type.ToLower() == "sms")
            {
                if (msg.Length > 160)
                {
                    throw new ValidException($"SMS message is too long ({msg.Length} characters). The limit is 160.");
                }
                PhoneValidator.Validate(user.email);
            }
            var notification = new Notification(message:msg,sentDate:DateTime.Now);

            sender.Send(user, notification); // Send it!
            _notifrepo.AddNotification(notification);
        }

    }
}
public class EmailValidator
{

    private static readonly Regex EmailRegex = new Regex(
        @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static void Validate(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ValidException("Email cannot be empty.");

        if (!EmailRegex.IsMatch(email))
            throw new ValidException($"'{email}' is not a valid email format.");
    }
}

public class PhoneValidator
{
    private static readonly Regex PhoneRegex = new Regex(
        @"^\+?[1-9]\d{1,14}$",
        RegexOptions.Compiled);

    public static void Validate(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            throw new ValidException("Phone number cannot be empty for SMS notifications.");

        string cleanedNumber = phoneNumber.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");

        if (!PhoneRegex.IsMatch(cleanedNumber))
            throw new ValidException($"'{phoneNumber}' is not a valid phone number format.");
    }
}