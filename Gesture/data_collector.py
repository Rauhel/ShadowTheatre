import cv2
import mediapipe as mp
import numpy as np
import os
import time
import json
import datetime

class GestureDataCollector:
    def __init__(self, base_dir="gesture_data"):
        self.mp_hands = mp.solutions.hands
        self.mp_drawing = mp.solutions.drawing_utils
        self.base_dir = base_dir
        
        # 创建基础数据目录
        os.makedirs(base_dir, exist_ok=True)
        
    def collect_gesture_data(self, gesture_name, is_two_hands=False, samples_count=100):
        """
        收集指定手势的特征数据
        
        参数:
            gesture_name: 手势名称
            is_two_hands: 是否为双手手势
            samples_count: 要收集的样本数量
        """
        # 添加双手标记到手势名称
        folder_name = gesture_name
        if is_two_hands:
            folder_name = f"{gesture_name}_TwoHands"
        
        # 为每种手势创建专门的文件夹
        gesture_dir = os.path.join(self.base_dir, folder_name)
        os.makedirs(gesture_dir, exist_ok=True)
        
        # 生成此次收集的唯一时间戳
        timestamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
        session_file = os.path.join(gesture_dir, f"session_{timestamp}.json")
        
        print(f"准备收集 {gesture_name} {'(双手)' if is_two_hands else '(单手)'} 手势数据，需要 {samples_count} 个样本")
        print(f"数据将保存到: {session_file}")
        print("请将手放在摄像头前，准备好后按空格键开始")
        
        cap = cv2.VideoCapture(0)
        
        with self.mp_hands.Hands(
            static_image_mode=False,
            max_num_hands=2,
            min_detection_confidence=0.5) as hands:
            
            # 等待用户准备好
            while True:
                success, image = cap.read()
                cv2.putText(image, f"准备收集: {gesture_name} {'(双手)' if is_two_hands else '(单手)'}", (10, 30), 
                           cv2.FONT_HERSHEY_SIMPLEX, 1, (0, 255, 0), 2)
                cv2.putText(image, "按空格键开始", (10, 70), 
                           cv2.FONT_HERSHEY_SIMPLEX, 1, (0, 255, 0), 2)
                cv2.imshow("手势数据收集", image)
                
                key = cv2.waitKey(1)
                if key == 32:  # 空格键
                    break
                elif key == 27:  # ESC键
                    cap.release()
                    cv2.destroyAllWindows()
                    return
            
            # 开始收集数据
            collected_samples = 0
            samples = []
            
            while collected_samples < samples_count:
                success, image = cap.read()
                if not success:
                    continue
                
                # 处理图像
                image_rgb = cv2.cvtColor(image, cv2.COLOR_BGR2RGB)
                results = hands.process(image_rgb)
                
                # 显示实时进度
                cv2.putText(image, f"收集中: {gesture_name} {'(双手)' if is_two_hands else '(单手)'}", (10, 30), 
                           cv2.FONT_HERSHEY_SIMPLEX, 1, (0, 255, 0), 2)
                cv2.putText(image, f"样本: {collected_samples}/{samples_count}", (10, 70), 
                           cv2.FONT_HERSHEY_SIMPLEX, 1, (0, 255, 0), 2)
                
                # 检测到的手数量
                hand_count = 0 if results.multi_hand_landmarks is None else len(results.multi_hand_landmarks)
                
                # 如果是双手手势，需要检测到2只手才收集
                # 如果是单手手势，只需要检测到1只手即可
                valid_sample = False
                
                if is_two_hands and hand_count == 2:
                    valid_sample = True
                    # 收集双手的关键点
                    hand_data = []
                    
                    for hand_landmarks in results.multi_hand_landmarks:
                        # 绘制手部关键点
                        self.mp_drawing.draw_landmarks(
                            image, hand_landmarks, self.mp_hands.HAND_CONNECTIONS)
                        
                        # 提取此手的特征
                        landmarks_list = []
                        for landmark in hand_landmarks.landmark:
                            landmarks_list.append({
                                "x": landmark.x,
                                "y": landmark.y,
                                "z": landmark.z
                            })
                        hand_data.append(landmarks_list)
                        
                    # 添加到样本集
                    samples.append({
                        "hand1": hand_data[0], 
                        "hand2": hand_data[1],
                        "is_two_hands": True
                    })
                    collected_samples += 1
                    
                elif not is_two_hands and hand_count >= 1:
                    valid_sample = True
                    # 只收集第一只手的关键点
                    hand_landmarks = results.multi_hand_landmarks[0]
                    
                    # 绘制手部关键点
                    self.mp_drawing.draw_landmarks(
                        image, hand_landmarks, self.mp_hands.HAND_CONNECTIONS)
                    
                    # 提取特征
                    landmarks_list = []
                    for landmark in hand_landmarks.landmark:
                        landmarks_list.append({
                            "x": landmark.x,
                            "y": landmark.y,
                            "z": landmark.z
                        })
                    
                    # 添加到样本集
                    samples.append({
                        "hand1": landmarks_list,
                        "hand2": None,
                        "is_two_hands": False
                    })
                    collected_samples += 1
                
                if valid_sample:
                    # 每收集一个样本暂停一下，防止连续的帧太相似
                    time.sleep(0.1)
                
                cv2.imshow("手势数据收集", image)
                if cv2.waitKey(5) & 0xFF == 27:  # ESC键退出
                    break
            
            # 保存数据
            if samples:
                with open(session_file, 'w') as f:
                    json.dump({
                        "gesture": gesture_name,
                        "is_two_hands": is_two_hands,
                        "samples": samples
                    }, f)
                
                print(f"成功收集并保存了 {len(samples)} 个 {gesture_name} {'(双手)' if is_two_hands else '(单手)'} 手势样本")
                
                # 更新此手势的样本总数
                total_samples = self.count_gesture_samples(gesture_dir)
                print(f"{folder_name} 手势当前共有 {total_samples} 个样本")
            
        cap.release()
        cv2.destroyAllWindows()
    
    def count_gesture_samples(self, gesture_dir):
        """计算某个手势目录下的总样本数"""
        total_samples = 0
        for filename in os.listdir(gesture_dir):
            if filename.endswith('.json'):
                try:
                    with open(os.path.join(gesture_dir, filename), 'r') as f:
                        data = json.load(f)
                        total_samples += len(data['samples'])
                except Exception as e:
                    print(f"读取文件 {filename} 时出错: {e}")
        return total_samples

if __name__ == "__main__":
    collector = GestureDataCollector()
    
    # 定义要收集的手势 - (手势名称, 是否双手)
    gestures = [
        ("Bird", True),  # 双手手势
        ("Deer", False),  # 单手手势
        #("Wolf", False),  # 单手手势
    ]
    
    for gesture_name, is_two_hands in gestures:
        collector.collect_gesture_data(gesture_name, is_two_hands, samples_count=100)
        print(f"{gesture_name} {'(双手)' if is_two_hands else '(单手)'} 手势数据收集完成!")
        print("按任意键继续下一个手势...")
        cv2.waitKey(0)