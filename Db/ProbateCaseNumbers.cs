﻿/********************************************************************************************
        Author: Sergey Stoyan
        sergey.stoyan@gmail.com
        http://www.cliversoft.com
********************************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Http;
using System.IO;
using System.Reflection;

namespace Cliver.Probidder
{
    public partial class Db
    {
        public class ProbateCaseNumbers : Db.Json.Table<CountyProbateCaseNumbers>
        {
            new static public void RefreshFile()
            {
                Type t = MethodBase.GetCurrentMethod().DeclaringType;
                Log.Main.Inform("Refreshing table: " + t.Name);
                string[] ls = File.ReadAllLines(Log.AppDir + "\\counties.csv");
                List<CountyProbateCaseNumbers> ccns = new List<CountyProbateCaseNumbers>();
                for (int i = 1; i < ls.Length; i++)
                    ccns.Add(get_CountyCaseNumbers(ls[i]));
                //if(string.IsNullOrEmpty(Settings.Location.County))
                //{
                //    Message.Exclaim("Your location is not specified so Case Numbers cannot be refreshed now.");
                //    return;
                //}
                //ccns.Add(get_CountyCaseNumbers(Settings.Location.County));
                string s = Serialization.Json.Serialize(ccns);
                s = GetJsonNormalized(s);
                System.IO.File.WriteAllText(db_dir + "\\" + t.Name + ".json", s);
            }

            static CountyProbateCaseNumbers get_CountyCaseNumbers(string county)
            {
                county = GetStringNormalized(county);
                HttpClient http_client = new HttpClient();
                HttpResponseMessage rm = http_client.GetAsync("https://i.probidder.com/api/record-gaps/index.php?probates&county=" + county).Result;
                if (!rm.IsSuccessStatusCode)
                    throw new Exception("Could not refresh table: " + rm.ReasonPhrase);
                if (rm.Content == null)
                    throw new Exception("Response content is null.");
                string s = rm.Content.ReadAsStringAsync().Result;
                List<string> case_ns = new List<string>();
                dynamic ccns = Serialization.Json.Deserialize<dynamic>(s);
                foreach (dynamic ccn in ccns)
                    case_ns.Add(((string)ccn.Name).Trim());
                return new CountyProbateCaseNumbers { county = county, case_ns = case_ns };
            }

            public CountyProbateCaseNumbers GetBy(string county)
            {
                lock (table)
                {
                    county = GetStringNormalized(county);
                    CountyProbateCaseNumbers ccns = table.Where(x => x.county == county).FirstOrDefault();
                    if (ccns == null)
                        return new CountyProbateCaseNumbers { case_ns = new List<string>()};
                    Db.Probates ps = new Probates();
                    //List<string> used_cns = fs.Get(x => GetNormalized(x.COUNTY) == county).ToList(); !!!does not work!!!
                    HashSet<string> used_cns = new HashSet<string>(ps.GetAll().Where(x => x.Filling_County == county).Select(x => x.Case_Number));
                    ccns.case_ns = ccns.case_ns.Where(x => !used_cns.Contains(x)).ToList();
                    return ccns;
                }
            }
        }

        public class CountyProbateCaseNumbers : Document
        {
            public string county { get; set; }
            public List<string> case_ns { get; set; }
        }
    }
}