import numpy as np
from feature_extractor import FeatureExtractor

class TwoHandsRecognizer:
    """双手手势识别器"""
    
    def __init__(self, model=None):
        self.model = model
        self.feature_extractor = FeatureExtractor()
        
    def recognize(self, landmarks1, landmarks2):
        """识别双手手势
        
        Args:
            landmarks1: 第一只手的关键点坐标字典列表
            landmarks2: 第二只手的关键点坐标字典列表
            
        Returns:
            预测的手势名称
        """
        # 提取增强特征
        features = self.feature_extractor.extract_two_hands_features(landmarks1, landmarks2)
        feature_vector = features["feature_vector"]
        
        # 返回预测结果
        try:
            # 确保特征向量是正确的形状
            feature_array = np.array([feature_vector])
            gesture = self.model.predict(feature_array)[0]
            return gesture
        except Exception as e:
            print(f"双手手势识别错误: {e}")
            return "Unknown"