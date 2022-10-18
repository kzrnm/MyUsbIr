using System.IO;
using System.IO.Compression;

namespace UsbIrSetting
{
    public static class Compress
    {
        public static byte[] CompressGZip(byte[] input)
        {
            using var ms = new MemoryStream();
            using (var compressStream = new GZipStream(ms, CompressionMode.Compress))
                compressStream.Write(input, 0, input.Length);
            return ms.ToArray();
        }
        public static byte[] CompressDeflate(byte[] input)
        {
            using var ms = new MemoryStream();
            using (var compressStream = new DeflateStream(ms, CompressionMode.Compress))
                compressStream.Write(input, 0, input.Length);
            return ms.ToArray();
        }
    }
}
