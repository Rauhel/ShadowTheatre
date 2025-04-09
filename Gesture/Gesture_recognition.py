import cv2
import mediapipe as mp
import numpy as np
import socket
import time

class GestureRecognition:
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
            self.is_connected = True
            print(f"GestureRecognition: 成功连接并开始监听端口 {self.port}")
        except Exception as e:
            print(f"GestureRecognition: 连接错误 - {e}")

    def disconnect(self):
        if self.sock:
            self.sock.close()
            self.sock = None
        if self.cap:
            self.cap.release()
            cv2.destroyAllWindows()
        self.is_connected = False
        print("GestureRecognition: 已断开连接")

    def hand_position(self):
        if not self.is_connected:
            print("GestureRecognition: 未连接，请先调用 connect() 方法")
            return

        # 打开摄像头
        self.cap = cv2.VideoCapture(0)

        # 设置MediaPipe参数 - 增加max_num_hands为2确保检测双手
        mp_hands = mp.solutions.hands
        mp_drawing = mp.solutions.drawing_utils
        with mp_hands.Hands(
                static_image_mode=False,
                max_num_hands=2,
                min_detection_confidence=0.5,
                min_tracking_confidence=0.5) as hands:

            # 使用 last_hand_detected_time 变量来记录最后一次检测到手的时间，
            # 在每次检测到手时更新该时间。如果检测不到手的时间超过 5s，则在控制台输出消息。
            last_hand_detected_time = time.time()

            while self.cap.isOpened():
                success, image = self.cap.read()
                if not success:
                    break

                # 将BGR图像转换为RGB
                image_rgb = cv2.cvtColor(image, cv2.COLOR_BGR2RGB)

                # 处理图像
                results = hands.process(image_rgb)

                if results.multi_hand_landmarks:
                    last_hand_detected_time = time.time()
                    for hand_idx, hand_landmarks in enumerate(results.multi_hand_landmarks):
                        # 获取所有关键点的坐标
                        landmarks = []
                        for landmark in hand_landmarks.landmark:
                            x = int(landmark.x * image.shape[1])
                            y = int(landmark.y * image.shape[0])
                            landmarks.append((x, y))

                        # 计算中点位置，用中点位置确定手影的位置。
                        x_coords = [point[0] for point in landmarks]
                        y_coords = [point[1] for point in landmarks]
                        mid_x = int(np.mean(x_coords))
                        mid_y = int(np.mean(y_coords))

                        # 这里需要替换为实际的手影识别代码
                        # 假设使用一个函数 hand_shadow_recognition 来识别手影
                        hand_shadow_type = self.hand_shadow_recognition(landmarks)

                        # 格式化消息
                        message = f"{hand_shadow_type}|{mid_x}|{mid_y}|0.9"

                        # 发送消息
                        self.sock.sendto(message.encode('utf-8'), (self.host, self.port))

                        # 可视化关键点（用于调试）
                        mp_drawing.draw_landmarks(
                            image, hand_landmarks, mp_hands.HAND_CONNECTIONS,
                            mp_drawing.DrawingSpec(color=(0, 255, 0), thickness=2, circle_radius=4),
                            mp_drawing.DrawingSpec(color=(0, 255, 0), thickness=2))
                else:
                    # 检测不到手的时间超过5s
                    if time.time() - last_hand_detected_time > 5:
                        print("屏幕中5s检测不到手")
                        last_hand_detected_time = time.time()
                    # 识别不出手影，认为是Point类型
                    mid_x = image.shape[1] // 2
                    mid_y = image.shape[0] // 2
                    message = f"Point|{mid_x}|{mid_y}|0.9"
                    self.sock.sendto(message.encode('utf-8'), (self.host, self.port))

                # 显示结果
                cv2.imshow('MediaPipe Hands', image)
                if cv2.waitKey(5) & 0xFF == 27:  # ESC键退出
                    break

        if self.cap:
            self.cap.release()
            cv2.destroyAllWindows()

    def hand_shadow_recognition(self, landmarks):
        # 这里需要替换为实际的手影识别代码
        # 目前只是简单返回Point类型
        return "Point"


if __name__ == "__main__":
    gr = GestureRecognition()
    gr.hand_position()
    gr.disconnect()