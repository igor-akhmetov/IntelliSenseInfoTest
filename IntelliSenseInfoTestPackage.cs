using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.VC;
using Microsoft.VisualStudio.Workspace;
using Microsoft.VisualStudio.Workspace.Indexing;
using Microsoft.VisualStudio.Workspace.VSIntegration.Contracts;
using Task = System.Threading.Tasks.Task;

namespace IntelliSenseInfoTest
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(PackageGuidString)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.ShellInitialized_string, PackageAutoLoadFlags.BackgroundLoad)]
    public sealed class IntelliSenseInfoTestPackage : AsyncPackage
    {
        public const string PackageGuidString = "1665bf18-c0b8-4608-80de-df2628bd5a49";

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            var serviceProvider = (IServiceProvider)this;
            var service = serviceProvider.GetService<SComponentModel, IComponentModel>();
            var vsFolderWorkspaceService = service.GetExtensions<IVsFolderWorkspaceService>().SingleOrDefault();
            if (vsFolderWorkspaceService == null)
                return;
            vsFolderWorkspaceService.OnActiveWorkspaceChanged += (o, args) =>
            {
                var workspace = vsFolderWorkspaceService.CurrentWorkspace;
                if (workspace == null)
                    return Task.CompletedTask;
                var indexService = workspace.GetIndexWorkspaceService();
                indexService.OnPropertyChanged += (sender, e) =>
                {
                    var currentState = ((IIndexWorkspaceService)sender).State;
                    if (e.HasProperty(IndexWorkspaceProperties.State) && currentState == IndexWorkspaceState.Completed)
                        GetFileContexts(workspace);
                    return Task.CompletedTask;
                };
                return Task.CompletedTask;
            };
        }

        private static readonly Guid NativeProjectContextTypeGuid = new Guid("ED814497-3055-46C1-9FE0-586CC9530310");

        private void GetFileContexts(IWorkspace workspace)
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;
            workspace.GetFileContextsAsync(
                    workspace.Location,
                    string.Empty,
                    new[] { NativeProjectContextTypeGuid },
                    cancellationToken)
                .ContinueWith(async task => { await CheckIntelliSenseInfo(task); }, cancellationToken, TaskContinuationOptions.None, TaskScheduler.Default);
        }

        private async Task CheckIntelliSenseInfo(Task<IReadOnlyList<IGrouping<Lazy<IFileContextProvider, IFileContextProviderMetadata>, FileContext>>> task)
        {
            var projectContexts = GetProjectContexts(task.Result);
            foreach (IProjectContext projectContext in projectContexts)
            {
                IIntelliSenseInfo info = projectContext.GetIntelliSenseInfo();
                if (info.CommandLinesCount == 0)
                {
                    await JoinableTaskFactory.SwitchToMainThreadAsync();
                    VsShellUtilities.ShowMessageBox(
                        this,
                        "IntelliSenseInfoTest: no command-lines in an IIntelliSenseInfo object",
                        null,
                        OLEMSGICON.OLEMSGICON_INFO,
                        OLEMSGBUTTON.OLEMSGBUTTON_OK,
                        OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                }
            }
        }

        private HashSet<IProjectContext> GetProjectContexts(IReadOnlyList<IGrouping<Lazy<IFileContextProvider, IFileContextProviderMetadata>, FileContext>> fileContextGroups)
        {
            var contexts = new HashSet<IProjectContext>();
            foreach (var group in fileContextGroups)
            {
                foreach (FileContext fileContext in group)
                {
                    fileContext.OnFileContextChanged += (obj, args) => Task.CompletedTask;
                    var projectContext = fileContext.Context as IProjectContext;
                    if (projectContext == null)
                        continue;
                    contexts.Add(projectContext);
                }
            }
            return contexts;
        }
    }
}
