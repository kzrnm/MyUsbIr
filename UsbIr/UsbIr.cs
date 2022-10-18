using Microsoft.Win32.SafeHandles;
using System;
using System.Runtime.InteropServices;
using static UsbIr.NativeMethods;


namespace UsbIr
{
    public sealed partial class UsbIr : IDisposable
    {
        private readonly SafeFileHandle handleToUSBDevice;

        public UsbIr() : this(IntPtr.Zero) { }
        public UsbIr(IntPtr hRecipient)
        {
            RegisterDeviceNotification(hRecipient);

            //Now make an initial attempt to find the USB device, if it was already connected to the PC and enumerated prior to launching the application.
            //If it is connected and present, we should open read and write handles to the device so we can communicate with it later.
            //If it was not connected, we will have to wait until the user plugs the device in, and the WM_DEVICECHANGE callback function can process
            //the message and again search for the device.
            try
            {
                var devicePath = GetUSBDevicePath();
                this.handleToUSBDevice = CreateFile(devicePath);
                if (Marshal.GetLastWin32Error() == ERROR_SUCCESS && this.handleToUSBDevice != null)
                    return;
                else //for some reason the device was physically plugged in, but one or both of the read/write handles didn't open successfully...
                    throw new UsbIrException("cannot open USB device");
            }
            catch (Exception e)
            {
                throw new UsbIrException("Device must not be connected (or not programmed with correct firmware)", e);
            }
        }
        private bool disposed = false;
        public void Dispose()
        {
            if (disposed) return;

            this.handleToUSBDevice.Dispose();

            disposed = true;
            GC.SuppressFinalize(this);
        }

        #region constants
        //Globally Unique Identifier (GUID) for HID class devices.  Windows uses GUIDs to identify things.
        internal static readonly Guid InterfaceClassGuid = new(0x4d1e55b2, 0xf16f, 0x11cf, 0x88, 0xcb, 0x00, 0x11, 0x11, 0x00, 0x00, 0x30);

        ////Other constant definitions
        private const uint DBT_DEVTYP_DEVICEINTERFACE = 0x05;

        public const uint IR_FREQ_MIN = 25000;                  // 赤外線周波数設定最小値 25KHz
        public const uint IR_FREQ_MAX = 50000;                  // 赤外線周波数設定最大値 50KHz
        public const int IR_SEND_DATA_USB_SEND_MAX_LEN = 14;   // USB送信１回で送信する最大ビット数
        public const uint IR_SEND_DATA_MAX_LEN = 300;           // 赤外線送信データ設定最大長[byte]
        #endregion constants


        public RecStatus RecStatus { set; get; }

        public void StartRecoding(uint frequency = 38000)
        {
            this.RecStatus = RecStatus.NowRecoding;

            Span<byte> outBuffer = stackalloc byte[65];
            Span<byte> inBuffer = stackalloc byte[65];

            outBuffer[0] = 0;         //The first byte is the "Report ID" and does not get sent over the USB bus.  Always set = 0.
            outBuffer[1] = 0x31;        //0x81 is the "Get Pushbutton State" command in the firmware
            outBuffer[2] = (byte)((frequency >> 8) & 0xFF);
            outBuffer[3] = (byte)(frequency & 0xFF);
            outBuffer[4] = 1;   // 読み込み停止フラグ
            outBuffer[5] = 0;   // 読み込み停止ON時間MSB
            outBuffer[6] = 0;   // 読み込み停止ON時間LSB
            outBuffer[7] = 10;   // 読み込み停止OFF時間MSB
            outBuffer[8] = 0;   // 読み込み停止OFF時間LSB

            if (!WriteAndRead(outBuffer, inBuffer))
                throw new UsbIrException("cannot start recoding");

            //INBuffer[0] is the report ID, which we don't care about.
            //INBuffer[1] is an echo back of the command (see microcontroller firmware).
            //INBuffer[2] contains the I/O port pin value for the pushbutton (see microcontroller firmware).  
            if (inBuffer[1] != 0x31)
                throw new UsbIrException("cannot start recoding");
        }
        public void EndRecoding()
        {
            Span<byte> outBuffer = stackalloc byte[65];
            Span<byte> inBuffer = stackalloc byte[65];

            outBuffer[0] = 0;           //The first byte is the "Report ID" and does not get sent over the USB bus.  Always set = 0.
            outBuffer[1] = 0x32;        //0x81 is the "Get Pushbutton State" command in the firmware

            if (!WriteAndRead(outBuffer, inBuffer))
                throw new UsbIrException("cannot end recoding");

            //INBuffer[0] is the report ID, which we don't care about.
            //INBuffer[1] is an echo back of the command (see microcontroller firmware).
            //INBuffer[2] contains the I/O port pin value for the pushbutton (see microcontroller firmware).  
            if (inBuffer[1] != 0x32 || inBuffer[2] != 0)
                throw new UsbIrException("cannot end recoding");

            this.RecStatus = RecStatus.Complete;
        }
        public byte[] Read()
        {
            if (this.RecStatus != RecStatus.Complete)
                throw new InvalidOperationException();

            Span<byte> outBuffer = stackalloc byte[65];
            Span<byte> inBuffer = stackalloc byte[65];

            Span<byte> resultBuffer = stackalloc byte[9600];
            Span<byte> resultCurrent = resultBuffer;

            //Get the pushbutton state from the microcontroller firmware.
            outBuffer[0] = 0;           //The first byte is the "Report ID" and does not get sent over the USB bus.  Always set = 0.
            outBuffer[1] = 0x33;        //0x81 is the "Get Pushbutton State" command in the firmware

            while (true)
            {
                if (!WriteAndRead(outBuffer, inBuffer))
                    throw new UsbIrException("cannot read");

                //INBuffer[0] is the report ID, which we don't care about.
                //INBuffer[1] is an echo back of the command (see microcontroller firmware).
                //INBuffer[2] contains the I/O port pin value for the pushbutton (see microcontroller firmware).  
                if (inBuffer[1] != 0x33)
                    throw new UsbIrException("cannot read");

                int totalSize = (inBuffer[2] << 8) | inBuffer[3];
                int startPosition = (inBuffer[4] << 8) | inBuffer[5];
                byte readSize = inBuffer[6];

                if (totalSize > 0 && totalSize >= (startPosition + readSize) && readSize > 0)
                {
                    inBuffer.Slice(7, 4 * readSize).CopyTo(resultCurrent);
                    resultCurrent = resultCurrent.Slice(4 * readSize);
                }
                else
                {
                    // 読み込み終了
                    return resultBuffer.Slice(0, totalSize * 4).ToArray();
                }
            }
        }

