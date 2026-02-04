using System;
using System.IO;
using System.Threading.Tasks;

namespace XianYuLauncher.Helpers
{
    public static class IconHelper
    {
        public static async Task ConvertPngToIcoAsync(string pngPath, string icoPath)
        {
            if (!File.Exists(pngPath)) return;
            byte[] pngData = await File.ReadAllBytesAsync(pngPath);
            byte[] icoData = CreateIcoFromPng(pngData);
            await File.WriteAllBytesAsync(icoPath, icoData);
        }
        
        public static byte[] CreateIcoFromPng(byte[] pngData)
        {
            // Simple ICO format with one image (the PNG)
            // Header (6 bytes) + Directory Entry (16 bytes) + PNG Data
            
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                // 1. Header
                writer.Write((short)0);      // Reserved, must be 0
                writer.Write((short)1);      // Type, 1 = ICON
                writer.Write((short)1);      // Count, 1 image

                // 2. Directory Entry
                // We need valid width/height from PNG to be perfectly spec compliant, 
                // but 0 means 256px or "look at image". 
                // For simplicity without image parsing lib, we use 0 (256) which is common for Vista+ PNG icons.
                // Or we can try to peek PNG header.
                
                int width = 0;
                int height = 0;
                
                // Peek PNG header for IHDR
                // Signature: 89 50 4E 47 0D 0A 1A 0A (8 bytes)
                // Chunk Length: 4 bytes
                // Chunk Type: "IHDR" (4 bytes)
                // Width: 4 bytes (Big Endian)
                // Height: 4 bytes (Big Endian)
                
                if (pngData.Length > 24 && pngData[0] == 0x89 && pngData[1] == 0x50 && pngData[2] == 0x4E && pngData[3] == 0x47)
                {
                    // Read width/height from IHDR (offset 16)
                    // PNG stores integers as big-endian, need to convert to system endianness
                    var wRaw = new byte[] { pngData[16], pngData[17], pngData[18], pngData[19] };
                    var hRaw = new byte[] { pngData[20], pngData[21], pngData[22], pngData[23] };
                    
                    // Convert from big-endian to the system endianness
                    if (BitConverter.IsLittleEndian)
                    {
                        Array.Reverse(wRaw);
                        Array.Reverse(hRaw);
                    }
                    
                    int w = BitConverter.ToInt32(wRaw, 0);
                    int h = BitConverter.ToInt32(hRaw, 0);
                    
                    if (w < 256) width = w;
                    if (h < 256) height = h;
                }

                writer.Write((byte)width);   // Width
                writer.Write((byte)height);  // Height
                writer.Write((byte)0);       // Palette Count (0 for No Palette)
                writer.Write((byte)0);       // Reserved (0)
                writer.Write((short)1);      // Color Planes (1)
                writer.Write((short)32);     // Bits Per Pixel (32)
                writer.Write((int)pngData.Length); // Size of image data
                writer.Write((int)22);       // Offset of image data (6+16=22)

                // 3. Image Data
                writer.Write(pngData);
                
                return ms.ToArray();
            }
        }
    }
}
