public class TodoItem
{
    public int Id {get;set;}
    public string? Title {get;set;}
    public string? IsCompleted {get;set;}
    public TodoItem(int Id, string Title,string IsCompleted)
    {
        this.Id= Id;
        this.Title= Title;
        this.IsCompleted = IsCompleted;

    }
    public TodoItem()
    {
        
    }
} 