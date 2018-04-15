using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.Win32;

namespace RegistryExplorer.ToolWindow
{
    public class RegistryTreeItem : TreeViewItem
    {
        public RegistryTreeItem(RegistryKey key, bool populateImmediateChildren = false)
        {
            Key = key;
            Header = Path.GetFileName(key.Name);

            if (populateImmediateChildren)
            {
                PopulateNode(this);
            }

            SetResourceReference(TextElement.ForegroundProperty, EnvironmentColors.BrandedUITitleBrushKey);
        }

        public static event EventHandler<RegistryTreeItem> ItemSelected;

        public RegistryKey Key { get; }

        protected override void OnSelected(RoutedEventArgs e)
        {
            if (IsSelected)
            {
                SetResourceReference(TextElement.ForegroundProperty, EnvironmentColors.SystemHighlightTextBrushKey);
                ItemSelected?.Invoke(this, this);
            }
        }

        protected override void OnUnselected(RoutedEventArgs e)
        {
            SetResourceReference(TextElement.ForegroundProperty, EnvironmentColors.BrandedUITitleBrushKey);
        }

        protected override void OnExpanded(RoutedEventArgs e)
        {
            var item = (RegistryTreeItem)e.Source;

            foreach (RegistryTreeItem child in item.Items)
            {
                PopulateNode(child);
            }
        }
        protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
        {
            if (e.Source is RegistryTreeItem item && !item.IsSelected)
            {
                item.IsSelected = true;
            }
        }

        public void Refresh(RegistryTreeItem item)
        {
            Items.Clear();
            PopulateNode(item);
            item.IsExpanded = true;
        }

        private void PopulateNode(RegistryTreeItem item)
        {
            if (item.HasItems)
                return;

            using (Dispatcher.DisableProcessing())
            {
                foreach (string name in item.Key.GetSubKeyNames())
                {
                    RegistryKey subkey = item.Key.OpenSubKey(name, false);
                    var child = new RegistryTreeItem(subkey);

                    item.Items.Add(child);
                }
            }
        }
    }
}
