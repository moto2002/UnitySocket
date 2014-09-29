using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Threading;

public delegate void DSCClientOnConnectedHandler(object sender, DSCClientConnectedEventArgs e);
public delegate void DSCClientOnErrorHandler(object sender, DSCClientErrorEventArgs e);
public delegate void DSCClientOnDataInHandler(object sender, DSCClientDataInEventArgs e);
public delegate void DSCClientOnDisconnectedHandler(object sender, DSCClientConnectedEventArgs e);

public class DSCClientConnectedEventArgs : EventArgs
{
    public Socket socket;

    public DSCClientConnectedEventArgs(Socket soc)
    {
        this.socket = soc;
    }
}

public class DSCClientErrorEventArgs : EventArgs
{
    public SocketException exception;

    public DSCClientErrorEventArgs(SocketException e)
    {
        this.exception = e;
    }
}

public class DSCClientDataInEventArgs : EventArgs
{
    public byte[] Data;
    public Socket socket;

    public DSCClientDataInEventArgs(Socket soc, byte[] datain)
    {
        this.socket = soc;
        this.Data = datain;
    }
}

public class XTcpClient {
	
	public event DSCClientOnConnectedHandler OnConnected;

    public event DSCClientOnErrorHandler OnError;

    public event DSCClientOnDisconnectedHandler OnDisconnected;
	
    private Socket m_Socket;
    private IPEndPoint m_Remote;
    private Thread m_SelectThread;
    private bool m_bStopRun;
    private ArrayList m_CheckRead, m_CheckSend, m_CheckError;
    private Queue<byte[]> m_SendBuff;
	private Queue<MessageData> m_Datas;
	private object _lock = new object();

    public static byte[] RECEIVE_KEY;
    public static byte[] SEND_KEY;
    
    private void _Init ()
    {
        m_Socket = null;
        m_Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        m_Socket.NoDelay = true;
        m_SelectThread = new Thread(new ThreadStart(_LoopRun));
        m_SelectThread.IsBackground = true;
        m_SelectThread.Start();
        m_bStopRun = false;
        m_CheckRead = new ArrayList();
        m_CheckSend = new ArrayList();
        m_CheckError = new ArrayList();
        m_SendBuff = new Queue<byte[]>();
		m_Datas = new Queue<MessageData>();
        setKey(MessageConst.KEY);
    }
    //设定加解密字串
    public void setKey(byte[] key)
    {
        RECEIVE_KEY = new byte[8];
        SEND_KEY = new byte[8];
        System.Array.Copy(key, 0, RECEIVE_KEY, 0,8);
        System.Array.Copy(key, 0, SEND_KEY, 0, 8);
    }
    //重置加解密字串
    public void resetKey()
    {
        setKey(MessageConst.KEY);
    }

    private void _LoopRun()
    {
        while (!m_bStopRun && Connected)
        {
            m_CheckRead.Clear();
            m_CheckSend.Clear();
            m_CheckError.Clear();
            m_CheckRead.Add(m_Socket);
            m_CheckSend.Add(m_Socket);
            m_CheckError.Add(m_Socket);

            Socket.Select(m_CheckRead, null, null, 100);
            if (m_CheckRead.Count > 0)
            {
                _OnRead();
            }

            Socket.Select(null, m_CheckSend, null, 100);
            if (m_CheckSend.Count > 0)
            {
                _OnSend();
            }

            Socket.Select(null, null, m_CheckError, 100);
            if (m_CheckError.Count > 0)
            {
                _OnError(null);
            }
        }
    }

