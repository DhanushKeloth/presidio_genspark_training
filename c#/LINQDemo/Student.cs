public class Student
{
    public required string Name { get; set; }
    public double Grade { get; set; }
    public int Age { get; set; }
    public List<string> Subjects {get;set;}= new List<string>();

    public override string ToString()
    {
        return $"Name: {Name} | Age: {Age} | Grade: {Grade}%";
    }
}