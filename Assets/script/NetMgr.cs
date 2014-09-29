using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class NetMgr
{

    private string sIP = "183.60.243.195";
    private int iPort = 31009;

    private XTcpClient m_Client;
    private System.Action m_ConnectSuccessCallBack;
    private bool m_bWarnLostConnect;

    private volatile static NetMgr _instance = null;
    private static readonly object lockHelper = new object();
    private NetMgr() { }
    public static NetMgr GetInstance()
    {
        if (_instance == null)
        {
            lock (lockHelper)
            {
                if (_instance == null)
                {
                    _instance = new NetMgr();
                    _instance.Init();
                }
            }
        }
        return _instance;
    }

    public void Init()
    {
        m_Client = new XTcpClient();
        m_Client.OnConnected += HandleM_ClientOnConnected;
        m_Client.OnDisconnected += HandleM_ClientOnDisconnected;
        m_Client.OnError += HandleM_ClientOnError;
    }

    void HandleM_ClientOnError(object sender, DSCClientErrorEventArgs e)
    {
        Debug.LogWarning("::OnError");
    }

    void HandleM_ClientOnDisconnected(object sender, DSCClientConnectedEventArgs e)
    {
        Debug.LogWarning("::OnDisconnected");
    }

    void HandleM_ClientOnConnected(object sender, DSCClientConnectedEventArgs e)
    {
        Debug.LogWarning("::OnConnected");
        if (Connected)
        {
            if (m_ConnectSuccessCallBack != null)
            {
                m_ConnectSuccessCallBack();
                m_ConnectSuccessCallBack = null;
            }
        }
        else
        {
            m_bWarnLostConnect = true;
        }
    }

    void _ShowLostConnect()
    {
        //Globals.It.HideWaiting();
        //Globals.It.ShowWarn(2, 5, null);
    }
    //处理接受消息循环
    public void ReceiveLoop()
    {
        if (m_Client != null && m_Client.Connected)
        {
            //Globals.It.ProcessMsg(m_Client.Loop());
            Debug.Log("处理消息数据");
        }
        if (m_bWarnLostConnect)
        {
            m_bWarnLostConnect = false;
            _ShowLostConnect();
        }
    }
    //指定服务器连接参数
    public void Config(string sip, int iport)
    {
        sIP = sip;
        iPort = iport;
    }

    public void ReInit()
    {
        Init();
    }

    public void Connect()
    {
        m_Client.Connect(sIP, iPort);
    }

    public void Connect(System.Action callback)
    {
        m_ConnectSuccessCallBack = callback;
        m_Client.Connect(sIP, iPort);
    }

    public void Send(byte[] buffer)
    {
        if (buffer != null && Connected)
        {
            m_Client.Send(buffer);
        }
    }

    public void Close()
    {
        if (Connected)
        {
            m_Client.Close();
        }
    }

    public bool Connected
    {
        get
        {
            return m_Client != null && m_Client.Connected;
        }
    }
}
