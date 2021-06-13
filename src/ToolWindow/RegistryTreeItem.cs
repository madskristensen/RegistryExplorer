using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;

using Microsoft.VisualStudio.PlatformUI;
using Microsoft.Win32;

namespace RegistryExplorer.ToolWindow
{
    public class RegistryTreeItem : TreeViewItem
    {
        public RegistryTreeItem(RegistryKey key, bool populateImmediateChildren = false)
        {
            this.Key          = key ?? throw new ArgumentNullException(nameof(key));
            this.AbsolutePath = key.Name.TrimEnd('\\');

            this.Header = Path.GetFileName(key.Name);

            if (populateImmediateChildren)
            {
                PopulateNode(this.Dispatcher, item: this);
            }

            this.SetResourceReference(TextElement.ForegroundProperty, EnvironmentColors.BrandedUITitleBrushKey);
        }

        public static event EventHandler<RegistryTreeItem> ItemSelected;

        public RegistryKey Key { get; }

        /// <summary>Absolute registry path of <see cref="Key"/> without any trailing slash.</summary>
        public string AbsolutePath { get; }

        protected override void OnSelected(RoutedEventArgs e)
        {
            if (this.IsSelected)
            {
                this.SetResourceReference(TextElement.ForegroundProperty, EnvironmentColors.SystemHighlightTextBrushKey);
                ItemSelected?.Invoke(this, this);
            }
        }

        protected override void OnUnselected(RoutedEventArgs e)
        {
            this.SetResourceReference(TextElement.ForegroundProperty, EnvironmentColors.BrandedUITitleBrushKey);
        }

        protected override void OnExpanded(RoutedEventArgs e)
        {
            RegistryTreeItem item = (RegistryTreeItem)e.Source;

            foreach (RegistryTreeItem child in item.Items)
            {
                PopulateNode(this.Dispatcher, item: child);
            }
        }
        protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
        {
            if (e.Source is RegistryTreeItem item && !item.IsSelected)
            {
                item.IsSelected = true;
            }
        }