    private void _OnRead()
    {
        if (m_Socket.Available >= MessageConst.HEADER_LENGTH)
        {
			try {
	            byte[] buffer = new byte[MessageConst.HEADER_LENGTH];
                //试读取消息头数据
                m_Socket.Receive(buffer, MessageConst.HEADER_LENGTH, SocketFlags.Peek);

                if (buffer.Length >= MessageConst.HEADER_LENGTH)//首先保证读取满足消息头的字节数据
                {

                    byte[] headTemp = MessageParse.Decrypt(buffer, 4, MessageParse.CopyByteArray(RECEIVE_KEY));
                    System.Array.Reverse(headTemp, 0, 2);
                    System.Array.Reverse(headTemp, 2, 4);

                    short flag = System.BitConverter.ToInt16(headTemp, 0);
                    short len = System.BitConverter.ToInt16(headTemp, 2);
                    if (flag == MessageConst.HEADER)//验证标志符
                    {
                        if (len <= m_Socket.Available)//满足完整消息的字节长度
                        {
                            //解密头消息
                            headTemp = MessageParse.Decrypt(buffer, MessageConst.HEADER_LENGTH, MessageParse.CopyByteArray(RECEIVE_KEY));
                            //解析头信息数据
                            Message_Head head = MessageParse.UnParseHead(headTemp);
                            if (head == null)//验证头信息有效性
                            {
                                _OnError(new SocketException(10042));
                                Close();
                            }
                            else
                            {
                                int iLength = head.Length;
                                buffer = new byte[iLength];
                                m_Socket.Receive(buffer, 0, buffer.Length, SocketFlags.None);
                                //解密字节数据
                                byte[] buffer1 = MessageParse.Decrypt(buffer, iLength, MessageParse.CopyByteArray(RECEIVE_KEY));
                                //正式解析整个消息信息数据
                                MessageData data = MessageParse.UnParse(buffer1);
                                if (data != null)
                                {
                                    //计算数据校验和
                                    int checksum = MessageParse.CalculateCheckSum(buffer1);
                                    //验证校验和有效性
                                    if (checksum == data.head.CHECK_SUM)
                                    {
                                        lock (_lock)
                                        {
                                            m_Datas.Enqueue(data);
                                        }
                                    }
                                }
                                else
                                {
                                    _OnError(new SocketException(10042));
                                    Close();
                                }

                            }
                        }
                    }
                }
			}
			catch (ObjectDisposedException)
        	{
				Close();
			}
			catch (SocketException ex) {
				_OnError(ex);
				Close();
			}
        }
    }

    private void _OnSend()
    {
        Monitor.Enter(m_SendBuff);
        while (m_SendBuff.Count > 0 && Connected)
        {
            byte[] buffer = m_SendBuff.Dequeue();
            m_Socket.Send(buffer);
        }
        Monitor.Exit(m_SendBuff);
    }

    private void _OnError(SocketException ex)
    {
		if (OnError != null) {
			this.OnError(this, new DSCClientErrorEventArgs(ex));
		}
    }

    private void _BeginConnect()
    {
        IAsyncResult result = m_Socket.BeginConnect(m_Remote, new AsyncCallback(_EndConnect), m_Socket);
        bool success = result.AsyncWaitHandle.WaitOne(MessageConst.TimeOut, true);
        if (!success)
        {
            //超时
            _OnError(new SocketException(10053));
            Close();
        }
    }

    private void _EndConnect(IAsyncResult async)
    {
        try
        {
            Socket client = (Socket)async.AsyncState;
            client.EndConnect(async);

            if (OnConnected != null)
            {
                this.OnConnected(this, new DSCClientConnectedEventArgs(m_Socket));
            }
        }
        catch(SocketException e)
        {
            _OnError(e);
        }
		
		//connectDone.Set();
    }

    #region Interface

    public void Connect(string ip, int port)
    {
        _Init();
        m_Remote = new IPEndPoint(IPAddress.Parse(ip), port);
        _BeginConnect();
    }

    public void Send(byte[] buffer)
    {
        if (buffer != null)
        {
            Monitor.Enter(m_SendBuff);
            m_SendBuff.Enqueue(buffer);
            Monitor.Exit(m_SendBuff);
        }
    }
	
	public MessageData Loop (){
		MessageData data = null;
		if (m_Datas.Count > 0) {
			Monitor.Enter(m_Datas);
			if (m_Datas.Count > 0) {
				data = m_Datas.Dequeue();
			}
			Monitor.Exit(m_Datas);
		}
		return data;
	}

    public void Close()
    {
		if (OnDisconnected != null) {
			this.OnDisconnected(this, new DSCClientConnectedEventArgs(m_Socket));
		}
        if (Connected)
        {
            m_bStopRun = true;
            Thread.Sleep(10);
            m_Socket.Shutdown(SocketShutdown.Both);
        }
        if (m_Socket!=null)
        {           
            m_Socket.Close();
        }
        m_Socket = null;
        m_bStopRun = false;
        m_CheckRead.Clear();
        m_CheckSend.Clear();
        m_CheckError.Clear();
        m_SendBuff.Clear();
        m_Datas.Clear();
        setKey(MessageConst.KEY);
    }

    public void ReConnect()
    {
		Close();
        //_Init();
        _BeginConnect();
    }

    public bool Connected
    {
        get { return m_Socket != null && m_Socket.Connected; }
    }

    #endregion
	
}
