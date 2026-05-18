namespace Models.Exceptions
{
    public class UserNotFoundException : Exception
    {
        public UserNotFoundException(string message) : base(message)
        {
            
        }
        public UserNotFoundException(int userid) 
        {
            
        }
    }
}