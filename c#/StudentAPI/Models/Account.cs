using System.Linq.Expressions;

namespace StudentAPI.Models
{
    
    public class Account
    {
        public string AccountNumber{get;set;}=string.Empty;
        public decimal Balance{get;set;}
        public DateTime OpeningDate{get;set;}
        public string Status{get;set;}=string.Empty;
    }
}