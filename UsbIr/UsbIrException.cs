using System;
using System.Runtime.Serialization;

namespace UsbIr
{
    [Serializable]
    public class UsbIrException : Exception
    {
        public UsbIrException() { }
        public UsbIrException(string message) : base(message) { }
        public UsbIrException(string message, Exception inner) : base(message, inner) { }
        protected UsbIrException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
