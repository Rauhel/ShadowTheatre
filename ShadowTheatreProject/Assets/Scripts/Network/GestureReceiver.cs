using UnityEngine;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public class GestureReceiver : MonoBehaviour
{
    [Header("Network Settings")]
    [SerializeField] private string hostIP = "127.0.0.1";
    [SerializeField] private int port = 5000;
    [SerializeField] private bool autoConnect = true;
    [SerializeField] private bool logMessages = true;
    
    [Header("Protocol Settings")]
    [SerializeField] private string messageDelimiter = "|";
    
    // 连接状态
    private bool isConnected = false;
    private bool isRunning = false;
    private UdpClient udpClient;
    private IPEndPoint endPoint;
    private Thread receiveThread;
    
    // 输入管理器引用
    private InputManager inputManager;
    
    private void Start()
    {
        inputManager = InputManager.Instance;
        
        if (autoConnect)
        {
            Connect();
        }
    }
    
    public void Connect()
    {
        try
        {
            // 初始化UDP客户端
            udpClient = new UdpClient(port);
            endPoint = new IPEndPoint(IPAddress.Any, port);
            
            // 启动接收线程
            isRunning = true;
            receiveThread = new Thread(new ThreadStart(ReceiveData));
            receiveThread.IsBackground = true;
            receiveThread.Start();
            
            isConnected = true;
            if (logMessages) Debug.Log("GestureReceiver: 成功连接并开始监听端口 " + port);
        }
        catch (Exception e)
        {
            Debug.LogError("GestureReceiver: 连接错误 - " + e.Message);
        }
    }
    
    private void ReceiveData()
    {
        while (isRunning)
        {
            try
            {
                // 接收数据
                byte[] data = udpClient.Receive(ref endPoint);
                string message = Encoding.UTF8.GetString(data);
                
                // 处理数据
                ProcessMessage(message);
            }
            catch (Exception e)
            {
                if (isRunning) // 仅当仍在运行时记录错误
                {
                    Debug.LogError("GestureReceiver: 接收数据错误 - " + e.Message);
                }
            }
        }
    }
    
    private void ProcessMessage(string message)
    {
        // 期望格式: "gesture_type|x|y|confidence|key1:value1|key2:value2"
        // 例如: "point|0.5|0.7|0.95|depth:0.3|angle:45.2"
        
        string[] parts = message.Split(messageDelimiter[0]);
        
        if (parts.Length < 4)
        {
            Debug.LogWarning("GestureReceiver: 无效消息格式: " + message);
            return;
        }
        
        try
        {
            string gestureType = parts[0];
            float x = float.Parse(parts[1]);
            float y = float.Parse(parts[2]);
            float confidence = float.Parse(parts[3]);
            
            // 解析额外数据
            Dictionary<string, float> additionalData = new Dictionary<string, float>();
            for (int i = 4; i < parts.Length; i++)
            {
                string[] keyValue = parts[i].Split(':');
                if (keyValue.Length == 2)
                {
                    additionalData.Add(keyValue[0], float.Parse(keyValue[1]));
                }
            }
            
            // 在主线程上更新输入管理器
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                if (inputManager != null)
                {
                    inputManager.UpdateGestureData(gestureType, new Vector2(x, y), confidence, additionalData);
                    
                    if (logMessages)
                    {
                        Debug.Log($"手势: {gestureType}, 位置: ({x}, {y}), 置信度: {confidence}");
                    }
                }
            });
        }
        catch (Exception e)
        {
            Debug.LogError("GestureReceiver: 解析消息错误 - " + e.Message);
        }
    }
    
    private void OnDestroy()
    {
        Disconnect();
    }
    
    private void OnApplicationQuit()
    {
        Disconnect();
    }
    
    public void Disconnect()
    {
        isRunning = false;
        
        if (receiveThread != null && receiveThread.IsAlive)
        {
            receiveThread.Abort();
            receiveThread = null;
        }
        
        if (udpClient != null)
        {
            udpClient.Close();
            udpClient = null;
        }
        
        isConnected = false;
        if (logMessages) Debug.Log("GestureReceiver: 已断开连接");
    }
    
    // 公共API: 检查是否已连接
    public bool IsConnected()
    {
        return isConnected;
    }
}