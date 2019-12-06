#define COMPOSITE_DEVICE    // 複合デバイスの場合は定義する

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using static UsbIr.NativeMethods;
using System.Linq;

#pragma warning disable CA1031

namespace UsbIr
{
    public class UsbIr : IDisposable
    {
        private readonly SafeFileHandle handleToUSBDevice;

        public UsbIr() : this(IntPtr.Zero) { }
        public UsbIr(IntPtr hRecipient)
        {
            //Register for WM_DEVICECHANGE notifications.  This code uses these messages to detect plug and play connection/disconnection events for USB devices
            RegisterDeviceNotification(hRecipient, new DEV_BROADCAST_DEVICEINTERFACE
            {
                dbcc_devicetype = DBT_DEVTYP_DEVICEINTERFACE,
                dbcc_reserved = 0,  //Reserved says not to use...
                dbcc_classguid = InterfaceClassGuid,
                dbcc_size = (uint)Marshal.SizeOf<DEV_BROADCAST_DEVICEINTERFACE>(),
            }, DeviceNotify.WindowHandle);

            //Now make an initial attempt to find the USB device, if it was already connected to the PC and enumerated prior to launching the application.
            //If it is connected and present, we should open read and write handles to the device so we can communicate with it later.
            //If it was not connected, we will have to wait until the user plugs the device in, and the WM_DEVICECHANGE callback function can process
            //the message and again search for the device.
            if (CheckIfPresentAndGetUSBDevicePath())    //Check and make sure at least one device with matching VID/PID is attached
            {
                uint ErrorStatusWrite;

                //We now have the proper device path, and we can finally open read and write handles to the device.
                this.handleToUSBDevice = CreateFile(DevicePath,
                    EFileAccess.GenericRead | EFileAccess.GenericWrite,
                    EFileShare.Read | EFileShare.Write,
                    IntPtr.Zero,
                    ECreationDisposition.OpenExisting,
                    EFileAttributes.Normal,
                    IntPtr.Zero);
                ErrorStatusWrite = (uint)Marshal.GetLastWin32Error();

                if (ErrorStatusWrite == ERROR_SUCCESS && this.handleToUSBDevice != null)
                {
                    return;
                }
                else //for some reason the device was physically plugged in, but one or both of the read/write handles didn't open successfully...
                {
                    throw new UsbIrException("cannot open USB device");
                }
            }
            else    //Device must not be connected (or not programmed with correct firmware)
            {
                throw new UsbIrException("Device must not be connected (or not programmed with correct firmware)");
            }
        }

        #region Rec & Read

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

            if (!this.WriteAndRead(outBuffer, inBuffer))
                throw new UsbIrException("cannot start recoding");

