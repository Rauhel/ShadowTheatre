import numpy as np
from feature_extractor import FeatureExtractor

class SingleHandRecognizer:
    """单手手势识别器"""
    
    def __init__(self, model=None):
        self.model = model
        self.feature_extractor = FeatureExtractor()
        
    def recognize(self, landmarks):
        """识别单手手势
        
        Args:
            landmarks: 包含手部关键点坐标的字典列表
            
        Returns:
            预测的手势名称
        """
        # 提取增强特征
        features = self.feature_extractor.extract_single_hand_features(landmarks)
        feature_vector = features["feature_vector"]
        
        # 返回预测结果
        try:
            # 确保特征向量是正确的形状
            feature_array = np.array([feature_vector])
            gesture = self.model.predict(feature_array)[0]
            return gesture
        except Exception as e:
            print(f"单手手势识别错误: {e}")
            return "Unknown"