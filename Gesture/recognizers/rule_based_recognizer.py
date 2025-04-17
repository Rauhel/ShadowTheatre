class RuleBasedRecognizer:
    """基于规则的手势识别器"""
    
    def recognize(self, landmarks):
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