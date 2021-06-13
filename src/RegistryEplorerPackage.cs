using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Win32;
using Task = System.Threading.Tasks.Task;

namespace RegistryExplorer
{
    [Guid(PackageGuids.guidRegistryEplorerPackageString)] 
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    [ProvideToolWindow(typeof(RegistryExplorerWindow), Style = VsDockStyle.Tabbed, Window = "DocumentWell", Orientation = ToolWindowOrientation.none)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    public sealed class RegistryEplorerPackage : AsyncPackage
    {
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            await ShowRegistryExplorer.InitializeAsync(this);
        }

        public override IVsAsyncToolWindowFactory GetAsyncToolWindowFactory(Guid toolWindowType)
        {
            if (toolWindowType.Equals(new Guid(RegistryExplorerWindow.WindowGuidString)))
            {
                return this;
            }

            return null;
        }

        protected override string GetToolWindowTitle(Type toolWindowType, int id)
        {
            if (toolWindowType == typeof(RegistryExplorerWindow))
            {
                return RegistryExplorerWindow.Title;
            }

            return base.GetToolWindowTitle(toolWindowType, id);
        }

        protected override Task<object> InitializeToolWindowAsync(Type toolWindowType, int id, CancellationToken cancellationToken)
        {
            RegistryKey[] keys = new[] { this.UserRegistryRoot, this.ApplicationRegistryRoot };
            return Task.FromResult<object>(keys);
        }
    }
}
