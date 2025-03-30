using CleanAll;
using System.CommandLine;

RootCommand command = new CleanCommand();

return await command.InvokeAsync(args);

