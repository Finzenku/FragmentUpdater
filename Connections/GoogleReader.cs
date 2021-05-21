using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using System.Collections.Generic;
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
        static SheetsService service;
        static GoogleCredential credential;

        public static IList<IList<object>> GetValuesFromRange(string sheetName)
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
                var request = service.Spreadsheets.Values.Get(SpreadsheetId, $"{sheetName}!A2:V");
                var response = request.Execute();
                return response.Values;
            }
        }
    }
}
