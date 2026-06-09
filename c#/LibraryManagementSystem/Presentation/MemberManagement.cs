using System;
using System.Collections.Generic;
using LibraryManagementSystem.Interfaces;
using LibraryManagementSystem.Models;
using LibraryManagementSystem.Enums;
using LibraryManagementSystem.Exceptions;
using LibraryManagementSystem.BusinessLayer;

namespace LibraryManagementSystem.Presentation
{
    public class MemberManagement
    {
        private readonly IMemberService _memberService;


        public MemberManagement(IMemberService memberService)
        {
            _memberService = memberService;
        }


        public void Run()
        {
            while (true)
            {
                Console.WriteLine("\n=== MEMBER MANAGEMENT SYSTEM ===");
                Console.WriteLine("1. Register New Member");
                Console.WriteLine("2. View All Members");
                Console.WriteLine("3. Search Member by Email/Phone");
                Console.WriteLine("4. Update Membership Status (Activate/Deactivate)");
                Console.WriteLine("5. Back to Main Menu");
                Console.Write("Select an option: ");
                string choice = Console.ReadLine() ?? "";

                switch (choice)
                {
                    case "1":
                        HandleRegistration();
                        break;
                    case "2":
                        HandleViewAllMembers();
                        break;
                    case "3":
                        HandleSearchMember();
                        break;
                    case "4":
                        HandleUpdateStatus();
                        break;
                    case "5":
                        Console.WriteLine("Returning to main");
                        return;
                    default:
                        Console.WriteLine(" Invalid option. Please choose between 1 and 5.");
                        break;
                }
            }
        }


        private void HandleRegistration()
        {
            string name = PromptUntilValid("Enter Name: ", UserValidator.ValidateName);
            string email = PromptUntilValid("Enter Email: ", EmailValidator.Validate);
            string phone = PromptUntilValid("Enter Phone: ", PhoneValidator.Validate);

            MembershipType type = PromptForMembershipType();

            try
            {
                bool registered = _memberService.RegisterMember(name, email, phone, type);
                if (registered)
                {
                    Console.WriteLine("\n Member registered successfully!");
                }
                else
                {
                    Console.WriteLine("\n Failed to register member.");
                }
            }
            catch (ValidException ex)
            {
                Console.WriteLine($"\n Database Validation Error: {ex.Message}");
            }
        }

        private void HandleViewAllMembers()
        {
            IEnumerable<Member> members = _memberService.ViewAllMembers();
            Console.WriteLine("\n--- All Members ---");
            foreach (var m in members)
            {
                string status = m.IsActive ? "Active" : "Inactive";
                Console.WriteLine($"ID: {m.MemberId} | Name: {m.Name} | Email: {m.Email} | Status: {status}");
            }
        }

        private void HandleSearchMember()
        {
            Console.Write("Enter Email or Phone number to search: ");
            string contact = Console.ReadLine() ?? "";

            Member? member = _memberService.SearchMember(contact);
            if (member != null)
            {
                string status = member.IsActive ? "Active" : "Inactive";
                Console.WriteLine($"\nFound Member -> ID: {member.MemberId}, Name: {member.Name}, Email: {member.Email}, Phone: {member.Phone}, Type: {member.Membership}, Status: {status}");
            }
            else
            {
                Console.WriteLine(" No member found with those contact details.");
            }
        }

        private void HandleUpdateStatus()
        {
            Console.Write("Enter Member ID to update: ");
            if (!int.TryParse(Console.ReadLine(), out int memberId))
            {
                Console.WriteLine("Invalid Member ID format.");
                return;
            }

            Console.Write("Set status (1 for Active, 0 for Inactive): ");
            string statusInput = Console.ReadLine() ?? "";
            bool isActive = statusInput == "1";

            bool statusUpdated = _memberService.UpdateMembershipStatus(memberId, isActive);
            if (statusUpdated)
            {
                Console.WriteLine(" Membership status updated successfully.");
            }
            else
            {
                Console.WriteLine(" Failed to update status. Member not found.");
            }
        }

        private string PromptUntilValid(string messagePrompt, Action<string> validationAction)
        {
            while (true)
            {
                Console.Write(messagePrompt);
                string input = Console.ReadLine() ?? "";
                try
                {
                    validationAction(input);
                    return input;
                }
                catch (ValidException ex)
                {
                    Console.WriteLine($" {ex.Message} Please try again.");
                }
            }
        }

        private MembershipType PromptForMembershipType()
        {
            while (true)
            {
                Console.WriteLine("Select Membership Type:");
                Console.WriteLine("1. Student");
                Console.WriteLine("2. Regular");
                Console.WriteLine("3. Premium");
                Console.Write("Choice: ");

                if (int.TryParse(Console.ReadLine(), out int typeChoice) && typeChoice >= 1 && typeChoice <= 3)
                {
                    return (MembershipType)typeChoice;
                }
                Console.WriteLine(" Invalid choice. Please pick 1, 2, or 3.");
            }
        }
    }
}