using System.Formats.Asn1;

class Program
{


    static void Main(string[] args)
    {
        List<int> marks = new List<int>
    {
        10,3,20,25,30,45,60,10,30
    };
        //where operator
        var filteredres = marks.Where(item => item > 25);
        foreach (var res in filteredres)
        {
            // Console.WriteLine(res);
        }
        //ordered ele 
        var orderedele = marks.OrderBy(item => item);
        Console.Write("ordered elements");
        foreach (var res in orderedele)
        {
            // Console.WriteLine(res);
        }

        List<Student> students = new List<Student>
        {
            new Student { Name = "Adhi", Grade = 95.5, Age = 21,Subjects=new List<string>{"c++","java"} },
            new Student { Name = "Rahul", Grade = 72.0, Age = 22,Subjects=new List<string>{"python","C#"} },
            new Student { Name = "Sneha", Grade = 88.0, Age = 20 ,Subjects=new List<string>{"sql","javascript"}},
            new Student { Name = "Arjun", Grade = 65.5, Age = 23 ,Subjects=new List<string>{"java","python"}},
            new Student { Name = "Priya", Grade = 91.2, Age = 21,Subjects=new List<string>{"c","sql"} }
        };

        //filtered students with age greater than 21
        var topstudents = students.Where(s=>s.Grade>80).Select(s=>s.Name);
        foreach(var res in topstudents)
        {
            Console.WriteLine(res);
        }   
        var orderedstudents = students.OrderBy(s=>s.Grade).ThenBy(s=>s.Name);
        foreach(var res in orderedstudents)
        {
            Console.WriteLine(res);
        }

        //count the students with age >21
        var major = students.Count(s=>s.Age>21);
        Console.WriteLine("major students are "+major);
        var sumofmarks = students.Sum(s=>s.Grade);
        Console.WriteLine("sum of marks is "+sumofmarks);
        var distinctmarks = marks.Distinct();
        foreach(var res in distinctmarks)
        {
            Console.WriteLine(res);
        }

        //groupby age
        var agegroups = students.GroupBy(s=>s.Age);
        foreach(var res in agegroups)
        {
            Console.WriteLine($"{res.Key}=>{res.Count()}");
        }

        //select many 
        var uniquesubjects = students.SelectMany(s=>s.Subjects);
        Console.WriteLine(uniquesubjects.Count());
        foreach(var res in uniquesubjects)
        {
            Console.WriteLine(res);
        }

        //set operations 
        List<string> A = new List<string>{"C++","java","python"};
        List<string> B = new List<string>{"java","C#","javascript"};
        var unionlists = A.Union(B);
        //union operation
        Console.WriteLine("union of lists");
        foreach(var res in unionlists)
        {
            Console.WriteLine(res);

        }
        var intersection = A.Intersect(B);
        Console.WriteLine("Common items in both lists");
        foreach(var res in intersection)
        {
            Console.WriteLine(res);

        }
        //except set difference
        var diff = A.Except(B);
        Console.WriteLine("difference A-B");
        foreach(var res in diff)
        {
            Console.WriteLine(res);

        }

        //check if all the students have atleast one subject
        var atleastonesubj = students.All(s=>s.Subjects.Count()>0);
        List<string> requiredSubjects = new List<string>{"rest api","nodejs","python"};
        var rahul = students.FirstOrDefault(s=>s.Name=="Rahul");
        if (rahul != null)
        {
            var subjectsmissingbyrahul = requiredSubjects.Except(rahul.Subjects);
            Console.WriteLine("subjects missing by rahul");
            foreach(var res in subjectsmissingbyrahul)
            {
                Console.WriteLine(res);
            }
        }
    }
}