import cv2
import mediapipe as mp
import numpy as np
import socket
import time

class HandPositionTracker:
    def __init__(self, host='127.0.0.1', port=5000, auto_connect=True):
        self.host = host
        self.port = port
        self.sock = None
        self.cap = None
        self.is_connected = False
        if auto_connect:
            self.connect()

    def connect(self):
        try:
            # 初始化UDP套接字
            self.sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
            # 不绑定本地地址，因为我们只是发送方
            self.is_connected = True
            # 测试发送一条消息
            test_message = "position|0.5|0.5|0.0"
            self.sock.sendto(test_message.encode('utf-8'), (self.host, self.port))
            print(f"HandPositionTracker: 成功连接并向{self.host}:{self.port}发送测试消息")
            return True
        except Exception as e:
            print(f"HandPositionTracker: 连接错误 - {e}")
            return False

    def disconnect(self):
        if self.sock:
            self.sock.close()
            self.sock = None
        if self.cap:
            self.cap.release()
            cv2.destroyAllWindows()
        self.is_connected = False
        print("HandPositionTracker: 已断开连接")

    def track_position(self):
        """跟踪手部位置并发送坐标信息"""
        if not self.is_connected:
            print("HandPositionTracker: 未连接，请先调用 connect() 方法")
            return

        # 打开摄像头
        self.cap = cv2.VideoCapture(0)
        if not self.cap.isOpened():
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

            last_positions = {}  # 存储上一次的手部位置
            last_send_time = time.time()  # 控制发送频率

            while self.cap.isOpened():
                success, image = self.cap.read()
                if not success:
                    break

                # 将BGR图像转换为RGB
                image_rgb = cv2.cvtColor(image, cv2.COLOR_BGR2RGB)

                # 处理图像
                results = hands.process(image_rgb)

                # 图像尺寸
                h, w, c = image.shape
                
                # 默认位置（当没有检测到手时）
                default_pos = (0.5, 0.5, 0.0)

                if results.multi_hand_landmarks:
                    current_time = time.time()
                    # 每30ms发送一次位置信息
                    should_send = (current_time - last_send_time) > 0.03
                    
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
                        
                        # 坐标转换为像素位置（用于显示）
                        pixel_x = int(cx * w)
                        pixel_y = int(cy * h)
                        
                        # 显示手部中心点
                        cv2.circle(image, (pixel_x, pixel_y), 10, (0, 0, 255), -1)
                        cv2.putText(image, f"Hand {hand_idx+1}", (pixel_x+10, pixel_y), 
                                   cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0, 0, 255), 2)
                        
                        # 检查位置是否有显著变化
                        key = f"hand_{hand_idx}"
                        if key in last_positions:
                            last_x, last_y, last_z = last_positions[key]
                            dist = np.sqrt((cx-last_x)**2 + (cy-last_y)**2)
                            # 只有当位置变化明显或者应该发送时才发送
                            if dist > 0.01 or should_send:
                                message = f"position|{hand_idx}|{cx:.4f}|{cy:.4f}|{wrist_depth:.4f}"
                                self.sock.sendto(message.encode('utf-8'), (self.host, self.port))
                                last_positions[key] = (cx, cy, wrist_depth)
                        else:
                            # 首次检测到此手
                            message = f"position|{hand_idx}|{cx:.4f}|{cy:.4f}|{wrist_depth:.4f}"
                            self.sock.sendto(message.encode('utf-8'), (self.host, self.port))
                            last_positions[key] = (cx, cy, wrist_depth)
                    
                    if should_send:
                        last_send_time = current_time
                        
                    # 可视化手部关键点
                    for hand_landmarks in results.multi_hand_landmarks:
                        mp_drawing.draw_landmarks(
                            image, hand_landmarks, mp_hands.HAND_CONNECTIONS,
                            mp_drawing.DrawingSpec(color=(0, 255, 0), thickness=2, circle_radius=4),
                            mp_drawing.DrawingSpec(color=(0, 255, 0), thickness=2))
                else:
                    # 如果没有检测到手，发送默认位置
                    current_time = time.time()
                    if (current_time - last_send_time) > 0.5:  # 降低无手时的发送频率
                        message = f"position|-1|{default_pos[0]}|{default_pos[1]}|{default_pos[2]}"
                        self.sock.sendto(message.encode('utf-8'), (self.host, self.port))
                        last_send_time = current_time

                # 显示结果
                cv2.imshow('手部位置跟踪', image)
                if cv2.waitKey(5) & 0xFF == 27:  # ESC键退出
                    break

        if self.cap:
            self.cap.release()
            cv2.destroyAllWindows()


if __name__ == "__main__":
    tracker = HandPositionTracker()
    tracker.track_position()
    tracker.disconnect()