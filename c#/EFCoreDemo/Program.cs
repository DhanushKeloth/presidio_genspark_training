using System.ComponentModel;
using System.Xml.Serialization;
using Microsoft.EntityFrameworkCore;
class Program
{
    static void AddTask(AppDbContext db)
    {   var task1 = new TodoItem
        {
            Title="learn python ",
            IsCompleted="false"
        };
        db.Todos.Add(task1);
        db.SaveChanges();
        
    }
    static void GetTasks(AppDbContext db)
    {

        var allTasks = db.Todos.ToList();
        var gettaskname = allTasks.Select(t=>t.Title);
        foreach(var res in allTasks)
        {
            // Console.WriteLine(res);
            Console.WriteLine($"ID: {res.Id} | Task: {res.Title} | Done: {res.IsCompleted}");
        }
    }
    static void UpdateTask(AppDbContext db)
    {
        Console.WriteLine("enter the id of task to be updated");
        int id = Convert.ToInt32(Console.ReadLine());
        var tasktoupdate = db.Todos.First(t=>t.Id==id);
        tasktoupdate.IsCompleted="true";
        db.SaveChanges();
    }
    static void DeleteTask(AppDbContext db)
    {
        Console.WriteLine("enter the task id to be deleted");
        int id = Convert.ToInt32(Console.ReadLine());
        var tasktodelete = db.Todos.FirstOrDefault(t=>t.Id==id);
        if (tasktodelete != null)
        {
            db.Todos.Remove(tasktodelete);
            db.SaveChanges();
        }
    }
    static void Main(string[] args)
    {
        var db = new AppDbContext();
        
        bool running =true;
        while (running)
        {
            Console.WriteLine("1.Addtask\n2.DisplayTask\n3.Updatetask\n4.DeleteTask\n5.exit\n");
            Console.WriteLine("enter your choice:");
            int choice = Convert.ToInt32(Console.ReadLine());
            
            switch (choice)
            {
                case 1:
                    AddTask(db);
                    break;
                case 2:
                    GetTasks(db);
                    break;
                case 3:
                    UpdateTask(db);
                    break;
                case 4:
                    DeleteTask(db);
                    break;
                case 5:
                    running=false;
                    break;
                default:
                    Console.WriteLine("invalid option");
                    break;
            }

        }
    }
}