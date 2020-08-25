#define COMPOSITE_DEVICE    // 複合デバイスの場合は定義する

using System;
using System.Runtime.InteropServices;
using static UsbIr.NativeMethods;

namespace UsbIr
{
    internal class NativeUsbDeviceTable : IDisposable
    {

        private readonly IntPtr deviceInfoTable;
        public NativeUsbDeviceTable()
        {
            //First populate a list of plugged in devices (by specifying "DIGCF_PRESENT"), which are of the specified class GUID. 
            deviceInfoTable = SetupDiGetClassDevs(UsbIr.InterfaceClassGuid, IntPtr.Zero, IntPtr.Zero, DiGetClassFlags.DIGCF_PRESENT | DiGetClassFlags.DIGCF_DEVICEINTERFACE);
            if (deviceInfoTable == IntPtr.Zero)
                throw new UsbIrException("failed to initialize " + nameof(deviceInfoTable));
        }

        private bool disposed = false;
        public void Dispose()
        {
            if (disposed) return;

            SetupDiDestroyDeviceInfoList(deviceInfoTable);
        }

        private static bool IsMatchDeviceID(string deviceIDFromRegistry)
        {
            //Use the formatting: "Vid_xxxx&Pid_xxxx" where xxxx is a 16-bit hexadecimal number.
            //Make sure the value appearing in the parathesis matches the USB device descriptor
            //of the device that this aplication is intending to find.
            const string DeviceIDToFind = "Vid_22ea&Pid_003A";
            const string DeviceIDToFind2 =
#if COMPOSITE_DEVICE
            "Mi_03";
#else
            "";
#endif
            return
                deviceIDFromRegistry.AsSpan().Contains(DeviceIDToFind, StringComparison.OrdinalIgnoreCase)
                && deviceIDFromRegistry.AsSpan().Contains(DeviceIDToFind2, StringComparison.OrdinalIgnoreCase);
        }
        private bool IsMatchDeviceIndex(uint interfaceIndex)
        {
            var devInfoData = new SP_DEVINFO_DATA();
            //Now retrieve the hardware ID from the registry.  The hardware ID contains the VID and PID, which we will then 
            //check to see if it is the correct device or not.

            //Initialize an appropriate SP_DEVINFO_DATA structure.  We need this structure for SetupDiGetDeviceRegistryProperty().
            devInfoData.cbSize = Marshal.SizeOf(devInfoData);
            if (!SetupDiEnumDeviceInfo(deviceInfoTable, interfaceIndex, ref devInfoData))
                return false;

            //First query for the size of the hardware ID, so we can know how big a buffer to allocate for the data.
            SetupDiGetDeviceRegistryProperty(deviceInfoTable, ref devInfoData, SPDRP_HARDWAREID, out _, IntPtr.Zero, 0, out var dwRegSize);

            //Allocate a buffer for the hardware ID.
            //Should normally work, but could throw exception "OutOfMemoryException" if not enough resources available.
            var propertyValueBuffer = Marshal.AllocHGlobal(dwRegSize);
            try
            {
                //Retrieve the hardware IDs for the current device we are looking at.  PropertyValueBuffer gets filled with a 
                //REG_MULTI_SZ (array of null terminated strings).  To find a device, we only care about the very first string in the
                //buffer, which will be the "device ID".  The device ID is a string which contains the VID and PID, in the example 
                //format "Vid_04d8&Pid_003f".
                if (!SetupDiGetDeviceRegistryProperty(deviceInfoTable, ref devInfoData, SPDRP_HARDWAREID, out _, propertyValueBuffer, dwRegSize, out _))
                    return false;
                return IsMatchDeviceID(Marshal.PtrToStringAuto(propertyValueBuffer));
            }
            finally
            {
                Marshal.FreeHGlobal(propertyValueBuffer);
            }
        }

        private SP_DEVICE_INTERFACE_DATA GetIntefaceData(uint interfaceIndex)
        {
            var data = new SP_DEVICE_INTERFACE_DATA();
            data.cbSize = Marshal.SizeOf(data);
            if (SetupDiEnumDeviceInterfaces(deviceInfoTable, IntPtr.Zero, UsbIr.InterfaceClassGuid, interfaceIndex, ref data))
                return data;
            throw new UsbIrException("failed to get " + nameof(SP_DEVICE_INTERFACE_DATA));
        }

        public SP_DEVICE_INTERFACE_DATA GetMatchedIntefaceData()
        {
            for (uint i = 0; i < 10000000; i++)
                if (IsMatchDeviceIndex(i))
                    return GetIntefaceData(i);
                else if (Marshal.GetLastWin32Error() == ERROR_NO_MORE_ITEMS)
                    throw new UsbIrException($"{nameof(ERROR_NO_MORE_ITEMS)}: index {i}");
            throw new UsbIrException("not found device");
        }

        private string SetupDiGetDeviceInterfaceDetail(in SP_DEVICE_INTERFACE_DATA interfaceData)
        {
            //First call populates "StructureSize" with the correct value
            NativeMethods.SetupDiGetDeviceInterfaceDetail(deviceInfoTable, interfaceData, IntPtr.Zero, 0, out var structureSize, IntPtr.Zero);

            //Need to call SetupDiGetDeviceInterfaceDetail() again, this time specifying a pointer to a SP_DEVICE_INTERFACE_DETAIL_DATA buffer with the correct size of RAM allocated.
            var pInterfaceDetailData = Marshal.AllocHGlobal(structureSize);

            try
            {
                Marshal.WriteInt32(pInterfaceDetailData, IntPtr.Size == 8 ? 8 : 6);
                //Now call SetupDiGetDeviceInterfaceDetail() a second time to receive the device path in the structure at pUnmanagedDetailedInterfaceDataStructure.
                if (NativeMethods.SetupDiGetDeviceInterfaceDetail(deviceInfoTable, interfaceData, pInterfaceDetailData, structureSize, out _, IntPtr.Zero))
                {
                    //Need to extract the path information from the unmanaged "structure".  The path starts at (pUnmanagedDetailedInterfaceDataStructure + sizeof(DWORD)).
                    var pToDevicePath = new IntPtr(pInterfaceDetailData.ToInt64() + 4);
                    return Marshal.PtrToStringAuto(pToDevicePath);
                }
                else //Some unknown failure occurred
                {
                    throw new UsbIrException("failed to " + nameof(SetupDiGetDeviceInterfaceDetail));
                }
            }
            finally
            {
                Marshal.FreeHGlobal(pInterfaceDetailData);
            }
        }

        public string GetMatchedDeviceName() => SetupDiGetDeviceInterfaceDetail(GetMatchedIntefaceData());
    }
}
