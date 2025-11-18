#nullable enable

using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.Win32;
using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Process = System.Diagnostics.Process;

namespace Aldaviva.VisualStudioToolbarButtons.Commands;

internal sealed class TotalCommander {

    public const int COMMAND_ID = 0x0102;

    public static readonly Guid COMMAND_SET = new("61da83b2-5868-4375-9649-92d5e4fabdcc");
    public static TotalCommander? instance { get; private set; }

    private IAsyncServiceProvider serviceProvider => package;

    private readonly AsyncPackage package;
    private readonly DTE          dte;
    // private readonly IVsSolution  solutionService;

    // 32-bit installations may actually appear in WOW6432Node
    private static readonly string[] UNINSTALL_KEYS = [
        @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Totalcmd64",
        @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Totalcmd",
        @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Totalcmd64",
        @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Totalcmd"
    ];

    private static readonly string[] EXECUTABLE_FILENAMES = [
        "totalcmd64.exe",
        "totalcmd.exe"
    ];

    private TotalCommander(AsyncPackage package, OleMenuCommandService commandService, DTE dte) {
        this.package = package;
        this.dte     = dte;

        CommandID   menuCommandId = new(COMMAND_SET, COMMAND_ID);
        MenuCommand menuItem      = new(execute, menuCommandId);
        commandService.AddCommand(menuItem);
    }

    public static async Task InitializeAsync(AsyncPackage package) {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

        OleMenuCommandService commandService = (OleMenuCommandService) (await package.GetServiceAsync(typeof(IMenuCommandService)))!;
        DTE                   dte            = (DTE) (await package.GetServiceAsync(typeof(DTE)))!;
        instance = new TotalCommander(package, commandService, dte);
    }

    private void execute(object sender, EventArgs e) {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (UNINSTALL_KEYS.Select(key => Registry.GetValue(key, "InstallLocation", null) as string).FirstOrDefault(s => s is not null) is not {} installationDirectory) {
            return;
        }

        if (EXECUTABLE_FILENAMES.Select(file => Path.Combine(installationDirectory, file)).FirstOrDefault(File.Exists) is not {} absoluteFilename) {
            return;
        }

        string? projectDir = dte.ActiveDocument?.ProjectItem?.ContainingProject?.FullName is {} projectFilename ? Path.GetDirectoryName(projectFilename) : null;

        ProcessStartInfo invocation = new(absoluteFilename) {
            Arguments = projectDir is not null ? $@"/o /t ""{projectDir}""" : "/o"
        };

        using Process? proc = Process.Start(invocation);

        // DTE                   service   = (DTE) ServiceProvider.GlobalProvider.GetService(typeof(DTE));

        // ProjectQueryableSpace workspace = (await package.GetServiceAsync<IProjectSystemQueryService, IProjectSystemQueryService>()).QueryableSpace;
        //
        // await foreach (IQueryResultItem<ISolutionSnapshot> solution in workspace.) {
        //     string solutionDir = solution.Value.Directory;
        //     if (!string.IsNullOrWhiteSpace(solutionDir)) {
        //         invocation.Arguments = $@"browse ""{solutionDir}""";
        //         break;
        //     }
        // }
    }

}