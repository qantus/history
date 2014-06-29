using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Linq;
using SQLite;
using System.Globalization;

namespace Daemon
{
    class Parser
    {
        Database _db;
        public string targetDir = "C:\\Daemon\\data\\";
        public string outputStream = "C:\\Daemon\\output.txt";
        public string dbPath = "C:\\Daemon\\db.db";

        public Database db()
        {
            if (this._db == null) {
                this._db = new Database(this.dbPath);
            }
            return this._db;
        }

        public void Parse()
        {
            string[] fileEntries = Directory.GetFiles(this.targetDir);
            foreach (string fileName in fileEntries)
                this.ProcessFile(fileName);
            this.writeData();
        }

        public void ProcessFile(string fileName)
        {
            if (fileName.EndsWith(".xml"))
            {
                this.processXML(fileName);
            }
            //else if (fileName.EndsWith(".html"))
            //{
            //    this.processHTML(fileName);
            //}
        }

        public DateTime DateFromString(string DateString, string format = "yyyy-MM-ddzzz")
        {
            const DateTimeStyles style = DateTimeStyles.AllowWhiteSpaces;
            DateTime result = new DateTime();
            DateTime dt;
            if (DateTime.TryParseExact(DateString, format, CultureInfo.InvariantCulture, style, out dt))
                result = dt;
            return result;   
        }

        public int IntFromString(string IntString)
        {
            try
            {
                return Convert.ToInt32(IntString);
            }
            catch (Exception e) { }
            return 0;
        }

        public Int64 LongFromString(string IntString)
        {
            try
            {
                return Convert.ToInt64(IntString);
            }
            catch (Exception e) { }
            return 0;
        }

        public string StringFromElement(XElement xElement, string DefaultValue = "")
        {
            if (xElement != null) 
            {
                return xElement.Value;
            }
            return DefaultValue;
        }

        public int IntFromElement(XElement xElement, int DefaultValue = 0)
        {
            if (xElement != null)
            {
                return this.IntFromString(xElement.Value);
            }
            return DefaultValue;
        }

        public long LongFromElement(XElement xElement, long DefaultValue = 0)
        {
            if (xElement != null)
            {
                return this.LongFromString(xElement.Value);
            }
            return DefaultValue;
        }

        public DateTime DateFromElement(XElement xElement, DateTime DefaultValue = new DateTime())
        {
            if (xElement != null)
            {
                return this.DateFromString(xElement.Value);
            }
            return DefaultValue;
        }

        public void processXML(string fileName)
        {
            Database db = this.db();
            User user = null;
            XDocument xDocument = new XDocument();

            try
            {
                xDocument = XDocument.Load(fileName);
                var rawUser = this.XMLGetUser(xDocument);
                user = rawUser;
            }
            catch (Exception e) { };

            if (user != null)
            {
                try
                {
                    user = this.XMLFillStatisticInfo(user, xDocument);
                }
                catch (Exception e) { };
                db.save(user);

                try
                {
                    this.XMLSyncRequests(user, xDocument);
                }
                catch (Exception e) { };

                try
                {
                    this.XMLSyncAccounts(user, xDocument);
                }
                catch (Exception e) { };
            }

        }

        public User XMLGetUser(XDocument xDocument)
        {
            Database db = this.db();
            var PersonReq = xDocument.Descendants("PersonReq");
            if (PersonReq.Count() > 0)
            {
                var person = PersonReq.ElementAt(0);
                var FirstName = this.StringFromElement(person.Element("first"));
                var MiddleName = this.StringFromElement(person.Element("paternal"));
                var LastName = this.StringFromElement(person.Element("name1"));
                var BirthDate = this.DateFromElement(person.Element("birthDt"));
                if (FirstName != "" && LastName != "")
                {
                    var user = db.GetOrCreateUser(FirstName, MiddleName, LastName);
                    user.Print = true;
                    user.BirthDate = BirthDate;
                    return user;
                }
            }

            return null;
        }

        public User XMLFillStatisticInfo(User user, XDocument xDocument) 
        {
            var CalcRaw = xDocument.Descendants("calc");
            if (CalcRaw.Count() > 0)
            {
                var person = CalcRaw.ElementAt(0);

                user.TotalAccounts = this.IntFromElement(person.Element("totalAccts"));
                user.ActiveAccounts = this.IntFromElement(person.Element("totalActiveBalanceAccounts"));

                user.HighCredit = this.XMLNestedLongFetch(person, "totalHighCredit", "Value");
                user.CurrentBalance = this.XMLNestedLongFetch(person, "totalCurrentBalance", "Value");
                user.PastDueBalance = this.XMLNestedLongFetch(person, "totalPastDueBalance", "Value");
                user.OutstandingBalance = this.XMLNestedLongFetch(person, "totalOutstandingBalance", "Value");
                user.SheduledPayments = this.XMLNestedLongFetch(person, "totalScheduledPaymnts", "Value");

                user.TotalRequests = this.IntFromElement(person.Element("totalInquiries"));
                user.RecentRequests = this.IntFromElement(person.Element("recentInquiries"));
                user.LongRequests = this.IntFromElement(person.Element("collectionsInquiries"));
                user.RecentRequest = this.StringFromElement(person.Element("mostRecentInqText"));
                user.RecentAcc = this.DateFromElement(person.Element("mostRecentAcc"));
                user.OldestAcc = this.DateFromElement(person.Element("oldest"));
            }
            return user;
        }

