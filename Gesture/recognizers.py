import numpy as np
from feature_extractor import FeatureExtractor

class BaseRecognizer:
    def __init__(self, model=None):
        self.model = model
        self.feature_extractor = FeatureExtractor()
        
    def predict_proba(self, features):
        """预测手势概率分布"""
        if self.model is None:
            raise ValueError("模型未加载，请先加载模型")
            
        # 确保特征是正确的形状
        features_array = np.array([features])
        
        # 预测概率
        return self.model.predict_proba(features_array)[0]
        
    def predict(self, features):
        """预测手势类别"""
        if self.model is None:
            raise ValueError("模型未加载，请先加载模型")
            
        # 确保特征是正确的形状
        features_array = np.array([features])
        
        # 预测类别
        return self.model.predict(features_array)[0]

class SingleHandRecognizer(BaseRecognizer):
    def recognize(self, landmarks):
        """识别单手手势
        
        Args:
            landmarks: 包含手部关键点坐标的列表或数组
            
        Returns:
            预测的手势名称
        """
        # 提取增强特征
        features = self.feature_extractor.extract_single_hand_features(landmarks)
        feature_vector = features["feature_vector"]
        
        # 返回预测结果
        try:
            gesture = self.predict(feature_vector)
            return gesture
        except Exception as e:
            print(f"单手手势识别错误: {e}")
            return "Unknown"

class TwoHandsRecognizer(BaseRecognizer):
    def recognize(self, landmarks1, landmarks2):
        """识别双手手势
        
        Args:
            landmarks1: 第一只手的关键点坐标列表
            landmarks2: 第二只手的关键点坐标列表
            
        Returns:
            预测的手势名称
        """
        # 提取增强特征
        features = self.feature_extractor.extract_two_hands_features(landmarks1, landmarks2)
        feature_vector = features["feature_vector"]
        
        # 返回预测结果
        try:
            gesture = self.predict(feature_vector)
            return gesture
        except Exception as e:
            print(f"双手手势识别错误: {e}")
            return "Unknown"