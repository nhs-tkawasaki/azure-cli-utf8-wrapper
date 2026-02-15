using System.Diagnostics;
using System.Globalization;
using System.Text;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var originAz = FindOriginAz();
if (originAz == null)
{
    Console.Error.WriteLine("Error: az.cmd not found in PATH");
    return 1;
}

var psi = new ProcessStartInfo
{
    FileName = originAz,
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    UseShellExecute = false,
    StandardOutputEncoding = null,
    StandardErrorEncoding = null,
};
foreach (var arg in args)
    psi.ArgumentList.Add(arg);

using var proc = Process.Start(psi)!;

var systemEnc = GetSystemEncoding();

var stdoutTask = Task.Run(() => TranscodeStream(proc.StandardOutput.BaseStream, Console.OpenStandardOutput(), systemEnc));
var stderrTask = Task.Run(() => TranscodeStream(proc.StandardError.BaseStream, Console.OpenStandardError(), systemEnc));

await Task.WhenAll(stdoutTask, stderrTask);
await proc.WaitForExitAsync();

return proc.ExitCode;

static string? FindOriginAz()
{
    var selfDir = Path.GetDirectoryName(Environment.ProcessPath)!;
    var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];

    foreach (var d in pathDirs)
    {
        if (string.IsNullOrWhiteSpace(d))
            continue;
        if (string.Equals(Path.GetFullPath(d), Path.GetFullPath(selfDir), StringComparison.OrdinalIgnoreCase))
            continue;

        var candidate = Path.Combine(d, "az.cmd");
        if (File.Exists(candidate))
            return candidate;
    }
    return null;
}

static void TranscodeStream(Stream input, Stream output, Encoding sourceEnc)
{
    using var reader = new StreamReader(input, sourceEnc);
    using var writer = new StreamWriter(output, new UTF8Encoding(false)) { AutoFlush = true };

    var buffer = new char[4096];
    int read;
    while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
    {
        writer.Write(buffer, 0, read);
    }
}

static Encoding GetSystemEncoding()
{
    var codePage = CultureInfo.CurrentCulture.TextInfo.ANSICodePage;
    if (codePage == 0) codePage = Encoding.Default.CodePage;
    return Encoding.GetEncoding(codePage);
}