        public void Send(ReadOnlySpan<byte> data, uint frequency = 38000)
        {
            Span<byte> outBuffer = stackalloc byte[65];
            Span<byte> inBuffer = stackalloc byte[65];
            int sendBitNum = data.Length / 4;            // 送信ビット数　リーダーコード + コード + 終了コード
            int sendBitPos = 0;                  // 送信セット済みビット位置

            if (!(IR_FREQ_MIN <= frequency && frequency <= IR_FREQ_MAX))
                throw new ArgumentOutOfRangeException(nameof(frequency));

            // データセット
            while (sendBitNum > sendBitPos)
            {
                var setBitSize = Math.Min(sendBitNum - sendBitPos, IR_SEND_DATA_USB_SEND_MAX_LEN);

                outBuffer[0] = 0x00;
                outBuffer[1] = 0x34;
                //送信総ビット数
                outBuffer[2] = (byte)((sendBitNum >> 8) & 0xFF);
                outBuffer[3] = (byte)(sendBitNum & 0xFF);
                outBuffer[4] = (byte)((sendBitPos >> 8) & 0xFF);
                outBuffer[5] = (byte)(sendBitPos & 0xFF);
                outBuffer[6] = (byte)(setBitSize & 0xFF);

                // データセット
                // 赤外線コードコピー
                data.Slice(sendBitPos * 4, setBitSize * 4).CopyTo(outBuffer.Slice(7));
                sendBitPos += setBitSize;

                if (!WriteFile(outBuffer))
                    throw new UsbIrException("on setting data");

                //Now get the response packet from the firmware.
                inBuffer[0] = 0;


                if (!ReadFileManagedBuffer(inBuffer))
                    throw new UsbIrException("on setting data");

                //INBuffer[0] is the report ID, which we don't care about.
                //INBuffer[1] is an echo back of the command (see microcontroller firmware).
                //INBuffer[2] contains the I/O port pin value for the pushbutton (see microcontroller firmware).  
                if (inBuffer[1] == 0x34 && inBuffer[2] != 0x00)
                    throw new UsbIrException("on setting data");
            }

            // データ送信要求セット
            outBuffer[0] = 0;           //The first byte is the "Report ID" and does not get sent over the USB bus.  Always set = 0.
            outBuffer[1] = 0x35;        //0x81 is the "Get Pushbutton State" command in the firmware
            outBuffer[2] = (byte)((frequency >> 8) & 0xFF);
            outBuffer[3] = (byte)(frequency & 0xFF);
            outBuffer[4] = (byte)((sendBitNum >> 8) & 0xFF);
            outBuffer[5] = (byte)(sendBitNum & 0xFF);

            if (!WriteAndRead(outBuffer, inBuffer))
                throw new UsbIrException("on sending data");

            //INBuffer[0] is the report ID, which we don't care about.
            //INBuffer[1] is an echo back of the command (see microcontroller firmware).
            //INBuffer[2] contains the I/O port pin value for the pushbutton (see microcontroller firmware).  
            if (inBuffer[1] == 0x35 && inBuffer[2] == 0x00)
                return;
            throw new UsbIrException("on sending data");
        }

        private static string GetUSBDevicePath()
        {
            using var udt = new NativeUsbDeviceTable();
            return udt.GetMatchedDeviceName();
        }
    }
}
