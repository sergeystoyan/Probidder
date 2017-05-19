using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Diagnostics;
using System.Reflection;
using System.Configuration;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace Cliver.Foreclosures
{
    public partial class Settings
    {
        [Cliver.Settings.Obligatory]
        public static readonly LoginSettings Login;

        public class LoginSettings : Cliver.Settings
        {
            public string UserName = "";
            public string EncryptedPassword = "";

            public string Decrypt(string s)
            {
                try
                {
                    return c.Decrypt(s);
                }
                catch(Exception e)
                {
                    Message.Error("Could not decrypt string: " + Log.GetExceptionMessage(e));
                }
                return null;
            }

            public string Encrypt(string s)
            {
                return c.Encrypt(s);
            }

            CryptoRijndael c = new CryptoRijndael("poiuytrewq");
        }
    }
}