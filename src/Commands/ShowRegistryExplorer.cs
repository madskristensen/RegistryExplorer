using System;
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
            IMenuCommandService commandService = (IMenuCommandService)await package.GetServiceAsync(typeof(IMenuCommandService));
            Assumes.Present(commandService);

            CommandID menuCommandID = new CommandID(PackageGuids.guidRegistryEplorerPackageCmdSet, PackageIds.ShowRegistryExplorerId);
            MenuCommand menuItem = new MenuCommand((sender, e) => Execute(package, sender, e), menuCommandID);
            commandService.AddCommand(menuItem);
        }

        private static void Execute(AsyncPackage package, object sender, EventArgs e)
        {
            package.JoinableTaskFactory.RunAsync(async () =>
            {
                ToolWindowPane window = await package.ShowToolWindowAsync(
                    toolWindowType   : typeof(RegistryExplorerWindow),
                    id               : 0,
                    create           : true,
                    cancellationToken: package.DisposalToken);
            });
        }
    }
}