            //INBuffer[0] is the report ID, which we don't care about.
            //INBuffer[1] is an echo back of the command (see microcontroller firmware).
            //INBuffer[2] contains the I/O port pin value for the pushbutton (see microcontroller firmware).  
            if (inBuffer[1] != 0x31)
            {
                throw new UsbIrException("cannot start recoding");
            }
        }
        public void EndRecoding()
        {
            Span<byte> outBuffer = stackalloc byte[65];
            Span<byte> inBuffer = stackalloc byte[65];

            outBuffer[0] = 0;           //The first byte is the "Report ID" and does not get sent over the USB bus.  Always set = 0.
            outBuffer[1] = 0x32;        //0x81 is the "Get Pushbutton State" command in the firmware

            if (!this.WriteAndRead(outBuffer, inBuffer))
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
                if (!this.WriteAndRead(outBuffer, inBuffer))
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
        #endregion Rec & Read

        public void Send(ReadOnlySpan<byte> data, uint frequency = 38000)
        {
            Span<byte> outBuffer = stackalloc byte[65];
            Span<byte> inBuffer = stackalloc byte[65];
            int send_bit_num = data.Length / 4;            // 送信ビット数　リーダーコード + コード + 終了コード
            int send_bit_pos = 0;                  // 送信セット済みビット位置
            int set_bit_size;

            if (!(IR_FREQ_MIN <= frequency && frequency <= IR_FREQ_MAX))
            {
                throw new ArgumentOutOfRangeException(nameof(frequency));
            }

            // データセット
            while (true)
            {
                if (send_bit_num > send_bit_pos)
                {
                    set_bit_size = send_bit_num - send_bit_pos;
                    if (set_bit_size > IR_SEND_DATA_USB_SEND_MAX_LEN)
                    {
                        set_bit_size = IR_SEND_DATA_USB_SEND_MAX_LEN;
                    }
                }
                else
                {   // 送信データなし
                    break;
                }

                outBuffer[0] = 0x00;
                outBuffer[1] = 0x34;
                //送信総ビット数
                outBuffer[2] = (byte)((send_bit_num >> 8) & 0xFF);
                outBuffer[3] = (byte)(send_bit_num & 0xFF);
                outBuffer[4] = (byte)((send_bit_pos >> 8) & 0xFF);
                outBuffer[5] = (byte)(send_bit_pos & 0xFF);
                outBuffer[6] = (byte)(set_bit_size & 0xFF);

                // データセット
                // 赤外線コードコピー
                data.Slice(send_bit_pos * 4, set_bit_size * 4).CopyTo(outBuffer.Slice(7));
                send_bit_pos += set_bit_size;

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
            outBuffer[4] = (byte)((send_bit_num >> 8) & 0xFF);
            outBuffer[5] = (byte)(send_bit_num & 0xFF);

            if (!this.WriteAndRead(outBuffer, inBuffer))
                throw new UsbIrException("on sending data");

            //INBuffer[0] is the report ID, which we don't care about.
            //INBuffer[1] is an echo back of the command (see microcontroller firmware).
            //INBuffer[2] contains the I/O port pin value for the pushbutton (see microcontroller firmware).  
            if (inBuffer[1] == 0x35 && inBuffer[2] == 0x00)
                return;
            throw new UsbIrException("on sending data");
        }

        private bool disposed = false;
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                this.handleToUSBDevice.Dispose();
            }

            disposed = true;
        }

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
        {
            try
            {
                if (ReadFile(this.handleToUSBDevice, ref MemoryMarshal.GetReference(inBuffer), inBuffer.Length, out _, lpOverlapped))
                    return true;
            }
            catch { }
            return false;
        }

        #region constants
        ////--------------- Global Varibles Section ------------------
        ////USB related variables that need to have wide scope.
        private static string DevicePath = null;   //Need the find the proper device path before you can open file handles.

        //Globally Unique Identifier (GUID) for HID class devices.  Windows uses GUIDs to identify things.
        private static readonly Guid InterfaceClassGuid = new Guid(0x4d1e55b2, 0xf16f, 0x11cf, 0x88, 0xcb, 0x00, 0x11, 0x11, 0x00, 0x00, 0x30);
        //--------------- End of Global Varibles ------------------

        ////Other constant definitions
        private const uint DBT_DEVTYP_DEVICEINTERFACE = 0x05;
        private const uint ERROR_SUCCESS = 0x00;
        private const uint ERROR_NO_MORE_ITEMS = 0x00000103;
        private const uint SPDRP_HARDWAREID = 0x00000001;

        public const uint IR_FREQ_MIN = 25000;                  // 赤外線周波数設定最小値 25KHz
        public const uint IR_FREQ_MAX = 50000;                  // 赤外線周波数設定最大値 50KHz
        public const int IR_SEND_DATA_USB_SEND_MAX_LEN = 14;   // USB送信１回で送信する最大ビット数
        public const uint IR_SEND_DATA_MAX_LEN = 300;           // 赤外線送信データ設定最大長[byte]
        #endregion constants


