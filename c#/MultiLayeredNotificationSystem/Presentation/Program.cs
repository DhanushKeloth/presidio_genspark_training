using System;
using Models;
using Interfaces;
using BusinessLayer;
using Notifications;
using DataAccessLayer;
using System.Net;
using System.Reflection.Metadata;
using Models.Exceptions;

public class UserManagement
{

    private static string GetValidInput(string prompt,Action<string> validationAction)
    {
        while (true)
        {
            Console.Write(prompt);
            string input = Console.ReadLine()??"";
            try
            {
                validationAction(input);
                return input;
            }
            catch(ValidException ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[Invalid] {ex.Message}");
                Console.ResetColor();
            }
        }
    }
    static User GetUserDetails()
    {
        string name = GetValidInput("enter the name",UserValidator.ValidateName);
        string email = GetValidInput("enter the email",EmailValidator.Validate);
        string phone = GetValidInput("enter the phone number",PhoneValidator.Validate);


        return new User(name, email, phone);
    }
    public void HandleUsers(UserRepository<int, User> repo)
    {
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
                    if (int.TryParse(Console.ReadLine(), out int deleteId))
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
                    running = false;
                    Console.WriteLine("exiting the application");
                    break;
                default:
                    Console.WriteLine("invalid option");
                    break;
            }
        }
    }
    
}
class Program
{
    static void SendNotificationWorkflow(NotificationService service)
    {

        try
        {
            Console.Write("\nEnter User ID to notify: ");
            if (!int.TryParse(Console.ReadLine(), out int id)) return;

            Console.Write("Enter Message: ");
            string message = Console.ReadLine() ?? "";

            Console.WriteLine("Choose Type: 1. Email | 2. SMS");
            string input = Console.ReadLine() ?? "";
            string type = (input == "1") ? "Email" : "Sms";

           
            service.ProcessNotification(id, message, type);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Success: Notification sent and logged!");
            Console.ResetColor();
        }
        catch (ValidException ex)
        {
            PrintColorMessage($"Validation Error: {ex.Message}", ConsoleColor.Yellow);
        }
        catch (UserNotFoundException ex)
        {
            PrintColorMessage($"Data Error: {ex.Message}", ConsoleColor.Red);
        }
        catch (Exception ex)
        {
            PrintColorMessage($"System Error: {ex.Message}", ConsoleColor.DarkRed);
        }
        
    }
    static void PrintColorMessage(string message, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ResetColor();
    }
    
    static void DisplayNotifications(NotificationRepository<string, Notification> repo){
    
        var data = repo.GetNotifications();

        if (data == null || data.Count == 0)
        {
            Console.WriteLine("No notifications found in the history.");
            return;
        }

        Console.WriteLine("\n--- Sent Notification History ---");
        var res = data.OrderBy(n=>n.sentDate);
        foreach(var n in res)
        {Console.WriteLine($"[{n.sentDate:yyyy-MM-dd HH:mm}] Message: {n.message}");
        }
    }
    static void Main(string[] args)
    {
        UserManagement um = new UserManagement();
        var senders = new List<INotification> 
        { 
            new EmailNotification(), 
            new SMSNotification() 
        };
        var userrepo = new UserRepository<int, User>();
        var notificationrepo = new NotificationRepository<string, Notification>();
        var service = new NotificationService(userrepo,notificationrepo,senders);
        bool running=true;
        while (running)
        {
            Console.WriteLine("\n--- MAIN MENU ---");
            Console.WriteLine("1. Manage Users");
            Console.WriteLine("2. Send Notification");
            Console.WriteLine("3.Display Notification history");
            Console.WriteLine("4. Exit");
            Console.Write("Choice: ");
            string choice = Console.ReadLine() ?? "";
            switch (choice)
            {
                case "1":
                    um.HandleUsers(userrepo);
                    break;
                case "2":
                    SendNotificationWorkflow(service);
                    break;
                case "3":
                    DisplayNotifications(notificationrepo);
                    break;
                case "4":
                    running=false;
                    break;
                default:
                    Console.WriteLine("invaid option selected");
                    break;

            }
        }
    }
}