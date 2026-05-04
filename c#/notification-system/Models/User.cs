namespace NotificationSystem.Models
{
    
public class User
{
    public string name{get;set;} = string.Empty;
    public string email{get;set;} = string.Empty;
    public string phoneNumber{get;set;} = string.Empty;
    public User(string name,string email,string phoneNumber)
    {
        this.name=name;
        this.email=email;
        this.phoneNumber=phoneNumber;
    }
}
}