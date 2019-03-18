using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;

namespace UsbIrRunner
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.Error.WriteLine("引数が足りません");
                Console.WriteLine("usage: UsbIrRunner.exe <--file> filepath [<-f|--freq> frequency] [-g|--gzip]");
                Console.WriteLine("usage: UsbIrRunner.exe <-b|--base64> base64String [<-f|--freq> frequency] [-g|--gzip]");
                return 1;
            }

            try
            {
                uint frequency = 38000;
                string base64String = null;
                string filePath = null;
                bool isGzip = false;
                for (var i = 0; i < args.Length; i++)
                {
                    var arg = args[i];
                    if (!arg.StartsWith("-"))
                    {
                        if (filePath != null)
                        {
                            Console.Error.WriteLine("不正な引数です");
                            return 2;
                        }
                        filePath = arg;
                    }
                    else if (arg == "--file")
                    {
                        if (filePath != null)
                        {
                            Console.Error.WriteLine("不正な引数です");
                            return 2;
                        }
                        filePath = args[++i];
                    }
                    else if (arg == "-b" || arg == "--base64")
                    {
                        if (base64String != null)
                        {
                            Console.Error.WriteLine("不正な引数です");
                            return 2;
                        }
                        base64String = args[++i];
                    }
                    else if (arg == "-f" || arg == "--freq")
                    {
                        if (uint.TryParse(args[++i], out var result))
                        {
                            frequency = result;
                        }
                        else
                        {
                            Console.Error.WriteLine("不正な引数です");
                            return 2;
                        }
                    }
                    else if (arg == "-g" || arg == "--gzip")
                    {
                        isGzip = true;
                    }
                }

                if ((filePath != null && base64String != null) || (filePath == null && base64String == null))
                {
                    Console.Error.WriteLine("不正な引数です");
                    return 2;
                }


                var bytes = GetBytes(filePath, base64String);

                if (isGzip)
                {
                    using (var ms = new MemoryStream(bytes))
                    using (var gz = new System.IO.Compression.GZipStream(ms, System.IO.Compression.CompressionMode.Decompress))
                    {
                        var buffer = new byte[9600];
                        var readSize = gz.Read(buffer, 0, buffer.Length);

                        bytes = new byte[readSize];
                        Array.Copy(buffer, 0, bytes, 0, readSize);
                    }
                }

                using (var usbIr = new UsbIr.UsbIr())
                    usbIr.Send(bytes, frequency);

                return 0;
            }
            catch
            {
                Console.Error.WriteLine("失敗しました");
                return 255;
            }
        }

        static byte[] GetBytes(string filePath, string base64String)
        {
            if (filePath != null)
            {
                if (!File.Exists(filePath))
                    Console.Error.WriteLine("ファイル:{0} が存在しません", filePath);

                return File.ReadAllBytes(filePath);
            }
            else
            {
                return Convert.FromBase64String(base64String);
            }
        }

    }
}
