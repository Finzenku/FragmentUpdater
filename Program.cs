using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using FragmentUpdater.Models;
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
            string inputISO = @"P:\DotHack\Fragment\Tellipatch\fragment.iso",
                   outputISO = @"P:\DotHack\Fragment\Tellipatch\fragmentCopy.iso";
#else
            string inputISO, outputISO, loc = System.AppContext.BaseDirectory;
            if (loc == "")
                loc = @".\";
            if (args.Length == 0)
            {
                inputISO = Path.Combine(Path.GetDirectoryName(loc), "fragment.iso");
                outputISO = Path.Combine(Path.GetDirectoryName(loc), "fragmentVi.iso");
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
                Log.Logger.Information($"Downloading patches from Google..");
                try
                {
                    foreach (DotHackFile file in DotHackFiles.GetFiles())
                    {
                        file.ISOLocation = GetFileLocation(outputISO, file.FileName);
                    }

                    foreach (DotHackPatch obj in PatchHandler.GetObjectsFromPatchSheet())
                    {
                        UpdateISO(outputISO, obj);
                    }
#if Full_Version
                    //Console.WriteLine("Reading WIP patches from google..");
                    foreach (DotHackPatch obj in PatchHandler.GetObjectsFromPatchSheet("WIP Patches"))
                    {
                        UpdateISO(outputISO, obj);
                    }
                    //Console.WriteLine("Reading image patches from google..");
                    foreach (DotHackPatch obj in PatchHandler.GetObjectsFromPatchSheet("IMG Patches"))
                    {
                        UpdateISO(outputISO, obj);
                    }
#endif
#if DEBUG                    
                    //Console.WriteLine("Reading WIP patches from google..");
                    foreach (DotHackPatch obj in PatchHandler.GetObjectsFromPatchSheet("WIP Patches"))
                    {
                        UpdateISO(outputISO, obj);
                    }
                    //Console.WriteLine("Reading image patches from google..");
                    foreach (DotHackPatch obj in PatchHandler.GetObjectsFromPatchSheet("IMG Patches"))
                    {
                        UpdateISO(outputISO, obj);
                    }

#endif
                }
                catch (Exception e)
                {
                    Log.Logger.Error(e, "An error occured while reading patches:");
                }
                finally
                {
                    Log.Logger.Information("Cleaning up patch files..");
                    PatchHandler.CleanUp();
                }

                Log.Logger.Information("Vi Patch process complete!");
            }
            else
            {
                Log.Logger.Error($"Could not find output file \"{outputISO}\" in the current directory.");
            }
        }

        private static void UpdateISO(string directory, DotHackPatch patch)
        {
            bool writeOffline = patch.OfflineFile.FileName != DotHackFiles.NONE.FileName,
                 writeOnline = patch.OnlineFile.FileName != DotHackFiles.NONE.FileName;

            using (FileStream isoStream = File.Open(directory, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
            using (BinaryWriter bw = new BinaryWriter(isoStream))
            {
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
                            bw.BaseStream.Position = patch.OfflineFile.ISOLocation + patch.OfflineStringBaseAddress + newoff;
                            bw.Write(enc.GetBytes(kvp.Value.Replace("\n", "\0").Replace("`", "\n")));
                        }
                        if (writeOnline)
                        {
                            bw.BaseStream.Position = patch.OnlineFile.ISOLocation + patch.OnlineStringBaseAddress + newoff;
                            bw.Write(enc.GetBytes(kvp.Value.Replace("\n", "\0").Replace("`", "\n")));
                        }
                        newoff += enc.GetBytes(kvp.Value).Length;
                        if (newoff > patch.StringByteLimit)
                            Log.Logger.Warning("Writing outside data bounds!");
                    }
                    textPointerDictionaries.Add(patch.TextSheetName,offsetPairs);
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
                                        bw.BaseStream.Position = patch.OfflineFile.ISOLocation + patch.OfflineBaseAddress + kvp.Key + i*4;
                                        bw.Write(LittleEndian((p).ToString("X8")));
                                    }
                                    if (writeOnline)
                                    {
                                        bw.BaseStream.Position = patch.OnlineFile.ISOLocation + patch.OnlineBaseAddress + kvp.Key + i*4;
                                        bw.Write(LittleEndian((p).ToString("X8")));
                                    }
                                }
                                else
                                {
                                    offsetPairs.TryGetValue(p, out int s);
                                    if (writeOffline)
                                    {
                                        bw.BaseStream.Position = patch.OfflineFile.ISOLocation + patch.OfflineBaseAddress + kvp.Key + patch.PointerOffsets[i];
                                        bw.Write(LittleEndian((patch.OfflineStringBaseAddress + patch.OfflineFile.LiveMemoryOffset + s).ToString("X8")));
                                    }
                                    if (writeOnline)
                                    {
                                        bw.BaseStream.Position = patch.OnlineFile.ISOLocation + patch.OnlineBaseAddress + kvp.Key + patch.PointerOffsets[i];
                                        bw.Write(LittleEndian((patch.OnlineStringBaseAddress + patch.OnlineFile.LiveMemoryOffset + s).ToString("X8")));
                                    }
                                }
                            }
                        }
                    }
                }
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

        private static int GetFileLocation(string directory, string fileName)
        {
            byte[] nameBytes = enc.GetBytes(fileName);
            using (FileStream stream = File.Open(directory, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
            using (BinaryReader br = new BinaryReader(stream))
            {
                br.BaseStream.Position = 0x80000;
                byte[] buffer = new byte[0x800];
                for (int i = 0; i < 125; i++)
                {
                    br.BaseStream.Position = 0x80000 + i * 0x800;
                    int pos = (int)br.BaseStream.Position;
                    buffer = br.ReadBytes(0x800);
                    int filePos = Search(buffer, nameBytes);
                    if (filePos >= 0)
                    {
                        br.BaseStream.Position = pos + filePos - 0x1F;
                        int res = br.ReadInt32() * 0x800;
                        return res;
                    }
                }
            }
            Log.Logger.Error($"Could not find file: {fileName}");
            return -1;
        }

        private static int Search(byte[] src, byte[] pattern)
        {
            int c = src.Length - pattern.Length + 1;
            int j;
            for (int i = 0; i < c; i++)
            {
                if (src[i] != pattern[0]) continue;
                for (j = pattern.Length - 1; j >= 1 && src[i + j] == pattern[j]; j--) ;
                if (j == 0) return i;
            }
            return -1;
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
