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

        public void processXML(string fileName)
        {
            Database db = this.db();
            User user = null;
            XDocument xDocument = new XDocument();

            try
            {
                xDocument = XDocument.Load(fileName);
                var rawUser = this.XMLGetUser(xDocument, db);
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
            }

        }

        public User XMLGetUser(XDocument xDocument, Database db)
        { 
            var PersonReq = xDocument.Descendants("PersonReq");
            if (PersonReq.Count() > 0)
            {
                var person = PersonReq.ElementAt(0);
                var FirstName = person.Element("first").Value;
                var MiddleName = person.Element("paternal").Value;
                var LastName = person.Element("name1").Value;
                var BirthDate = this.DateFromString(person.Element("birthDt").Value);
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

                user.TotalAccounts = this.IntFromString(person.Element("totalAccts").Value);
                user.ActiveAccounts = this.IntFromString(person.Element("totalActiveBalanceAccounts").Value);

                user.HighCredit = this.XMLNestedLongFetch(person, "totalHighCredit", "Value");
                user.CurrentBalance = this.XMLNestedLongFetch(person, "totalCurrentBalance", "Value");
                user.PastDueBalance = this.XMLNestedLongFetch(person, "totalPastDueBalance", "Value");
                user.OutstandingBalance = this.XMLNestedLongFetch(person, "totalOutstandingBalance", "Value");
                user.SheduledPayments = this.XMLNestedLongFetch(person, "totalScheduledPaymnts", "Value");

                user.TotalRequests = this.IntFromString(person.Element("totalInquiries").Value);
                user.RecentRequests = this.IntFromString(person.Element("recentInquiries").Value);
                user.LongRequests = this.IntFromString(person.Element("collectionsInquiries").Value);
                user.RecentRequest = person.Element("mostRecentInqText").Value;
                user.RecentAcc = this.DateFromString(person.Element("mostRecentAcc").Value);
                user.OldestAcc = this.DateFromString(person.Element("oldest").Value);
            }
            return user;
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
