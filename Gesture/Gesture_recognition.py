import cv2
import mediapipe as mp
import numpy as np
import socket

"""
类的初始化：__init__ 方法接收 host、port 和 auto_connect 三个参数，默认情况下自动连接到本地 IP 127.0.0.1 的端口 5000。
连接方法：connect 方法用于初始化 UDP 套接字并建立连接。
断开连接方法：disconnect 方法用于关闭套接字、释放摄像头资源并销毁所有窗口。
手势识别方法：hand_position 方法用于执行手势识别任务，在执行前会检查是否已经连接。

在HandPosition中，暂且用识别到手部的第五个点确定player的位置，之后再根据手势识别精确度等修改算法。

"""

class GestureRecognition:
    def __init__(self, host='127.0.0.1', port=8000, auto_connect=True):
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
            test_message = "test|0.5|0.5|1.0|depth:0.0"
            self.sock.sendto(test_message.encode('utf-8'), (self.host, self.port))
            print(f"GestureRecognition: 成功连接并向{self.host}:{self.port}发送测试消息")
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
        # 检查摄像头是否成功打开
        if not self.cap.isOpened():
            print("错误：无法打开摄像头")
            return

        # 设置MediaPipe参数 - 增加max_num_hands为2确保检测双手
        mp_hands = mp.solutions.hands
        mp_drawing = mp.solutions.drawing_utils
        with mp_hands.Hands(
                static_image_mode=False,
                max_num_hands=2,
                min_detection_confidence=0.5,
                min_tracking_confidence=0.5) as hands:

            while self.cap.isOpened():
                success, image = self.cap.read()
                if not success:
                    break

                # 将BGR图像转换为RGB
                image_rgb = cv2.cvtColor(image, cv2.COLOR_BGR2RGB)

                # 处理图像
                results = hands.process(image_rgb)

                if results.multi_hand_landmarks:
                    for hand_idx, hand_landmarks in enumerate(results.multi_hand_landmarks):
                        # 获取第五个关键点的信息
                        landmark_5 = hand_landmarks.landmark[4]  # 索引4代表第五个关键点
                        x = float(landmark_5.x)
                        y = float(landmark_5.y)
                        z = float(landmark_5.z)
                        confidence = 0.9  # 假设置信度为0.9

                        # 格式化消息
                        message = f"point|{x}|{y}|{confidence}|depth:{z}"

                        # 发送消息
                        self.sock.sendto(message.encode('utf-8'), (self.host, self.port))

                        # 可视化关键点（用于调试）
                        mp_drawing.draw_landmarks(
                            image, hand_landmarks, mp_hands.HAND_CONNECTIONS,
                            mp_drawing.DrawingSpec(color=(0, 255, 0), thickness=2, circle_radius=4),
                            mp_drawing.DrawingSpec(color=(0, 255, 0), thickness=2))

                # 显示结果
                cv2.imshow('MediaPipe Hands', image)
                if cv2.waitKey(5) & 0xFF == 27:  # ESC键退出
                    break

        if self.cap:
            self.cap.release()
            cv2.destroyAllWindows()


if __name__ == "__main__":
    gr = GestureRecognition()
    gr.hand_position()
    gr.disconnect()