#if HOGE
using Microsoft.Win32.SafeHandles;
using System;
using static USB_IR_Library.USBIR;

namespace USB_IR_Library
{
    public class UsbIrWrapper : IDisposable
    {
        private SafeFileHandle handle;
        public UsbIrWrapper() : this(IntPtr.Zero) { }
        public UsbIrWrapper(IntPtr hRecipient)
        {
            handle = openUSBIR(hRecipient);
            if (handle == null)
            {
                throw new UsbIrException("USB赤外線リモコンが未接続です");
            }
        }

        public bool RecStart(uint freqency = 38000)
        => recUSBIRData_Start(handle, freqency) == 0;
        public bool RecStop()
        => recUSBIRData_Stop(handle) == 0;
        public byte[] Read()
        {
            var b = new byte[9600];
            uint retLength = 0;
            readUSBIRData(handle, ref b, (uint)b.Length, ref retLength);
            var ret = new byte[retLength * 4];
            Array.Copy(b, ret, ret.Length);
            return ret;
        }
        public bool Send(uint freqency, byte[] data)
        => writeUSBIRData(handle, freqency, data, (uint)data.Length / 4) == 0;
        public bool Send(uint freqency, uint[] data)
        => writeUSBIRData(handle, freqency, data, (uint)data.Length / 2) == 0;
        public bool Send(uint freqency, uint reader_code, uint bit_0, uint bit_1, uint stop_code, byte[] code)
        => writeUSBIRCode(handle, freqency, reader_code, bit_0, bit_1, stop_code, code, (uint)code.Length * 8) == 0;
        public bool Send(uint freqency, uint reader_code, uint bit_0, uint bit_1, uint stop_code, byte[] code, uint[] repeatcode, uint repeat_code_send_num)
        => writeUSBIRCode(handle, freqency, reader_code, bit_0, bit_1, stop_code, code, (uint)code.Length * 8, repeatcode, (uint)repeatcode.Length, repeat_code_send_num) == 0;

        public bool SendPlarailStop(PLARAIL_BAND band)
            => writeUSBIR_Plarail_Stop(handle, band) == 0;
        public bool SendPlarailSpeedUp(PLARAIL_BAND band, PLARAIL_DIRECTION dir)
            => writeUSBIR_Plarail_Speed_Up(handle, band, dir) == 0;
        public bool SendPlarailSpeedDown(PLARAIL_BAND band)
            => writeUSBIR_Plarail_Speed_Down(handle, band) == 0;

        public void Dispose()
        {
            closeUSBIR(handle);
        }

    }
    public class UsbIrCollection : IDisposable
    {
        public int Count { get; }
        public UsbIrCollection()
        {
            Count = openUSBIR_all();
            if (Count == 0)
            {
                throw new UsbIrException("USB赤外線リモコンが未接続です");
            }
        }

        public bool Send(uint freqency, byte[] data)
        => writeUSBIRData_all(freqency, data, (uint)data.Length / 4) == 0;
        public bool Send(uint freqency, uint[] data)
        => writeUSBIRData_all(freqency, data, (uint)data.Length / 2) == 0;
        public bool Send(uint freqency, uint reader_code, uint bit_0, uint bit_1, uint stop_code, byte[] code)
        => writeUSBIRCode_all(freqency, reader_code, bit_0, bit_1, stop_code, code, (uint)code.Length * 8) == 0;
        public bool Send(uint freqency, uint reader_code, uint bit_0, uint bit_1, uint stop_code, byte[] code, uint[] repeatcode, uint repeat_code_send_num)
        => writeUSBIRCode_all(freqency, reader_code, bit_0, bit_1, stop_code, code, (uint)code.Length * 8, repeatcode, (uint)repeatcode.Length, repeat_code_send_num) == 0;

        public void Dispose()
        {
            closeUSBIR_all();
        }
    }

    [System.Serializable]
    public class UsbIrException : System.Exception
    {
        public UsbIrException() { }
        public UsbIrException(string message) : base(message) { }
        public UsbIrException(string message, System.Exception inner) : base(message, inner) { }
        protected UsbIrException(
            System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
#endif