
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.CommandLine;

var outputOption = new Option<FileInfo>("--output")
{
    Description = "Output bundle file",
    Required = true
};
outputOption.Aliases.Add("-o");

var languageOption = new Option<string[]>("--language")
{
    Description = "Languages to include, or all",
    Required = true,
    AllowMultipleArgumentsPerToken = true
};
languageOption.Aliases.Add("-l");

var noteOption = new Option<bool>("--note")
{
    Description = "Include source file paths as comments"
};
noteOption.Aliases.Add("-n");

var sortOption = new Option<string>("--sort")
{
    Description = "Sort by name or type"
};
sortOption.Aliases.Add("-s");

var removeEmptyLinesOption = new Option<bool>("--remove-empty-lines")
{
    Description = "Remove empty lines from source files"
};
removeEmptyLinesOption.Aliases.Add("-r");

var authorOption = new Option<string>("--author")
{
    Description = "Bundle author name"
};
authorOption.Aliases.Add("-a");

// Supported programming languages and file extensions.
var languageExtensions = new Dictionary<string, string[]>(
    StringComparer.OrdinalIgnoreCase)
{
    { "CSharp", new[] { ".cs", ".csx" } },
    { "Python", new[] { ".py", ".pyw" } },
    { "Java", new[] { ".java" } },
    { "JavaScript", new[] { ".js", ".jsx", ".mjs", ".cjs" } },
    { "TypeScript", new[] { ".ts", ".tsx", ".mts", ".cts" } },
    { "C", new[] { ".c", ".h" } },
    { "Cpp", new[] { ".cpp", ".hpp", ".cc", ".hh", ".cxx", ".hxx" } },
    { "HTML", new[] { ".html", ".htm" } },
    { "CSS", new[] { ".css", ".scss", ".sass", ".less" } },
    { "SQL", new[] { ".sql" } },
    { "PHP", new[] { ".php" } },
    { "Ruby", new[] { ".rb" } },
    { "Go", new[] { ".go" } },
    { "Rust", new[] { ".rs" } },
    { "Kotlin", new[] { ".kt", ".kts" } },
    { "Swift", new[] { ".swift" } },
    { "R", new[] { ".r", ".R" } },
    { "PowerShell", new[] { ".ps1", ".psm1" } },
    { "Shell", new[] { ".sh", ".bash" } },
    { "XML", new[] { ".xml", ".xaml" } },
    { "JSON", new[] { ".json" } },
    { "YAML", new[] { ".yaml", ".yml" } },
    { "FSharp", new[] { ".fs", ".fsi", ".fsx" } },
    { "VisualBasic", new[] { ".vb" } }
};

string GetFileNote(string path)
{
    string relativePath = path.Replace('\\', '/');
    string extension = Path.GetExtension(path).ToLowerInvariant();

    return extension switch
    {
        ".py" or ".pyw" or ".rb" or ".sh" or ".bash"
            or ".yaml" or ".yml" or ".r" or ".ps1" or ".psm1"
            => $"# File: {relativePath}",

        ".html" or ".htm" or ".xml" or ".xaml"
            => $"<!-- File: {relativePath} -->",

        ".css" or ".scss" or ".sass" or ".less"
            => $"/* File: {relativePath} */",

        ".sql"
            => $"-- File: {relativePath}",

        ".json"
            => $"// File: {relativePath}",

        _ => $"// File: {relativePath}"
    };
}

string RemoveEmptyLines(string content)
{
    return string.Join(
        Environment.NewLine,
        content.Split(
            new[] { "\r\n", "\n", "\r" },
            StringSplitOptions.None)
        .Where(line => !string.IsNullOrWhiteSpace(line)));
}

// Quote arguments containing spaces for a response file.
string QuoteArgument(string value)
{
    if (!value.Any(char.IsWhiteSpace) && !value.Contains('"'))
        return value;

    var result = new System.Text.StringBuilder("\"");
    int backslashes = 0;

    foreach (char c in value)
    {
        if (c == '\\')
        {
            backslashes++;
        }
        else if (c == '"')
        {
            result.Append('\\', backslashes * 2 + 1);
            result.Append('"');
            backslashes = 0;
        }
        else
        {
            result.Append('\\', backslashes);
            result.Append(c);
            backslashes = 0;
        }
    }

    result.Append('\\', backslashes * 2);
    result.Append('"');

    return result.ToString();
}

string RemoveSurroundingQuotes(string value)
{
    value = value.Trim();

    if (value.Length >= 2 &&
        value.StartsWith('"') &&
        value.EndsWith('"'))
    {
        return value[1..^1];
    }

    return value;
}

