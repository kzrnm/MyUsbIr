## �T�v

���J����Ă���[USB�ԊO�������R���A�h�o���X](http://bit-trade-one.co.jp/product/module/adir01p/)�̃��C�u������C#�����ǂ��܂�C#�炵���Ȃ��݌v�������̂ŁAC#�炵���Ȃ�悤�ɏ����܂����B

## �g����

### ��M

`StartRecoding`�Ŏ�M�J�n�A`EndRecoding`�Ŏ�M�I���������`Read`��byte�z���ǂݍ���

### ���M

`Send`�ɔz���n��

### �T���v��

```c#
class Program
{
    static void Main(string[] args)
    {
        using (var usbIr = new UsbIr.UsbIr())
        {
            usbIr.StartRecoding(38000);
            System.Threading.Thread.Sleep(2000); // ���̊ԂɎ�M����
            usbIr.EndRecoding();
            byte[] res = usbIr.Read();

            usbIr.Send(res, 38000);
        }
    }
}
```

## ��

.NET Framework 3.5�ȏ��z�肵�Ă��܂��B.NET Core 3�ł���

## �Q�l�ɂ�������

- http://bit-trade-one.co.jp/support/download/#ADIR01P
- http://bit-trade-one.co.jp/blog/20150827/