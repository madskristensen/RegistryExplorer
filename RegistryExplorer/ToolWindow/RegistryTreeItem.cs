using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.Win32;

namespace RegistryExplorer.ToolWindow
{
    public class RegistryTreeItem : TreeViewItem
    {
        public RegistryTreeItem(RegistryKey key, bool populateImmediateChildren = false)
        {
            Key = key;


            Expanded += OnExpanded;

            if (populateImmediateChildren)
            {
                PopulateNode(this);
            }

            MouseRightButtonUp += RegistryTreeItem_MouseRightButtonUp;
            SetResourceReference(TextElement.ForegroundProperty, EnvironmentColors.BrandedUITitleBrushKey);
        }

        protected override void OnUnselected(RoutedEventArgs e)
        {
            SetResourceReference(TextElement.ForegroundProperty, EnvironmentColors.BrandedUITitleBrushKey);
        }

        private void RegistryTreeItem_MouseRightButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {

            if (e.Source is RegistryTreeItem item && !item.IsSelected)
            {
                item.IsSelected = true;
            }
        }

        protected override void OnSelected(RoutedEventArgs e)
        {
            if (IsSelected)
            {
                SetResourceReference(TextElement.ForegroundProperty, EnvironmentColors.SystemHighlightTextBrushKey);
                ItemSelected?.Invoke(this, this);
            }
        }

        public static event EventHandler<RegistryTreeItem> ItemSelected;

        public RegistryKey Key { get; }

        private void OnExpanded(object sender, System.Windows.RoutedEventArgs e)
        {
            var item = (RegistryTreeItem)sender;

            foreach (RegistryTreeItem child in item.Items)
            {
                PopulateNode(child);
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

            foreach (string name in item.Key.GetSubKeyNames())
            {
                RegistryKey subkey = item.Key.OpenSubKey(name);
                var child = new RegistryTreeItem(subkey)
                {
                    Header = name,
                };

                item.Items.Add(child);
            }
        }
    }
}
