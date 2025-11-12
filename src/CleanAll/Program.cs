using MinimalCli;

MinimalCommandLineBuilder builder = new(args);

builder.MapRootCommand(options => {
    options.Command.Description = "Delete all of the bin and obj directories and their contents in a solution or project folder.";
    
    options.PathArgument.HelpName = "Solution|Project";
    options.PathArgument.Description = "Path to the solution file, project file or directory to clean.";

    options.DryRunOption.Aliases.Add("-dr");
    options.DryRunOption.Description = "Run the command without removing anything so you can see what will be deleted.";
});

MinimalCommandLineApp app = builder.Build();

await app.StartAsync();

