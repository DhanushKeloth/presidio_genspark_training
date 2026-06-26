
internal class AccountRepository : IRepository<string, Account>
    {
        Dictionary<string, Account> _accountMap = new Dictionary<string, Account>();
        static string lastAccountNumber = "9990001000";
        public Account Create(Account item)
        {
            long accNum = Convert.ToInt64(lastAccountNumber);
            item.AccountNumber = (++accNum).ToString();
            lastAccountNumber = accNum.ToString();
            _accountMap.Add(lastAccountNumber, item);
            return item;
        }

        public Account? Delete(string key)
        {
            var account = GetAccount(key);
            if (account == null)
                return null;
            _accountMap.Remove(key);
            return account;
        }

        public Account? GetAccount(string key)
        {
            if( _accountMap.ContainsKey(key))
                return _accountMap[key];
            return null;
        }

        public List<Account>? GetAccounts()
        {
            if(_accountMap.Count == 0) 
                return null;
            var list = _accountMap.Values.ToList();
            list.Sort();
            return list;
        }

        public Account? Update(string key, Account item)
        {
            var account = GetAccount(key);
            if (account == null)
                return null;
            _accountMap[key] = item;
            return item;
        }
    }
