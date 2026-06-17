
using System.ComponentModel.DataAnnotations.Schema;

namespace TodoAPI.Models
{
    [Table("Todos")]
    public class TodoItem
    {
        public int Id {get;set;}
        public string Title {get;set;} = string.Empty;
        public bool IsCompleted {get;set;}
    }
}