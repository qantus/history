using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SQLite;
using System.Globalization;

namespace Daemon
{
    public class User
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        [MaxLength(255)]
        public string FirstName { get; set; }
        [MaxLength(255)]
        public string MiddleName { get; set; }
        [MaxLength(255)]
        public string LastName { get; set; }
        public DateTime BirthDate { get; set; }
        public bool Print { get; set; }

        // Statistic
        public int TotalAccounts { get; set; }
        public int ActiveAccounts { get; set; }

        public long HighCredit { get; set; }
        public long SheduledPayments { get; set; }
        public long CurrentBalance { get; set; }
        public long PastDueBalance { get; set; }
        public long OutstandingBalance { get; set; }

        public DateTime RecentAcc { get; set; }
        public DateTime OldestAcc { get; set; }

        public int TotalRequests { get; set; }
        public int RecentRequests { get; set; }
        public int LongRequests { get; set; }

        [MaxLength(255)]
        public string RecentRequest { get; set; }

        public override string ToString()
        {
            return string.Format ("{0} {1} {2}", FirstName, MiddleName, LastName);
        }    
    }

    public class Request
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        [Indexed]
        public int UserId { get; set; }
        [MaxLength(255)]
        public string Name { get; set; }
        [MaxLength(255)]
        public string Period { get; set; }

        public long Sum { get; set; }
        [Indexed, MaxLength(255)]
        public string Number { get; set; }

        public override string ToString()
        {
            return string.Format("{0} - {1}", Name, Sum);
        }
    }

    public class Account
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        [Indexed]
        public int UserId { get; set; }
        [Indexed, MaxLength(255)]
        public string Number { get; set; }

        [MaxLength(255)]
        public string Type { get; set; }
        [MaxLength(255)]
        public string Relation { get; set; }
        [MaxLength(255)]
        public string Frequency { get; set; }
        [MaxLength(255)]
        public string Collateral { get; set; }
        [MaxLength(255)]
        public string Status { get; set; }

        public long Limit { get; set; }
        public long CurrentBalance { get; set; }
        public long PastDue { get; set; }
        public long NextPay { get; set; }
        public long Outstanding { get; set; }
        
        public int Outstanding30 { get; set; }
        public int Outstanding60 { get; set; }
        public int Outstanding90 { get; set; }

        public int Months { get; set; }
        [MaxLength(255)]
        public string MonthsScheme { get; set; }
        public DateTime MonthsSchemeStartDate { get; set; }

        public DateTime CloseDate { get; set; }
        public DateTime OpenDate { get; set; }
        public DateTime StatusDate { get; set; }
        public DateTime LastPaymentDate { get; set; }
        public DateTime UpdateDate { get; set; }

        public override string ToString()
        {
            return string.Format("{0} - {1}", Type, Limit);
        }
    }

    public class Database : SQLiteConnection
    {
        public Database(string path) : base(path)
        {
            CreateTable<User>();
            CreateTable<Request>();
            CreateTable<Account>();
        }

        public IEnumerable<User> QueryAllUsers()
        {
            return from s in Table<User>()
                   select s;
        }

        public IEnumerable<User> QueryUsersPrint()
        {
            return from s in Table<User>()
                   where s.Print == true
                   select s;
        }

        // @TODO: Birth date check
        public User QueryUser(string FirstName, string MiddleName, string LastName)
        {
            return (from s in Table<User>()
                    where s.FirstName == FirstName && s.MiddleName == MiddleName && s.LastName == LastName 
                    select s).FirstOrDefault();
        }

        public User GetOrCreateUser(string FirstName, string MiddleName, string LastName)
        {
            var user = this.QueryUser(FirstName, MiddleName, LastName);
            if (user == null)
            {
                user = new User();
                user.FirstName = FirstName;
                user.MiddleName = MiddleName;
                user.LastName = LastName;
            }
            return user;
        }

        public Request QueryRequest(User user, string Number)
        {
            return (from s in Table<Request>()
                    where s.UserId == user.Id && s.Number == Number
                    select s).FirstOrDefault();
        }

        public Request GetOrCreateRequest(User user, string Number)
        {
            var request = this.QueryRequest(user, Number);
            if (request == null)
            {
                request = new Request();
                request.UserId = user.Id;
                request.Number = Number;
            }
            return request;
        }

        public Account QueryAccount(User user, string Number)
        {
            return (from s in Table<Account>()
                    where s.UserId == user.Id && s.Number == Number
                    select s).FirstOrDefault();
        }

        public Account GetOrCreateAccount(User user, string Number)
        {
            var account = this.QueryAccount(user, Number);
            if (account == null)
            {
                account = new Account();
                account.UserId = user.Id;
                account.Number = Number;
            }
            return account;
        }

        public void save(object obj)
        {
            if (this.InDatabase(obj))
            {
                this.Update(obj);
            }
            else 
            {
                this.Insert(obj);
            }
        }

        public bool InDatabase(object obj)
        { 
            var type = obj.GetType();
            var map = GetMapping(type);
            var pk = map.PK;

            var found = this.Find(pk.GetValue(obj), map);
            return found != null;
        }
    }
}
