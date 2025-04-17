class SingleHandRecognizer:
    """单手手势识别器"""
    
    def __init__(self, model=None):
        self.model = model
    
    def recognize(self, landmarks):
        """单手手势识别"""
        if self.model is None:
            from .rule_based_recognizer import RuleBasedRecognizer
            # 使用规则识别作为后备
            return RuleBasedRecognizer().recognize(landmarks)
        
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
            gesture = self.model.predict([features])[0]
            confidence = max(self.model.predict_proba([features])[0])
            
            # 如果置信度较低，返回Unknown
            if confidence < 0.6:
                print(f"单手手势置信度过低: {gesture} ({confidence:.2f})")
                return "Unknown"
            
            print(f"识别到单手手势: {gesture} (置信度: {confidence:.2f})")
            return gesture
        except Exception as e:
            print(f"单手识别错误: {e}")
            return "Unknown"