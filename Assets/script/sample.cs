using UnityEngine;
using System.Collections;
using ProtoBuf;
using System.IO;
using com.beitown.net.msg;


public class sample : MonoBehaviour {
 	// Use this for initialization
	void Start () {

        NetMgr.GetInstance().Config("127.0.0.1", 31009);
        System.Action connectCallback = () =>
        {
            Debug.Log("连接成功，开始发送数据");
            doSend();
        };
        NetMgr.GetInstance().Connect(connectCallback);
	}

    void doSend()
    {
        Debug.Log("发送数据");
    }

    void FixedUpdate()
    {
        NetMgr.GetInstance().ReceiveLoop();
    }

    public static byte[] Serialize(IExtensible msg)
    {
        byte[] result;
        using (var stream = new MemoryStream())
        {
            Serializer.Serialize(stream, msg);
            result = stream.ToArray();
        }
        return result;
    }

    public static IExtensible Deserialize<IExtensible>(byte[] message)
    {
        IExtensible result;
        using (var stream = new MemoryStream(message))
        {
            result = Serializer.Deserialize<IExtensible>(stream);
        }
        return result;
    }
}
