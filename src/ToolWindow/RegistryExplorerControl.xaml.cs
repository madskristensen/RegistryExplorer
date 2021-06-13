using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

using Microsoft.Win32;

namespace RegistryExplorer.ToolWindow
{
    public partial class RegistryExplorerControl : UserControl
    {
        private readonly RegistryKey[] keys;
        
        private static readonly object _defaultValue = new { Name = "(Default)", Length = "", Type = RegistryValueKind.String, Value = "(value not set)" };

        private readonly TreeViewItem searchTreeItem = new TreeViewItem()
        {
            Header = "Search"
        };

        public RegistryExplorerControl(RegistryKey[] keys)
        {
            this.keys = keys;

            this.searchTreeItem.Selected += this.SearchTreeItem_Selected;
            
            this.GoToRegistryKeyCommand = new GoToRegistryKeyCommandImpl(ctrl: this);

            this.Loaded += this.OnLoaded;

            RegistryTreeItem.ItemSelected += this.OnItemSelected;

            this.InitializeComponent();
        }

        private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            this.tree.Items.Clear();

            _ = this.tree.Items.Add(this.searchTreeItem);

            foreach (RegistryKey key in this.keys)
            {
                RegistryTreeItem item = new RegistryTreeItem(key, populateImmediateChildren: true);

                _ = this.tree.Items.Add(item);

                if (this.tree.SelectedItem == null)
                {
                    item.IsSelected = true;
                }
            }

            _ = this.Focus();
        }

