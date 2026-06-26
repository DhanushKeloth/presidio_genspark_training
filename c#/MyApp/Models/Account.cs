
public enum AccType
{
    SavingAccount = 1, CurrentAccount = 2
}
internal class Account
{
    public string AccountNumber { get; set; } = string.Empty;
    public string NameOnAccount { get; set; } = string.Empty;
    public DateTime DoB { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public float Balance { get; set; }
    public AccType AccountType { get; set; }
    public Account()
    {

    }
    public Account(string AccountNumber, string NameOnAccount, DateTime DoB, string Email, string Phone, float Balance)
    {
        this.AccountNumber = AccountNumber;
        this.NameOnAccount = NameOnAccount;
        this.DoB = DoB;
        this.Balance = Balance;
        this.Phone = Phone;
        this.Email = Email;
    }
    public override string ToString()
    {
        return $"acc no{AccountNumber}\naccount name:{NameOnAccount}\n phone:{Phone}\nemail:{Email}\nbalance:{Balance}\ndob:{DoB}";

    }
    public int CompareTo(Account? other)
    {
        return this.AccountNumber.CompareTo(other.AccountNumber);
    }

}
