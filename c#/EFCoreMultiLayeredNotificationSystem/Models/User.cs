namespace Models
{

    public class User
    {
        public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string? PhoneNumber { get; set; }

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
        public User(string Name, string Email, string PhoneNumber)
        {
            this.Name = Name;
            this.Email = Email;
            this.PhoneNumber = PhoneNumber;
        }
        public override string ToString()
        {
            return $"{Name}, {Email}, {PhoneNumber}";
        }
    }
}