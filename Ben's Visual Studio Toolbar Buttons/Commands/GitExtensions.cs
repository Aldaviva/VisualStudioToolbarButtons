#nullable enable

using Microsoft.VisualStudio.ProjectSystem.Query;
using Microsoft.VisualStudio.Shell;
using Microsoft.Win32;
using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using Task = System.Threading.Tasks.Task;

namespace Aldaviva.VisualStudioToolbarButtons.Commands;

/// <summary>
/// Command handler
/// </summary>
internal sealed class GitExtensions {

    /// <summary>
    /// Command ID.
    /// </summary>
    public const int CommandId = 0x0101;

    /// <summary>
    /// Command menu group (command set GUID).
    /// </summary>
    public static readonly Guid CommandSet = new("61da83b2-5868-4375-9649-92d5e4fabdcc");

    /// <summary>
    /// VS Package that provides this command, not null.
    /// </summary>
    private readonly AsyncPackage package;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitExtensions"/> class.
    /// Adds our command handlers for menu (commands must exist in the command table file)
    /// </summary>
    /// <param name="package">Owner package, not null.</param>
    /// <param name="commandService">Command service to add command to, not null.</param>
    /// <exception cref="ArgumentNullException"></exception>
    private GitExtensions(AsyncPackage package, OleMenuCommandService commandService) {
        this.package   = package ?? throw new ArgumentNullException(nameof(package));
        commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

        CommandID   menuCommandID = new(CommandSet, CommandId);
        MenuCommand menuItem      = new(Execute, menuCommandID);
        commandService.AddCommand(menuItem);
    }

    /// <summary>
    /// Gets the instance of the command.
    /// </summary>
    public static GitExtensions? Instance { get; private set; }

    /// <summary>
    /// Gets the service provider from the owner package.
    /// </summary>
    private IAsyncServiceProvider ServiceProvider => package;

    /// <summary>
    /// Initializes the singleton instance of the command.
    /// </summary>
    /// <param name="package">Owner package, not null.</param>
    public static async Task InitializeAsync(AsyncPackage package) {
        // Switch to the main thread - the call to AddCommand in GitExtensions's constructor requires
        // the UI thread.
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

        OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
        Instance = new GitExtensions(package, commandService);
    }

    /// <summary>
    /// This function is the callback used to execute the command when the menu item is clicked.
    /// See the constructor to see how the menu item is associated with this function using
    /// OleMenuCommandService service and MenuCommand class.
    /// </summary>
    /// <param name="sender">Event sender.</param>
    /// <param name="e">Event args.</param>
    private async void Execute(object sender, EventArgs e) {
        ProjectQueryableSpace workspace = (await package.GetServiceAsync<IProjectSystemQueryService, IProjectSystemQueryService>()).QueryableSpace;

        if ((Registry.GetValue(@"HKEY_CURRENT_USER\SOFTWARE\Classes\GitExtensions\shell\open\command", string.Empty, null) ??
                Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Classes\GitExtensions\shell\open\command", string.Empty, null)) is not string openCommand) {
            return;
        }

        ProcessStartInfo invocation = new(openCommand.Split([" openrepo"], 2, StringSplitOptions.None)[0].Trim('"'));

        await foreach (IQueryResultItem<ISolutionSnapshot> solution in workspace.Solutions.QueryAsync(package.DisposalToken)) {
            string solutionDir = solution.Value.Directory;
            if (!string.IsNullOrWhiteSpace(solutionDir)) {
                invocation.Arguments = $@"browse ""{solutionDir}""";
                break;
            }
        }

        using Process proc = Process.Start(invocation);
    }

}