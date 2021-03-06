﻿using System.Collections.Generic;

namespace FragmentUpdater.Models
{
    public static class DotHackFiles
    {
        public static DotHackFile SLUS { get; } = new DotHackFile { FileName = "SLPS_255.27", LiveMemoryOffset = 0xFFF00 };
        public static DotHackFile HACK1 { get; } = new DotHackFile { FileName = "HACK_01.ELF", LiveMemoryOffset = 0xFFF00 };
        public static DotHackFile HACK0 { get; } = new DotHackFile { FileName = "HACK_00.ELF", LiveMemoryOffset = 0xFFF00 };
        public static DotHackFile GCMNF { get; } = new DotHackFile { FileName = "GCMNF.PRG", LiveMemoryOffset = 0x537600 };
        public static DotHackFile GCMNO { get; } = new DotHackFile { FileName = "GCMNO.PRG", LiveMemoryOffset = 0x78CB80 };
        public static DotHackFile DESKTOPF { get; } = new DotHackFile { FileName = "DESKTOPF.PRG", LiveMemoryOffset = 0x537600 };
        public static DotHackFile TOPPAGEF { get; } = new DotHackFile { FileName = "TOPPAGEF.PRG", LiveMemoryOffset = 0x537600 };
        public static DotHackFile MATCHING { get; } = new DotHackFile { FileName = "MATCHING.PRG", LiveMemoryOffset = 0x78CB80 };
        public static DotHackFile DATA { get; } = new DotHackFile { FileName = "DATA.BIN", LiveMemoryOffset = 0 };
        public static DotHackFile NONE { get; } = new DotHackFile { FileName = "NONE", LiveMemoryOffset = 0 };

        public static List<DotHackFile> GetFiles()
        {
            return new List<DotHackFile>() { SLUS, GCMNO, GCMNF, HACK0, HACK1, DESKTOPF, TOPPAGEF, MATCHING, DATA };
        }

        public static DotHackFile GetFileByName(string fileName)
        {
            return fileName switch
            {
                "SLPS_255.27" => SLUS,
                "HACK_01.ELF" => HACK1,
                "HACK_00.ELF" => HACK0,
                "GCMNF.PRG" => GCMNF,
                "GCMNO.PRG" => GCMNO,
                "DESKTOPF.PRG" => DESKTOPF,
                "TOPPAGEF.PRG" => TOPPAGEF,
                "MATCHING.PRG" => MATCHING,
                "DATA.BIN" => DATA,
                _ => NONE
            };
        }
    }
}
