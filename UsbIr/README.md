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
    static async Task Main(string[] args)
    {
        using (var usbIr = new UsbIr.UsbIr())
        {
            usbIr.StartRecoding();
            await System.Threading.Tasks.Task.Delay(5000); // ���̊ԂɎ�M����
            usbIr.EndRecoding();
            byte[] res = usbIr.Read();

            usbIr.Send(res);
        }
    }
}
```

## ��

.NET Framework 4.6.2 �ȏ��z�肵�Ă��܂��B.NET 6 �ł���

## �Q�l�ɂ�������

- http://bit-trade-one.co.jp/support/download/#ADIR01P
- http://bit-trade-one.co.jp/blog/20150827/