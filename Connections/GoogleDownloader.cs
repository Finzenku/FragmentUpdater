using System.IO;
using System.Reflection;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;

namespace FragmentUpdater.Connections
{
    static class GoogleDownloader
    {
        static readonly string[] Scopes = { DriveService.Scope.DriveReadonly };
        static readonly string ApplicationName = "Vi's Google Doc Downloader";
        static readonly string ConnectionJson = "FragmentUpdater.Connections.fragment_strings.json";
        static readonly string SpreadsheetId = "1vQvaTQXel9jUuUt-GwZZCcGi-JKyhm-LE6MKdIXKxIA";
        static readonly string MIMEType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        public static string DownloadFragmentPatches()
        {
#if DEBUG
            string filePath = @"P:\AppDownloads\Fragment Strings.xlsx";
#else
            string filePath = Path.Combine(Path.GetDirectoryName(AppContext.BaseDirectory), "FragmentPatchData.xlsx");
#endif

            GoogleCredential credential;

            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ConnectionJson))
            {
                credential = GoogleCredential.FromStream(stream).CreateScoped(Scopes);
            }

            var service = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });

            using (FileStream fileStream = File.Create(filePath))
            {
                service.Files.Export(SpreadsheetId, MIMEType).Download(fileStream);
            }

            if (File.Exists(filePath))
                return filePath;
            else
                return null;
        }

    }
}
