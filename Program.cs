using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using FragmentUpdater.Connections;
using FragmentUpdater.Models;

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
            Trace.Listeners.Clear();

            TextWriterTraceListener twt1 = new(@".\ViPatchLog.txt");
            twt1.Name = "TextLogger";
            twt1.TraceOutputOptions = TraceOptions.ThreadId | TraceOptions.DateTime;

            ConsoleTraceListener ctl = new(false);
            ctl.TraceOutputOptions = TraceOptions.DateTime;

            Trace.Listeners.Add(twt1);
            Trace.Listeners.Add(ctl);
            Trace.AutoFlush = true;

            Trace.WriteLine(DateTime.Now.ToString());
#if DEBUG
            string vanillaISO = @"P:\DotHack\Fragment\Tellipatch\fragment.iso",
                   coldbirdISO = @"P:\DotHack\Fragment\Fragment (Coldbird v08.13).iso",
                   telliISO = @"P:\DotHack\Fragment\Tellipatch\dotHack Fragment (0.9).iso",
                   aliceISO = @"P:\DotHack\Fragment\Tellipatch\fragmentAlice.iso",
                   inputISO = vanillaISO,
                   outputISO = @"P:\DotHack\Fragment\Tellipatch\fragmentCopy.iso";
#else
            string inputISO, outputISO;
            if (args.Length == 0)
            {
                inputISO = @".\fragment.iso";
                outputISO = @".\fragmentVi.iso";
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
                    Trace.WriteLine($"Could not find input file \"{inputISO}\" in the current directory.");
            }

            if (File.Exists(outputISO))
            {
                Trace.WriteLine($"Writing patches to: {outputISO}");
                Trace.WriteLine("Reading patches from google..");
                foreach (DotHackObject obj in GoogleReader.GetObjectsFromPatchSheet())
                {
                    UpdateISO(outputISO, obj);
                }
#if DEBUG
                Trace.WriteLine("Reading WIP patches from google..");
                foreach (DotHackObject obj in GoogleReader.GetObjectsFromPatchSheet("WIP Patches"))
                {
                    UpdateISO(outputISO, obj);
                }
#endif
                Trace.WriteLine("ISO patched successfully!");
            }
            else
            {
                Trace.WriteLine($"Could not find output file \"{outputISO}\" in the current directory.");
            }
        }

        private static void UpdateISO(string directory, DotHackObject objectType)
        {
            bool writeOffline = objectType.OfflineFile.FileName != DotHackFiles.NONE.FileName,
                 writeOnline = objectType.OnlineFile.FileName != DotHackFiles.NONE.FileName;
            if (writeOffline)
            {
                objectType.OfflineFile.ISOLocation = GetFileLocation(directory, objectType.OfflineFile.FileName);
            }
            if (writeOnline)
            {
                objectType.OnlineFile.ISOLocation = GetFileLocation(directory, objectType.OnlineFile.FileName);
            }

            using (FileStream isoStream = File.Open(directory, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
            using (BinaryWriter bw = new BinaryWriter(isoStream))
            {
                Dictionary<int, int> offsetPairs = new Dictionary<int, int>();
                if (objectType.TextSheetName != "None")
                {
                    //If we already made the text pointer dictionary we don't need to redo any of this
                    if (!textPointerDictionaries.TryGetValue(objectType.TextSheetName, out offsetPairs))
                    {
                        Trace.WriteLine($"Patching {objectType.Name} Text..");
                        Dictionary<int, string> objText = GoogleReader.GetNewStringsFromSheet($"{objectType.TextSheetName}");
                        offsetPairs = new Dictionary<int, int>();
                        int newoff = 0;
                        foreach (KeyValuePair<int, string> kvp in objText)
                        {
                            if (objectType.PointerOffsets.Length == 0)
                            {
                                newoff = kvp.Key;
                            }
                            if (!offsetPairs.ContainsKey(kvp.Key))
                            {
                                offsetPairs.Add(kvp.Key, newoff);
                            }
                            if (writeOffline)
                            {
                                bw.BaseStream.Position = objectType.OfflineFile.ISOLocation + objectType.OfflineStringBaseAddress + newoff;
                                bw.Write(enc.GetBytes(kvp.Value.Replace("\n", "\0").Replace("`", "\n")));
                            }
                            if (writeOnline)
                            {
                                bw.BaseStream.Position = objectType.OnlineFile.ISOLocation + objectType.OnlineStringBaseAddress + newoff;
                                //Trace.WriteLine($"{(objectType.OnlineStringBaseAddress + newoff).ToString("X")} => {kvp.Value}");
                                bw.Write(enc.GetBytes(kvp.Value.Replace("\n", "\0").Replace("`", "\n")));
                            }
                            newoff += enc.GetBytes(kvp.Value).Length;
                            if (newoff > objectType.StringByteLimit)
                                Trace.WriteLine("Writing outside string bounds!");
                            //Trace.WriteLine(objectType.OnlineStringBaseAddress.ToString("X8") +" "+newoff.ToString("X8"));
                        }
                        textPointerDictionaries.Add(objectType.TextSheetName,offsetPairs);
                    }
                }

                if (objectType.DataSheetName != "None")
                {
                    Trace.WriteLine($"Patching {objectType.Name} Pointers..");
                    var objs = GoogleReader.GetObjectsFromSheet($"{objectType.DataSheetName}");
                    foreach (KeyValuePair<int, List<int>> kvp in objs)
                    {
                        for (int i = 0; i < kvp.Value.Count; i++)
                        {
                            int p = kvp.Value[i];
                            if (p > 0)
                            {
                                //If an object has no text associated, we write the value data directly to the address
                                if (offsetPairs.Count == 0)
                                {
                                    if (writeOffline)
                                    {
                                        bw.BaseStream.Position = objectType.OfflineFile.ISOLocation + objectType.OfflineBaseAddress + kvp.Key + i*4;
                                        bw.Write(LittleEndian((p).ToString("X8")));
                                    }
                                    if (writeOnline)
                                    {
                                        bw.BaseStream.Position = objectType.OnlineFile.ISOLocation + objectType.OnlineBaseAddress + kvp.Key + i*4;
                                        bw.Write(LittleEndian((p).ToString("X8")));
                                    }
                                }
                                else
                                {
                                    offsetPairs.TryGetValue(p, out int s);
                                    if (writeOffline)
                                    {
                                        bw.BaseStream.Position = objectType.OfflineFile.ISOLocation + objectType.OfflineBaseAddress + kvp.Key + objectType.PointerOffsets[i];
                                        bw.Write(LittleEndian((objectType.OfflineStringBaseAddress + objectType.OfflineFile.LiveMemoryOffset + s).ToString("X8")));
                                    }
                                    if (writeOnline)
                                    {
                                        bw.BaseStream.Position = objectType.OnlineFile.ISOLocation + objectType.OnlineBaseAddress + kvp.Key + objectType.PointerOffsets[i];
                                        bw.Write(LittleEndian((objectType.OnlineStringBaseAddress + objectType.OnlineFile.LiveMemoryOffset + s).ToString("X8")));
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
            if (fileName.Contains(@"\"))
            {
                if (fileName.Contains(".iso"))
                    return fileName;
                else
                    return fileName = $"{fileName}.iso";
            }
            else
            {
                if (fileName.Contains(".iso"))
                    return fileName = @".\" + fileName;
                else
                    return fileName = @".\" + fileName + ".iso";
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
            Trace.WriteLine($"Could not find file: {fileName}");
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

        private static void ReadSheets(DotHackObject objectType)
        {
            var itemtext = GoogleReader.GetNewStringsFromSheet($"{objectType.TextSheetName}");
            int newoff = 0;
            Dictionary<int, int> offsetPairs = new Dictionary<int, int>();
            foreach (KeyValuePair<int, string> kvp in itemtext)
            {
                if (!offsetPairs.ContainsKey(kvp.Key)) offsetPairs.Add(kvp.Key, newoff);
                Trace.WriteLine(kvp.Key.ToString("X") + "\t" + newoff.ToString("X") + "\t\"" + kvp.Value.Replace("\n", " ") + "\"");
                newoff += enc.GetBytes(kvp.Value).Length;
            }
            var items2 = GoogleReader.GetObjectsFromSheet($"{objectType.DataSheetName}s");
            foreach (KeyValuePair<int, List<int>> kvp in items2)
            {
                Trace.Write(kvp.Key.ToString("X"));
                foreach (int p in kvp.Value)
                {
                    offsetPairs.TryGetValue(p, out int s);
                    Trace.Write("\t" + (objectType.OfflineStringBaseAddress + objectType.OfflineFile.LiveMemoryOffset + p).ToString("X8") + " => " + (objectType.OfflineStringBaseAddress + objectType.OfflineFile.LiveMemoryOffset + s).ToString("X8"));
                }
                Trace.WriteLine("");
            }
        }

        private static void CopyFile(string inputFilePath, string outputFilePath)
        {
            if (File.Exists(outputFilePath))
                File.Delete(outputFilePath);
            int bufferSize = 1024 * 1024;
            int progress = 0;
            Trace.WriteLine($"Copying {inputFilePath} to {outputFilePath}");
            using (FileStream fileStream = new FileStream(outputFilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite))
            {
                FileStream fs = new FileStream(inputFilePath, FileMode.Open, FileAccess.ReadWrite);
                fileStream.SetLength(fs.Length);
                int bytesRead = -1;
                byte[] bytes = new byte[bufferSize];

                while ((bytesRead = fs.Read(bytes, 0, bufferSize)) > 0)
                {
                    progress += bytesRead;
                    Console.Write($"\r{progress.ToString("X8")} / {fs.Length.ToString("X8")} Bytes...  ");
                    fileStream.Write(bytes, 0, bytesRead);
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
