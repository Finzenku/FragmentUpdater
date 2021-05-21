using FragmentUpdater.Connections;
using FragmentUpdater.Models;
using System.Collections.Generic;
using System.Globalization;

namespace FragmentUpdater
{
    public static class PatchReader
    {
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
    }
}
