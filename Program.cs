//********************************************************************************************
//Author: Sergey Stoyan, CliverSoft.com
//        http://cliversoft.com
//        stoyan@cliversoft.com
//        sergey.stoyan@gmail.com
//        27 February 2007
//Copyright: (C) 2007, Sergey Stoyan
//********************************************************************************************

using System;
using System.Linq;
using System.Net;
using System.Text;
using System.IO;
using System.Threading;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Script.Serialization;
using System.Collections.Generic;
using System.Net.Mail;
using Cliver;
using System.Configuration;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Cliver.Probidder
{
    public partial class Program
    {
        static Program()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            AppDomain.CurrentDomain.UnhandledException += delegate (object sender, UnhandledExceptionEventArgs args)
            {
                Exception e = (Exception)args.ExceptionObject;
                Message.Error(e);
                Application.Exit();
            };

            Message.TopMost = true;
            Log.Initialize(Log.Mode.ONLY_LOG);
            //Cliver.Config.Initialize(new string[] { "General" });
            Cliver.Config.Reload();

            SetTextSelectOnFocus();
        }  

        [STAThread]
        public static void Main(string[] args)
        {
            //InternetDateTime.CHECK_TEST_PERIOD_VALIDITY(2022, 9, 20);

            try
            {
                Win.ProcessRoutines.RunSingleProcessOnly();
                
                if (string.IsNullOrWhiteSpace(Settings.Network.UserName) || string.IsNullOrWhiteSpace(Settings.Network.EncryptedPassword))
                    NetworkWindow.OpenDialog();

                if (string.IsNullOrWhiteSpace(Settings.Location.County))
                    LocationWindow.OpenDialog();

                Service.Running = true;
                //Db.BeginRefresh();
                ListWindow.OpenDialog();

                //Application.Run(SysTray.This);
            }
            catch (Exception e)
            {
                Message.Error(e);
            }
            finally
            {
                Environment.Exit(0);
            }
        }
    }
}