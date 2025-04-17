import cv2
import mediapipe as mp
import numpy as np
import time

# 导入自定义模块
from model_loader import ModelLoader
from gesture_stabilizer import GestureStabilizer
from utils.network import NetworkManager
from recognizers import SingleHandRecognizer, TwoHandsRecognizer
# 导入位置跟踪模块
from HandPosition import HandPositionTracker

class GestureRecognition:
    def __init__(self, gesture_host='127.0.0.1', gesture_port=8000, 
                 position_host='127.0.0.1', position_port=5000, auto_connect=True):
        """初始化手势识别器"""
        # 创建网络管理器
        self.network = NetworkManager(gesture_host, gesture_port)
        self.cap = None
        
        # 创建模型加载器
        self.model_loader = ModelLoader()
        
        # 创建手势识别器实例(暂未设置模型)
        self.single_hand_recognizer = SingleHandRecognizer()
        self.two_hands_recognizer = TwoHandsRecognizer()
        
        # 创建手势稳定器
        self.gesture_stabilizer = GestureStabilizer(time_window=1.0, threshold=0.9)
        
        # 创建位置跟踪器
        self.position_tracker = HandPositionTracker(host=position_host, port=position_port, auto_connect=False)
        self.enable_position_tracking = False
        
        # 自动连接
        if auto_connect:
            self.connect()
    
    def connect(self):
        """建立连接"""
        gesture_connected = self.network.connect()
        
        # 只有当启用位置跟踪时才连接
        if self.enable_position_tracking:
            position_connected = self.position_tracker.connect()
            return gesture_connected and position_connected
        
        return gesture_connected
    
    def disconnect(self):
        """断开连接并释放资源"""
        self.network.disconnect()
        if self.enable_position_tracking:
            self.position_tracker.disconnect()
        if self.cap:
            self.cap.release()
            cv2.destroyAllWindows()
    
    def enable_position(self, enable=True):
        """启用或禁用位置跟踪"""
        self.enable_position_tracking = enable
        if enable and not self.position_tracker.is_connected:
            self.position_tracker.connect()
    
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
                
                # 如果启用了位置跟踪，处理位置信息
                hands_info = {}
                if self.enable_position_tracking:
                    hands_info = self.position_tracker.process_frame(results, image.shape)
                    # 在图像上绘制位置标记
                    if hands_info:
                        image = self.position_tracker.draw_position_markers(image, hands_info)
                
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
                    # 检测不到手的时间超过0.5s
                    if time.time() - last_hand_detected_time > 0.5:
                        print("屏幕中0.5s检测不到手")
                        # 发送手部检测状态：未检测到手
                        self.network.send_gesture("HandDetectionStatus|False")
                        last_hand_detected_time = time.time()
                if results.multi_hand_landmarks:
                    # 有手被检测到，发送检测状态
                    if time.time() - last_hand_detected_time > 1:  # 避免频繁发送状态
                        self.network.send_gesture("HandDetectionStatus|True")
                        last_hand_detected_time = time.time()  # 重置计时器
                
                # 只有当稳定手势变化时才发送
                if current_gesture != last_sent_gesture:
                    print(f"发送手势: {current_gesture}")
                    self.network.send_gesture(current_gesture)
                    last_sent_gesture = current_gesture
                
                # 显示结果
                window_title = '手势与位置跟踪' if self.enable_position_tracking else '手势识别'
                cv2.imshow(window_title, image)
                if cv2.waitKey(5) & 0xFF == 27:  # ESC键退出
                    break
            
            # 清理资源
            if self.cap:
                self.cap.release()
                cv2.destroyAllWindows()


if __name__ == "__main__":
    # 创建手势识别实例
    gr = GestureRecognition(gesture_port=8000, position_port=5000)
    # 启用位置跟踪功能
    gr.enable_position(True)
    # 开始识别
    gr.recognize_gestures()
    # 断开连接
    gr.disconnect()