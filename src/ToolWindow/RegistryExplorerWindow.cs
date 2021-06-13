﻿using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.Win32;
using RegistryExplorer.ToolWindow;

namespace RegistryExplorer
{
    [Guid(WindowGuidString)]
    public class RegistryExplorerWindow : ToolWindowPane
    {
        public const string WindowGuidString = "2f8202f6-41f5-4436-8652-8618fd0e12b1";
        public const string Title = "Registry Hive Explorer";

        public RegistryExplorerWindow(RegistryKey[] keys) : base()
        {
            this.Caption = Title;

            RegistryExplorerControl elm = new RegistryExplorerControl(keys);
            this.Content = elm;
        }
    }
}
