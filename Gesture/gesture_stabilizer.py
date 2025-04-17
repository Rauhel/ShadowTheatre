import time

class GestureStabilizer:
    """手势稳定器，用于平滑手势识别结果"""
    
    def __init__(self, time_window=1.0, threshold=0.9):
        """
        初始化手势稳定器
        
        参数:
            time_window: 时间窗口大小(秒)
            threshold: 判定为稳定手势的阈值百分比(0-1)
        """
        self.time_window = time_window    # 时间窗口大小(秒)
        self.threshold = threshold        # 阈值
        self.gesture_history = []         # 历史记录 [(timestamp, gesture), ...]
        self.current_stable_gesture = "Unknown"  # 当前稳定的手势
    
    def add_gesture(self, gesture):
        """
        添加一个新识别的手势
        
        参数:
            gesture: 识别到的手势类型字符串
        
        返回:
            当前稳定的手势类型
        """
        current_time = time.time()
        
        # 添加新手势到历史
        self.gesture_history.append((current_time, gesture))
        
        # 移除超出时间窗口的历史记录
        cutoff_time = current_time - self.time_window
        self.gesture_history = [(t, g) for t, g in self.gesture_history if t >= cutoff_time]
        
        # 统计各手势出现次数
        total_count = len(self.gesture_history)
        if total_count == 0:
            return self.current_stable_gesture
            
        gesture_counts = {}
        for _, g in self.gesture_history:
            if g not in gesture_counts:
                gesture_counts[g] = 0
            gesture_counts[g] += 1
        
        # 检查是否有手势超过阈值
        for g, count in gesture_counts.items():
            if count / total_count >= self.threshold:
                # 如果检测到新的稳定手势
                if g != self.current_stable_gesture:
                    print(f"手势稳定为: {g} ({count}/{total_count})")
                    self.current_stable_gesture = g
                return self.current_stable_gesture
        
        # 如果没有手势超过阈值，返回当前稳定的手势
        return self.current_stable_gesture