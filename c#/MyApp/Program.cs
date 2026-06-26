using System;
using System.ComponentModel.Design;
using System.Diagnostics.Contracts;

class Program
{
    ICustomerInteract customerInteract;
    public Program()
    {
        customerInteract = new CustomerService();
    }
    void DoBanking()
    {
        bool running = true;
        while (running)
        {
            Console.WriteLine("1.Open Account");
            Console.WriteLine("2.Print account details with account number");
            Console.WriteLine("3.Print Account details with phone number");
            Console.WriteLine("4.Exit");
            int choice;
            while (!int.TryParse(Console.ReadLine(), out choice))
            {
                Console.WriteLine("invalid input. enter a number");
            }
            switch (choice)
            {
                case 1:
                    var account = customerInteract.OpensAccount();
                    Console.WriteLine("Account Created Successfully");
                    Console.WriteLine(account);
                    // Console.WriteLine(account);
                    break;
                case 2:
                    Console.WriteLine("enter the account number");
                    string accNum = Console.ReadLine() ?? "";
                    customerInteract.PrintAccountDetails(accNum);
                    break;
                case 3:
                    //account details with phone number;
                    Console.WriteLine("enter the phone number");
                    string phone = Console.ReadLine() ?? "";
                    customerInteract.PrintAccountDetailsWithPhone(phone);
                    break;
                case 4:
                    running = false;
                    Console.WriteLine("Exiting application...");
                    break;

            }
        }

        // string accNum = Console.ReadLine()??"";
        // customerInteract.PrintAccountDetails(accNum);

    }
    static void Main(string[] args)
    {
        // new Program().DoBanking();
        try
        {
            checked
            {
                int num1 = int.MaxValue;
                num1--; num1++;
                Console.WriteLine("The updated value is " + num1);
                Console.WriteLine("Now you can enter a number");
                num1 = Convert.ToInt32(Console.ReadLine());
                Console.WriteLine("Please enter the dinominator");
                int num2 = Convert.ToInt32(Console.ReadLine());
                var result = num1 / num2;
                Console.WriteLine("The final result is " + result);
            }
        }
        catch (OverflowException ofe)
        {
            Console.WriteLine(ofe.Message);//for programmer
            Console.WriteLine("Sorry the data could not be saved. Please start over");//end user
        }
        catch (FormatException fe)
        {
            Console.WriteLine(fe.Message);
            Console.WriteLine("The input you gave was not a number. We are expectecting a whole number");
        }
        catch (DivideByZeroException dbze)
        {
            Console.WriteLine(dbze.Message);
            Console.WriteLine("Opps unfortunate number for a division. cannot proceed further.");
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            Console.WriteLine("Sorry something went wrong");
        }
        Console.WriteLine("Bye bye");

    }

}
