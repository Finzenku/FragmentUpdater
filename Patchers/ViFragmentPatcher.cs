using FragmentUpdater.Connections;
using FragmentUpdater.Models;
using Ps2IsoTools.UDF;
using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace FragmentUpdater.Patchers
{
    public static class ViFragmentPatcher
    {
        private static Dictionary<string, Dictionary<int, int>> textPointerDictionaries;
        private static Encoding enc;

        static ViFragmentPatcher()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            enc = Encoding.GetEncoding(932);

            textPointerDictionaries = new();
        }

        public static void PatchISO(UdfEditor editor)
        {
            Dictionary<DotHackFile, Stream> fileStreams = new();
            Log.Logger.Information($"Downloading Vi's patches from Google..");
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

                foreach (DotHackPatch patch in ViFragmentPatcher.GetObjectsFromPatchSheet())
                {
                    ApplyDotHackPatch(patch, fileStreams);
                }

                foreach (DotHackPatch patch in ViFragmentPatcher.GetObjectsFromPatchSheet("IMG Patches"))
                {
                    ApplyDotHackPatch(patch, fileStreams);
                }
            }
            catch (Exception e)
            {
                Log.Logger.Error(e, "An error occured while reading patches:");
            }
            finally
            {
                Log.Logger.Information("Cleaning up Google patch files..");
                CleanUp();
            }
        }

        private static void ApplyDotHackPatch(DotHackPatch patch, Dictionary<DotHackFile, Stream> fileStreams)
        {
            bool writeOffline = patch.OfflineFile.FileName != DotHackFiles.NONE.FileName,
                 writeOnline = patch.OnlineFile.FileName != DotHackFiles.NONE.FileName;

            using (BinaryWriter offlineWriter = new(fileStreams[patch.OfflineFile], enc, true))
            using (BinaryWriter onlineWriter = new(fileStreams[patch.OnlineFile], enc, true))
            {
                Dictionary<int, int> offsetPairs = new Dictionary<int, int>();

                //If we already made the text pointer dictionary we don't need to redo any of this
                if (patch.TextSheetName != "None" && !textPointerDictionaries.TryGetValue(patch.TextSheetName, out offsetPairs))
                {
                    Log.Logger.Information($"Patching {patch.Name} Text..");
                    Dictionary<int, string> pointerTextPairs = ViFragmentPatcher.GetNewStringsFromSheet($"{patch.TextSheetName}");
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
                    var dataPatches = ViFragmentPatcher.GetPointersFromSheet($"{patch.DataSheetName}");
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
            }
        }

        public static List<DotHackPatch> GetObjectsFromPatchSheet(string patchSheet = "Patches")
        {
            List<DotHackPatch> objects = new();
            var values = ExcelReader.GetValuesFromSheet(patchSheet);
            if (values != null && values.Count > 0)
            {
                foreach (var row in values)
                {
                    DotHackPatch o = new DotHackPatch()
                    {
                        Name = (string)row[0],
                        DataSheetName = (string)row[1],
                        TextSheetName = (string)row[2],
                        OfflineFile = DotHackFiles.GetFileByName((string)row[3]),
                        OnlineFile = DotHackFiles.GetFileByName((string)row[4]),
                        OfflineBaseAddress = int.Parse((string)row[5], NumberStyles.HexNumber),
                        OnlineBaseAddress = int.Parse((string)row[6], NumberStyles.HexNumber),
                        OfflineStringBaseAddress = int.Parse((string)row[7], NumberStyles.HexNumber),
                        OnlineStringBaseAddress = int.Parse((string)row[8], NumberStyles.HexNumber),
                        ObjectReadLength = int.Parse((string)row[9], NumberStyles.HexNumber),
                        ObjectCount = int.Parse((string)row[10]),
                        StringByteLimit = int.Parse((string)row[11], NumberStyles.HexNumber),
                        PointerOffsets = new int[row.Count - 12]
                    };
                    for (int i = 0; i < o.PointerOffsets.Length; i++)
                        o.PointerOffsets[i] = int.Parse((string)row[12 + i], NumberStyles.HexNumber);
                    objects.Add(o);
                }
            }

            return objects;
        }

        public static Dictionary<int, string> GetNewStringsFromSheet(string sheetname)
        {
            var stringsByOffset = new Dictionary<int, string>();
            var values = ExcelReader.GetValuesFromSheet(sheetname);

            if (values != null && values.Count > 0)
            {
                foreach (var row in values)
                {
                    int offset = row.Count >= 1 ? int.Parse((string)row[0], NumberStyles.HexNumber) : 0;
                    string text = row.Count >= 3 ? (string)row[2] : row.Count >= 2 ? (string)row[1] : "\n";
                    stringsByOffset.Add(offset, text);
                }
            }

            return stringsByOffset;
        }

        public static Dictionary<int, List<int>> GetPointersFromSheet(string sheetname)
        {
            var objByOffset = new Dictionary<int, List<int>>();
            var values = ExcelReader.GetValuesFromSheet(sheetname);

            if (values != null && values.Count > 0)
            {
                foreach (var row in values)
                {
                    int offset = int.Parse((string)row[0], NumberStyles.HexNumber);
                    List<int> pointers = new List<int>();
                    for (int i = 1; i < row.Count; i++)
                        if ((string)row[i] != "-1") pointers.Add(int.Parse((string)row[i], NumberStyles.HexNumber));
                        else pointers.Add(-1);
                    objByOffset.Add(offset, pointers);
                }
            }

            return objByOffset;
        }

        private static byte[] LittleEndian(string hexString)
        {
            byte[] result = new byte[hexString.Length / 2];
            for (int i = 0; i < result.Length; i++)
            {
                result[result.Length -1 -i] = (byte)int.Parse(hexString.Substring(i*2, 2), NumberStyles.HexNumber);
            }
            return result;
        }

        public static void CleanUp()
        {
            ExcelReader.DeleteWorkbook();
        }
    }
}
