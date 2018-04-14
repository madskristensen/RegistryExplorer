using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using Microsoft.Win32;

namespace RegistryExplorer.ToolWindow
{
    /// <summary>
    /// Interaction logic for RegistryExplorerControl.xaml
    /// </summary>
    public partial class RegistryExplorerControl : UserControl
    {
        private RegistryKey[] _keys;

        public RegistryExplorerControl(RegistryKey[] keys)
        {
            _keys = keys;
            Loaded += RegistryExplorerControl_Loaded;
            RegistryTreeItem.ItemSelected += RegistryTreeItem_ItemSelected;
            InitializeComponent();
        }

        private void Refresh_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (tree.SelectedItem is RegistryTreeItem selected)
            {
                selected.Refresh(selected);
                UpdateDetailsGridAsync(selected);
            }
        }

        private void RegistryTreeItem_ItemSelected(object sender, RegistryTreeItem e)
        {
            UpdateDetailsGridAsync(e);
        }

        private async Task UpdateDetailsGridAsync(RegistryTreeItem item)
        {
            values.Items.Clear();
            await Task.Delay(300);

            // Another tree node was selected in the meantime.
            if (item != tree.SelectedItem)
            {
                return;
            }

            IOrderedEnumerable<string> names = item.Key.GetValueNames().OrderBy(x => x);

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

            if (!names.Any())
            {
                values.Items.Add(new { Name = "(Default)", Type = RegistryValueKind.String, Value = "(value not set)" });
            }
        }

        private void RegistryExplorerControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (tree.HasItems)
                return;

            foreach (RegistryKey key in _keys)
            {
                string name = key.Name.Substring(key.Name.LastIndexOf('\\') + 1);

                var item = new RegistryTreeItem(key, true)
                {
                    Header = name,
                };

                tree.Items.Add(item);
            }
        }
    }
}
