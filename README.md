# dotnet clean-all

A dotnet tool that permanently removes all bin and obj folders from a directory's subdirectories.

#### Installation 

To install globally, just run: `dotnet tool install -g CleanAll`

## Usage

Delete all the `bin\` and `obj\` folders from all the projects in a solution:

 > Attempt to read the solution file, iterate through the projects
 > and use msbuild to get the `bin\` and `obj\` folder paths.

```bash
CleanAll MyFolder\MyProject.sln
```

Delete all from a project's parent directory:

 > Use msbuild to get the bin and obj folder paths.

```bash
CleanAll MyFolder\MyProject\MyProject.csproj
```

Delete all from the current directory and it's sub-directories:

```bash
CleanAll
```

Test what the command would do without deleting anything (dry run):

```bash
CleanAll MyFolder --dry-run
```

