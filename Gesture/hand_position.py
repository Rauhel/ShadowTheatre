import cv2
import mediapipe as mp
import numpy as np
import socket
import time

class HandPositionTracker:
    def __init__(self, host='127.0.0.1', port=5000, gesture_port=8000, auto_connect=True):
        self.host = host
        self.port = port
        self.gesture_port = gesture_port  # 新增手势端口
        self.sock = None
        self.gesture_sock = None  # 新增手势套接字
        self.is_connected = False
        self.last_positions = {}  # 存储上一次的手部位置
        self.last_send_time = time.time()  # 控制发送频率
        self.last_hand_detected = False  # 记录上次手部检测状态
        
        if auto_connect:
            self.connect()

    def connect(self):
        try:
            # 初始化UDP套接字
            self.sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
            self.gesture_sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
            # 不绑定本地地址，因为我们只是发送方
            self.is_connected = True
            # 测试发送一条消息
            test_message = "position|0|0.5|0.5|0.0"
            self.sock.sendto(test_message.encode('utf-8'), (self.host, self.port))
            print(f"HandPositionTracker: 成功连接并向{self.host}:{self.port}发送测试消息")
            
            # 发送初始手部检测状态
            self.send_hand_detection_status(False)
            return True
        except Exception as e:
            print(f"HandPositionTracker: 连接错误 - {e}")
            self.is_connected = False
            return False

    def send_hand_detection_status(self, detected):
        """发送手部检测状态到手势端口"""
        try:
            if self.gesture_sock and self.is_connected:
                status_message = f"HandDetectionStatus|{str(detected).lower()}"
                self.gesture_sock.sendto(status_message.encode('utf-8'), (self.host, self.gesture_port))
                print(f"HandPositionTracker: 发送手部检测状态: {detected}")
        except Exception as e:
            print(f"HandPositionTracker: 发送手部检测状态失败 - {e}")

    def disconnect(self):
        # 发送手部丢失状态
        if self.is_connected:
            self.send_hand_detection_status(False)
        
        if self.sock:
            self.sock.close()
            self.sock = None
        if self.gesture_sock:
            self.gesture_sock.close()
            self.gesture_sock = None
        self.is_connected = False
        print("HandPositionTracker: 已断开连接")

    # 新方法：处理外部传入的帧和检测结果
    def process_frame(self, results, image_shape):
        """
        处理外部传入的MediaPipe检测结果并发送位置信息
        
        参数:
            results: MediaPipe手部检测结果
            image_shape: 图像尺寸 (height, width, channels)
        
        返回:
            None
        """
        if not self.is_connected:
            print("HandPositionTracker: 未连接，请先调用 connect() 方法")
            return {}
        
        # 解析图像尺寸
        h, w, c = image_shape
        
        # 当前检测到的手的位置信息
        current_hands = {}
        hands_detected = False

        if results.multi_hand_landmarks and len(results.multi_hand_landmarks) > 0:
            hands_detected = True
            current_time = time.time()
            # 每30ms发送一次位置信息
            should_send = (current_time - self.last_send_time) > 0.03
            
            for hand_idx, hand_landmarks in enumerate(results.multi_hand_landmarks):
                # 计算手部中心点
                cx = 0
                cy = 0
                for landmark in hand_landmarks.landmark:
                    cx += landmark.x
                    cy += landmark.y
                
                cx /= len(hand_landmarks.landmark)
                cy /= len(hand_landmarks.landmark)
                
                # 获取手腕深度作为z坐标
                wrist_depth = hand_landmarks.landmark[0].z
                
                # 记录当前手的位置
                current_hands[hand_idx] = (cx, cy, wrist_depth)
                
                # 检查位置是否有显著变化
                key = f"hand_{hand_idx}"
                if key in self.last_positions:
                    last_x, last_y, last_z = self.last_positions[key]
                    dist = np.sqrt((cx-last_x)**2 + (cy-last_y)**2)
                    # 只有当位置变化明显或者应该发送时才发送
                    if dist > 0.01 or should_send:
                        # 坐标已经是镜像的，因为图像已经翻转，MediaPipe检测的是翻转后的图像
                        message = f"position|{hand_idx}|{cx:.4f}|{cy:.4f}|{wrist_depth:.4f}"
                        self.sock.sendto(message.encode('utf-8'), (self.host, self.port))
                        self.last_positions[key] = (cx, cy, wrist_depth)
                else:
                    # 首次检测到此手
                    message = f"position|{hand_idx}|{cx:.4f}|{cy:.4f}|{wrist_depth:.4f}"
                    self.sock.sendto(message.encode('utf-8'), (self.host, self.port))
                    self.last_positions[key] = (cx, cy, wrist_depth)
            
            if should_send:
                self.last_send_time = current_time
        
        # 检查手部检测状态变化
        if hands_detected != self.last_hand_detected:
            self.send_hand_detection_status(hands_detected)
            self.last_hand_detected = hands_detected
        
        return current_hands

    def draw_position_markers(self, image, hands_info):
        """
        在图像上绘制手部位置标记
        
        参数:
            image: 要绘制标记的图像
            hands_info: 手部位置信息 {hand_idx: (x, y, z), ...}
        
        返回:
            带有标记的图像
        """
        h, w, c = image.shape
        
        for hand_idx, (x, y, _) in hands_info.items():
            # 坐标转换为像素位置（用于显示）
            pixel_x = int(x * w)
            pixel_y = int(y * h)
            
            # 显示手部中心点
            cv2.circle(image, (pixel_x, pixel_y), 10, (0, 0, 255), -1)
            cv2.putText(image, f"Hand {hand_idx+1}", (pixel_x+10, pixel_y), 
                      cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0, 0, 255), 2)
        
        return image

    # 保留原始方法以支持独立运行
    def track_position(self):
        """跟踪手部位置并发送坐标信息 - 独立运行模式"""
        if not self.is_connected:
            print("HandPositionTracker: 未连接，请先调用 connect() 方法")
            return

        # 打开摄像头
        cap = cv2.VideoCapture(0)
        if not cap.isOpened():
            print("错误：无法打开摄像头")
            return

        # 设置MediaPipe参数
        mp_hands = mp.solutions.hands
        mp_drawing = mp.solutions.drawing_utils
        with mp_hands.Hands(
                static_image_mode=False,
                max_num_hands=2,
                min_detection_confidence=0.5,
                min_tracking_confidence=0.5) as hands:

            print("HandPositionTracker: 开始跟踪手部位置，按ESC键退出")
            print(f"位置数据端口: {self.port}, 手势状态端口: {self.gesture_port}")

            while cap.isOpened():
                success, image = cap.read()
                if not success:
                    break

                # 水平镜像翻转图像
                image = cv2.flip(image, 1)

                # 将BGR图像转换为RGB
                image_rgb = cv2.cvtColor(image, cv2.COLOR_BGR2RGB)

                # 处理图像
                results = hands.process(image_rgb)

                # 处理检测结果并发送位置信息
                hands_info = self.process_frame(results, image.shape)
                
                # 绘制手部标记
                if hands_info:
                    image = self.draw_position_markers(image, hands_info)
                
                # 可视化手部关键点
                if results.multi_hand_landmarks:
                    for hand_landmarks in results.multi_hand_landmarks:
                        mp_drawing.draw_landmarks(
                            image, hand_landmarks, mp_hands.HAND_CONNECTIONS,
                            mp_drawing.DrawingSpec(color=(0, 255, 0), thickness=2, circle_radius=4),
                            mp_drawing.DrawingSpec(color=(0, 255, 0), thickness=2))

                # 显示手部检测状态
                status_text = f"Hand Detected: {self.last_hand_detected}"
                cv2.putText(image, status_text, (10, 30), cv2.FONT_HERSHEY_SIMPLEX, 1, (0, 255, 0), 2)

                # 显示结果
                cv2.imshow('手部位置跟踪', image)
                if cv2.waitKey(5) & 0xFF == 27:  # ESC键退出
                    break

        cap.release()
        cv2.destroyAllWindows()


if __name__ == "__main__":
    # 独立运行模式
    print("启动手部位置跟踪器...")
    tracker = HandPositionTracker()
    if tracker.is_connected:
        tracker.track_position()
    else:
        print("连接失败，无法启动跟踪")
    tracker.disconnect()