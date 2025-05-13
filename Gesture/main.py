import argparse
import time
import cv2
from utils.network import NetworkManager
from gesture_recognition import GestureRecognition
from HandPosition import HandPositionTracker

class ShadowTheatreApp:
    def __init__(self, gesture_host='127.0.0.1', gesture_port=8000, 
                position_host='127.0.0.1', position_port=5000,
                enable_gesture=True, enable_position=True):
        """初始化影院应用"""
        # 配置参数
        self.enable_gesture = enable_gesture
        self.enable_position = enable_position
        
        # 网络管理
        self.gesture_network = None
        self.position_tracker = None
        
        # 初始化模块
        if self.enable_gesture:
            # 创建手势网络，稍后传递给手势识别器
            self.gesture_network = NetworkManager(gesture_host, gesture_port)
            # 创建手势识别器，不自动连接
            self.gesture_recognizer = GestureRecognition()
        
        if self.enable_position:
            # 创建位置跟踪器，不自动连接
            self.position_tracker = HandPositionTracker(
                host=position_host, port=position_port, auto_connect=False)
    
    def connect(self):
        """连接到服务器"""
        success = True
        
        if self.enable_gesture and self.gesture_network:
            gesture_success = self.gesture_network.connect()
            success = success and gesture_success
            print(f"手势识别网络连接: {'成功' if gesture_success else '失败'}")
            
        if self.enable_position and self.position_tracker:
            position_success = self.position_tracker.connect()
            success = success and position_success
            print(f"位置跟踪网络连接: {'成功' if position_success else '失败'}")
            
        return success
    
    def disconnect(self):
        """断开所有连接"""
        if self.gesture_network:
            self.gesture_network.disconnect()
            
        if self.position_tracker:
            self.position_tracker.disconnect()
            
        # 清理摄像头资源
        cv2.destroyAllWindows()
    
    def run(self):
        """运行主程序"""
        # 连接网络
        if not self.connect():
            print("连接失败，程序退出")
            return
        
        # 配置并启动手势识别器
        if self.enable_gesture:
            print("正在配置手势识别...")
            self.gesture_recognizer.setup(self.gesture_network)
            
        # 开始进行视频处理和手势识别
        print("正在启动视频处理...")
        cap = cv2.VideoCapture(0)
        if not cap.isOpened():
            print("错误：无法打开摄像头")
            self.disconnect()
            return
        
        print("开始处理视频流...")
        try:
            # 在手势识别器中处理视频流
            if self.enable_gesture and self.enable_position:
                # 同时进行手势识别和位置跟踪
                self.gesture_recognizer.process_video_stream(cap, position_tracker=self.position_tracker)
            elif self.enable_gesture:
                # 只进行手势识别
                self.gesture_recognizer.process_video_stream(cap)
            elif self.enable_position:
                # 只进行位置跟踪（简单模式）
                self.position_tracker.track_position(cap)
        except KeyboardInterrupt:
            print("用户中断，正在退出...")
        finally:
            # 释放资源
            cap.release()
            self.disconnect()
            print("程序已退出")


def parse_arguments():
    """解析命令行参数"""
    parser = argparse.ArgumentParser(description='影子剧场手势&位置识别系统')
    parser.add_argument('--gesture', action='store_true', default=True,
                        help='启用手势识别')
    parser.add_argument('--no-gesture', dest='gesture', action='store_false',
                        help='禁用手势识别')
    parser.add_argument('--position', action='store_true', default=True,
                        help='启用位置跟踪')
    parser.add_argument('--no-position', dest='position', action='store_false',
                        help='禁用位置跟踪')
    parser.add_argument('--gesture-host', default='127.0.0.1', 
                        help='手势识别服务器地址')
    parser.add_argument('--gesture-port', type=int, default=8000,
                        help='手势识别服务器端口')
    parser.add_argument('--position-host', default='127.0.0.1',
                        help='位置跟踪服务器地址')
    parser.add_argument('--position-port', type=int, default=5000,
                        help='位置跟踪服务器端口')
    return parser.parse_args()


if __name__ == "__main__":
    # 解析命令行参数
    args = parse_arguments()
    
    # 创建并运行应用
    app = ShadowTheatreApp(
        gesture_host=args.gesture_host,
        gesture_port=args.gesture_port,
        position_host=args.position_host,
        position_port=args.position_port,
        enable_gesture=args.gesture,
        enable_position=args.position
    )
    
    # 开始运行
    app.run()