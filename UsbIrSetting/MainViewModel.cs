using Microsoft.Win32;
using System;
using System.IO;

namespace UsbIrSetting
{
    public class MainViewModel : BindableBase
    {
        private readonly UsbIr.UsbIr usbIr = new();

        private byte[] _RawResult;
        private byte[] RawResult
        {
            set
            {
                _RawResult = value;
                Result = Clip(value, ClipUnit);
            }
            get => _RawResult;
        }

        private byte[] _Result;
        private byte[] Result
        {
            set
            {
                _Result = value;
                SendCommand.RaiseCanExecuteChanged();
                SaveCommand.RaiseCanExecuteChanged();
                Base64String = Convert.ToBase64String(value);
                Base64GZipString = Convert.ToBase64String(Compress.CompressGZip(value));
                Base64DeflateString = Convert.ToBase64String(Compress.CompressDeflate(value));
            }
            get => _Result;
        }

        private byte _ClipUnit = 5;
        public byte ClipUnit
        {
            set
            {
                if (RawResult != null)
                    Result = Clip(RawResult, value);
                SetProperty(ref _ClipUnit, value);
            }
            get => _ClipUnit;
        }

        private static byte ClipByte(byte value, byte unit)
        {
            var remainder = value % unit;
            if (remainder == 0)
                return value;

            if (remainder >= unit / 2)
                return (byte)(value + unit - remainder);
            else
                return (byte)(value - remainder);
        }
        private static byte[] Clip(byte[] raw, byte unit)
        {
            var result = new byte[raw.Length];
            Array.Copy(raw, result, raw.Length);
            if (unit <= 1)
                return result;

            for (int i = 0; i < result.Length; i++)
            {
                result[i] = ClipByte(result[i], unit);
            }

            return result;
        }

        #region Base64
        private string _Base64String;
        public string Base64String
        {
            set => SetProperty(ref _Base64String, value);
            get => _Base64String;
        }
        private string _Base64GZipString;
        public string Base64GZipString
        {
            set => SetProperty(ref _Base64GZipString, value);
            get => _Base64GZipString;
        }
        private string _Base64DeflateString;
        public string Base64DeflateString
        {
            set => SetProperty(ref _Base64DeflateString, value);
            get => _Base64DeflateString;
        }
        #endregion Base64

        private uint _Frequency = 38000;
        public uint Frequency
        {
            set => SetProperty(ref _Frequency, value);
            get => _Frequency;
        }

        private bool _IsReading;
        public bool IsReading
        {
            set
            {
                if (SetProperty(ref _IsReading, value))
                {
                    RaisePropertyChanged(nameof(IsIdle));
                }
            }
            get => _IsReading;
        }

        public bool IsIdle => !_IsReading;

        public bool HasResult => Result != null && Result.Length > 0;

        private RelayCommand _StartReadingCommand;
        public RelayCommand StartReadingCommand
            => _StartReadingCommand ??= new RelayCommand(StartReading);
        private RelayCommand _EndReadingCommand;
        public RelayCommand EndReadingCommand
            => _EndReadingCommand ??= new RelayCommand(EndReading);
        private RelayCommand _SendCommand;
        public RelayCommand SendCommand
            => _SendCommand ??= new RelayCommand(Send, () => HasResult);

        private RelayCommand _ImportCommand;
        public RelayCommand ImportCommand
            => _ImportCommand ??= new RelayCommand(Import);
        private RelayCommand _SaveCommand;
        public RelayCommand SaveCommand
            => _SaveCommand ??= new RelayCommand(Save, () => HasResult);

        private void StartReading()
        {
            IsReading = true;
            usbIr.StartRecoding(Frequency);
        }
        private void EndReading()
        {
            usbIr.EndRecoding();
            RawResult = usbIr.Read();
            IsReading = false;
        }
        private void Send()
        {
            try
            {
                usbIr.Send(Result, Frequency);
            }
            catch
            {
            }
        }

        private void Import()
        {
            var dialog = new OpenFileDialog
            {
                Title = "ファイルを開く",
                Filter = "全てのファイル(*.*)|*.*"
            };
            if (dialog.ShowDialog() == true)
            {
                RawResult = File.ReadAllBytes(dialog.FileName);
            }
        }
        private void Save()
        {
            var dialog = new SaveFileDialog
            {
                Title = "ファイルを保存",
                Filter = "全てのファイル(*.*)|*.*"
            };
            if (dialog.ShowDialog() == true)
            {
                File.WriteAllBytes(dialog.FileName, Result);
            }
        }
        public MainViewModel()
        {
        }
    }
}