        private async void Refresh_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (this.tree.SelectedItem is RegistryTreeItem selected)
            {
                selected.Refresh(selected, expand: true);
                await this.UpdateDetailsGridAsync(selected);
            }
        }

        private async void OnItemSelected(object sender, RegistryTreeItem e)
        {
            await this.UpdateDetailsGridAsync(e);
        }

        private async Task UpdateDetailsGridAsync(RegistryTreeItem item)
        {
            if (this.values.ItemsSource == this.lastSearchResults)
            {
                this.values.ItemsSource = null;
            }

            this.currentKeyPath.Text = item.Key.Name;

            this.values.Items.Clear();

            // Wait a bit in case the user moved to a different tree node quickly
            await Task.Delay(200);

            // Another tree node was selected in the meantime.
            if (item != this.tree.SelectedItem)
            {
                return;
            }

            this.ShowRegistryKey(item.Key);
        }

        private RegistryKey lastKey;

        private void ShowRegistryKey(RegistryKey key)
        {
            if (this.values.ItemsSource == this.lastSearchResults)
            {
                this.values.ItemsSource = null;
            }

            if (key.ValueCount == 0)
            {
                _ = this.values.Items.Add(_defaultValue);
                this.lastKey = key;
                return;
            }

            IOrderedEnumerable<string> names = key.GetValueNames().OrderBy(x => x);

            using (this.Dispatcher.DisableProcessing())
            {
                foreach (string name in names)
                {
                    RegistryValueKind kind = key.GetValueKind(name);
                    object value = key.GetValue(name);
                    value = FormatRegKeyValue(kind, value, out string length);

                    string displayName = string.IsNullOrEmpty(name) ? "(Default)" : name;

                    _ = this.values.Items.Add(new { Name = displayName, Length = length, Type = kind, Value = value });
                }
            }

            this.lastKey = key;
        }

        internal static object FormatRegKeyValue(RegistryValueKind kind, object value, out string length)
        {
            if (kind == RegistryValueKind.Binary && value is byte[] bytes)
            {
                length = string.Format("{0:N0} bytes", bytes.Length);
                return BitConverter.ToString(bytes).Replace("-", " ");
            }
            else if (kind == RegistryValueKind.DWord && value is int dword)
            {
                length = "";
                return string.Format("0x{0:X} ({0:d})", dword);
            }
            else if (kind == RegistryValueKind.QWord && value is long qword)
            {
                length = "";
                return string.Format("0x{0:X} ({0:d})", qword).ToLowerInvariant();
            }
            else if(value is string str)
            {
                length = string.Format("{0:N0} chars", str.Length);
                return value;
            }
            else if(value is null)
            {
                length = "";
                return "(null)";
            }
            else
            {
                length = "";
                return value;
            }
        }

        #region Search

        private ListCollectionView lastSearchResults; 

        private void SearchTreeItem_Selected(object sender, System.Windows.RoutedEventArgs e)
        {
            if (this.lastSearchResults != null)
            {
                this.values.ItemsSource = this.lastSearchResults;
            }
        }

        /// <summary>This is the event-handler for the tree node context-menu's Search command.</summary>
        private async void Search_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (this.tree.SelectedItem is RegistryTreeItem selected)
            {
                this.searchCurrentRad.IsChecked = true;
                selected.Refresh(selected, expand: true);
                await this.UpdateDetailsGridAsync(selected);
            }
        }

        private async void searchTerms_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await this.StartSearchAsync();
            }
        }

        /// <summary>This is the event-handler for the search bar button.</summary>
        private async void StartSearch_Click(object sender, RoutedEventArgs e)
        {
            await this.StartSearchAsync();
        }

        private async Task StartSearchAsync()
        {
            IReadOnlyCollection<string> terms = GetSearchTerms(this.searchTerms.Text);
            if (terms.Count == 0) return;

            bool allTerms      = this.searchOpAnd.IsChecked == true;
            bool caseSensitive = this.searchCaseSensitive.IsChecked == true;

            IReadOnlyCollection<RegistryTreeItem> fromItems = this.GetSearchFrom();
            
            if (fromItems.Count > 0)
            {
                this.IsEnabled = false;
                this.Cursor = Cursors.Wait;
                try
                {
                    // Preload entire tree in the UI thread:
                    foreach (RegistryTreeItem item in fromItems)
                    {
                        RegistryTreeItem.Prepopulate(this.Dispatcher, item);
                    }

                     // Do the actual search in a background thread:
                    Task<List<SearchResult>> task = Task.Run(() => this.DoSearch(allTerms, caseSensitive, terms, fromItems));

                    List<SearchResult> results = await task;

                    this.ShowSearchResults(results);
                }
                finally
                {
                    this.Cursor = null;
                    this.IsEnabled = true;
                }
            }
        }

        private static IReadOnlyCollection<string> GetSearchTerms(string userInput)
        {
            if (string.IsNullOrWhiteSpace(userInput)) return Array.Empty<string>();
            if (userInput.IndexOf('"') < 0) return userInput.Split(separator: (char[])null, options: StringSplitOptions.RemoveEmptyEntries);

            List<string> terms = new List<string>();

            bool inQuotes = false;
            StringBuilder sb = new StringBuilder();

            foreach (char c in userInput)
            {
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (sb.Length > 0)
                        {
                            terms.Add(sb.ToString());
                            sb.Length = 0;
                        }
                        inQuotes = false;
                    }
                    else
                    {
                        _ = sb.Append(c);
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inQuotes = true;
                    }
                    else
                    {
                        if( Char.IsWhiteSpace(c))
                        {
                            if (sb.Length > 0)
                            {
                                terms.Add(sb.ToString());
                                sb.Length = 0;
                            }
                        }
                        else
                        {
                            _ = sb.Append(c);
                        }
                    }
                }
            }

            if(sb.Length > 0)
            {
                terms.Add(sb.ToString());
                sb.Length = 0;
            }

            return terms;
        }

        private IReadOnlyCollection<RegistryTreeItem> GetSearchFrom()
        {
            if (this.searchAllRad.IsChecked == true)
            {
                return this.tree.Items.OfType<RegistryTreeItem>().ToList();
            }
            else if (this.searchCurrentRad.IsChecked == true)
            {
                if (this.tree.SelectedItem is RegistryTreeItem currentItem)
                {
                    return new[] { currentItem };
                }
                else
                {
                    return Array.Empty<RegistryTreeItem>();
                }
            }
            else
            {
                return Array.Empty<RegistryTreeItem>();
            }
        }

        private void CloseSearch_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if(this.lastKey != null)
            {
                this.ShowRegistryKey(this.lastKey);
            }
        }

        private List<SearchResult> DoSearch(bool allTerms, bool caseSensitive, IReadOnlyCollection<string> terms, IReadOnlyCollection<RegistryTreeItem> fromItems)
        {
            List<SearchResult> searchResults = new List<SearchResult>();

            Stopwatch sw = Stopwatch.StartNew();

            foreach (RegistryTreeItem item in fromItems)
            {
                item.Search(allTerms, caseSensitive, terms, searchResults);
            }

            TimeSpan searchTime = sw.Elapsed;

            return searchResults;
        }

        private void ShowSearchResults(List<SearchResult> searchResults)
        {
            this.values.Items.Clear();

            this.lastSearchResults = new ListCollectionView(searchResults)
            {
                GroupDescriptions =
                {
                    new PropertyGroupDescription(propertyName: nameof(SearchResult.KeyPath))
                }
            };

            this.values.ItemsSource = this.lastSearchResults;
        }

        #endregion

        #region Go-to-Key

        public ICommand GoToRegistryKeyCommand { get; }

        private class GoToRegistryKeyCommandImpl : ICommand
        {
            private readonly RegistryExplorerControl ctrl;

            public GoToRegistryKeyCommandImpl(RegistryExplorerControl ctrl)
            {
                this.ctrl = ctrl ?? throw new ArgumentNullException(nameof(ctrl));
            }

            public bool CanExecute(object parameter) => true;

            public void Execute(object parameter)
            {
                if (parameter is string keyPath)
                {
                    this.ctrl.GoToKeyPath(keyPath);
                }
            }

            public event EventHandler CanExecuteChanged;
        }

        public void GoToKeyPath(string path)
        {
            path = path.TrimEnd('\\');

            foreach (RegistryTreeItem rootTreeNode in this.tree.Items.OfType<RegistryTreeItem>())
            {
                RegistryTreeItem exactMatch = null;
                if (rootTreeNode.TryGetItemForPath(absolutePathWithoutTrailingSlash: path, ref exactMatch) && exactMatch != null)
                {
                    RegistryTreeItem node = exactMatch;
                    while (node != null)
                    {
                        node.IsExpanded = true;
                        node = node.Parent as RegistryTreeItem;
                    }

                    exactMatch.IsSelected = true; // <-- This triggers `OnItemSelected`.
//                  exactMatch.IsExpanded = true; // Hmm, this isn't recursive, is it?

                    break;
                }
            }
        }

        #endregion
    }

    public class SearchResult
    {
        public SearchResult(
            string            keyPath,
            string            valueName,
            RegistryValueKind kind,
            object            value
        )
        {
            value = RegistryExplorerControl.FormatRegKeyValue(kind, value, out string length);

            this.KeyPath = keyPath   ?? throw new ArgumentNullException(nameof(value));
            this.Name    = valueName ?? throw new ArgumentNullException(nameof(value));
            this.Length  = length;
            this.Type    = kind;
            this.Value   = value;
        }

        // Same member names as the anonymous objects used for normal key items, so the <DataGrid> definition can be reused:

        public string            KeyPath { get; }
        public string            Name    { get; }
        public string            Length  { get; }
        public RegistryValueKind Type    { get; }
        public object            Value   { get; }
    }
}
