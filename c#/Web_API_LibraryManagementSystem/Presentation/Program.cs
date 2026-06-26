using System;
using System.Collections.Generic;
using LibraryManagementSystem.Data;
using LibraryManagementSystem.Repositories;
using LibraryManagementSystem.BusinessLayer;
using LibraryManagementSystem.Enums;
using LibraryManagementSystem.Models;
using LibraryManagementSystem.Interfaces;
using LibraryManagementSystem.Presentation;
using LibraryManagementSystem.Services;

namespace LibraryManagementSystem
{
    class Program
    {

        static void Main(string[] args)
        {
            LibraryDbContext context = new LibraryDbContext();
            IMemberRepository<int, Member> memberRepo = new MemberRepository(context);
            
         
            IMemberService memberService = new MemberService(memberRepo);

            
            MemberManagement memberUI = new MemberManagement(memberService);
            
            // memberUI.Run();
            IBookRepository<Book> bookrepo = new BookRepository(context);
            IBookService bookService = new BookService(bookrepo);
            BookManagement bookUI = new BookManagement(bookService);
            // bookUI.Run();

            IBorrowingRepository<Borrowing> borrowingrepo = new BorrowingRepository(context);
            IBorrowingService borrowingService = new BorrowingService(borrowingrepo,bookrepo,memberRepo);
            BorrowManagement borrowManagement = new BorrowManagement(borrowingService);
            // borrowManagement.Run();

            IFineRepository<int,FinePayment> fineRepository = new FineRepository(context);
            IFineService fineService = new FineService(fineRepository,memberRepo,borrowingrepo);
            FineManagement fineManagement = new FineManagement(fineService);
            // fineManagement.DisplayFineMenu();

            IReportRepository reportRepository = new ReportRepository(context);
            IReportService reportService = new ReportService(reportRepository,memberRepo);
            ReportManagement reportManagement = new ReportManagement(reportService);
            // reportManagement.DisplayReportMenu();


            bool running = true;
            while (running)
            {
                Console.WriteLine("Library management system");
                Console.WriteLine("1.Member Management");
                Console.WriteLine("2.Book Management");
                Console.WriteLine("3.Borrow Book Management");
                Console.WriteLine("4.Fine Management");
                Console.WriteLine("5.Reports");
                Console.WriteLine("6.Exit");
                Console.WriteLine("enter your choice: ");
                string choice = Console.ReadLine()??"";
                switch (choice)
                {
                    case "1":
                        memberUI.Run();
                        break;
                    case "2":
                        bookUI.Run();
                        break;
                    case "3":
                        borrowManagement.Run();
                        break;
                    case "4":
                        fineManagement.DisplayFineMenu();
                        break;
                    case "5":
                        reportManagement.DisplayReportMenu();
                        break;
                    case "6":
                        running=false;
                        Console.WriteLine("Exiting the application");
                        break;
                    default:
                        Console.WriteLine("invalid choice please enter the another choice");
                        break;
                    
                }
            }
        }
    }
}