        //FUNCTION:	CheckIfPresentAndGetUSBDevicePath()
        //PURPOSE:	Check if a USB device is currently plugged in with a matching VID and PID
        //INPUT:	Uses globally declared String DevicePath, globally declared GUID, and the MY_DEVICE_ID constant.
        //OUTPUT:	Returns BOOL.  TRUE when device with matching VID/PID found.  FALSE if device with VID/PID could not be found.
        //			When returns TRUE, the globally accessable "DetailedInterfaceDataStructure" will contain the device path
        //			to the USB device with the matching VID/PID.
        private static bool CheckIfPresentAndGetUSBDevicePath()
        {
            /* 
            Before we can "connect" our application to our USB embedded device, we must first find the device.
            A USB bus can have many devices simultaneously connected, so somehow we have to find our device only.
            This is done with the Vendor ID (VID) and Product ID (PID).  Each USB product line should have
            a unique combination of VID and PID.  

            Microsoft has created a number of functions which are useful for finding plug and play devices.  Documentation
            for each function used can be found in the MSDN library.  We will be using the following functions (unmanaged C functions):

            SetupDiGetClassDevs()					//provided by setupapi.dll, which comes with Windows
            SetupDiEnumDeviceInterfaces()			//provided by setupapi.dll, which comes with Windows
            GetLastError()							//provided by kernel32.dll, which comes with Windows
            SetupDiDestroyDeviceInfoList()			//provided by setupapi.dll, which comes with Windows
            SetupDiGetDeviceInterfaceDetail()		//provided by setupapi.dll, which comes with Windows
            SetupDiGetDeviceRegistryProperty()		//provided by setupapi.dll, which comes with Windows
            CreateFile()							//provided by kernel32.dll, which comes with Windows

            In order to call these unmanaged functions, the Marshal class is very useful.

            We will also be using the following unusual data types and structures.  Documentation can also be found in
            the MSDN library:

            PSP_DEVICE_INTERFACE_DATA
            PSP_DEVICE_INTERFACE_DETAIL_DATA
            SP_DEVINFO_DATA
            HDEVINFO
            HANDLE
            GUID

            The ultimate objective of the following code is to get the device path, which will be used elsewhere for getting
            read and write handles to the USB device.  Once the read/write handles are opened, only then can this
            PC application begin reading/writing to the USB device using the WriteFile() and ReadFile() functions.

            Getting the device path is a multi-step round about process, which requires calling several of the
            SetupDixxx() functions provided by setupapi.dll.
            */

            try
            {
                IntPtr DeviceInfoTable = IntPtr.Zero;
                SP_DEVICE_INTERFACE_DATA InterfaceDataStructure = new SP_DEVICE_INTERFACE_DATA();
                SP_DEVICE_INTERFACE_DETAIL_DATA DetailedInterfaceDataStructure = new SP_DEVICE_INTERFACE_DETAIL_DATA();
                SP_DEVINFO_DATA DevInfoData = new SP_DEVINFO_DATA();

                uint InterfaceIndex = 0;
                uint dwRegType = 0;
                uint dwRegSize = 0;
                uint dwRegSize2 = 0;
                IntPtr PropertyValueBuffer = IntPtr.Zero;
                bool MatchFound = false;
                uint ErrorStatus;
                uint LoopCounter = 0;

                //Use the formatting: "Vid_xxxx&Pid_xxxx" where xxxx is a 16-bit hexadecimal number.
                //Make sure the value appearing in the parathesis matches the USB device descriptor
                //of the device that this aplication is intending to find.
                string DeviceIDToFind = "Vid_22ea&Pid_003A";
#if COMPOSITE_DEVICE
                bool MatchFound2 = false;
                string DeviceIDToFind2 = "Mi_03";
#else
#endif

                //First populate a list of plugged in devices (by specifying "DIGCF_PRESENT"), which are of the specified class GUID. 
                DeviceInfoTable = SetupDiGetClassDevs(InterfaceClassGuid, IntPtr.Zero, IntPtr.Zero,
                    DiGetClassFlags.DIGCF_PRESENT | DiGetClassFlags.DIGCF_DEVICEINTERFACE);

                if (DeviceInfoTable != IntPtr.Zero)
                {
                    //Now look through the list we just populated.  We are trying to see if any of them match our device. 
                    while (true)
                    {
                        InterfaceDataStructure.cbSize = Marshal.SizeOf(InterfaceDataStructure);
                        if (SetupDiEnumDeviceInterfaces(DeviceInfoTable, IntPtr.Zero, InterfaceClassGuid, InterfaceIndex, ref InterfaceDataStructure))
                        {
                            ErrorStatus = (uint)Marshal.GetLastWin32Error();
                            if (ErrorStatus == ERROR_NO_MORE_ITEMS) //Did we reach the end of the list of matching devices in the DeviceInfoTable?
                            {   //Cound not find the device.  Must not have been attached.
                                SetupDiDestroyDeviceInfoList(DeviceInfoTable);  //Clean up the old structure we no longer need.
                                return false;
                            }
                        }
                        else    //Else some other kind of unknown error ocurred...
                        {
                            ErrorStatus = (uint)Marshal.GetLastWin32Error();
                            SetupDiDestroyDeviceInfoList(DeviceInfoTable);  //Clean up the old structure we no longer need.
                            return false;
                        }

                        //Now retrieve the hardware ID from the registry.  The hardware ID contains the VID and PID, which we will then 
                        //check to see if it is the correct device or not.

                        //Initialize an appropriate SP_DEVINFO_DATA structure.  We need this structure for SetupDiGetDeviceRegistryProperty().
                        DevInfoData.cbSize = Marshal.SizeOf(DevInfoData);
                        SetupDiEnumDeviceInfo(DeviceInfoTable, InterfaceIndex, ref DevInfoData);

                        //First query for the size of the hardware ID, so we can know how big a buffer to allocate for the data.
                        SetupDiGetDeviceRegistryProperty(DeviceInfoTable, ref DevInfoData, SPDRP_HARDWAREID, ref dwRegType, IntPtr.Zero, 0, ref dwRegSize);

                        //Allocate a buffer for the hardware ID.
                        //Should normally work, but could throw exception "OutOfMemoryException" if not enough resources available.
                        PropertyValueBuffer = Marshal.AllocHGlobal((int)dwRegSize);

                        //Retrieve the hardware IDs for the current device we are looking at.  PropertyValueBuffer gets filled with a 
                        //REG_MULTI_SZ (array of null terminated strings).  To find a device, we only care about the very first string in the
                        //buffer, which will be the "device ID".  The device ID is a string which contains the VID and PID, in the example 
                        //format "Vid_04d8&Pid_003f".
                        SetupDiGetDeviceRegistryProperty(DeviceInfoTable, ref DevInfoData, SPDRP_HARDWAREID, ref dwRegType, PropertyValueBuffer, dwRegSize, ref dwRegSize2);

                        //Now check if the first string in the hardware ID matches the device ID of the USB device we are trying to find.
                        String DeviceIDFromRegistry = Marshal.PtrToStringUni(PropertyValueBuffer); //Make a new string, fill it with the contents from the PropertyValueBuffer

                        Marshal.FreeHGlobal(PropertyValueBuffer);       //No longer need the PropertyValueBuffer, free the memory to prevent potential memory leaks

                        //Convert both strings to lower case.  This makes the code more robust/portable accross OS Versions
                        DeviceIDFromRegistry = DeviceIDFromRegistry.ToLowerInvariant();
#if COMPOSITE_DEVICE
                        DeviceIDToFind = DeviceIDToFind.ToLowerInvariant();
                        DeviceIDToFind2 = DeviceIDToFind2.ToLowerInvariant();

                        //Now check if the hardware ID we are looking at contains the correct VID/PID
                        MatchFound = DeviceIDFromRegistry.Contains(DeviceIDToFind);
                        MatchFound2 = DeviceIDFromRegistry.Contains(DeviceIDToFind2);

                        if ((MatchFound == true) && (MatchFound2 == true))
#else
                        DeviceIDToFind = DeviceIDToFind.ToLowerInvariant();

                        //Now check if the hardware ID we are looking at contains the correct VID/PID
                        MatchFound = DeviceIDFromRegistry.Contains(DeviceIDToFind);

                        if (MatchFound == true)
#endif
                        {
                            //Device must have been found.  In order to open I/O file handle(s), we will need the actual device path first.
                            //We can get the path by calling SetupDiGetDeviceInterfaceDetail(), however, we have to call this function twice:  The first
                            //time to get the size of the required structure/buffer to hold the detailed interface data, then a second time to actually 
                            //get the structure (after we have allocated enough memory for the structure.)

                            //First call populates "StructureSize" with the correct value
                            SetupDiGetDeviceInterfaceDetail(DeviceInfoTable, InterfaceDataStructure, IntPtr.Zero, 0, out var StructureSize, IntPtr.Zero);




                            //Need to call SetupDiGetDeviceInterfaceDetail() again, this time specifying a pointer to a SP_DEVICE_INTERFACE_DETAIL_DATA buffer with the correct size of RAM allocated.
                            //First need to allocate the unmanaged buffer and get a pointer to it.
                            IntPtr pUnmanagedDetailedInterfaceDataStructure = Marshal.AllocHGlobal((int)StructureSize);    //Reserve some unmanaged memory for the structure.

                            DetailedInterfaceDataStructure.cbSize = IntPtr.Size == 8 ? 8 : 6;
                            //DetailedInterfaceDataStructure.cbSize = sizeof(int) + Marshal.SystemDefaultCharSize; //Initialize the cbSize parameter (4 bytes for DWORD + 2 bytes for unicode null terminator)

                            Marshal.StructureToPtr(DetailedInterfaceDataStructure, pUnmanagedDetailedInterfaceDataStructure, false); //Copy managed structure contents into the unmanaged memory buffer.

                            //Now call SetupDiGetDeviceInterfaceDetail() a second time to receive the device path in the structure at pUnmanagedDetailedInterfaceDataStructure.
                            if (SetupDiGetDeviceInterfaceDetail(DeviceInfoTable, InterfaceDataStructure, pUnmanagedDetailedInterfaceDataStructure, StructureSize, out _, IntPtr.Zero))
                            {
                                //Need to extract the path information from the unmanaged "structure".  The path starts at (pUnmanagedDetailedInterfaceDataStructure + sizeof(DWORD)).
                                IntPtr pToDevicePath = new IntPtr(pUnmanagedDetailedInterfaceDataStructure.ToInt64() + 4);  //Add 4 to the pointer (to get the pointer to point to the path, instead of the DWORD cbSize parameter)
                                DevicePath = Marshal.PtrToStringUni(pToDevicePath); //Now copy the path information into the globally defined DevicePath String.

                                //We now have the proper device path, and we can finally use the path to open I/O handle(s) to the device.
                                SetupDiDestroyDeviceInfoList(DeviceInfoTable);  //Clean up the old structure we no longer need.
                                Marshal.FreeHGlobal(pUnmanagedDetailedInterfaceDataStructure);  //No longer need this unmanaged SP_DEVICE_INTERFACE_DETAIL_DATA buffer.  We already extracted the path information.
                                return true;    //Returning the device path in the global DevicePath String
                            }
                            else //Some unknown failure occurred
                            {
                                uint ErrorCode = (uint)Marshal.GetLastWin32Error();
                                SetupDiDestroyDeviceInfoList(DeviceInfoTable);  //Clean up the old structure.
                                Marshal.FreeHGlobal(pUnmanagedDetailedInterfaceDataStructure);  //No longer need this unmanaged SP_DEVICE_INTERFACE_DETAIL_DATA buffer.  We already extracted the path information.
                                return false;
                            }
                        }

                        InterfaceIndex++;
                        //Keep looping until we either find a device with matching VID and PID, or until we run out of devices to check.
                        //However, just in case some unexpected error occurs, keep track of the number of loops executed.
                        //If the number of loops exceeds a very large number, exit anyway, to prevent inadvertent infinite looping.
                        LoopCounter++;
                        if (LoopCounter == 10000000)    //Surely there aren't more than 10 million devices attached to any forseeable PC...
                        {
                            return false;
                        }
                    }//end of while(true)
                }
                return false;
            }//end of try
            catch
            {
                //Something went wrong if PC gets here.  Maybe a Marshal.AllocHGlobal() failed due to insufficient resources or something.
                return false;
            }
        }
    }
}
