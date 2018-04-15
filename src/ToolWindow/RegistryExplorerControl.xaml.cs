using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using Microsoft.Win32;

namespace RegistryExplorer.ToolWindow
{
    public partial class RegistryExplorerControl : UserControl
    {
        private RegistryKey[] _keys;
        private object _defaultValue = new { Name = "(Default)", Type = RegistryValueKind.String, Value = "(value not set)" };

        public RegistryExplorerControl(RegistryKey[] keys)
        {
            _keys = keys;
            Loaded += OnLoaded;
            RegistryTreeItem.ItemSelected += OnItemSelected;
            InitializeComponent();
        }

        private void Refresh_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (tree.SelectedItem is RegistryTreeItem selected)
            {
                selected.Refresh(selected);
                UpdateDetailsGridAsync(selected).ConfigureAwait(false);
            }
        }

        private void OnItemSelected(object sender, RegistryTreeItem e)
        {
            UpdateDetailsGridAsync(e).ConfigureAwait(false);
        }

        private async Task UpdateDetailsGridAsync(RegistryTreeItem item)
        {
            values.Items.Clear();
            await Task.Delay(200);

            // Another tree node was selected in the meantime.
            if (item != tree.SelectedItem)
            {
                return;
            }

            if (item.Key.ValueCount > 0)
            {
                IOrderedEnumerable<string> names = item.Key.GetValueNames().OrderBy(x => x);

                using (Dispatcher.DisableProcessing())
                {
                    foreach (string name in names)
                    {
                        RegistryValueKind type = item.Key.GetValueKind(name);
                        object value = item.Key.GetValue(name);

                        if (type == RegistryValueKind.Binary && value is byte[] bytes)
                        {
                            value = BitConverter.ToString(bytes).Replace("-", " ");
                        }
                        else if (type == RegistryValueKind.DWord && value is int dword)
                        {
                            value = string.Format("0x{0:X}", dword) + $" ({dword})";
                        }
                        else if (type == RegistryValueKind.QWord && value is long qword)
                        {
                            value = string.Format("0x{0:X}", qword).ToLowerInvariant() + $" ({qword})";
                        }

                        string displayName = string.IsNullOrEmpty(name) ? "(Default)" : name;

                        values.Items.Add(new { Name = displayName, Type = type, Value = value });
                    }
                }
            }
            else
            {
                values.Items.Add(_defaultValue);
            }
        }

        private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            tree.Items.Clear();

            foreach (RegistryKey key in _keys)
            {
                var item = new RegistryTreeItem(key, true);

                tree.Items.Add(item);

                if (tree.SelectedItem == null)
                {
                    item.IsSelected = true;
                }
            }
        }
    }
}
