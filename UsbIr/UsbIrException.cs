using System;

namespace UsbIr
{
    [Serializable]
    public class UsbIrException : Exception
    {
        public UsbIrException() { }
        public UsbIrException(string message) : base(message) { }
        public UsbIrException(string message, Exception inner) : base(message, inner) { }
        protected UsbIrException(
            System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
