import numpy as np
import math

class FeatureExtractor:
    def __init__(self):
        # MediaPipe手部关键点索引定义
        self.WRIST = 0
        self.THUMB_CMC = 1
        self.THUMB_MCP = 2
        self.THUMB_IP = 3
        self.THUMB_TIP = 4
        self.INDEX_MCP = 5
        self.INDEX_PIP = 6
        self.INDEX_DIP = 7
        self.INDEX_TIP = 8
        self.MIDDLE_MCP = 9
        self.MIDDLE_PIP = 10
        self.MIDDLE_DIP = 11
        self.MIDDLE_TIP = 12
        self.RING_MCP = 13
        self.RING_PIP = 14
        self.RING_DIP = 15
        self.RING_TIP = 16
        self.PINKY_MCP = 17
        self.PINKY_PIP = 18
        self.PINKY_DIP = 19
        self.PINKY_TIP = 20
        
        # 指尖索引
        self.FINGERTIPS = [self.THUMB_TIP, self.INDEX_TIP, self.MIDDLE_TIP, self.RING_TIP, self.PINKY_TIP]
        
        # 指根索引
        self.FINGER_MCPS = [self.THUMB_CMC, self.INDEX_MCP, self.MIDDLE_MCP, self.RING_MCP, self.PINKY_MCP]

    def extract_single_hand_features(self, landmarks):
        """提取单手特征
        
        Args:
            landmarks: 包含x,y,z坐标的关键点列表
            
        Returns:
            dict: 包含所有特征的字典
        """
        features = {}
        
        # 1. 中心化处理 - 相对于手腕的坐标
        wrist = landmarks[self.WRIST]
        normalized_landmarks = []
        for lm in landmarks:
            normalized_landmarks.append({
                "x": lm["x"] - wrist["x"],
                "y": lm["y"] - wrist["y"],
                "z": lm["z"] - wrist["z"]
            })
        
        features["normalized_landmarks"] = normalized_landmarks
        
        # 2. 骨架长度向量 - 计算关键距离
        distance_features = []
        
        # 指尖到手腕的距离
        for tip in self.FINGERTIPS:
            distance = self._calculate_distance(normalized_landmarks[tip], {"x": 0, "y": 0, "z": 0})
            distance_features.append(distance)
        
        # 指尖到对应MCP的距离
        for i, tip in enumerate(self.FINGERTIPS):
            mcp = self.FINGER_MCPS[i]
            distance = self._calculate_distance(normalized_landmarks[tip], normalized_landmarks[mcp])
            distance_features.append(distance)
            
        features["distances"] = distance_features
        
        # 3. 夹角特征
        angle_features = []
        
        # 计算指尖-手腕-指尖夹角
        for i in range(len(self.FINGERTIPS)):
            for j in range(i+1, len(self.FINGERTIPS)):
                angle = self._calculate_angle(
                    normalized_landmarks[self.FINGERTIPS[i]], 
                    normalized_landmarks[self.WRIST], 
                    normalized_landmarks[self.FINGERTIPS[j]]
                )
                angle_features.append(angle)
        
        # 计算 MCP-PIP-TIP 关节角度
        finger_joints = [
            [self.INDEX_MCP, self.INDEX_PIP, self.INDEX_TIP],
            [self.MIDDLE_MCP, self.MIDDLE_PIP, self.MIDDLE_TIP],
            [self.RING_MCP, self.RING_PIP, self.RING_TIP],
            [self.PINKY_MCP, self.PINKY_PIP, self.PINKY_TIP]
        ]
        
        for joints in finger_joints:
            angle = self._calculate_angle(
                normalized_landmarks[joints[0]], 
                normalized_landmarks[joints[1]], 
                normalized_landmarks[joints[2]]
            )
            angle_features.append(angle)
            
        features["angles"] = angle_features
        
        # 4. 指尖高度排序特征
        fingertip_heights = []
        for tip in self.FINGERTIPS:
            fingertip_heights.append((tip, normalized_landmarks[tip]["y"]))
        
        # 排序并获取索引顺序
        sorted_heights = sorted(fingertip_heights, key=lambda x: x[1])
        height_order = [x[0] for x in sorted_heights]
        
        # 指尖高度顺序特征
        height_pattern = []
        for i in range(len(height_order) - 1):
            height_pattern.append(height_order[i+1] - height_order[i])
        
        features["height_pattern"] = height_pattern
        
        # 5. 保留归一化的关键点坐标作为基本特征
        flat_coords = []
        for lm in normalized_landmarks:
            flat_coords.extend([lm["x"], lm["y"], lm["z"]])
        
        features["flat_coordinates"] = flat_coords
        
        # 6. 构建最终特征向量（扁平化所有特征）
        final_feature_vector = []
        final_feature_vector.extend(distance_features)
        final_feature_vector.extend(angle_features)
        final_feature_vector.extend(height_pattern)
        final_feature_vector.extend(flat_coords)
        
        features["feature_vector"] = final_feature_vector
        
        return features

    def extract_two_hands_features(self, landmarks1, landmarks2):
        """提取双手特征
        
        Args:
            landmarks1: 第一只手的关键点列表
            landmarks2: 第二只手的关键点列表
            
        Returns:
            dict: 包含所有特征的字典
        """
        # 提取每只手的单手特征
        features1 = self.extract_single_hand_features(landmarks1)
        features2 = self.extract_single_hand_features(landmarks2)
        
        combined_features = {}
        
        # 双手对称性差值特征
        mirror_diff = []
        
        # 计算两手关键点的镜像差异
        for i in range(len(landmarks1)):
            # 创建第二只手的镜像点
            mirror_point = {
                "x": -landmarks2[i]["x"],
                "y": landmarks2[i]["y"],
                "z": landmarks2[i]["z"]
            }
            diff = self._calculate_distance(landmarks1[i], mirror_point)
            mirror_diff.append(diff)
        
        # 计算两手指尖高度差异
        height_diff = []
        for tip in self.FINGERTIPS:
            diff = abs(features1["normalized_landmarks"][tip]["y"] - features2["normalized_landmarks"][tip]["y"])
            height_diff.append(diff)
        
        combined_features["mirror_diff"] = mirror_diff
        combined_features["height_diff"] = height_diff
        
        # 合并单手特征
        combined_features["hand1"] = features1
        combined_features["hand2"] = features2
        
        # 构建最终特征向量
        final_feature_vector = []
        final_feature_vector.extend(features1["feature_vector"])  # 第一只手特征
        final_feature_vector.extend(features2["feature_vector"])  # 第二只手特征
        final_feature_vector.extend(mirror_diff)                  # 镜像差异
        final_feature_vector.extend(height_diff)                  # 高度差异
        
        combined_features["feature_vector"] = final_feature_vector
        
        return combined_features
    
    def _calculate_distance(self, p1, p2):
        """计算两点间的欧氏距离"""
        return math.sqrt((p1["x"] - p2["x"])**2 + (p1["y"] - p2["y"])**2 + (p1["z"] - p2["z"])**2)
    
    def _calculate_angle(self, p1, p2, p3):
        """计算三点间的角度，p2为顶点"""
        vector1 = [p1["x"] - p2["x"], p1["y"] - p2["y"], p1["z"] - p2["z"]]
        vector2 = [p3["x"] - p2["x"], p3["y"] - p2["y"], p3["z"] - p2["z"]]
        
        # 计算向量长度
        len1 = math.sqrt(sum([x**2 for x in vector1]))
        len2 = math.sqrt(sum([x**2 for x in vector2]))
        
        if len1 == 0 or len2 == 0:
            return 0
        
        # 计算点积
        dot_product = sum([vector1[i] * vector2[i] for i in range(3)])
        
        # 计算角度（弧度）
        cosine = dot_product / (len1 * len2)
        # 确保值在有效范围内
        cosine = max(min(cosine, 1.0), -1.0)
        angle = math.acos(cosine)
        
        return angle  # 弧度值