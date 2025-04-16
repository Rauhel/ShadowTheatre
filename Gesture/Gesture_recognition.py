import cv2
import mediapipe as mp
import numpy as np
import time

# 导入自定义模块
from model_loader import ModelLoader
from gesture_stabilizer import GestureStabilizer
from utils.network import NetworkManager
from recognizers import SingleHandRecognizer, TwoHandsRecognizer

class GestureRecognition:
    def __init__(self, host='127.0.0.1', port=8000, auto_connect=True):
        """初始化手势识别器"""
        # 创建网络管理器
        self.network = NetworkManager(host, port)
        self.cap = None
        
        # 创建模型加载器
        self.model_loader = ModelLoader()
        
        # 创建手势识别器实例(暂未设置模型)
        self.single_hand_recognizer = SingleHandRecognizer()
        self.two_hands_recognizer = TwoHandsRecognizer()
        
        # 创建手势稳定器
        self.gesture_stabilizer = GestureStabilizer(time_window=1.0, threshold=0.9)
        
        # 自动连接
        if auto_connect:
            self.connect()
    
    def connect(self):
        """建立连接"""
        return self.network.connect()
    
    def disconnect(self):
        """断开连接并释放资源"""
        self.network.disconnect()
        if self.cap:
            self.cap.release()
            cv2.destroyAllWindows()
    
    def load_models(self):
        """加载手势识别模型"""
        if self.model_loader.load_gesture_models():
            # 使用加载的模型更新识别器
            self.single_hand_recognizer.model = self.model_loader.single_hand_model
            self.two_hands_recognizer.model = self.model_loader.two_hands_model
            return True
        return False
    
    def recognize_gestures(self):
        """执行手势识别任务"""
        if not self.network.is_connected:
            print("GestureRecognition: 未连接，请先调用 connect() 方法")
            return
        
        # 加载模型
        self.load_models()
        
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
            
            last_sent_gesture = None
            last_hand_detected_time = time.time()
            
            while self.cap.isOpened():
                success, image = self.cap.read()
                if not success:
                    break
                
                # 将BGR图像转换为RGB
                image_rgb = cv2.cvtColor(image, cv2.COLOR_BGR2RGB)
                
                # 处理图像
                results = hands.process(image_rgb)
                
                # 检测到手的数量
                hand_count = 0 if results.multi_hand_landmarks is None else len(results.multi_hand_landmarks)
                
                current_gesture = "Unknown"
                
                if results.multi_hand_landmarks:
                    last_hand_detected_time = time.time()
                    
                    # 处理双手情况
                    if hand_count == 2:
                        # 提取两手关键点
                        hand1_landmarks = results.multi_hand_landmarks[0]
                        hand2_landmarks = results.multi_hand_landmarks[1]
                        
                        landmarks1 = []
                        for landmark in hand1_landmarks.landmark:
                            x = int(landmark.x * image.shape[1])
                            y = int(landmark.y * image.shape[0])
                            landmarks1.append((x, y))
                        
                        landmarks2 = []
                        for landmark in hand2_landmarks.landmark:
                            x = int(landmark.x * image.shape[1])
                            y = int(landmark.y * image.shape[0])
                            landmarks2.append((x, y))
                        
                        # 识别双手手势
                        raw_gesture = self.two_hands_recognizer.recognize(landmarks1, landmarks2)
                        
                        # 使用稳定器处理
                        current_gesture = self.gesture_stabilizer.add_gesture(raw_gesture)
                        
                        # 显示在画面上
                        status_text = f"双手: {raw_gesture}"
                        if raw_gesture != current_gesture:
                            status_text += f" -> {current_gesture}"
                        cv2.putText(image, status_text, (10, 30), 
                                   cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0, 255, 0), 2)
                        
                        # 可视化两只手
                        for hand_landmarks in results.multi_hand_landmarks:
                            mp_drawing.draw_landmarks(
                                image, hand_landmarks, mp_hands.HAND_CONNECTIONS,
                                mp_drawing.DrawingSpec(color=(0, 255, 0), thickness=2, circle_radius=4),
                                mp_drawing.DrawingSpec(color=(0, 255, 0), thickness=2))
                    
                    # 处理单手情况
                    elif hand_count == 1:
                        # 单手手势识别
                        hand_landmarks = results.multi_hand_landmarks[0]
                        
                        # 获取所有关键点的坐标
                        landmarks = []
                        for landmark in hand_landmarks.landmark:
                            x = int(landmark.x * image.shape[1])
                            y = int(landmark.y * image.shape[0])
                            landmarks.append((x, y))
                        
                        # 单手识别
                        raw_gesture = self.single_hand_recognizer.recognize(landmarks)
                        
                        # 使用稳定器处理
                        current_gesture = self.gesture_stabilizer.add_gesture(raw_gesture)
                        
                        # 显示在画面上
                        status_text = f"单手: {raw_gesture}"
                        if raw_gesture != current_gesture:
                            status_text += f" -> {current_gesture}"
                        cv2.putText(image, status_text, (10, 30), 
                                  cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0, 255, 0), 2)
                        
                        # 可视化
                        mp_drawing.draw_landmarks(
                            image, hand_landmarks, mp_hands.HAND_CONNECTIONS,
                            mp_drawing.DrawingSpec(color=(0, 255, 0), thickness=2, circle_radius=4),
                            mp_drawing.DrawingSpec(color=(0, 255, 0), thickness=2))
                else:
                    # 检测不到手的时间超过5s
                    if time.time() - last_hand_detected_time > 5:
                        print("屏幕中5s检测不到手")
                        last_hand_detected_time = time.time()
                
                # 只有当稳定手势变化时才发送
                if current_gesture != last_sent_gesture:
                    print(f"发送手势: {current_gesture}")
                    self.network.send_gesture(current_gesture)
                    last_sent_gesture = current_gesture
                
                # 显示结果
                cv2.imshow('手势识别', image)
                if cv2.waitKey(5) & 0xFF == 27:  # ESC键退出
                    break
            
            # 清理资源
            if self.cap:
                self.cap.release()
                cv2.destroyAllWindows()

if __name__ == "__main__":
    gr = GestureRecognition()
    gr.recognize_gestures()
    gr.disconnect()