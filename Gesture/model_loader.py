import os
import pickle
import json

class ModelLoader:
    """模型加载器，负责加载和管理手势识别模型"""
    
    def __init__(self):
        self.single_hand_model = None
        self.two_hands_model = None
        self.hand_type_dict = {}
    
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
            if self.single_hand_model:
                print(f"单手手势: {self.single_hand_model.classes_}")
            if self.two_hands_model:
                print(f"双手手势: {self.two_hands_model.classes_}")
            
            return (self.single_hand_model is not None or self.two_hands_model is not None)
        except Exception as e:
            print(f"加载手势模型失败: {e}")
            self.single_hand_model = None
            self.two_hands_model = None
            return False