bool IsValidLanguageSelection(string[] languages)
{
    if (languages.Length == 0 ||
        languages.Any(string.IsNullOrWhiteSpace))
    {
        return false;
    }

    bool hasAll = languages.Any(
        language => language.Equals(
            "all", StringComparison.OrdinalIgnoreCase));

    if (hasAll && languages.Length != 1)
        return false;

    return hasAll ||
           languages.All(language =>
               languageExtensions.ContainsKey(language));
}

HashSet<string> GetSelectedExtensions(string[] languages)
{
    var extensions = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase);

    bool allLanguages = languages.Any(
        language => language.Equals(
            "all", StringComparison.OrdinalIgnoreCase));

    var selectedLanguages = allLanguages
        ? languageExtensions.Values
        : languages
            .Select(language => languageExtensions[language]);

    foreach (var languageExtensionsList in selectedLanguages)
    {
        foreach (var extension in languageExtensionsList)
            extensions.Add(extension);
    }

    return extensions;
}

var bundleCommand = new Command(
    "bundle",
    "Bundle source files into one file");

bundleCommand.Options.Add(outputOption);
bundleCommand.Options.Add(languageOption);
bundleCommand.Options.Add(noteOption);
bundleCommand.Options.Add(sortOption);
bundleCommand.Options.Add(removeEmptyLinesOption);
bundleCommand.Options.Add(authorOption);

