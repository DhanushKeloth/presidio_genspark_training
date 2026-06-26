using Microsoft.AspNetCore.Mvc;
using StudentAPI.Models;
namespace StudentAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        static List<Account> accounts = new List<Account>
        {
            new Account
            {
                AccountNumber = "ACC-1002341",
                Balance = 25000.75m, // The 'm' suffix specifies a literal of type decimal
                OpeningDate = new DateTime(2023, 05, 12),
                Status = "Active"
            },
            new Account
            {
                AccountNumber = "ACC-1002342",
                Balance = 1450.00m,
                OpeningDate = new DateTime(2024, 01, 19),
                Status = "Active"
            }
        };
        [HttpGet]
        public IActionResult Get()
        {
            if (accounts.Count == 0)
            {
                return NotFound("no accounts");
            }
            return Ok(accounts);
        }

        [HttpGet("{accountnumber}")]
        public ActionResult<Account> Get(string accountnumber)
        {
            if (accounts.Count == 0)
            {
                return NotFound("no accounts found");
            }
            var acc = accounts.SingleOrDefault(a => a.AccountNumber == accountnumber);
            if (acc == null)
            {
                return NotFound($"account with {accountnumber} not found");
            }

            return Ok(acc);
        }
        [HttpPost]
        public ActionResult<Account> Post([FromBody] Account account)
        {
            accounts.Add(account);
            return CreatedAtAction(nameof(Get),new{id=account.AccountNumber},account);
        }

        
        // [HttpPost]
        // public string GreetPost(Account account)
        // {
        //     return $"hello from {account.Name}";
        // }

    }

}