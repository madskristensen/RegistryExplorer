﻿using System;
using System.ComponentModel.Design;
using Microsoft;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace RegistryExplorer
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal static class ShowRegistryExplorer
    {
        public static async Task InitializeAsync(AsyncPackage package)
        {
            var commandService = (IMenuCommandService)await package.GetServiceAsync(typeof(IMenuCommandService));
            Assumes.Present(commandService);

            var menuCommandID = new CommandID(PackageGuids.guidRegistryEplorerPackageCmdSet, PackageIds.ShowRegistryExplorerId);
            var menuItem = new MenuCommand((sender, e) => Execute(package, sender, e), menuCommandID);
            commandService.AddCommand(menuItem);
        }

        private static void Execute(AsyncPackage package, object sender, EventArgs e)
        {
            package.JoinableTaskFactory.RunAsync(async () =>
            {
                ToolWindowPane window = await package.ShowToolWindowAsync(
                    typeof(RegistryExplorerWindow),
                    0,
                    create: true,
                    cancellationToken: package.DisposalToken);
            });
        }
    }
}
