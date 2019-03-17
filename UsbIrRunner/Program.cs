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
                Console.WriteLine("usage: UsbIrRunner.exe <--file> filepath [<-f|--freq> frequency]");
                Console.WriteLine("usage: UsbIrRunner.exe <-b|--base64> base64String [<-f|--freq> frequency]");
                return 1;
            }

            uint frequency = 38000;
            string base64String = null;
            string filePath = null;
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
            }

            if ((filePath != null && base64String != null) || (filePath == null && base64String == null))
            {

                Console.Error.WriteLine("不正な引数です");
                return 2;
            }

            if (filePath != null)
                RunFromFile(filePath, frequency);
            else
                RunFromBase64(base64String, frequency);

            return 0;
        }

        static void RunFromFile(string filePath, uint frequency)
        {
            if (!File.Exists(filePath))
                Console.Error.WriteLine("ファイル:{0} が存在しません", filePath);

            byte[] bytes = File.ReadAllBytes(filePath);
            using (var usbIr = new UsbIr.UsbIr())
                usbIr.Send(bytes, frequency);
        }

        static void RunFromBase64(string base64String, uint frequency)
        {
            byte[] bytes = Convert.FromBase64String(base64String);

            using (var usbIr = new UsbIr.UsbIr())
                usbIr.Send(bytes, frequency);
        }
    }
}
