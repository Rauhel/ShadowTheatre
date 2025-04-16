import cv2
import mediapipe as mp
import numpy as np
import socket
import time
import pickle
import json
import os

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

    def load_gesture_model(self, model_file="gesture_model.pkl"):
        """加载训练好的手势识别模型"""
        try:
            with open(model_file, 'rb') as f:
                self.gesture_model = pickle.load(f)
            print(f"成功加载手势识别模型，可识别的手势类型: {self.gesture_model.classes_}")
            return True
        except Exception as e:
            print(f"加载手势模型失败: {e}")
            self.gesture_model = None
            return False

    def load_gesture_models(self):
        """加载双模型系统"""
        try:
            # 获取当前文件夹的绝对路径
            current_dir = os.path.dirname(os.path.abspath(__file__))
            
            # 加载单手模型
            single_hand_path = os.path.join(current_dir, "gesture_model_single_hand.pkl")
            if os.path.exists(single_hand_path):
                with open(single_hand_path, 'rb') as f:
                    self.single_hand_model = pickle.load(f)
                print("成功加载单手模型")
            else:
                print(f"单手模型文件不存在: {single_hand_path}")
                self.single_hand_model = None
            
            # 加载双手模型
            two_hands_path = os.path.join(current_dir, "gesture_model_two_hands.pkl")
            if os.path.exists(two_hands_path):
                with open(two_hands_path, 'rb') as f:
                    self.two_hands_model = pickle.load(f)
                print(f"成功加载双手模型")
            else:
                print(f"双手模型文件不存在: {two_hands_path}")
                self.two_hands_model = None
            
            # 加载手势类型信息
            hand_types_path = os.path.join(current_dir, "gesture_model_hand_types.json")
            if os.path.exists(hand_types_path):
                with open(hand_types_path, 'r') as f:
                    self.hand_type_dict = json.load(f)
                print("成功加载手势类型信息")
            else:
                print(f"手势类型文件不存在: {hand_types_path}")
                self.hand_type_dict = {}
            
            # 显示加载结果
            if hasattr(self, 'single_hand_model') and self.single_hand_model:
                print(f"单手手势: {self.single_hand_model.classes_}")
            if hasattr(self, 'two_hands_model') and self.two_hands_model:
                print(f"双手手势: {self.two_hands_model.classes_}")
            
            return (self.single_hand_model is not None or self.two_hands_model is not None)
        except Exception as e:
            print(f"加载手势模型失败: {e}")
            self.single_hand_model = None
            self.two_hands_model = None
            return False

    def hand_position(self):
        """修改后的手势识别主函数，支持单/双手识别"""
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

                if results.multi_hand_landmarks:
                    last_hand_detected_time = time.time()
                    
                    # 优先检测双手，所以先处理双手情况
                    if hand_count == 2:
                        # 尝试识别双手手势
                        hand1_landmarks = results.multi_hand_landmarks[0]
                        hand2_landmarks = results.multi_hand_landmarks[1]
                        
                        # 提取两手关键点
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
                        
                        # 优先识别双手手势
                        two_hands_gesture = self.recognize_two_hands(landmarks1, landmarks2)
                        
                        # 添加调试信息
                        print(f"双手手势检测结果: {two_hands_gesture}")
                        
                        if two_hands_gesture != "Unknown":
                            # 如果识别出双手手势，使用双手中点
                            all_x = [p[0] for p in landmarks1] + [p[0] for p in landmarks2]
                            all_y = [p[1] for p in landmarks1] + [p[1] for p in landmarks2]
                            mid_x = int(np.mean(all_x))
                            mid_y = int(np.mean(all_y))
                            
                            # 发送双手手势
                            message = f"{two_hands_gesture}|{mid_x}|{mid_y}|0.9"
                            self.sock.sendto(message.encode('utf-8'), (self.host, self.port))
                            
                            # 显示识别结果
                            cv2.putText(image, f"双手: {two_hands_gesture}", (10, 30), 
                                       cv2.FONT_HERSHEY_SIMPLEX, 1, (0, 255, 0), 2)
                        else:
                            # 如果双手识别失败，尝试分别识别单手
                            gesture1 = self.recognize_single_hand(landmarks1)
                            gesture2 = self.recognize_single_hand(landmarks2)
                            
                            # 如果至少有一只手能够识别
                            if gesture1 != "Unknown" or gesture2 != "Unknown":
                                # 选择识别成功的一只手
                                if gesture1 != "Unknown":
                                    gesture_type = gesture1
                                    mid_x = int(np.mean([p[0] for p in landmarks1]))
                                    mid_y = int(np.mean([p[1] for p in landmarks1]))
                                else:
                                    gesture_type = gesture2
                                    mid_x = int(np.mean([p[0] for p in landmarks2]))
                                    mid_y = int(np.mean([p[1] for p in landmarks2]))
                                
                                # 发送单手识别结果
                                message = f"{gesture_type}|{mid_x}|{mid_y}|0.9"
                                self.sock.sendto(message.encode('utf-8'), (self.host, self.port))
                                
                                # 显示识别结果
                                cv2.putText(image, f"手势: {gesture_type}", (10, 30), 
                                           cv2.FONT_HERSHEY_SIMPLEX, 1, (0, 255, 0), 2)
                            else:
                                # 如果两只手都无法识别，则返回Unknown
                                mid_x = image.shape[1] // 2
                                mid_y = image.shape[0] // 2
                                message = f"Unknown|{mid_x}|{mid_y}|0.9"
                                self.sock.sendto(message.encode('utf-8'), (self.host, self.port))
                        
                        # 可视化两只手
                        for hand_landmarks in results.multi_hand_landmarks:
                            mp_drawing.draw_landmarks(
                                image, hand_landmarks, mp_hands.HAND_CONNECTIONS,
                                mp_drawing.DrawingSpec(color=(0, 255, 0), thickness=2, circle_radius=4),
                                mp_drawing.DrawingSpec(color=(0, 255, 0), thickness=2))
                    
                    # 单独处理单手情况
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
                        gesture_type = self.recognize_single_hand(landmarks)
                        
                        # 计算中点位置
                        mid_x = int(np.mean([point[0] for point in landmarks]))
                        mid_y = int(np.mean([point[1] for point in landmarks]))
                        
                        # 如果无法识别，返回Unknown
                        if gesture_type == "Unknown":
                            gesture_type = "Unknown"
                        
                        # 发送消息
                        message = f"{gesture_type}|{mid_x}|{mid_y}|0.9"
                        self.sock.sendto(message.encode('utf-8'), (self.host, self.port))
                        
                        # 可视化
                        mp_drawing.draw_landmarks(
                            image, hand_landmarks, mp_hands.HAND_CONNECTIONS,
                            mp_drawing.DrawingSpec(color=(0, 255, 0), thickness=2, circle_radius=4),
                            mp_drawing.DrawingSpec(color=(0, 255, 0), thickness=2))
                        
                        # 显示识别结果
                        cv2.putText(image, f"单手: {gesture_type}", (10, 30), 
                                   cv2.FONT_HERSHEY_SIMPLEX, 1, (0, 255, 0), 2)
                else:
                    # 检测不到手的时间超过5s
                    if time.time() - last_hand_detected_time > 5:
                        print("屏幕中5s检测不到手")
                        last_hand_detected_time = time.time()
                        
                    # 默认点
                    mid_x = image.shape[1] // 2
                    mid_y = image.shape[0] // 2
                    message = f"Unknown|{mid_x}|{mid_y}|0.9"
                    self.sock.sendto(message.encode('utf-8'), (self.host, self.port))

                # 显示结果
                cv2.imshow('手势识别', image)
                if cv2.waitKey(5) & 0xFF == 27:  # ESC键退出
                    break

            if self.cap:
                self.cap.release()
                cv2.destroyAllWindows()

    def recognize_single_hand(self, landmarks):
        """单手手势识别"""
        if not hasattr(self, 'single_hand_model') or self.single_hand_model is None:
            return self.rule_based_recognition(landmarks)  # 使用规则识别作为后备
        
        # 将坐标转换为模型所需格式
        features = []
        img_h, img_w = 480, 640  # 假设图像尺寸
        
        for x, y in landmarks:
            # 归一化坐标
            norm_x = x / img_w
            norm_y = y / img_h
            # 添加z坐标（没有则为0）
            features.extend([norm_x, norm_y, 0.0])
        
        # 预测手势
        try:
            gesture = self.single_hand_model.predict([features])[0]
            confidence = max(self.single_hand_model.predict_proba([features])[0])
            
            # 如果置信度较低，返回Unknown
            if confidence < 0.6:
                print(f"单手手势置信度过低: {gesture} ({confidence:.2f})")
                return "Unknown"
            
            print(f"识别到单手手势: {gesture} (置信度: {confidence:.2f})")
            return gesture
        except Exception as e:
            print(f"单手识别错误: {e}")
            return "Unknown"

    def recognize_two_hands(self, landmarks1, landmarks2):
        """双手手势识别"""
        if not hasattr(self, 'two_hands_model') or self.two_hands_model is None:
            print("错误: 未加载双手模型")
            return "Unknown"  # 没有双手模型
        
        # 输出可识别的手势类型进行调试
        if hasattr(self, 'two_hands_model') and hasattr(self.two_hands_model, 'classes_'):
            print(f"可识别的手势类型: {self.two_hands_model.classes_}")
        
        # 将两手坐标转换为模型所需格式
        features = []
        img_h, img_w = 480, 640
        
        # 检查关键点数量
        if len(landmarks1) < 21 or len(landmarks2) < 21:
            print(f"警告: 关键点不足21个 (手1: {len(landmarks1)}, 手2: {len(landmarks2)})")
            return "Unknown"
        
        # 第一只手特征
        for x, y in landmarks1:
            norm_x = x / img_w
            norm_y = y / img_h
            features.extend([norm_x, norm_y, 0.0])
        
        # 第二只手特征
        for x, y in landmarks2:
            norm_x = x / img_w
            norm_y = y / img_h
            features.extend([norm_x, norm_y, 0.0])
        
        # 检查特征向量长度
        expected_length = 21 * 3 * 2  # 21个关键点 * 3坐标 * 2只手
        if len(features) != expected_length:
            print(f"警告: 特征向量长度不匹配 (预期: {expected_length}, 实际: {len(features)})")
        
        # 预测手势
        try:
            # 获取概率分布
            probabilities = self.two_hands_model.predict_proba([features])[0]
            
            # 输出所有类别的概率
            for i, gesture_class in enumerate(self.two_hands_model.classes_):
                print(f"  {gesture_class}: {probabilities[i]:.4f}")
            
            gesture = self.two_hands_model.predict([features])[0]
            confidence = max(probabilities)
            
            # 如果置信度可疑地高
            if confidence > 0.99:
                print("警告: 置信度异常高，可能模型过拟合")
                
            # 如果置信度太低，返回Unknown
            if confidence < 0.6:
                print(f"双手手势置信度过低: {gesture} ({confidence:.2f})")
                return "Unknown"
            
            print(f"识别到双手手势: {gesture} (置信度: {confidence:.2f})")
            return gesture
        except Exception as e:
            print(f"双手识别错误: {e}")
            import traceback
            traceback.print_exc()  # 打印详细错误信息
            return "Unknown"

    def hand_shadow_recognition(self, landmarks):
        """
        使用训练好的模型识别手势
        
        Args:
            landmarks: 手部关键点列表 [(x, y), ...]
        
        Returns:
            识别出的手势类型字符串
        """
        # 如果没有加载模型，使用简单规则
        if not hasattr(self, 'gesture_model') or self.gesture_model is None:
            return self.rule_based_recognition(landmarks)
        
        # 将关键点坐标转换为模型需要的特征向量格式
        features = []
        img_h, img_w = 480, 640  # 假设的图像尺寸，需要与您的实际尺寸匹配
        
        for x, y in landmarks:
            # 归一化坐标
            norm_x = x / img_w
            norm_y = y / img_h
            # 这里没有z坐标，用0代替
            features.extend([norm_x, norm_y, 0.0])
        
        # 预测手势类型
        try:
            gesture = self.gesture_model.predict([features])[0]
            confidence = max(self.gesture_model.predict_proba([features])[0])
            return gesture
        except Exception as e:
            print(f"手势识别错误: {e}")
            return "Unknown"

    def rule_based_recognition(self, landmarks):
        """
        基于简单规则的手势识别，作为备选方案
        """
        if len(landmarks) < 21:
            return "Unknown"
        
        # 提取关键点
        thumb_tip = landmarks[4]
        index_tip = landmarks[8]
        middle_tip = landmarks[12]
        ring_tip = landmarks[16]
        pinky_tip = landmarks[20]
        
        # 计算中心点
        center_x = sum(point[0] for point in landmarks) // len(landmarks)
        center_y = sum(point[1] for point in landmarks) // len(landmarks)
        
        # 获取指尖高度相对于手掌的位置
        palm_base = landmarks[0]  # 手腕
        is_thumb_up = thumb_tip[1] < palm_base[1]
        is_index_up = index_tip[1] < palm_base[1]
        is_middle_up = middle_tip[1] < palm_base[1]
        is_ring_up = ring_tip[1] < palm_base[1]
        is_pinky_up = pinky_tip[1] < palm_base[1]
        
        # 计算指尖之间的距离
        def distance(p1, p2):
            return ((p1[0]-p2[0])**2 + (p1[1]-p2[1])**2)**0.5
        
        thumb_index_dist = distance(thumb_tip, index_tip)
        
        # 识别常见手势
        if is_index_up and not is_middle_up and not is_ring_up and not is_pinky_up:
            return "Point"
        elif is_index_up and is_middle_up and not is_ring_up and not is_pinky_up:
            return "Peace"
        elif is_index_up and is_middle_up and is_ring_up and is_pinky_up:
            return "Hand"
        elif thumb_index_dist < 30:  # 阈值需要调整
            return "Circle"
        else:
            return "Unknown"


if __name__ == "__main__":
    gr = GestureRecognition()
    # 确保先加载模型再启动手势识别
    gr.load_gesture_models()
    gr.hand_position()
    gr.disconnect()