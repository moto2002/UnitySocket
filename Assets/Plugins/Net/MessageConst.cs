using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

class MessageConst
{
    //加解密字串
    public  static byte[]  KEY = new byte[8] {0xae,0xbf,0x56,0x78,0xab,0xcd,0xef,0xfe};
    //消息标志符号
    public  const short HEADER = 0x71ab;
    //消息头长度
    public  const int HEADER_LENGTH = 20;
    //客户端ID
    public static int ClientId = 0;
    //连接超时毫秒数
    public static int TimeOut = 50000;
}