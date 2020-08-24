## 概要

公開されている[USB赤外線リモコンアドバンス](http://bit-trade-one.co.jp/product/module/adir01p/)のライブラリがC#だけどあまりC#らしくない設計だったので、C#らしくなるように書きました。

## 使い方

### 受信

`StartRecoding`で受信開始、`EndRecoding`で受信終了した後に`Read`でbyte配列を読み込む

### 送信

`Send`に配列を渡す

### サンプル

```c#
class Program
{
    static void Main(string[] args)
    {
        using (var usbIr = new UsbIr.UsbIr())
        {
            usbIr.StartRecoding(38000);
            System.Threading.Thread.Sleep(2000); // この間に受信する
            usbIr.EndRecoding();
            byte[] res = usbIr.Read();

            usbIr.Send(res, 38000);
        }
    }
}
```

## 環境

.NET Framework 3.5以上を想定しています。.NET Core 3でも可

## 参考にしたもの

- http://bit-trade-one.co.jp/support/download/#ADIR01P
- http://bit-trade-one.co.jp/blog/20150827/