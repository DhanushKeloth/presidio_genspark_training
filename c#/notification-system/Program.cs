using System;
using NotificationSystem.Models;
using NotificationSystem.Interfaces;
using NotificationSystem.Services;
using NotificationSystem.Notifications;
using System.Net;

class Program
{
    
    static User GetUserDetails()
    {
        Console.Write("Enter Name: ");
        string name = Console.ReadLine() ?? "";

        Console.Write("Enter Email: ");
        string email = Console.ReadLine() ?? "";

        Console.Write("Enter Phone Number: ");
        string phone = Console.ReadLine() ?? "";

        return new User(name, email, phone);
    }
    static int GetUserChoice()
    {
        Console.WriteLine("\nChoose Notification Type:");
        Console.WriteLine("1. Email");
        Console.WriteLine("2. SMS");
        
        Console.WriteLine("3. Exit");
        Console.Write("Enter choice: ");

        int input;
        while (!int.TryParse(Console.ReadLine(), out input))
        {
            Console.WriteLine("Invalid input. Enter a number:");
        }

        return input;
    }
    static void RunLoop(NotificationService service, User user)
    {
        bool running = true;

        while (running)
        {
            int choice = GetUserChoice();

            switch (choice)
            {
                case 1:
                    HandleNotification(service, user, new EmailNotification());
                    break;

                case 2:
                    HandleNotification(service, user, new SMSNotification());
                    break;

                case 3:
                    Console.WriteLine("Exiting app...");
                    running = false;
                    break;

                default:
                    Console.WriteLine("Invalid choice!");
                    break;
            }
        }
    }
    static void HandleNotification(NotificationService service, User user, INotification sender)
    {
        Console.Write("Enter Message: ");
        string message = Console.ReadLine() ?? "";

        var notification = new Notification(message, DateTime.Now);

        service.SendNotification(sender, user, notification);
    }
    static void Main(string[] args)
    {
        var service = new NotificationService();
        User user = GetUserDetails();
        RunLoop(service,user);
    }
}