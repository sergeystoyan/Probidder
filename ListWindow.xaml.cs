﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
//using System.Windows.Shapes;
using System.Net.Http;
using System.Net;
using System.IO;
using System.Management;
using System.Threading;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace Cliver.Foreclosures
{
    public partial class ListWindow : Window
    {
        public static void OpenDialog()
        {
            if (lw == null || !lw.IsLoaded)
            {
                lw = new ListWindow();
                System.Windows.Forms.Integration.ElementHost.EnableModelessKeyboardInterop(lw);
            }
            lw.ShowDialog();
        }

        public static void ItemSaved(Db.Foreclosure f)
        {
            if (lw == null || !lw.IsLoaded)
                return;

            for (int i = lw.list.Items.Count - 1; i >= 0; i--)
            {
                Item t = (Item)lw.list.Items[i];
                if (f.Id == t.Foreclosure.Id)
                {
                    t.Foreclosure = f;
                    lw.list.Items.Refresh();
                    return;
                }
            }
            lw.list.Items.Add(new Item { Foreclosure = f });
            lw.list.Items.Refresh();
        }

        public static void ItemDeleted(int foreclosure_id)
        {
            if (lw == null || !lw.IsLoaded)
                return;

            for (int i = lw.list.Items.Count - 1; i >= 0; i--)
            {
                Item t = (Item)lw.list.Items[i];
                if (foreclosure_id == t.Foreclosure.Id)
                {
                    lw.list.Items.Remove(t);
                    return;
                }
            }
        }

        static ListWindow lw = null;

        public static ListWindow This
        {
            get
            {
                return lw;
            }
        }

        ListWindow()
        {
            InitializeComponent();

            Icon = AssemblyRoutines.GetAppIconImageSource();

            fill();

            Closing += delegate (object sender, System.ComponentModel.CancelEventArgs e)
            {
            };

            Closed += delegate
            {
                lw = null;
                foreclosures.Dispose();
            };

            Db.RefreshStateChanged += delegate
            {
                refresh_db.Dispatcher.Invoke(() =>
                {
                    if (Db.RefreshRuns)
                    {
                        refresh_db.Header = "Refreshing...";
                        refresh_db.IsEnabled = false;
                    }
                    else
                    {
                        refresh_db.Header = "Refresh Database";
                        refresh_db.IsEnabled = true;
                    }
                });
            };

            //CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(list.ItemsSource);
            //view.SortDescriptions.Add(new SortDescription("Id", ListSortDirection.Descending));
            //view.SortDescriptions.Add(new SortDescription("City", ListSortDirection.Ascending));
        }
        Db.Foreclosures foreclosures = new Db.Foreclosures();

        void fill()
        {
            list.ItemsSource = foreclosures.GetAll().Select(x => new Item { Foreclosure = x });
            //list.Items.Clear();
            //foreach (Db.Foreclosure f in foreclosures.GetAll())
            //    list.Items.Add(new Item { Foreclosure = f });
        }

        public class Item
        {
            public Db.Foreclosure Foreclosure { get; set; }
            public AuctionWindow Aw = null;
        }

        private void close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void export_Click(object sender, RoutedEventArgs e)
        {
            string file;
            using (var d = new System.Windows.Forms.FolderBrowserDialog())
            {
                d.Description = "Choose a folder where to save the exported file.";
                if (d.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                    return;
                file = d.SelectedPath + "\\foreclosure_" + DateTime.Now.ToString("yy-MM-dd_HH-mm-ss") + ".csv";
            }

            try
            {
                TextWriter tw = new StreamWriter(file);
                tw.WriteLine(FieldPreparation.GetCsvHeaderLine(typeof(Db.Foreclosure), FieldPreparation.FieldSeparator.COMMA));
                foreach (Db.Foreclosure f in foreclosures.GetAll())
                    tw.WriteLine(FieldPreparation.GetCsvLine(f, FieldPreparation.FieldSeparator.COMMA));
                tw.Close();

                if (Message.YesNo("Data has been exported succesfully to " + file + "\r\n\r\nClean up the database?"))
                {
                    foreclosures.Drop();
                    fill();
                }
            }
            catch (Exception ex)
            {
                LogMessage.Error(ex);
            }
        }

        private void new_Click(object sender, RoutedEventArgs e)
        {
            AuctionWindow.OpenNew();
        }

        private void list_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            foreach (Item i in e.AddedItems)
            {
                show_AuctionWindow(i);
            }
        }

        void show_AuctionWindow(Item i)
        {
            if (i.Aw == null || !i.Aw.IsLoaded)
            {
                i.Aw = AuctionWindow.OpenNew(i.Foreclosure.Id);
                System.Windows.Forms.Integration.ElementHost.EnableModelessKeyboardInterop(i.Aw);
                i.Aw.Show();
            }
            else
            {
                i.Aw.BringIntoView();
                i.Aw.Activate();
            }
        }

        private void list_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ListViewItem lvi = sender as ListViewItem;
            if (lvi == null)
                return;
            Item i = (Item)lvi.Content;
            if (i == null)
                return;
            show_AuctionWindow(i);
            e.Handled = true;
        }

        private void refresh_db_Click(object sender, RoutedEventArgs e)
        {
            Db.BeginRefresh(true);
        }

        private void work_dir_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(Log.WorkDir);
        }

        private void about_Click(object sender, RoutedEventArgs e)
        {
            AboutForm.Open();
        }

        private void exit_Click(object sender, RoutedEventArgs e)
        {
            Environment.Exit(0);
        }

        private void refresh_db_last_time_GotFocus(object sender, RoutedEventArgs e)
        {
            if (Settings.Database.LastRefreshTime != null)
            {
                refresh_db_last_time.Text = "Last refreshed at: " + Settings.Database.LastRefreshTime.ToString();
                refresh_db_last_time0.IsOpen = true;
            }
        }

        private void refresh_db_LostFocus(object sender, RoutedEventArgs e)
        {
            refresh_db_last_time0.IsOpen = false;
        }

        private void login_Click(object sender, RoutedEventArgs e)
        {
            LoginWindow.OpenDialog();
        }

        private void database_Click(object sender, RoutedEventArgs e)
        {
            DatabaseWindow.OpenDialog();
        }

        private void location_Click(object sender, RoutedEventArgs e)
        {
            LocationWindow.OpenDialog();
        }

        private void reset_Click(object sender, RoutedEventArgs e)
        {
            if (Message.YesNo("This will reset all the settings to their initial values. Proceed?"))
                Config.Reset();
        }

        private void list_Click(object sender, RoutedEventArgs e)
        {
            GridViewColumnHeader column = e.OriginalSource as GridViewColumnHeader;
            if (column == null)
                return;

            ListSortDirection direction;
            if (sorted_columns2direction.Contains(column))
            {
                direction = (ListSortDirection)sorted_columns2direction[column];
                direction = direction == ListSortDirection.Ascending ? ListSortDirection.Descending : ListSortDirection.Ascending;
                sorted_columns2direction[column] = direction;
            }
            else
            {                
                foreach (GridViewColumnHeader c in sorted_columns2direction.Keys)//should be removed if multi-column sort
                {
                    c.Column.HeaderTemplate = null;
                    c.Column.Width = c.ActualWidth - 20;
                }
                direction = ListSortDirection.Ascending;
                sorted_columns2direction.Add(column, direction);
                column.Column.Width = column.ActualWidth + 20;
            }

            if (direction == ListSortDirection.Ascending)
                column.Column.HeaderTemplate = Resources["ArrowUp"] as DataTemplate;
            else
                column.Column.HeaderTemplate = Resources["ArrowDown"] as DataTemplate;

            ICollectionView resultDataView = CollectionViewSource.GetDefaultView(list.ItemsSource);
            resultDataView.SortDescriptions.Clear();
            foreach (GridViewColumnHeader c in sorted_columns2direction.Keys)
            {
            string header;
            Binding b = c.Column.DisplayMemberBinding as Binding;
            if (b != null)
                header = b.Path.Path;
            else
                header = (string)c.Column.Header;
                resultDataView.SortDescriptions.Add(new SortDescription(header, (ListSortDirection)sorted_columns2direction[c]));
            }
        }
        System.Collections.Specialized.OrderedDictionary sorted_columns2direction = new System.Collections.Specialized.OrderedDictionary { };
    }
}