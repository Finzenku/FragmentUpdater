namespace FragmentUpdater.Models
{
    public class DotHackPatch
    {
        public string Name { get; set; }
        public string DataSheetName { get; set; }
        public string TextSheetName { get; set; }
        public DotHackFile OnlineFile { get; set; }
        public DotHackFile OfflineFile { get; set; }
        public int OnlineBaseAddress { get; set; }
        public int OfflineBaseAddress { get; set; }
        public int OnlineStringBaseAddress { get; set; }
        public int OfflineStringBaseAddress { get; set; }
        public int ObjectReadLength { get; set; }
        public int ObjectCount { get; set; }
        public int StringByteLimit { get; set; }
        public int[] PointerOffsets { get; set; }
    }
}
