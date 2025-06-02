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
        # 检查模型是否已加载
        if self.model is None:
            print("单手手势识别错误: 模型未加载")
            return "Unknown"
        
        # 提取增强特征
        try:
            features = self.feature_extractor.extract_single_hand_features(landmarks)
            feature_vector = features["feature_vector"]
        except Exception as e:
            print(f"单手特征提取错误: {e}")
            return "Unknown"
        
        # 返回预测结果
        try:
            # 确保特征向量是正确的形状
            feature_array = np.array([feature_vector])
            gesture = self.model.predict(feature_array)[0]
            print(f"单手识别成功: {gesture}")
            return gesture
        except Exception as e:
            print(f"单手手势识别错误: {e}")
            print(f"特征向量形状: {len(feature_vector) if feature_vector else 'None'}")
            print(f"模型类别: {self.model.classes_ if hasattr(self.model, 'classes_') else 'Unknown'}")
            return "Unknown"