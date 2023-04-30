using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using FragmentUpdater.Models;
using Ps2IsoTools.UDF;
using Serilog;

namespace FragmentUpdater
{
    class Program
    {
        static Encoding enc;
        private static Dictionary<string, Dictionary<int, int>> textPointerDictionaries;

        static void Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            enc = Encoding.GetEncoding(932);

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

            textPointerDictionaries = new Dictionary<string, Dictionary<int, int>>();
            if (inputISO != outputISO)
            {
                if (File.Exists(inputISO))
                    CopyFile(inputISO, outputISO);
                else
                    Log.Logger.Error($"Could not find input file \"{inputISO}\" in the current directory.");
            }
            if (File.Exists(outputISO))
            {
                Log.Logger.Information($"Writing patches to: {outputISO}");

                using (UdfEditor editor = new(outputISO))
                {
                    ApplyViGooglePatchs(editor);
                }

                Log.Logger.Information("Vi Patch process complete!");
            }
            else
            {
                Log.Logger.Error($"Could not find output file \"{outputISO}\" in the current directory.");
            }
        }

        private static void ApplyViGooglePatchs(UdfEditor editor)
        {
            Dictionary<DotHackFile, Stream> fileStreams = new();
            Log.Logger.Information($"Downloading patches from Google..");
            try
            {
                foreach (DotHackFile file in DotHackFiles.GetFiles())
                {
                    var fileId = editor.GetFileByName(file.FileName);
                    if (fileId is null)
                        throw new ArgumentException($"Could not find file: {file.FileName}");
                    fileStreams.Add(file, editor.GetFileStream(fileId));
                }
                fileStreams.Add(DotHackFiles.NONE, new MemoryStream());

                foreach (DotHackPatch patch in PatchHandler.GetObjectsFromPatchSheet())
                {
                    UpdateISO(patch, fileStreams);
                }

                foreach (DotHackPatch patch in PatchHandler.GetObjectsFromPatchSheet("IMG Patches"))
                {
                    UpdateISO(patch, fileStreams);
                }
            }
            catch (Exception e)
            {
                Log.Logger.Error(e, "An error occured while reading patches:");
            }
            finally
            {
                Log.Logger.Information("Cleaning up Google patch files..");
                PatchHandler.CleanUp();
            }
        }

        private static void UpdateISO(DotHackPatch patch, Dictionary<DotHackFile, Stream> fileStreams)
        {
            bool writeOffline = patch.OfflineFile.FileName != DotHackFiles.NONE.FileName,
                 writeOnline = patch.OnlineFile.FileName != DotHackFiles.NONE.FileName;

            BinaryWriter offlineWriter = new(fileStreams[patch.OfflineFile], enc, true);
            BinaryWriter onlineWriter = new(fileStreams[patch.OnlineFile], enc, true);

            Dictionary<int, int> offsetPairs = new Dictionary<int, int>();

            //If we already made the text pointer dictionary we don't need to redo any of this
            if (patch.TextSheetName != "None" && !textPointerDictionaries.TryGetValue(patch.TextSheetName, out offsetPairs))
            {
                Log.Logger.Information($"Patching {patch.Name} Text..");
                Dictionary<int, string> pointerTextPairs = PatchHandler.GetNewStringsFromSheet($"{patch.TextSheetName}");
                offsetPairs = new Dictionary<int, int>();
                int newoff = 0;
                foreach (KeyValuePair<int, string> kvp in pointerTextPairs)
                {
                    if (patch.PointerOffsets.Length == 0)
                    {
                        newoff = kvp.Key;
                    }
                    if (!offsetPairs.ContainsKey(kvp.Key))
                    {
                        offsetPairs.Add(kvp.Key, newoff);
                    }
                    if (writeOffline)
                    {
                        offlineWriter.BaseStream.Position = patch.OfflineStringBaseAddress + newoff;
                        offlineWriter.Write(enc.GetBytes(kvp.Value.Replace("\n", "\0").Replace("`", "\n")));
                    }
                    if (writeOnline)
                    {
                        onlineWriter.BaseStream.Position = patch.OnlineStringBaseAddress + newoff;
                        onlineWriter.Write(enc.GetBytes(kvp.Value.Replace("\n", "\0").Replace("`", "\n")));
                    }
                    newoff += enc.GetBytes(kvp.Value).Length;
                    if (newoff > patch.StringByteLimit)
                        Log.Logger.Warning("Writing outside data bounds!");
                }
                textPointerDictionaries.Add(patch.TextSheetName, offsetPairs);
            }

            if (patch.DataSheetName != "None")
            {
                Log.Logger.Information($"Patching {patch.Name} Data..");
                var dataPatches = PatchHandler.GetPointersFromSheet($"{patch.DataSheetName}");
                foreach (KeyValuePair<int, List<int>> kvp in dataPatches)
                {
                    for (int i = 0; i < kvp.Value.Count; i++)
                    {
                        int p = kvp.Value[i];
                        if (p != -1)
                        {
                            //If an object has no text associated, we write the value data directly to the address
                            if (offsetPairs.Count == 0)
                            {
                                if (writeOffline)
                                {
                                    offlineWriter.BaseStream.Position = patch.OfflineBaseAddress + kvp.Key + i*4;
                                    offlineWriter.Write(LittleEndian((p).ToString("X8")));
                                }
                                if (writeOnline)
                                {
                                    onlineWriter.BaseStream.Position = patch.OnlineBaseAddress + kvp.Key + i*4;
                                    onlineWriter.Write(LittleEndian((p).ToString("X8")));
                                }
                            }
                            else
                            {
                                offsetPairs.TryGetValue(p, out int s);
                                if (writeOffline)
                                {
                                    offlineWriter.BaseStream.Position = patch.OfflineBaseAddress + kvp.Key + patch.PointerOffsets[i];
                                    offlineWriter.Write(LittleEndian((patch.OfflineStringBaseAddress + patch.OfflineFile.LiveMemoryOffset + s).ToString("X8")));
                                }
                                if (writeOnline)
                                {
                                    onlineWriter.BaseStream.Position = patch.OnlineBaseAddress + kvp.Key + patch.PointerOffsets[i];
                                    onlineWriter.Write(LittleEndian((patch.OnlineStringBaseAddress + patch.OnlineFile.LiveMemoryOffset + s).ToString("X8")));
                                }
                            }
                        }
                    }
                }
            }
            offlineWriter.Dispose();
            onlineWriter.Dispose();
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

        private static byte[] LittleEndian(string hexString)
        {
            byte[] result = new byte[hexString.Length / 2];
            for (int i = 0; i < result.Length; i++)
            {
                result[result.Length -1 -i] = (byte)int.Parse(hexString.Substring(i*2,2), NumberStyles.HexNumber);
            }
            return result;
        }
    }
}
