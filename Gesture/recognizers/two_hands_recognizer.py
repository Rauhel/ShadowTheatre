class TwoHandsRecognizer:
    """双手手势识别器"""
    
    def __init__(self, model=None):
        self.model = model
    
    def recognize(self, landmarks1, landmarks2):
        """双手手势识别"""
        if self.model is None:
            print("错误: 未加载双手模型")
            return "Unknown"  # 没有双手模型
        
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
        
        # 预测手势
        try:
            # 获取概率分布
            probabilities = self.model.predict_proba([features])[0]
            
            # 输出所有类别的概率
            for i, gesture_class in enumerate(self.model.classes_):
                print(f"  {gesture_class}: {probabilities[i]:.4f}")
            
            gesture = self.model.predict([features])[0]
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