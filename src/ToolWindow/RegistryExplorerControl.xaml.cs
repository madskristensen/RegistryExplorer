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
            this._keys = keys;
            Loaded += this.OnLoaded;
            RegistryTreeItem.ItemSelected += this.OnItemSelected;
            this.InitializeComponent();
        }

        private void Refresh_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (this.tree.SelectedItem is RegistryTreeItem selected)
            {
                selected.Refresh(selected);
                this.UpdateDetailsGridAsync(selected).ConfigureAwait(false);
            }
        }

        private void OnItemSelected(object sender, RegistryTreeItem e)
        {
            this.UpdateDetailsGridAsync(e).ConfigureAwait(false);
        }

        private async Task UpdateDetailsGridAsync(RegistryTreeItem item)
        {
            this.values.Items.Clear();

            // Wait a bit in case the user moved to a different tree node quickly
            await Task.Delay(200);

            // Another tree node was selected in the meantime.
            if (item != this.tree.SelectedItem)
            {
                return;
            }

            if (item.Key.ValueCount > 0)
            {
                IOrderedEnumerable<string> names = item.Key.GetValueNames().OrderBy(x => x);

                using (this.Dispatcher.DisableProcessing())
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

                        this.values.Items.Add(new { Name = displayName, Type = type, Value = value });
                    }
                }
            }
            else
            {
                this.values.Items.Add(this._defaultValue);
            }
        }

        private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            this.tree.Items.Clear();

            foreach (RegistryKey key in this._keys)
            {
                RegistryTreeItem item = new RegistryTreeItem(key, true);

                this.tree.Items.Add(item);

                if (this.tree.SelectedItem == null)
                {
                    item.IsSelected = true;
                }
            }

            this.Focus();
        }
    }
}
