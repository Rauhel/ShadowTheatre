import socket

class NetworkManager:
    """网络通信管理器，负责与Unity通信"""
    
    def __init__(self, host='127.0.0.1', port=8000):
        self.host = host
        self.port = port
        self.sock = None
        self.is_connected = False
    
    def connect(self):
        """建立网络连接"""
        try:
            # 初始化UDP套接字
            self.sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
            # 不绑定本地地址，因为我们只是发送方
            self.is_connected = True
            # 测试发送一条消息
            test_message = "test_gesture|Unknown"
            self.sock.sendto(test_message.encode('utf-8'), (self.host, self.port))
            print(f"NetworkManager: 成功连接并向{self.host}:{self.port}发送测试消息")
            return True
        except Exception as e:
            print(f"NetworkManager: 连接错误 - {e}")
            return False
    
    def disconnect(self):
        """断开网络连接"""
        if self.sock:
            self.sock.close()
            self.sock = None
        self.is_connected = False
        print("NetworkManager: 已断开连接")
    
    def send_gesture(self, gesture_type):
        """发送手势类型"""
        if not self.is_connected:
            print("NetworkManager: 未连接，请先调用 connect() 方法")
            return False
        
        try:
            message = f"gesture|{gesture_type}"
            self.sock.sendto(message.encode('utf-8'), (self.host, self.port))
            return True
        except Exception as e:
            print(f"NetworkManager: 发送错误 - {e}")
            return False

    def send_position_and_gesture(self, gesture_type, x, y, confidence=0.9):
        """发送位置和手势类型"""
        if not self.is_connected:
            print("NetworkManager: 未连接，请先调用 connect() 方法")
            return False
        
        try:
            message = f"{gesture_type}|{x}|{y}|{confidence}"
            self.sock.sendto(message.encode('utf-8'), (self.host, self.port))
            return True
        except Exception as e:
            print(f"NetworkManager: 发送错误 - {e}")
            return False