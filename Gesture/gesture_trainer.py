import os
import json
import numpy as np
from sklearn.model_selection import train_test_split
from sklearn.ensemble import RandomForestClassifier
import pickle
import matplotlib.pyplot as plt
from sklearn.metrics import confusion_matrix, ConfusionMatrixDisplay

class GestureTrainer:
    def __init__(self, data_dir="gesture_data", model_file="gesture_model.pkl"):
        self.data_dir = data_dir
        self.model_file = model_file
        self.model = None
        self.hand_type_dict = {}  # 存储每个手势是单手还是双手
        
    def load_data(self):
        """从每个手势的文件夹加载所有样本"""
        X_single = []  # 单手特征
        X_double = []  # 双手特征
        y_single = []  # 单手标签
        y_double = []  # 双手标签
        
        # 获取所有手势文件夹
        gesture_folders = [d for d in os.listdir(self.data_dir) 
                         if os.path.isdir(os.path.join(self.data_dir, d))]
        
        if not gesture_folders:
            print(f"错误：在 {self.data_dir} 中没有找到手势文件夹!")
            return np.array([]), np.array([])
        
        print(f"发现以下手势类型: {gesture_folders}")
        
        # 从每个手势文件夹加载所有会话文件
        for folder_name in gesture_folders:
            gesture_dir = os.path.join(self.data_dir, folder_name)
            json_files = [f for f in os.listdir(gesture_dir) if f.endswith('.json')]
            
            if not json_files:
                print(f"警告：{folder_name} 文件夹中没有找到数据文件")
                continue
            
            # 检查是否是双手手势
            is_two_hands = "_TwoHands" in folder_name
            gesture_name = folder_name.replace("_TwoHands", "") if is_two_hands else folder_name
            
            # 记录手势类型
            self.hand_type_dict[gesture_name] = is_two_hands
                
            print(f"正在处理 {gesture_name} {'(双手)' if is_two_hands else '(单手)'} 手势，共 {len(json_files)} 个会话文件")
            gesture_samples_count = 0
            
            # 处理每个会话文件
            for json_file in json_files:
                file_path = os.path.join(gesture_dir, json_file)
                try:
                    with open(file_path, 'r') as f:
                        data = json.load(f)
                    
                    session_samples = data['samples']
                    gesture_samples_count += len(session_samples)
                    
                    # 将会话数据添加到训练集
                    for sample in session_samples:
                        if is_two_hands:
                            # 双手特征
                            if sample["hand2"] is None:
                                continue  # 跳过不完整的数据
                                
                            features = []
                            # 第一只手特征
                            for landmark in sample["hand1"]:
                                features.extend([landmark['x'], landmark['y'], landmark['z']])
                            # 第二只手特征
                            for landmark in sample["hand2"]:
                                features.extend([landmark['x'], landmark['y'], landmark['z']])
                            
                            X_double.append(features)
                            y_double.append(gesture_name)
                        else:
                            # 单手特征
                            features = []
                            for landmark in sample["hand1"]:
                                features.extend([landmark['x'], landmark['y'], landmark['z']])
                            
                            X_single.append(features)
                            y_single.append(gesture_name)
                except Exception as e:
                    print(f"处理文件 {file_path} 时出错: {e}")
            
            print(f"  - 已加载 {gesture_name} {'(双手)' if is_two_hands else '(单手)'} 手势的 {gesture_samples_count} 个样本")
        
        # 保存手势类型信息
        with open(self.model_file.replace('.pkl', '_hand_types.json'), 'w') as f:
            json.dump(self.hand_type_dict, f)
            
        print(f"单手手势样本: {len(X_single)}，双手手势样本: {len(X_double)}")
        
        # 分别训练单手和双手手势模型
        self.train_single_hand_model(X_single, y_single)
        self.train_two_hands_model(X_double, y_double)
        
        return True
    
    def train_single_hand_model(self, X, y):
        """训练单手手势识别模型"""
        if len(X) == 0:
            print("错误：没有找到单手手势训练数据!")
            return False
            
        print(f"训练单手手势模型：{len(X)} 个样本，{len(set(y))} 种不同的手势")
        
        # 划分训练集和测试集
        X_train, X_test, y_train, y_test = train_test_split(
            X, y, test_size=0.2, random_state=42)
        
        # 训练模型
        print("训练单手手势模型中...")
        self.single_hand_model = RandomForestClassifier(n_estimators=100, random_state=42)
        self.single_hand_model.fit(X_train, y_train)
        
        # 评估模型
        score = self.single_hand_model.score(X_test, y_test)
        print(f"单手模型准确率: {score:.2f}")
        
        # 显示混淆矩阵
        y_pred = self.single_hand_model.predict(X_test)
        cm = confusion_matrix(y_test, y_pred, labels=self.single_hand_model.classes_)
        disp = ConfusionMatrixDisplay(confusion_matrix=cm, display_labels=self.single_hand_model.classes_)
        disp.plot(xticks_rotation=45)
        plt.title("单手手势识别混淆矩阵")
        plt.tight_layout()
        plt.savefig("single_hand_confusion_matrix.png")
        plt.close()
        
        # 保存模型
        with open(self.model_file.replace('.pkl', '_single_hand.pkl'), 'wb') as f:
            pickle.dump(self.single_hand_model, f)
            
        print(f"单手模型已保存")
        return True
        
    def train_two_hands_model(self, X, y):
        """训练双手手势识别模型"""
        if len(X) == 0:
            print("错误：没有找到双手手势训练数据!")
            return False
            
        print(f"训练双手手势模型：{len(X)} 个样本，{len(set(y))} 种不同的手势")
        
        # 划分训练集和测试集
        X_train, X_test, y_train, y_test = train_test_split(
            X, y, test_size=0.2, random_state=42)
        
        # 训练模型
        print("训练双手手势模型中...")
        self.two_hands_model = RandomForestClassifier(n_estimators=100, random_state=42)
        self.two_hands_model.fit(X_train, y_train)
        
        # 评估模型
        score = self.two_hands_model.score(X_test, y_test)
        print(f"双手模型准确率: {score:.2f}")
        
        # 显示混淆矩阵
        y_pred = self.two_hands_model.predict(X_test)
        cm = confusion_matrix(y_test, y_pred, labels=self.two_hands_model.classes_)
        disp = ConfusionMatrixDisplay(confusion_matrix=cm, display_labels=self.two_hands_model.classes_)
        disp.plot(xticks_rotation=45)
        plt.title("双手手势识别混淆矩阵")
        plt.tight_layout()
        plt.savefig("two_hands_confusion_matrix.png")
        plt.close()
        
        # 保存模型
        with open(self.model_file.replace('.pkl', '_two_hands.pkl'), 'wb') as f:
            pickle.dump(self.two_hands_model, f)
            
        print(f"双手模型已保存")
        return True
    
    def train_model(self):
        """训练手势识别模型"""
        print("加载训练数据...")
        return self.load_data()
        
    def load_models(self):
        """加载已训练的模型"""
        try:
            # 加载单手模型
            single_hand_model_file = self.model_file.replace('.pkl', '_single_hand.pkl')
            with open(single_hand_model_file, 'rb') as f:
                self.single_hand_model = pickle.load(f)
            
            # 加载双手模型
            two_hands_model_file = self.model_file.replace('.pkl', '_two_hands.pkl')
            with open(two_hands_model_file, 'rb') as f:
                self.two_hands_model = pickle.load(f)
            
            # 加载手势类型信息
            hand_types_file = self.model_file.replace('.pkl', '_hand_types.json')
            with open(hand_types_file, 'r') as f:
                self.hand_type_dict = json.load(f)
            
            print(f"成功加载模型")
            print(f"单手手势: {self.single_hand_model.classes_}")
            print(f"双手手势: {self.two_hands_model.classes_}")
            return True
        except Exception as e:
            print(f"加载模型失败: {e}")
            return False

if __name__ == "__main__":
    trainer = GestureTrainer()
    trainer.train_model()