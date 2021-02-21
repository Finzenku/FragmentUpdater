using FragmentUpdater.Models;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace FragmentUpdater.Connections
{
    public class GoogleReader
    {
        static readonly string[] Scopes = { SheetsService.Scope.Spreadsheets };
        static readonly string ApplicationName = "Fragment Updater";
        static readonly string ConnectionJson = "FragmentUpdater.Connections.fragment_strings.json";
        static readonly string SpreadsheetId = "1vQvaTQXel9jUuUt-GwZZCcGi-JKyhm-LE6MKdIXKxIA";
        private static SheetsService service;
        private static GoogleCredential credential;

        public static List<DotHackObject> GetObjectsFromPatchSheet(string patchSheet = "Patches")
        {
            List<DotHackObject> objects = new();
            var assembly = Assembly.GetExecutingAssembly();
            
            
            using (Stream stream = assembly.GetManifestResourceStream(ConnectionJson))
            {
                credential = GoogleCredential.FromStream(stream).CreateScoped(Scopes);
            }
            

            
            
            
            using (service = new SheetsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            }))
            {
                var stringsByOffset = new Dictionary<int, string>();
                var range = $"{patchSheet}!A2:V";
                var request = service.Spreadsheets.Values.Get(SpreadsheetId, range);
                var response = request.Execute();
                var values = response.Values;
                if (values != null && values.Count > 0)
                {
                    foreach (var row in values)
                    {
                        DotHackObject o = new DotHackObject()
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
            }

            return objects;
        }
        public static Dictionary<int, string> GetNewStringsFromSheet(string sheetname)
        {
            var assembly = Assembly.GetExecutingAssembly();
            
            using (Stream stream = assembly.GetManifestResourceStream(ConnectionJson))
            {
                credential = GoogleCredential.FromStream(stream).CreateScoped(Scopes);
            }


            using (service = new SheetsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            }))
            {
                var stringsByOffset = new Dictionary<int, string>();
                var range = $"{sheetname}!A2:C";
                var request = service.Spreadsheets.Values.Get(SpreadsheetId, range);
                var response = request.Execute();
                var values = response.Values;
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
        }

        public static Dictionary<int, List<int>> GetObjectsFromSheet(string sheetname)
        {
            var assembly = Assembly.GetExecutingAssembly();
            
            using (Stream stream = assembly.GetManifestResourceStream(ConnectionJson))
            {
                credential = GoogleCredential.FromStream(stream).CreateScoped(Scopes);
            }



            using (service = new SheetsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            }))
            {
                var objByOffset = new Dictionary<int, List<int>>();
                var range = $"{sheetname}!A2:G";
                var request = service.Spreadsheets.Values.Get(SpreadsheetId, range);
                var response = request.Execute();
                var values = response.Values;
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

}
