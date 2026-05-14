namespace WordGuessingGame.Models
{
    public class User
    {
        public int Id { get; set; }
        public  string Username { get; set; } = null!;
        public User(int Id,string Username)
        {
            this.Id=Id;
            this.Username=Username;

        }
        
    }

    
}