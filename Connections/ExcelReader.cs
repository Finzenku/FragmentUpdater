using ExcelDataReader;
using System;
using System.Collections.Generic;
using System.IO;

namespace FragmentUpdater.Connections
{
    public static class ExcelReader
    {
        private static string filePath;

        static ExcelReader()
        {
            filePath = GoogleDownloader.DownloadFragmentPatches();
        }

        public static List<List<object>> GetValuesFromSheet(string sheetName)
        {
            List<List<object>> cellValues = new();

            using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read))
            {
                using (var reader = ExcelReaderFactory.CreateReader(stream, new ExcelReaderConfiguration()))
                {
                    //Iterate through the sheets until we find the correct sheet name
                    //Limit to the number of sheets in the file (ResultsCount)
                    for (int i = 0; i < reader.ResultsCount && reader.Name != sheetName; i++)
                    {
                        reader.NextResult();
                    }

                    //If the previous loop finished without finding the correct sheet
                    if (reader.Name != sheetName)
                        throw new Exception($"Could not find sheet with name: {sheetName}");

                    //Read the first row (They're headers so we don't do anything)
                    reader.Read();

                    //Read each remaining row until the first value is empty
                    while (reader.Read() && reader.GetValue(0) != null)
                    {
                        List<object> list = new();

                        //Read each column in the row until an empty value is reached
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            var o = reader.GetString(i);
                            if (o != null)
                                list.Add(o);
                            else
                                break;
                        }

                        cellValues.Add(list);
                    }
                }
            }
            return cellValues;
        }

        public static void DeleteWorkbook()
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }
}
