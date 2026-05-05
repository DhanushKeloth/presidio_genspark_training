using System;
using NotificationSystem.Models;
using NotificationSystem.Interfaces;
using NotificationSystem.Services;
using NotificationSystem.Notifications;
using System.Net;
using NotificationSystem.Repositories;

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
        // var repo = new UserRepository<int, User>();
        // var user1 = new User("dhanush", "d@mail.com", "9876543210");
        // repo.CreateUser(1, user1);
        // // repo.GetUsers();
        // Console.WriteLine(repo.GetUser(1));

        var repo = new UserRepository<int, User>();
        bool running = true;
        int idCounter = 1;

        while (running)
        {
            Console.WriteLine("1. Add User");
            Console.WriteLine("2. Get User (By ID)");
            Console.WriteLine("3. Get All Users");
            Console.WriteLine("4. Update User");
            Console.WriteLine("5. Delete User");
            Console.WriteLine("6. Exit");
            Console.Write("Select an option: ");

            string input = Console.ReadLine() ?? "";

            switch (input)
            {
                case "1": 
                    User newUser = GetUserDetails();
                    repo.CreateUser(idCounter, newUser);
                    Console.WriteLine($"User added successfully with ID: {idCounter}");
                    idCounter++;
                    break;

                case "2": 
                    Console.Write("Enter User ID to find: ");
                    if (int.TryParse(Console.ReadLine(), out int findId))
                    {
                        var user = repo.GetUser(findId);
                        if (user != null)
                            Console.WriteLine($"Found: {user.name}  {user.email} | {user.phoneNumber}");
                        else
                            Console.WriteLine("User not found!");
                    }
                    break;

                case "3": 
                    var users = repo.GetUsers();
                    if (users != null && users.Count > 0)
                    {
                        Console.WriteLine("\nAll Registered Users:");
                        foreach (var u in users)
                            Console.WriteLine($"{u.name} {u.email}");
                    }
                    else
                        Console.WriteLine("No users in the system.");
                    break;

                case "4": 
                    Console.Write("Enter User ID to update: ");
                    if (int.TryParse(Console.ReadLine(), out int updateId))
                    {
                        var existing = repo.GetUser(updateId);
                        if (existing != null)
                        {
                            Console.WriteLine("Enter NEW details:");
                            User updatedData = GetUserDetails();
                            repo.UpdateUser(updateId, updatedData);
                            Console.WriteLine("User updated successfully!");
                        }
                        else
                            Console.WriteLine("User not found!");
                    }
                    break;

                case "5": 
                    Console.WriteLine("Enter the user id to delete");
                    if(int.TryParse(Console.ReadLine(),out int deleteId))
                    {
                        var deleteduser = repo.DeleteUser(deleteId);
                        if (deleteduser != null)
                        {
                            Console.WriteLine($"deleted user {deleteduser.name}");
                        }
                        else
                        {
                            Console.WriteLine("user not found");
                        }
                    }    
                    break;
                case "6":
                    running=false;
                    Console.WriteLine("exiting the application");
                    break;
                default:
                    Console.WriteLine("invalid option");
                    break;
            }
        }
    }
}