        public void XMLSyncRequests(User user, XDocument xDocument)
        {
            Database db = this.db();
            var RequestsRaw = xDocument.Descendants("InquiryReply");
            foreach(XElement RawRequest in RequestsRaw)
            {
                var Period = this.StringFromElement(RawRequest.Element("inquiryPeriod"));
                var Number = this.StringFromElement(RawRequest.Element("inqControlNum"));
                var Sum = this.LongFromElement(RawRequest.Element("inqAmount"));
                var Name = this.StringFromElement(RawRequest.Element("inqPurposeText"));
                
                if (Number != "")
                {
                    var request = db.GetOrCreateRequest(user, Number);
                    request.Sum = Sum;
                    request.Name = Name;
                    request.Period = Period;
                    db.save(request);
                }
            }
        }

        public void XMLSyncAccounts(User user, XDocument xDocument)
        {
            Database db = this.db();
            var RequestsRaw = xDocument.Descendants("AccountReply");
            foreach (XElement RawRequest in RequestsRaw)
            {
                var Number = this.StringFromElement(RawRequest.Element("serialNum"));
                var Type = this.StringFromElement(RawRequest.Element("acctTypeText"));
                var Relation = this.StringFromElement(RawRequest.Element("ownerIndicText"));
                var Collateral = this.StringFromElement(RawRequest.Element("collateral2Text"));
                var Status = this.StringFromElement(RawRequest.Element("accountRatingText"));
                var Frequency = this.StringFromElement(RawRequest.Element("interestPaymentFrequencyText"));

                var Limit = this.LongFromElement(RawRequest.Element("creditLimit"));
                var CurrentBalance = this.LongFromElement(RawRequest.Element("curBalanceAmt"));
                var PastDue = this.LongFromElement(RawRequest.Element("amtPastDue"));
                var NextPay = this.LongFromElement(RawRequest.Element("termsAmt"));
                var Outstanding = this.LongFromElement(RawRequest.Element("amtOutstanding"));

                var Outstanding30 = this.IntFromElement(RawRequest.Element("numDays30"));
                var Outstanding60 = this.IntFromElement(RawRequest.Element("numDays60"));
                var Outstanding90 = this.IntFromElement(RawRequest.Element("numDays90"));

                var Months = this.IntFromElement(RawRequest.Element("monthsReviewed"));
                var MonthsScheme = this.StringFromElement(RawRequest.Element("paymtPat"));
                var MonthsSchemeStartDate = this.DateFromElement(RawRequest.Element("paymtPatStartDt"));

                var CloseDate = this.DateFromElement(RawRequest.Element("closedDt"));
                var OpenDate = this.DateFromElement(RawRequest.Element("openedDt"));
                var StatusDate = this.DateFromElement(RawRequest.Element("accountRatingDate"));
                var LastPaymentDate = this.DateFromElement(RawRequest.Element("lastPaymtDt"));
                var UpdateDate = this.DateFromElement(RawRequest.Element("reportingDt"));

                if (Number != "")
                {
                    var account = db.GetOrCreateAccount(user, Number);

                    account.Type = Type;
                    account.Relation = Relation;
                    account.Collateral = Collateral;
                    account.Status = Status;
                    account.Frequency = Frequency;

                    account.Limit = Limit;
                    account.CurrentBalance = CurrentBalance;
                    account.PastDue = PastDue;
                    account.NextPay = NextPay;
                    account.Outstanding = Outstanding;

                    account.Outstanding30 = Outstanding30;
                    account.Outstanding60 = Outstanding60;
                    account.Outstanding90 = Outstanding90;

                    account.Months = Months;
                    account.MonthsScheme = MonthsScheme;
                    account.MonthsSchemeStartDate = MonthsSchemeStartDate;

                    account.CloseDate = CloseDate;
                    account.OpenDate = OpenDate;
                    account.StatusDate = StatusDate;
                    account.LastPaymentDate = LastPaymentDate;
                    account.UpdateDate = UpdateDate;

                    db.save(account);
                }
            }
        }

        public long XMLNestedLongFetch(XElement xElement, string FirstLevel, string SecondLevel)
        {
            var firstLevel = xElement.Element(FirstLevel);
            if (firstLevel != null)
            {
                var secondLevel = firstLevel.Element(SecondLevel);
                if (secondLevel != null)
                {
                    return this.LongFromString(secondLevel.Value);
                }
            }
            return 0;
        }
        //public void processHTML(string fileName)
        //{
        //    Dictionary<string, object> data = new Dictionary<string, object>();
        //    HtmlAgilityPack.HtmlDocument htmlDoc = new HtmlAgilityPack.HtmlDocument();
        //    htmlDoc.OptionFixNestedTags = true;
        //    htmlDoc.Load(fileName);
        //    data.Add(fileName, htmlDoc.DocumentNode.InnerHtml); 
        //}

        public void writeData()
        {
            var db = this.db();

            System.IO.StreamWriter file = new System.IO.StreamWriter(this.outputStream);

            foreach (User user in db.QueryUsersPrint())
            {
                file.WriteLine(user.FirstName);
                //foreach (KeyValuePair<string, object> variable in fileData.Value)
                //{
                //    file.WriteLine(variable.Key);
                //    file.WriteLine(variable.Value);
                //}
            }
            file.Close();
        }
    }
}