bundleCommand.SetAction(parseResult =>
{
    var output = parseResult.GetValue(outputOption);
    var languages = parseResult.GetValue(languageOption)
                    ?? Array.Empty<string>();
    var includeNotes = parseResult.GetValue(noteOption);
    var sortBy = parseResult.GetValue(sortOption) ?? "name";
    var removeEmptyLines = parseResult.GetValue(removeEmptyLinesOption);
    var author = parseResult.GetValue(authorOption);

    if (output == null)
    {
        Console.WriteLine("Error: Output file is required.");
        return 1;
    }

    if (!IsValidLanguageSelection(languages))
    {
        Console.WriteLine(
            "Error: Enter valid language names, or 'all' by itself.");
        return 1;
    }

    if (!sortBy.Equals("name", StringComparison.OrdinalIgnoreCase) &&
        !sortBy.Equals("type", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("Error: --sort must be 'name' or 'type'.");
        return 1;
    }

    var currentDirectory = Directory.GetCurrentDirectory();
    var outputPath = Path.GetFullPath(output.FullName);

    try
    {
        var excludedDirectories = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            "bin",
            "obj",
            "debug",
            "release",
            ".git",
            ".vs",
            ".idea",
            ".vscode",
            "node_modules",
            "dist",
            "coverage",
            "packages"
        };

        var selectedExtensions = GetSelectedExtensions(languages);

        var selectedFiles = Directory
            .EnumerateFiles(
                currentDirectory,
                "*",
                SearchOption.AllDirectories)
            .Where(path =>
            {
                var relativePath = Path.GetRelativePath(
                    currentDirectory, path);

                var parts = relativePath.Split(
                    new[]
                    {
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar
                    },
                    StringSplitOptions.RemoveEmptyEntries);

                return !parts.Any(part =>
                    excludedDirectories.Contains(part));
            })
            .Where(path =>
                !Path.GetFullPath(path).Equals(
                    outputPath,
                    OperatingSystem.IsWindows()
                        ? StringComparison.OrdinalIgnoreCase
                        : StringComparison.Ordinal))
            .Where(path =>
                selectedExtensions.Contains(Path.GetExtension(path)))
            .OrderBy(path =>
                sortBy.Equals("type", StringComparison.OrdinalIgnoreCase)
                    ? Path.GetExtension(path)
                    : Path.GetFileName(path),
                StringComparer.OrdinalIgnoreCase)
            .ThenBy(
                path => Path.GetFileName(path),
                StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var outputDirectory = Path.GetDirectoryName(outputPath);

        if (string.IsNullOrEmpty(outputDirectory) ||
            !Directory.Exists(outputDirectory))
        {
            Console.WriteLine("Error: Output directory does not exist.");
            return 1;
        }

        using var writer = new StreamWriter(outputPath);

        if (!string.IsNullOrWhiteSpace(author))
        {
            writer.WriteLine($"// Author: {author.Trim()}");
            writer.WriteLine();
        }

        foreach (var path in selectedFiles)
        {
            var relativePath = Path.GetRelativePath(
                currentDirectory, path);

            var content = File.ReadAllText(path);

            if (removeEmptyLines)
                content = RemoveEmptyLines(content);

            if (includeNotes)
                writer.WriteLine(GetFileNote(relativePath));

            writer.WriteLine(content);
            writer.WriteLine();
        }

        Console.WriteLine($"Bundle created: {outputPath}");
        Console.WriteLine($"Files included: {selectedFiles.Length}");
    }
    catch (Exception ex) when (
        ex is IOException ||
        ex is UnauthorizedAccessException ||
        ex is ArgumentException ||
        ex is NotSupportedException ||
        ex is System.Security.SecurityException)
    {
        Console.WriteLine(
            $"Error: Could not create bundle. {ex.Message}");
        return 1;
    }

    return 0;
});

// Create a response file for the bundle command.
var createRspCommand = new Command(
    "create-rsp",
    "Create a response file for the bundle command");

createRspCommand.SetAction(_ =>
{
    Console.WriteLine("=== Create Response File ===");

    Console.Write("Output bundle path: ");
    var output = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(output))
    {
        Console.WriteLine("Error: Output path is required.");
        return 1;
    }

    output = RemoveSurroundingQuotes(output);

    Console.Write("Languages (all or names separated by spaces): ");
    var languageInput = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(languageInput))
    {
        Console.WriteLine("Error: Languages are required.");
        return 1;
    }

    var languages = languageInput.Split(
        ' ',
        StringSplitOptions.RemoveEmptyEntries);

    if (!IsValidLanguageSelection(languages))
    {
        Console.WriteLine(
            "Error: Enter valid language names, or 'all' by itself.");
        return 1;
    }

    Console.Write("Include source file notes? (y/n): ");
    var noteInput = Console.ReadLine()?.Trim().ToLowerInvariant();

    if (noteInput != "y" && noteInput != "n")
    {
        Console.WriteLine("Error: Enter y or n.");
        return 1;
    }

    Console.Write("Sort by name or type? (name/type): ");
    var sortBy = Console.ReadLine()?.Trim().ToLowerInvariant();

    if (sortBy != "name" && sortBy != "type")
    {
        Console.WriteLine("Error: Enter name or type.");
        return 1;
    }

    Console.Write("Remove empty lines? (y/n): ");
    var removeInput = Console.ReadLine()?.Trim().ToLowerInvariant();

    if (removeInput != "y" && removeInput != "n")
    {
        Console.WriteLine("Error: Enter y or n.");
        return 1;
    }

    Console.Write("Author name (leave empty to skip): ");
    var author = Console.ReadLine()?.Trim();

    Console.Write("Response file name (default: bundle.rsp): ");
    var rspName = Console.ReadLine()?.Trim();

    if (string.IsNullOrWhiteSpace(rspName))
        rspName = "bundle.rsp";
    else
        rspName = RemoveSurroundingQuotes(rspName);

    if (!rspName.EndsWith(
            ".rsp", StringComparison.OrdinalIgnoreCase))
    {
        rspName += ".rsp";
    }

    var arguments = new List<string>
    {
        "run",
        "--",
        "bundle",
        "--output",
        QuoteArgument(output),
        "--language"
    };

    arguments.AddRange(languages.Select(QuoteArgument));

    if (noteInput == "y")
        arguments.Add("--note");

    arguments.Add("--sort");
    arguments.Add(sortBy);

    if (removeInput == "y")
        arguments.Add("--remove-empty-lines");

    if (!string.IsNullOrWhiteSpace(author))
    {
        arguments.Add("--author");
        arguments.Add(QuoteArgument(author));
    }

    try
    {
        var rspPath = Path.GetFullPath(rspName);
        var rspDirectory = Path.GetDirectoryName(rspPath);

        if (string.IsNullOrEmpty(rspDirectory) ||
            !Directory.Exists(rspDirectory))
        {
            Console.WriteLine(
                "Error: Response file directory does not exist.");
            return 1;
        }

        File.WriteAllLines(rspPath, arguments);

        Console.WriteLine($"Response file created: {rspPath}");
        Console.WriteLine($"Run from this project directory: dotnet @{rspName}");
    }
    catch (Exception ex) when (
        ex is IOException ||
        ex is UnauthorizedAccessException ||
        ex is ArgumentException ||
        ex is NotSupportedException ||
        ex is System.Security.SecurityException)
    {
        Console.WriteLine(
            $"Error: Could not create response file. {ex.Message}");
        return 1;
    }

    return 0;
});

var rootCommand = new RootCommand("File Bundler CLI");

rootCommand.Subcommands.Add(bundleCommand);
rootCommand.Subcommands.Add(createRspCommand);

return rootCommand.Parse(args).Invoke();