        public bool TryGetItemForPath(string absolutePathWithoutTrailingSlash, ref RegistryTreeItem exactMatch)
        {
            if (absolutePathWithoutTrailingSlash.StartsWith(this.AbsolutePath, StringComparison.OrdinalIgnoreCase))
            {
                if (absolutePathWithoutTrailingSlash.Length == this.AbsolutePath.Length)
                {
                    exactMatch = this;
                    return true;
                }
                else
                {
                    foreach (RegistryTreeItem child in this.Items)
                    {
                        if (absolutePathWithoutTrailingSlash.StartsWith(child.AbsolutePath, StringComparison.OrdinalIgnoreCase))
                        {
                            return child.TryGetItemForPath(absolutePathWithoutTrailingSlash, ref exactMatch);
                        }
                    }

                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        public void Refresh(RegistryTreeItem item, bool? expand)
        {
            this.Items.Clear();
            PopulateNode(this.Dispatcher, item);
            if (expand.HasValue)
            {
                item.IsExpanded = expand.Value;
            }
        }

        public static void Prepopulate(Dispatcher dispatcher, RegistryTreeItem item)
        {
            using (dispatcher.DisableProcessing())
            {
                PrepopulateInner(item);
            }
        }

        private static void PrepopulateInner(RegistryTreeItem item)
        {
            bool alreadyLoadedSelf = item.Items.Count == item.Key.SubKeyCount;
            if (!alreadyLoadedSelf)
            {
                item.Items.Clear();
                if (item.Key.SubKeyCount > 0)
                {
                    foreach (string name in item.Key.GetSubKeyNames())
                    {
                        RegistryKey subkey = item.Key.OpenSubKey(name, writable: false);
                        RegistryTreeItem child = new RegistryTreeItem(subkey);

                        _ = item.Items.Add(child);
                    }
                }
            }

            foreach (RegistryTreeItem child in item.Items)
            {
                PrepopulateInner(item: child);
            }
        }

        private static void PopulateNode(Dispatcher dispatcher, RegistryTreeItem item)
        {
            if (item.HasItems || item.Key.SubKeyCount == 0)
            {
                return;
            }

            using (dispatcher.DisableProcessing())
            {
                foreach (string name in item.Key.GetSubKeyNames())
                {
                    RegistryKey subkey = item.Key.OpenSubKey(name, false);
                    RegistryTreeItem child = new RegistryTreeItem(subkey);

                    _ = item.Items.Add(child);
                }
            }
        }

        #region Search

        public void Search(bool allTerms, bool caseSensitive, IReadOnlyCollection<string> terms, List<SearchResult> searchResults)
        {
            foreach (string valueName in this.Key.GetValueNames())
            {
                RegistryValueKind valueKind = this.Key.GetValueKind(valueName);
                object            value     = this.Key.GetValue(valueName);

                if (RegKeyValueMatchesTerms(allTerms, caseSensitive, terms, valueName, valueKind, value))
                {
                    searchResults.Add(new SearchResult(keyPath: this.Key.Name, valueName, valueKind, value));
                }
            }

            //PopulateNode(this.Dispatcher, item: this); // Commented-out due to dispatcher thread issues.

            foreach (object child in this.Items)
            {
                if (child is RegistryTreeItem regTreeItem)
                {
                    regTreeItem.Search(allTerms, caseSensitive, terms, searchResults);
                }
            }
        }

        private static bool RegKeyValueMatchesTerms(bool allTerms, bool caseSensitive, IReadOnlyCollection<string> terms, string valueName, RegistryValueKind valueKind, object value)
        {
            StringComparison sc = caseSensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

            if (allTerms) // aka AND
            {
                foreach (string term in terms)
                {
                    long? integerTerm = Int64.TryParse(term, NumberStyles.Any, CultureInfo.CurrentCulture, out long v) ? v : (long?)null;

                    if (!RegKeyValueMatchesTerm(sc, term, integerTerm, valueName, valueKind, value))
                    {
                        return false;
                    }
                }

                return true;
            }
            else // i.e. any-terms, aka OR
            {
                foreach (string term in terms)
                {
                    long? integerTerm = Int64.TryParse(term, NumberStyles.Any, CultureInfo.CurrentCulture, out long v) ? v : (long?)null;

                    if (RegKeyValueMatchesTerm(sc, term, integerTerm, valueName, valueKind, value))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private static bool RegKeyValueMatchesTerm(StringComparison sc, string term, long? integerTerm, string valueName, RegistryValueKind valueKind, object value)
        {
            if (valueName.IndexOf(term, sc) > 0) return true;

            switch (valueKind)
            {
                case RegistryValueKind.String:
                case RegistryValueKind.ExpandString:
                    {
                        string valueStr = value.ToString();
                        return valueStr.IndexOf(term, sc) > 0;
                    }
                case RegistryValueKind.Binary:
                    {
                        byte[] bytes = (byte[])value;

                        // Try UTF-8:
                        try
                        {
                            string text = Encoding.UTF8.GetString(bytes);
                            if (text.IndexOf(term, sc) > 0) return true;
                        }
                        catch
                        {
                        }

                        // Try UTF-16LE:
                        try
                        {
                            string text = Encoding.Unicode.GetString(bytes);
                            if (text.IndexOf(term, sc) > 0) return true;
                        }
                        catch
                        {
                        }

                        return false;
                    }
                case RegistryValueKind.DWord:
                    if (integerTerm.HasValue)
                    {
                        int valueInt32 = (int)value;
                        return integerTerm.Value == valueInt32;
                    }
                    return false;
                    
                 case RegistryValueKind.QWord:
                    if (integerTerm.HasValue)
                    {
                        long valueInt64 = (long)value;
                        return integerTerm.Value == valueInt64;
                    }
                    return false;

                case RegistryValueKind.MultiString:
                    {
                        string[] values = (string[])value;
                        foreach (string valueStr in values)
                        {
                            if(valueStr.IndexOf(term, sc) > 0) return true;
                        }
                        return false;
                    }
                case RegistryValueKind.None:
                case RegistryValueKind.Unknown:
                default:
                    return false;
            }
        }

        #endregion
    }
}
