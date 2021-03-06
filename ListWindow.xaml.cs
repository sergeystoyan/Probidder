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
using System.Reflection;
using System.Collections.ObjectModel;
using Cliver.Wpf;

namespace Cliver.Probidder
{
    public partial class ListWindow : Window
    {
        public static void OpenDialog()
        {
            if (_This == null || !_This.IsLoaded)
            {
                _This = new ListWindow();
                System.Windows.Forms.Integration.ElementHost.EnableModelessKeyboardInterop(_This);
            }
            _This.ShowDialog();
        }

        static ListWindow _This = null;

        public static ListWindow This
        {
            get
            {
                return _This;
            }
        }

        ListWindow()
        {
            InitializeComponent();

            Icon = Win.AssemblyRoutines.GetAppIconImageSource();

            Loaded += delegate
            {
                AddHandler(Keyboard.KeyDownEvent, (KeyEventHandler)AutoComplete.Wpf.KeyDownHandler);
                PreviewKeyDown += ListWindow_PreviewKeyDown;
            };

            Closing += delegate (object sender, System.ComponentModel.CancelEventArgs e)
            {
                foreach (var n2v in tables2Table)
                {
                    if (!is_table_ok(n2v.Key))
                    {
                        e.Cancel = true;
                        return;
                    }
                }

                foreach (var n2v in tables2Table)
                {
                    get_columns_order2settings(n2v.Key);
                    get_column_widths2settings(n2v.Key);
                }
                Settings.View.Save();
            };

            Closed += delegate
            {
                views.Dispose();
                _This = null;
            };

            Db.RefreshStateChanged += delegate
            {
                Dispatcher.BeginInvoke((Action)(() =>
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
                }));
            };

            Export.ToServerStateChanged += delegate
            {
                refresh_db.Dispatcher.BeginInvoke((Action)(() =>
                {
                    if (Export.ToServerRuns)
                    {
                        upload.Header = "Uploading...";
                        upload.IsEnabled = false;
                    }
                    else
                    {
                        upload.Header = "Upload";
                        upload.IsEnabled = true;
                    }
                }));
            };

