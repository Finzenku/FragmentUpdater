using System;
using System.IO;
using FragmentUpdater.Patchers;
using Ps2IsoTools.UDF;
using Serilog;

namespace FragmentUpdater
{
    class Program
    {
        static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File(Path.Combine(AppContext.BaseDirectory, "ViPatchLog.txt"), outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message}{NewLine}{Exception}")
                .MinimumLevel.Information()
                .CreateLogger();

            Log.Logger.Information("Beginning Vi's Fragment Updater");
#if DEBUG
            string inputISO = @"P:\DotHack\Fragment\ViPatch\fragment.iso",
                   outputISO = @"P:\DotHack\Fragment\ViPatch\fragmentCopy.iso";
#else
            string inputISO, outputISO, loc = System.AppContext.BaseDirectory;
            if (loc == "")
                loc = @".\";
            if (args.Length == 0)
            {
                inputISO = Path.Combine(Path.GetDirectoryName(loc), "fragment.iso");
                outputISO = Path.Combine(Path.GetDirectoryName(loc), "dotHack fragment (EN).iso");
            }
            else if (args.Length == 1)
            {
                inputISO = CheckFilePath(args[0]);
                outputISO = inputISO;
            }
            else
            {
                inputISO = CheckFilePath(args[0]);
                outputISO = CheckFilePath(args[1]);
            }
#endif

            if (inputISO != outputISO)
            {
                if (File.Exists(inputISO))
                    CopyFile(inputISO, outputISO);
                else
                    Log.Logger.Error($"Could not find input file \"{inputISO}\".");
            }
            if (File.Exists(outputISO))
            {
                Log.Logger.Information($"Writing patches to: {outputISO}");

                using (UdfEditor editor = new(outputISO))
                {
                    ViFragmentPatcher.PatchISO(editor);
                }

                Log.Logger.Information("Patch process complete!");
            }
            else
            {
                Log.Logger.Error($"Could not find output file \"{outputISO}\".");
            }
        }

        private static string CheckFilePath(string fileName)
        {
            if (fileName.Contains(@"\") || fileName.Contains(@"/"))
            {
                if (fileName.Contains(".iso"))
                    return fileName;
                else
                    return $"{fileName}.iso";
            }
            else
            {
                string loc = AppContext.BaseDirectory;
                if (loc == "")
                    loc = @".\";
                if (fileName.Contains(".iso"))
                    return Path.Combine(Path.GetDirectoryName(loc), fileName);
                else
                    return Path.Combine(Path.GetDirectoryName(loc), $"{fileName}.iso");
            }
        }

        private static void CopyFile(string inputFilePath, string outputFilePath)
        {
            if (File.Exists(outputFilePath))
                File.Delete(outputFilePath);
            int bufferSize = 1024 * 1024;
            int progress = 0;
            Log.Logger.Information($"Copying {inputFilePath} to {outputFilePath}");
            using (FileStream fileStream = new FileStream(outputFilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite))
            {
                using (FileStream fs = new FileStream(inputFilePath, FileMode.Open, FileAccess.ReadWrite))
                {
                    fileStream.SetLength(fs.Length);
                    int bytesRead = -1;
                    byte[] bytes = new byte[bufferSize];

                    while ((bytesRead = fs.Read(bytes, 0, bufferSize)) > 0)
                    {
                        progress += bytesRead;
                        Console.Write($"\r{progress.ToString("X8")} / {fs.Length.ToString("X8")} bytes copied...  ");
                        fileStream.Write(bytes, 0, bytesRead);
                    }
                }
                Console.WriteLine("");
            }
        }

    }
}
