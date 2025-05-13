import cv2
import mediapipe as mp
import numpy as np
import time

# 导入自定义模块
from model_loader import ModelLoader
from gesture_stabilizer import GestureStabilizer
from recognizers import SingleHandRecognizer, TwoHandsRecognizer

class GestureRecognition:
    def __init__(self):
        """初始化手势识别器"""
        # 创建模型加载器
        self.model_loader = ModelLoader()
        
        # 创建手势识别器实例(暂未设置模型)
        self.single_hand_recognizer = SingleHandRecognizer()
        self.two_hands_recognizer = TwoHandsRecognizer()
        
        # 创建手势稳定器
        self.gesture_stabilizer = GestureStabilizer(time_window=1.0, threshold=0.9)
        
        # 网络通信
        self.network = None
        
        # 状态跟踪
        self.last_hand_detected_time = time.time()
        self.last_sent_gesture = "Unknown"
    
    def setup(self, network):
        """配置手势识别器，设置网络连接"""
        self.network = network
        # 加载手势识别模型
        self.load_models()
    
    def load_models(self):
        """加载手势识别模型"""
        if self.model_loader.load_gesture_models():
            # 使用加载的模型更新识别器
            self.single_hand_recognizer.model = self.model_loader.single_hand_model
            self.two_hands_recognizer.model = self.model_loader.two_hands_model
            return True
        return False
    
    def recognize_single_hand(self, landmarks):
        """识别单手手势
        
        参数:
            landmarks: 手部关键点坐标列表
            
        返回:
            稳定的手势识别结果
        """
        raw_gesture = self.single_hand_recognizer.recognize(landmarks)
        return self.gesture_stabilizer.add_gesture(raw_gesture)
    
    def recognize_two_hands(self, landmarks1, landmarks2):
        """识别双手手势
        
        参数:
            landmarks1: 第一只手的关键点坐标列表
            landmarks2: 第二只手的关键点坐标列表
            
        返回:
            稳定的手势识别结果
        """
        raw_gesture = self.two_hands_recognizer.recognize(landmarks1, landmarks2)
        return self.gesture_stabilizer.add_gesture(raw_gesture)
    
    def process_video_stream(self, cap, position_tracker=None):
        """处理视频流并进行手势识别
        
        参数:
            cap: OpenCV视频捕获对象
            position_tracker: 可选，位置跟踪器对象
        """
        if not self.network or not self.network.is_connected:
            print("GestureRecognition: 未连接网络，请先调用 setup() 方法设置网络")
            return
        
        # 设置MediaPipe
        mp_hands = mp.solutions.hands
        mp_drawing = mp.solutions.drawing_utils
        
        with mp_hands.Hands(
                static_image_mode=False,
                max_num_hands=2,
                min_detection_confidence=0.5,
                min_tracking_confidence=0.5) as hands:
            
            while cap.isOpened():
                success, image = cap.read()
                if not success:
                    print("无法读取视频帧")
                    break
                
                # 水平镜像翻转图像
                image = cv2.flip(image, 1)
                
                # 将BGR图像转换为RGB
                image_rgb = cv2.cvtColor(image, cv2.COLOR_BGR2RGB)
                
                # 处理图像
                results = hands.process(image_rgb)
                
                # 检测到的手数量
                hand_count = 0 if results.multi_hand_landmarks is None else len(results.multi_hand_landmarks)
                
                # 处理位置跟踪
                hands_info = {}
                if position_tracker:
                    hands_info = position_tracker.process_frame(results, image.shape)
                    if hands_info:
                        image = position_tracker.draw_position_markers(image, hands_info)
                
                # 手势识别
                current_gesture = "Unknown"
                
                if results.multi_hand_landmarks:
                    self.last_hand_detected_time = time.time()
                    
                    # 处理双手情况
                    if hand_count == 2:
                        # 提取两手关键点
                        landmarks1 = []
                        for landmark in results.multi_hand_landmarks[0].landmark:
                            x = int(landmark.x * image.shape[1])
                            y = int(landmark.y * image.shape[0])
                            landmarks1.append((x, y))
                        
                        landmarks2 = []
                        for landmark in results.multi_hand_landmarks[1].landmark:
                            x = int(landmark.x * image.shape[1])
                            y = int(landmark.y * image.shape[0])
                            landmarks2.append((x, y))
                        
                        # 识别双手手势
                        current_gesture = self.recognize_two_hands(landmarks1, landmarks2)
                        
                        # 显示在画面上
                        status_text = f"双手: {current_gesture}"
                        cv2.putText(image, status_text, (10, 30), 
                                   cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0, 255, 0), 2)
                    
                    # 处理单手情况
                    elif hand_count == 1:
                        # 提取手部关键点坐标
                        landmarks = []
                        for landmark in results.multi_hand_landmarks[0].landmark:
                            x = int(landmark.x * image.shape[1])
                            y = int(landmark.y * image.shape[0])
                            landmarks.append((x, y))
                        
                        # 识别单手手势
                        current_gesture = self.recognize_single_hand(landmarks)
                        
                        # 显示在画面上
                        status_text = f"单手: {current_gesture}"
                        cv2.putText(image, status_text, (10, 30), 
                                  cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0, 255, 0), 2)
                    
                    # 可视化手部关键点
                    for hand_landmarks in results.multi_hand_landmarks:
                        mp_drawing.draw_landmarks(
                            image, hand_landmarks, mp_hands.HAND_CONNECTIONS,
                            mp_drawing.DrawingSpec(color=(0, 255, 0), thickness=2, circle_radius=4),
                            mp_drawing.DrawingSpec(color=(0, 255, 0), thickness=2))
                        
                else:
                    # 检测不到手的情况
                    if time.time() - self.last_hand_detected_time > 0.5:
                        print("屏幕中0.5s检测不到手")
                        self.network.send_gesture("HandDetectionStatus|False")
                        self.last_hand_detected_time = time.time()
                        current_gesture = "Unknown"
                
                # 有手时发送状态
                if results.multi_hand_landmarks and time.time() - self.last_hand_detected_time > 1:
                    self.network.send_gesture("HandDetectionStatus|True")
                    self.last_hand_detected_time = time.time()
                
                # 发送手势（只在手势变化时发送）
                if current_gesture != self.last_sent_gesture:
                    print(f"发送手势: {current_gesture}")
                    self.network.send_gesture(current_gesture)
                    self.last_sent_gesture = current_gesture
                
                # 显示图像
                window_title = '手势与位置跟踪' if position_tracker else '手势识别'
                cv2.imshow(window_title, image)
                
                # 退出条件
                if cv2.waitKey(5) & 0xFF == 27:  # ESC键退出
                    break