import socket
import time
import datetime

def parse_position_data(message, parts):
    """
    解析位置相关数据（暂时禁用）
    
    参数:
        message: 原始消息
        parts: 已分割的消息部分
    """
    # 此代码段暂时禁用，但保留以供将来使用
    """
    if len(parts) >= 3:
        gesture_type = parts[0]
        x = float(parts[1]) if parts[1] else 0
        y = float(parts[2]) if parts[2] else 0
        confidence = float(parts[3]) if len(parts) > 3 and parts[3] else 0
        
        print(f"  解析: 手势类型={gesture_type}, 位置=({x:.3f}, {y:.3f}), 置信度={confidence:.2f}")
        
        # 提取附加数据
        if len(parts) > 4:
            for i in range(4, len(parts)):
                if ":" in parts[i]:
                    key, value = parts[i].split(":", 1)
                    print(f"    {key}={value}")
    """
    pass

def udp_listener(host='0.0.0.0', port=8000, timeout=None, track_gesture_changes=True):
    """
    创建一个UDP监听器，接收并显示所有传入的UDP数据包
    
    参数:
        host: 监听的IP地址，'0.0.0.0'表示所有可用接口
        port: 监听的端口号
        timeout: 监听超时时间（秒），None表示永不超时
        track_gesture_changes: 是否只跟踪手势类型的变化
    """
    # 创建UDP套接字
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    
    try:
        # 绑定到指定地址和端口
        sock.bind((host, port))
        print(f"UDP监听器已启动 - 监听 {host}:{port}")
        print("等待数据包...(按Ctrl+C停止)")
        
        # 设置超时
        if timeout:
            sock.settimeout(timeout)
            end_time = time.time() + timeout
            print(f"监听将在 {timeout} 秒后停止")
        
        # 计数器
        packet_count = 0
        start_time = time.time()
        last_gesture_type = None
        
        # 开始监听
        while True:
            try:
                # 接收数据包 (最大65535字节)
                data, addr = sock.recvfrom(65535)
                packet_count += 1
                
                # 解码并显示
                try:
                    message = data.decode('utf-8')
                except UnicodeDecodeError:
                    message = f"<无法解码的二进制数据: {len(data)} 字节>"
                
                # 获取当前时间
                current_time = datetime.datetime.now().strftime("%H:%M:%S.%f")[:-3]
                
                # 如果是按照我们的格式发送的，尝试提取手势类型
                current_gesture = None
                if "|" in message:
                    parts = message.split("|")
                    if len(parts) >= 1:
                        current_gesture = parts[0]
                
                # 根据跟踪模式决定显示内容
                if track_gesture_changes:
                    # 只在手势类型变化时显示信息
                    if current_gesture and current_gesture != last_gesture_type:
                        print(f"[{current_time}] 手势变化: {current_gesture}")
                        last_gesture_type = current_gesture
                else:
                    # 显示完整信息
                    print(f"[{current_time}] 从 {addr[0]}:{addr[1]} 接收到数据包 #{packet_count}:")
                    print(f"  内容: {message}")
                    
                    # 调用位置解析函数（当前被禁用）
                    if "|" in message:
                        parts = message.split("|")
                        parse_position_data(message, parts)
                
                # 检查是否超时
                if timeout and time.time() > end_time:
                    print(f"\n监听超时 - 共接收 {packet_count} 个数据包")
                    break
                    
            except socket.timeout:
                print("\n接收超时")
                break
            except KeyboardInterrupt:
                print("\n用户中断")
                break
                
    except Exception as e:
        print(f"错误: {e}")
    finally:
        # 关闭套接字
        sock.close()
        
        # 显示统计信息
        duration = time.time() - start_time
        print(f"\n监听结束 - 共接收 {packet_count} 个数据包，用时 {duration:.1f} 秒")
        if packet_count > 0:
            print(f"平均每秒接收 {packet_count/duration:.1f} 个数据包")

if __name__ == "__main__":
    # 您可以根据需要修改端口
    port = 8000
    track_changes_only = True  # 设置为True只显示手势变化，False显示所有信息
    
    print(f"开始在端口 {port} 监听UDP数据包")
    if track_changes_only:
        print("仅显示手势类型变化")
    else:
        print("显示完整数据包信息")
    print("按Ctrl+C停止监听\n")
    
    udp_listener(port=port, track_gesture_changes=track_changes_only)