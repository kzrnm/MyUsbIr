#define COMPOSITE_DEVICE    // 複合デバイスの場合は定義する

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using static UsbIr.NativeMethods;
using System.Linq;
using System.Diagnostics.CodeAnalysis;


namespace UsbIr
{
    public sealed partial class UsbIr
    {
        /// <summary>Register for WM_DEVICECHANGE notifications.  This code uses these messages to detect plug and play connection/disconnection events for USB devices</summary>
        private static void RegisterDeviceNotification(IntPtr hRecipient) =>
            NativeMethods.RegisterDeviceNotification(hRecipient, new DEV_BROADCAST_DEVICEINTERFACE
            {
                dbcc_devicetype = DBT_DEVTYP_DEVICEINTERFACE,
                dbcc_reserved = 0,  //Reserved says not to use...
                dbcc_classguid = InterfaceClassGuid,
                dbcc_size = (uint)Marshal.SizeOf<DEV_BROADCAST_DEVICEINTERFACE>(),
            }, DeviceNotify.WindowHandle);

        /// <summary>We now have the proper device path, and we can finally open read and write handles to the device.</summary>
        private static SafeFileHandle CreateFile(string devicePath) =>
            NativeMethods.CreateFile(devicePath,
                    EFileAccess.GenericRead | EFileAccess.GenericWrite,
                    EFileShare.Read | EFileShare.Write,
                    IntPtr.Zero,
                    ECreationDisposition.OpenExisting,
                    EFileAttributes.Normal,
                    IntPtr.Zero);


        private bool WriteAndRead(ReadOnlySpan<byte> outBuffer, Span<byte> inBuffer)
        {
            inBuffer[0] = 0;

            return
                this.WriteFile(outBuffer)
                && this.ReadFileManagedBuffer(inBuffer);
        }

        private bool WriteFile(ReadOnlySpan<byte> lpBuffer) => WriteFile(lpBuffer, IntPtr.Zero);
        private bool WriteFile(ReadOnlySpan<byte> lpBuffer, IntPtr lpOverlapped)
            => NativeMethods.WriteFile(this.handleToUSBDevice, MemoryMarshal.GetReference(lpBuffer), lpBuffer.Length, out _, lpOverlapped);
        private bool ReadFileManagedBuffer(Span<byte> inBuffer)
            => ReadFileManagedBuffer(inBuffer, IntPtr.Zero);
        private bool ReadFileManagedBuffer(Span<byte> inBuffer, IntPtr lpOverlapped)
            => ReadFile(this.handleToUSBDevice, ref MemoryMarshal.GetReference(inBuffer), inBuffer.Length, out _, lpOverlapped);
    }
}