            tables.ItemsSource = new List<Settings.ViewSettings.Tables> { Settings.ViewSettings.Tables.Foreclosures, Settings.ViewSettings.Tables.Probates };
            tables.SelectedItem = Settings.View.ActiveTable;
            ActiveTableChanged();

            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle, new Action(() =>
            {
                SizeToContent = SizeToContent.Manual;
                Wpf.Routines.TrimWindowSize(this);
            }));
        }

        private void ListWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            //if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            //{
            //    e.Handled = true;
            //    commit_and_provide_blank_row();
            //}
        }

        bool is_table_ok(Settings.ViewSettings.Tables table)
        {
            Table t = get_Table(table);
            return is_grid_valid(t.List)
             || !Message.YesNo("Table " + table + " has some error data that has not been saved. This data will be lost. Do you want to fix it before proceeding?", null, Message.Icons.Exclamation);
        }

        bool is_grid_valid(DataGrid dg)
        {
            ListCollectionView cv = (ListCollectionView)CollectionViewSource.GetDefaultView(dg.ItemsSource);
            if (cv != null)
            {
                foreach (object o in cv)
                {
                    IView v = o as IView;
                    if (v != null && v.HasErrors)
                        return false;
                }
            }
            return true;
        }

        Table get_Table(Settings.ViewSettings.Tables table)
        {
            lock (tables2Table)
            {
                Table t;
                if (!tables2Table.TryGetValue(table, out t))
                {
                    DataGrid g;
                    IViews views;
                    switch (table)
                    {
                        case Settings.ViewSettings.Tables.Foreclosures:
                            View<Db.Foreclosure>.Views<ForeclosureView, Db.Foreclosures> fvs = View<Db.Foreclosure>.Views<ForeclosureView, Db.Foreclosures>.Create(this);
                            g = new ForeclosuresControl();
                            (g as ForeclosuresControl).OpenClick += open_Click;
                            (g as ForeclosuresControl).DeleteClick += delete_Click;
                            fvs.CollectionChanged += delegate { update_indicator(); };
                            g.ItemsSource = fvs;
                            views = fvs;
                            break;
                        case Settings.ViewSettings.Tables.Probates:
                            View<Db.Probate>.Views<ProbateView, Db.Probates> pvs = View<Db.Probate>.Views<ProbateView, Db.Probates>.Create(this);
                            g = new ProbatesControl();
                            (g as ProbatesControl).OpenClick += open_Click;
                            (g as ProbatesControl).DeleteClick += delete_Click;
                            pvs.CollectionChanged += delegate { update_indicator(); };
                            g.ItemsSource = pvs;
                            views = pvs;
                            break;
                        default:
                            throw new Exception("Unknown option: " + table);
                    }
                    t = new Table { List = g, Views = views };
                    tables2Table[table] = t;
                    list_container.Children.Add(g);

                    OrderColumns(table);

                    g.ItemContainerGenerator.StatusChanged += ItemContainerGenerator_StatusChanged;
                    g.SelectionChanged += list_SelectionChanged;
                    g.BeginningEdit += list_BeginningEdit;
                    g.RowEditEnding += list_RowEditEnding;                    
                    g.CellEditEnding += list_CellEditEnding;
                    //g.LostKeyboardFocus += delegate { };
                    g.GotKeyboardFocus += list_KeyboardFocusChangedEventHandler;
                    g.PreviewLostKeyboardFocus += delegate (object sender, KeyboardFocusChangedEventArgs e)
                    {
                        Control c = e.NewFocus as Control;
                        if (c == null)
                            return;
                        DataGridRow next_r = c.FindVisualParentOfType<DataGridRow>();
                        if (!(c is DataGridRow) && next_r == null)//when moving out of rows, force committing
                        {
                            list.CommitEdit();
                            //commit_and_provide_blank_row();
                            //save_all_rows();//!!!for unknown reason it does not fire list_RowEditEnding implicitly
                            if (list.SelectedItem != null)//!!!for unknown reason it does not fire list_RowEditEnding implicitly
                            {
                                var r = list.ItemContainerGenerator.ContainerFromItem(list.SelectedItem) as DataGridRow;
                                save_row(r);
                            }
                        }
                    };
                    g.PreviewGotKeyboardFocus += list_PreviewKeyboardFocusChangedEventHandler;
                    g.ColumnDisplayIndexChanged += list_ColumnDisplayIndexChanged;
                }
                return t;
            }
        }

        readonly Dictionary<Settings.ViewSettings.Tables, Table> tables2Table = new Dictionary<Settings.ViewSettings.Tables, Table>();
        class Table
        {
            public DataGrid List;
            public IViews Views;
        }

        private void ItemContainerGenerator_StatusChanged(object sender, EventArgs e)
        {//needed for highlighting search keyword            
            ItemContainerGenerator c = sender as ItemContainerGenerator;
            if (c == null)
                return;
            if (c.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
                highlight();
        }

        void get_columns_order2settings(Settings.ViewSettings.Tables table)
        {
            Settings.View.Tables2Columns[table].Showed.Clear();
            DataGrid g = get_Table(table).List;
            foreach (DataGridColumn dgc in g.Columns.OrderBy(x => x.DisplayIndex))
            {
                if (dgc.Visibility != Visibility.Visible)
                    continue;
                string cn = get_column_name(dgc);
                if (cn == null)//buttons
                    continue;
                Settings.View.Tables2Columns[table].Showed.Add(cn);
            }
        }

        void get_column_widths2settings(Settings.ViewSettings.Tables table)
        {
            Settings.View.Tables2Columns[table].Names2Width.Clear();
            DataGrid g = get_Table(table).List;
            foreach (DataGridColumn dgc in g.Columns.OrderBy(x => x.DisplayIndex))
            {
                if (dgc.Visibility != Visibility.Visible)
                    continue;
                string cn = get_column_name(dgc);
                if (cn == null)//buttons
                    continue;
                Settings.View.Tables2Columns[table].Names2Width[cn] = dgc.ActualWidth;
            }
        }

        public void ActiveTableChanged()
        {
            Table t = get_Table(Settings.View.ActiveTable);
            list = t.List;
            views = t.Views;
            foreach (var n2v in tables2Table)
                if (n2v.Key == Settings.View.ActiveTable)
                    n2v.Value.List.Visibility = Visibility.Visible;
                else
                    n2v.Value.List.Visibility = Visibility.Collapsed;
            //list_container.Content = list;
            update_indicator();
            filter();
        }
        DataGrid list;
        public IViews Views { get { return views; } }
        IViews views;

        static string get_column_name(DataGridColumn dgc)
        {
            TextBlock tb = dgc.Header as TextBlock;
            if (tb == null)
                return null;
            return tb.Text;
        }

        public void OrderColumns(Settings.ViewSettings.Tables table)
        {
            ignore_list_ColumnDisplayIndexChanged = true;
            try
            {
                Table t = get_Table(table);

                ListCollectionView cv = (ListCollectionView)CollectionViewSource.GetDefaultView(t.List.ItemsSource);
                if (cv != null)
                {
                    if (cv.IsEditingItem)
                        cv.CommitEdit();
                    if (cv.IsAddingNew)
                        cv.CommitNew();
                }

                int non_data_columns_count = 0;
                foreach (DataGridColumn dgc in t.List.Columns)
                {
                    string cn = get_column_name(dgc);
                    if (cn == null)
                    {
                        dgc.Visibility = Visibility.Visible;
                        dgc.DisplayIndex = non_data_columns_count++;
                    }
                    else
                    {
                        if (Settings.View.Tables2Columns[table].Showed.Where(x => x == cn).FirstOrDefault() == null)
                            dgc.Visibility = Visibility.Collapsed;
                    }
                }
                for (int i = 0; i < Settings.View.Tables2Columns[table].Showed.Count; i++)
                {
                    string cn = Settings.View.Tables2Columns[table].Showed[i];
                    DataGridColumn dgc = t.List.Columns.Where(x => get_column_name(x) == cn).FirstOrDefault();
                    if (dgc == null)
                    {
                        string m = "Column " + cn + " does not exist.";
                        Log.Main.Error(m);
                        if (IgnoreColumnDoesNotExist)
                            continue;
                        if (Message.YesNo(m + @"
If the application was recently updated then this error can be ignored because it will be fixed while exiting.
However, if it appeared again after re-launch, please contact the vendor as it means a fatal problem.
Ignore this error now?", null, Message.Icons.Error
                        ))
                            IgnoreColumnDoesNotExist = true;
                        continue;
                    }
                    dgc.Visibility = Visibility.Visible;
                    dgc.DisplayIndex = non_data_columns_count + i;
                    dgc.CanUserSort = true;
                    dgc.CanUserResize = true;
                    dgc.CanUserReorder = true;
                    double width;
                    if (Settings.View.Tables2Columns[table].Names2Width.TryGetValue(cn, out width))
                        dgc.Width = new DataGridLength(width, DataGridLengthUnitType.Pixel);
                    else
                        dgc.Width = new DataGridLength(100, DataGridLengthUnitType.SizeToHeader);
                }
            }
            finally
            {
                ignore_list_ColumnDisplayIndexChanged = false;
            }
        }
        public bool IgnoreColumnDoesNotExist = false;

        void update_indicator()
        {
            indicator_total.Content = "Total: " + views.Count();
            //filter();
        }

        private void close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void export_Click(object sender, RoutedEventArgs e)
        {
            if (!is_table_ok(Settings.View.ActiveTable))
                return;
            switch (Settings.View.ActiveTable)
            {
                case Settings.ViewSettings.Tables.Foreclosures:
                    Export.ToDisk(new Db.Foreclosures());
                    break;
                case Settings.ViewSettings.Tables.Probates:
                    Export.ToDisk(new Db.Probates());
                    break;
                default:
                    throw new Exception("Unknown option: " + Settings.View.ActiveTable);
            }
        }

        private void upload_Click(object sender, RoutedEventArgs e)
        {
            if (!is_table_ok(Settings.View.ActiveTable))
                return;
            switch (Settings.View.ActiveTable)
            {
                case Settings.ViewSettings.Tables.Foreclosures:
                    Export.BeginToServer(new Db.Foreclosures());
                    break;
                case Settings.ViewSettings.Tables.Probates:
                    Export.BeginToServer(new Db.Probates());
                    break;
                default:
                    throw new Exception("Unknown option: " + Settings.View.ActiveTable);
            }
        }

        private void new_Click(object sender, RoutedEventArgs e)
        {
            RecordWindow.OpenDialog(Settings.View.ActiveTable, null, views);
        }

        private void list_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            IView v = list.SelectedItem as IView;
            if (v == null)
            {
                indicator_selected.Content = null;
                return;
            }
            indicator_selected.Content = "Selected ID: " + v.Id;
        }

        private void refresh_db_Click(object sender, RoutedEventArgs e)
        {
            Db.BeginRefresh(true);
        }

        private void work_dir_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(Log.WorkDir);
        }

        private void drop_db_Click(object sender, RoutedEventArgs e)
        {
            if (!Message.YesNo(Settings.View.ActiveTable + " table will be dropped. Are you sure to proceed?"))
                return;
            views.Drop();
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

        private void network_Click(object sender, RoutedEventArgs e)
        {
            NetworkWindow.OpenDialog();
        }

        private void database_Click(object sender, RoutedEventArgs e)
        {
            DatabaseWindow.OpenDialog();
        }

        private void auto_complete_Click(object sender, RoutedEventArgs e)
        {
            AutoCompleteWindow.OpenDialog();
        }

        private void location_Click(object sender, RoutedEventArgs e)
        {
            LocationWindow.OpenDialog();
        }

        private void view_Click(object sender, RoutedEventArgs e)
        {
            ViewWindow.OpenDialog(Settings.View.ActiveTable);
        }

        private void open_Click(object sender, RoutedEventArgs e)
        {
            IView v = list.SelectedItem as IView;
            RecordWindow.OpenDialog(Settings.View.ActiveTable, v, views);
        }

        private void show_search_Click(object sender, RoutedEventArgs e)
        {
            show_search.IsChecked = !show_search.IsChecked;
            keyword.Visibility = ((MenuItem)e.Source).IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            filter();
        }

        private void keyword_TextChanged(object sender, TextChangedEventArgs e)
        {
            filter();
        }

        void filter()
        {
            string k = keyword.Text;

            commit_and_provide_blank_row(false);
            ListCollectionView cv = (ListCollectionView)CollectionViewSource.GetDefaultView(list.ItemsSource);

            if (string.IsNullOrEmpty(k) || keyword.Visibility != Visibility.Visible)
            {
                filter_regex = null;
                cv.Filter = null;
                indicator_filtered.Content = null;
                return;
            }

            PropertyInfo[] pis_;
            switch (Settings.View.ActiveTable)
            {
                case Settings.ViewSettings.Tables.Foreclosures:
                    pis_ = typeof(ForeclosureView).GetProperties();
                    break;
                case Settings.ViewSettings.Tables.Probates:
                    pis_ = typeof(ProbateView).GetProperties();
                    break;
                default:
                    throw new Exception("Unknown option: " + Settings.View.ActiveTable);
            }
            List<PropertyInfo> pis = new List<PropertyInfo>();
            foreach (string c in Settings.View.Tables2Columns[Settings.View.ActiveTable].Searched)
            {
                PropertyInfo pi = pis_.FirstOrDefault(x => x.Name == c);
                if (pi == null)
                    continue;
                pis.Add(pi);
            }

            filter_regex = new Regex("(" + Regex.Escape(k) + ")", RegexOptions.IgnoreCase);
            cv.Filter = o =>
            {
                IView iv = (IView)o;
                foreach (PropertyInfo pi in pis)
                {
                    object v = pi.GetValue(iv);
                    if (v == null)
                        continue;
                    string s;
                    if (pi.PropertyType == typeof(DateTime?))
                        s = ((DateTime)v).ToString(DATE_FORMAT);
                    else
                        s = v.ToString();
                    if (!string.IsNullOrEmpty(s) && filter_regex.IsMatch(s))
                        return true;
                }
                return false;
            };

           //foreach(DataGridRow r in list.FindChildrenOfType<DataGridRow>())
           //     validate_row(r);

            int count = 0;
            foreach (object o in cv)
                if (o is IView)
                    count++;
            indicator_filtered.Content = "Filtered: " + count;
        }
        Regex filter_regex = null;
        private void highlight()
        {
            if (filter_regex == null)
                return;

            HashSet<int> searched_columns = new HashSet<int>();
            for (int i = 0; i < list.Columns.Count; i++)
                if (Settings.View.Tables2Columns[Settings.View.ActiveTable].Searched.Contains(get_column_name(list.Columns[i])))
                    searched_columns.Add(i);

            var g = list.FindChildrenOfType<DataGridRow>();

            foreach (DataGridRow r in list.FindChildrenOfType<DataGridRow>())
            {
                for (int j = 0; j < list.Columns.Count; j++)
                {
                    if (!searched_columns.Contains(j))
                        continue;
                    foreach (TextBlock tb in list.GetCell(r, j).FindChildrenOfType<TextBlock>())
                        highlight_TextBlock(tb);
                }
            }
        }
        void highlight_TextBlock(TextBlock tb)
        {
            if (tb == null || filter_regex == null)
                return;
            string text = tb.Text;
            string[] ts = filter_regex.Split(text);
            if (ts.Length < 2)
                return;
            tb.Inlines.Clear();
            bool match = false;
            foreach (string t in ts)
            {
                if (match)
                    tb.Inlines.Add(new Run() { Text = t, Background = Settings.View.SearchHighlightColor });
                else
                    tb.Inlines.Add(new Run() { Text = t });
                match = !match;
            }
        }
        readonly string DATE_FORMAT = "MM/dd/yyyy";

        private void list_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
        }

        private void list_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
            //save_all_rows();
            save_row(e.Row);
        }

        //private void save_all_rows()
        //{
        //    foreach (DataGridRow r in list.FindChildrenOfType<DataGridRow>())
        //        if (r.IsNewItem || r.IsEditing)
        //            save_row(r);
        //}

        private void save_row(DataGridRow row)
        {
            IView v = row.DataContext as IView;
            if (v == null)
                return;
            if (v.Id != 0 && !v.Edited)
                return;
            if (v.Id == 0 && !v.Edited)
            {
                //DataGridRow prev_r = null;
                //int i = row.GetIndex() - 1;
                //if (i >= 0)
                //    prev_r = list.FindChildrenOfType<DataGridRow>().ElementAt(i);
                views.Delete(v);
                //prev_r?.Focus();
                return;
            }
            if (!validate_row(row))
                return;
            //if (row.IsNewItem)//added from the grid (not clear how to commit it?)
            //{
            //    views.Delete(v);//to get rid from IsNewItem which is unclear how to update
            //    views.Update(v);
            //}
            //else
                //e.Row.FindParentOfType<DataGrid>().CommitEdit(DataGridEditingUnit.Row, true);
                views.Update(v);
            //provide_blank_row();
        }

        private void delete_Click(object sender, RoutedEventArgs e)
        {
            //IView v = e.Row.DataContext as IView;
            IView v = list.SelectedItem as IView;
            if (v == null)
                return;
            //DataGridRow r = ((Control)sender).FindVisualParentOfType<DataGridRow>();
            if (v.Id != 0 && !Message.YesNo("You are about deleting record [Id=" + v.Id + "]. Proceed?"))
                return;
            views.Delete(v);
            //commit_and_provide_blank_row();
            //if(list.SelectedItem == CollectionView.NewItemPlaceholder)
        }

        bool commit_and_provide_blank_row(bool provide_blank_row = true)
        {
            if (list == null)
                return true;
            ListCollectionView cv = (ListCollectionView)CollectionViewSource.GetDefaultView(list.ItemsSource);
            if (cv == null)
                return true;

            //foreach (DataGridRow r in list.FindChildrenOfType<DataGridRow>())
            //    if (r.IsNewItem || r.IsEditing)
            //        break;

            //list.CommitEdit();
            //allow set CanUserAddRows = true, when it is frozen for some reason
            if (cv.IsEditingItem)
                cv.CommitEdit();
            if (cv.IsAddingNew)
                cv.CommitNew();

            if (provide_blank_row)
            {
                //foreach (object o in cv)
                 //{
                 //    IView v = o as IView;
                 //    if (v != null && v.Id == 0)
                 //        return;
                 //}

                //if (!cv.CanAddNew/*|| !list.IsValid()*/)
                //{
                //    //Message.Error("The table contains errors. Please correct the data.");
                //    return;
                //}
                //IView iv = (IView)cv.AddNew();
                list.CanUserAddRows = false;//to make a new placeholder displayed
                list.CanUserAddRows = true;
            }
            return true;
        }

        private void list_ColumnDisplayIndexChanged(object sender, DataGridColumnEventArgs e)
        {
            if (ignore_list_ColumnDisplayIndexChanged)
                return;
            get_columns_order2settings(Settings.View.ActiveTable);
        }
        bool ignore_list_ColumnDisplayIndexChanged = false;

        private void list_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            list.SelectedItem = e.Row.DataContext as IView;
            validate_row(e.Row);
            if (current_editing_row != e.Row)
            {
                current_editing_row = e.Row;
                commit_and_provide_blank_row();
            }
        }
        DataGridRow current_editing_row = null;
        
        private bool validate_row(DataGridRow row)
        {
            var v = row.DataContext as IView;
            if (v == null)
                return false;
            if (v.Id != 0 && !v.Edited)
                return true;
            v.ValidateAllProperties();
            if (v.HasErrors)
            {
                row.MarkInvalid("error");
                if (row_error_template != null)
                    row.ValidationErrorTemplate = row_error_template;
                //e.Cancel = true;//needed to prevent auto-creating one more blank row//when using ValidationRule, it prevents validation though.                
                return false;
            }
            row.MarkValid();
            if (row.ValidationErrorTemplate != null)//once the exclamation mark is shown, it does not disppear upon validation
            {
                row_error_template = row.ValidationErrorTemplate;
                row.ValidationErrorTemplate = null;
            }
            return true;
        }
        ControlTemplate row_error_template = null;

        public void list_KeyboardFocusChangedEventHandler(object sender, KeyboardFocusChangedEventArgs e)
        {
        }

        public void list_PreviewKeyboardFocusChangedEventHandler(object sender, KeyboardFocusChangedEventArgs e)
        {//provides editing cell on TAB
            DataGridCell dgc = e.NewFocus as DataGridCell;
            if (dgc == null)
            {
                Control c = e.NewFocus as Control;
                if (c == null)
                    return;
                dgc = c.FindVisualParentOfType<DataGridCell>();
                if (dgc == null)
                    return;
                if (active_cell != dgc)
                {
                    active_cell = dgc;
                    //dgc.Focus();
                }
                return;
            }
            if (active_cell == dgc)
                return;
            e.Handled = true;
            active_cell = dgc;
            //list.CommitEdit();
            dgc.Focus();
            list.BeginEdit();
            Control co = dgc.FindVisualChildrenOfType<Control>().FirstOrDefault();
            co?.Focus();
        }
        DataGridCell active_cell = null;

        private void tables_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Settings.View.ActiveTable == (Settings.ViewSettings.Tables)tables.SelectedItem)
                return;

            if (/*!commit_and_provide_blank_row() || !list.IsValid()*/!is_grid_valid(list))
            {
                Wpf.Message.Error("The table contains errors. Please correct the data.");
                tables.SelectedItem = Settings.View.ActiveTable;
            }
            Settings.View.ActiveTable = (Settings.ViewSettings.Tables)tables.SelectedItem;
        }
    }

    /// <summary>
    /// allows to display/hide the exclamation mark on row
    /// </summary>
    public class RowValidationRule : ValidationRule //sometimes exclamation mark freezes, for unclear reason!
    {
        public override ValidationResult Validate(object value, System.Globalization.CultureInfo cultureInfo)
        {
            BindingGroup bg = (BindingGroup)value;
            if (bg.Items.Count < 1)
                return ValidationResult.ValidResult;
            var v = bg.Items[0] as IView;
            if (v.HasErrors)
                return new ValidationResult(false, "error");
            return ValidationResult.ValidResult;
        }
    }

    //public class ConvertToFormatedRuns : IValueConverter
    //{
    //    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    //    {
    //        var tb = new TextBlock();
    //        tb.Inlines.Add(new Run() { Text = (string)value, Background = Brushes.Yellow });

    //        return tb;
    //    }

    //    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    //    {
    //        return null;
    //    }
    //}
}