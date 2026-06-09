using System;
using System.Collections.Generic;
using LibraryManagementSystem.Interfaces;
using LibraryManagementSystem.Models;
using LibraryManagementSystem.Exceptions;

namespace LibraryManagementSystem.Presentation
{
    public class FineManagement
    {
        private readonly IFineService _fineService;

        public FineManagement(IFineService fineService)
        {
            _fineService = fineService;
        }

        public void DisplayFineMenu()
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine(" 1. View Pending Fines of a Member");
                Console.WriteLine(" 2. Pay Fine");
                Console.WriteLine(" 3. View Fine History Ledger");
                Console.WriteLine(" 4. Return to Main Desk Menu");
                Console.WriteLine("==================================================");
                Console.Write("enter the choice ");

                string choice = Console.ReadLine();

                try
                {
                    switch (choice)
                    {
                        case "1":
                            HandleViewPendingFines();
                            break;
                        case "2":
                            HandlePayFine();
                            break;
                        case "3":
                            HandleViewFineHistory();
                            break;
                        case "4":
                            return; // Steps back to the main console view loop
                        default:
                            Console.WriteLine("\n Invalid choice. Please select an option between 1 and 4.");
                            break;
                    }
                }
                catch (RecordNotFoundException ex)
                {
                    Console.WriteLine($"\nSearch Failed: {ex.Message}");
                }
                catch (BusinessRuleException ex)
                {
                    Console.WriteLine($"\nPolicy Enforcement: {ex.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\nProcessing Error: {ex.Message}");
                }

                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
            }
        }

        private void HandleViewPendingFines()
        {
            Console.WriteLine("\n--- View Pending Fines ---");
            int memberId = PromptForInteger("Enter Cardholder Member ID: ");
            
            decimal pendingBalance = _fineService.CheckPendingBalance(memberId);
            
            Console.WriteLine("\n==================================================");
            Console.WriteLine($" Member ID        : #{memberId}");
            Console.WriteLine($" Total Pending Balance: ₹{pendingBalance:F2}");
            Console.WriteLine("==================================================");
            
            if (pendingBalance > 500.00m)
            {
                Console.WriteLine(" Account is currently BLOCKED from borrowing.");
                Console.WriteLine("Reason: Outstanding balance exceeds the maximum threshold of ₹500.");
            }
            else if (pendingBalance > 0)
            {
                Console.WriteLine(" Status: Account is clear to borrow, but has active micro-fines.");
            }
            else
            {
                Console.WriteLine(" Status: Account in good financial standing. No debt found.");
            }
        }

        // • CRITERIA 2: Pay fine
        private void HandlePayFine()
        {
            Console.WriteLine("\n--- Process Fine Payment ---");
            int memberId = PromptForInteger("Enter Cardholder Member ID: ");
            
            decimal balance = _fineService.CheckPendingBalance(memberId);
            Console.WriteLine($" Total outstanding amount due: ₹{balance:F2}");
            
            if (balance <= 0)
            {
                Console.WriteLine("\n This account currently owes ₹0.00. No payment action needed!");
                return;
            }

            Console.Write("Enter payment amount to collect: ₹");
            if (!decimal.TryParse(Console.ReadLine(), out decimal paymentAmount))
            {
                Console.WriteLine(" Error: Invalid format entered.");
                return;
            }

            // Executes the back-end payment processing matrix
            bool isSuccessful = _fineService.ProcessFinePayment(memberId, paymentAmount);
            if (isSuccessful)
            {
                Console.WriteLine("\n✨ Payment successfully posted! Ledger balances recalculated.");
            }
        }

        // • CRITERIA 3: View fine history
        private void HandleViewFineHistory()
        {
            Console.WriteLine("\n--- View Fine History  ---");
            int memberId = PromptForInteger("Enter Cardholder Member ID: ");
            
            var history = _fineService.ViewMemberFineHistory(memberId);

            Console.WriteLine("\n==========================================================================");
            Console.WriteLine(string.Format(" {0,-12} | {1,-15} | {2,-15} | {3,-12}", "Log Item ID", "Posting Date", "Amount Charge", "Status"));
            Console.WriteLine("==========================================================================");
            
            int recordCount = 0;
            foreach (var item in history)
            {
                recordCount++;
                string statusLabel = item.IsPaid ? "CLEARED / PAID" : "PENDING DEBT";
                Console.WriteLine(string.Format(" #{0,-11} | {1,-15:d} | ₹{2,-14:F2} | {3,-12}", 
                    item.FinePaymentId, item.PaymentDate.ToString(), item.Amount, statusLabel));
            }

            if (recordCount == 0)
            {
                Console.WriteLine("No data found for  this member.");
            }
         }

        private int PromptForInteger(string promptText)
        {
            while (true)
            {
                Console.Write(promptText);
                if (int.TryParse(Console.ReadLine(), out int validatedResult))
                {
                    return validatedResult;
                }
                Console.WriteLine(" Input error. Please type a valid numeric integer value.");
            }
        }
    }
}