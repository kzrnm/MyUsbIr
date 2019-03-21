using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace UsbIrSetting
{
    public class MainViewModel : BindableBase
    {
        private readonly UsbIr.UsbIr usbIr = new UsbIr.UsbIr();

        private byte[] _RawResult;
        private byte[] RawResult
        {
            set
            {
                this._RawResult = value;
                this.Result = Clip(value, this.ClipUnit);
            }
            get => _RawResult;
        }

        private byte[] _Result;
        private byte[] Result
        {
            set
            {
                this._Result = value;
                this.SendCommand.RaiseCanExecuteChanged();
                this.SaveCommand.RaiseCanExecuteChanged();
                this.Base64String = Convert.ToBase64String(value);
                this.Base64GZipString = Convert.ToBase64String(Compress.CompressGZip(value));
                this.Base64DeflateString = Convert.ToBase64String(Compress.CompressDeflate(value));
            }
            get => _Result;
        }

        private byte _ClipUnit = 5;
        public byte ClipUnit
        {
            set
            {
                if (this.RawResult != null)
                    this.Result = Clip(this.RawResult, value);
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
            set => SetProperty(ref _IsReading, value);
            get => _IsReading;
        }

        public bool HasResult => this.Result != null && this.Result.Length > 0;

        private RelayCommand _StartReadingCommand;
        public RelayCommand StartReadingCommand
            => _StartReadingCommand ?? (_StartReadingCommand = new RelayCommand(this.StartReading));
        private RelayCommand _EndReadingCommand;
        public RelayCommand EndReadingCommand
            => _EndReadingCommand ?? (_EndReadingCommand = new RelayCommand(this.EndReading));
        private RelayCommand _SendCommand;
        public RelayCommand SendCommand
            => _SendCommand ?? (_SendCommand = new RelayCommand(this.Send, () => this.HasResult));

        private RelayCommand _ImportCommand;
        public RelayCommand ImportCommand
            => _ImportCommand ?? (_ImportCommand = new RelayCommand(this.Import));
        private RelayCommand _SaveCommand;
        public RelayCommand SaveCommand
            => _SaveCommand ?? (_SaveCommand = new RelayCommand(this.Save, () => this.HasResult));

        private void StartReading()
        {
            this.IsReading = true;
            this.usbIr.StartRecoding(this.Frequency);
        }
        private void EndReading()
        {
            this.usbIr.EndRecoding();
            this.RawResult = this.usbIr.Read();
            this.IsReading = false;
        }
        private void Send()
        {
            try
            {
                this.usbIr.Send(this.Result, this.Frequency);
            }
            catch
            {
            }
        }

        private void Import()
        {
            var dialog = new OpenFileDialog();
            dialog.Title = "ファイルを開く";
            dialog.Filter = "全てのファイル(*.*)|*.*";
            if (dialog.ShowDialog() == true)
            {
                this.RawResult = File.ReadAllBytes(dialog.FileName);
            }
        }
        private void Save()
        {
            var dialog = new SaveFileDialog();
            dialog.Title = "ファイルを保存";
            dialog.Filter = "全てのファイル(*.*)|*.*";
            if (dialog.ShowDialog() == true)
            {
                File.WriteAllBytes(dialog.FileName, this.Result);
            }
        }
        public MainViewModel()
        {
        }
    }
}
