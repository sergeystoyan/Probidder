﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Http;
using System.IO;
//using MongoDB.Bson;
//using MongoDB.Driver;
using LiteDB;

namespace Cliver.Foreclosures
{
    public partial class Db
    {
        public static Thread BeginRefresh()
        {
            if (refresh_t != null && refresh_t.IsAlive)
                return refresh_t;

            DateTime refresh_started = DateTime.Now;
            refresh_t = ThreadRoutines.StartTry(() =>
            {
                Log.Inform("Refreshing db.");

                //InfoWindow iw = InfoWindow.Create("Foreclosures", "Refreshing database... Please wait for completion.", null, "OK", null);

                HttpClientHandler handler = new HttpClientHandler();
                http_client = new HttpClient(handler);

                List<Task> tasks = new List<Task>();
                var t = new Task(() =>
                {
                    refresh_table("https://i.probidder.com/api/fields/index.php?type=foreclosures&field=mortgage_type", "mortgage_types");
                });
                t.Start();
                tasks.Add(t);
                t = new Task(() =>
                {
                    refresh_table("https://i.probidder.com/api/fields/index.php?type=foreclosures&field=attorney_phone", "attorney_phones");
                });
                t.Start();
                tasks.Add(t);
                t = new Task(() =>
                {
                    refresh_table("https://i.probidder.com/api/fields/index.php?type=foreclosures&field=attorney", "attorneys");
                });
                t.Start();
                tasks.Add(t);
                t = new Task(() =>
               {
                   refresh_table("https://i.probidder.com/api/fields/index.php?type=foreclosures&field=city", "cities");
               });
                t.Start();
                tasks.Add(t);
                t = new Task(() =>
               {
                   refresh_table("https://i.probidder.com/api/fields/index.php?type=foreclosures&field=plaintiff", "plaintiffs");
               });
                t.Start();
                tasks.Add(t);

                if (!File.Exists(db_dir + "\\illinois_postal_codes.csv"))
                {
                    string s = File.ReadAllText(Log.AppDir + "\\illinois_postal_codes.csv");
                    File.WriteAllText(db_dir + "\\illinois_postal_codes.csv", get_normalized(s));
                }

                if (!File.Exists(db_dir + "\\property_codes.csv"))
                    File.Copy(Log.AppDir + "\\property_codes.csv", db_dir + "\\property_codes.csv");

                if (!File.Exists(db_dir + "\\owner_role.csv"))
                    File.Copy(Log.AppDir + "\\owner_role.csv", db_dir + "\\owner_role.csv");

                Task.WaitAll(tasks.ToArray());
                //Log.Inform("Db has been refreshed.");
                //iw.Dispatcher.Invoke(iw.Close);
                refresh_time = DateTime.Now;
                Settings.General.NextDbRefreshTime = refresh_started.AddSeconds(Settings.General.DbRefreshPeriodInSecs);
                InfoWindow.Create(ProgramRoutines.GetAppName(), "Database has been refreshed successfully.", null, "OK", null, System.Windows.Media.Brushes.White, System.Windows.Media.Brushes.Green);
            },
            (Exception e) =>
            {
                Log.Error(e);
                Log.Error("Could not refresh db.");
                Settings.General.NextDbRefreshTime = refresh_started.AddSeconds(Settings.General.DbRefreshPeriodInSecs);
                InfoWindow.Create(ProgramRoutines.GetAppName() + ": database could not refresh!", Log.GetExceptionMessage(e), null, "OK", null, System.Windows.Media.Brushes.Beige, System.Windows.Media.Brushes.Red);
            },
            () =>
            {
                Settings.General.Save();
            }
            );
            return refresh_t;
        }
        static Thread refresh_t = null;
        static HttpClient http_client;
        static DateTime refresh_time = DateTime.MinValue;

        static public DateTime RefreshTime
        {
            get
            {
                return refresh_time;
            }
        }

        static void refresh_table(string url, string table)
        {
            Log.Main.Inform("Refreshing table: " + table);
            HttpResponseMessage rm = http_client.GetAsync(url).Result;
            if (!rm.IsSuccessStatusCode)
                throw new Exception("Could not refresh table: " + rm.ReasonPhrase);
            if (rm.Content == null)
                throw new Exception("Response content is null.");
            string s = rm.Content.ReadAsStringAsync().Result;
            System.IO.File.WriteAllText(db_dir + "\\" + table + ".json", s);
        }

        public static List<string> GetValuesFromTable(string table, string field, Dictionary<string, string> keys2value)
        {
            Dictionary<string, string> ks2v = new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> k2v in keys2value)
                ks2v[k2v.Key] = get_normalized(k2v.Value);

            List<string> vs = new List<string>();
            string s = System.IO.File.ReadAllText(db_dir + "\\" + table + ".json");
            dynamic json = SerializationRoutines.Json.Deserialize<dynamic>(s);
            foreach (dynamic d in (dynamic)json)
            {
                bool found = true;
                foreach (KeyValuePair<string, string> k2v in ks2v)
                    if (k2v.Value != null && get_normalized((string)d[k2v.Key]) != k2v.Value)
                    {
                        found = false;
                        break;
                    }
                if (found)
                    vs.Add((string)d[field]);
            }
            return vs;
        }

        public static List<string> GetZipCodes(string county, string city)
        {
            county = get_normalized(county);
            city = get_normalized(city);
            List<string> vs = new List<string>();
            string[] ss = File.ReadAllLines(db_dir + "\\illinois_postal_codes.csv");
            foreach (string s in ss)
            {
                string[] fs = s.Split(',');
                if (fs[1] == city && fs[3] == county)
                    vs.Add(fs[0]);
            }
            return vs;
        }

        public static List<string> GetPropertyCodes()
        {
            List<string> vs = new List<string>();
            string[] ss = File.ReadAllLines(db_dir + "\\property_codes.csv");
            foreach (string s in ss)
            {
                string[] fs = s.Split(',');
                vs.Add(fs[0]);
            }
            return vs;
        }

        public static List<string> GetOwnerRole()
        {
            List<string> vs = new List<string>();
            string[] ss = File.ReadAllLines(db_dir + "\\owner_role.csv");
            foreach (string s in ss)
            {
                string[] fs = s.Split(',');
                vs.Add(fs[0]);
            }
            return vs;
        }
    }
}