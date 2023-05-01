using Ps2IsoTools.UDF;
using Serilog;
using System;
using System.IO;

namespace FragmentUpdater.Patchers
{
    public static class EnglishGruntyFoodPatcher
    {
        private static readonly byte[] gcmnPatchData = new byte[]
        {
            0x00, 0x00, 0x00, 0x00, 0x00, 0xD1, 0x00, 0x00, 0x00, 0xD8, 0x00, 0x00, 0x30, 0xD2, 0x01, 0x00,
            0x00, 0xB0, 0x02, 0x00, 0x00, 0x16, 0x01, 0x00, 0x00, 0xC8, 0x03, 0x00, 0x00, 0xB6, 0x01, 0x00,
            0x00, 0x80, 0x05, 0x00, 0x74, 0x7C, 0x01, 0x00, 0x00, 0x00, 0x07, 0x00, 0xA6, 0xC6, 0x01, 0x00,
            0x00, 0xC8, 0x08, 0x00, 0x00, 0xC2, 0x01, 0x00, 0x00, 0x90, 0x0A, 0x00, 0x50, 0x22, 0x01, 0x00,
            0x00, 0xB8, 0x0B, 0x00, 0x9A, 0xBB, 0x01, 0x00, 0x00, 0x78, 0x0D, 0x00, 0x56, 0x3C, 0x01, 0x00,
            0x00, 0xB8, 0x0E, 0x00, 0x0E, 0x65, 0x01, 0x00, 0x00, 0x20, 0x10, 0x00, 0x00, 0xC1, 0x01, 0x00,
            0x00, 0xE8, 0x11, 0x00, 0x54, 0x1B, 0x01, 0x00, 0x00, 0x08, 0x13, 0x00, 0xD8, 0x64, 0x01, 0x00,
            0x00, 0x70, 0x14, 0x00, 0x80, 0x51, 0x01, 0x00, 0x00, 0xC8, 0x15, 0x00, 0xBE, 0x7D, 0x01, 0x00
        };

        private static readonly int gcmnFPos = 0xFBF80, gcmnOPos = 0xEC620;

        public static void PatchISO(UdfEditor editor, Stream foodEStream, string outputIso = "")
        {
            try
            {
                Log.Logger.Information("Patching English grunty voices..");
                var food = editor.GetFileByName("FOOD.BIN");
                if (food is null)
                    throw new ArgumentException($"Could not find file: FOOD.BIN");

                var gcmnf = editor.GetFileByName("gcmnf.prg");
                if (gcmnf is null)
                    throw new ArgumentException($"Could not find file: gcmnf.prg");

                var gcmno = editor.GetFileByName("gcmno.prg");
                if (gcmno is null)
                    throw new ArgumentException($"Could not find file: gcmno.prg");

                Log.Logger.Information("Replacing file..");
                editor.ReplaceFileStream(food, foodEStream);
                Log.Logger.Information("Patching data..");
                using (BinaryWriter bw = new(editor.GetFileStream(gcmnf)))
                {
                    bw.BaseStream.Position = gcmnFPos;
                    bw.Write(gcmnPatchData);
                }
                using (BinaryWriter bw = new(editor.GetFileStream(gcmno)))
                {
                    bw.BaseStream.Position = gcmnOPos;
                    bw.Write(gcmnPatchData);
                }

                Log.Logger.Information("Rebuilding ISO..");
                editor.Rebuild(outputIso);
            }
            catch (Exception e)
            {
                Log.Logger.Error(e, "An error occured while reading patches:");
            }
            finally
            {

            }
        }
    }
}
