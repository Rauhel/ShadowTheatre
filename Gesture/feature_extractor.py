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
        
        # 新增特征 - 针对"狼"和"拳头"的区分
        
        # 6. 食指和中指V型特征 - "狼"手势的典型特征
        # 计算食指-中指间的角度
        index_middle_angle = self._calculate_angle(
            normalized_landmarks[self.INDEX_TIP], 
            normalized_landmarks[self.INDEX_MCP], 
            normalized_landmarks[self.MIDDLE_TIP]
        )
        
        # 7. 拳头闭合度 - 指尖到手掌中心的平均距离
        # 计算手掌中心点 (使用所有MCP的平均位置)
        palm_center = {
            "x": sum(normalized_landmarks[mcp]["x"] for mcp in self.FINGER_MCPS) / len(self.FINGER_MCPS),
            "y": sum(normalized_landmarks[mcp]["y"] for mcp in self.FINGER_MCPS) / len(self.FINGER_MCPS),
            "z": sum(normalized_landmarks[mcp]["z"] for mcp in self.FINGER_MCPS) / len(self.FINGER_MCPS)
        }
        
        # 计算所有指尖到手掌中心的距离
        fingertips_to_palm = []
        for tip in self.FINGERTIPS:
            dist = self._calculate_distance(normalized_landmarks[tip], palm_center)
            fingertips_to_palm.append(dist)
        
        # 计算闭合度 (平均距离)
        fist_closeness = sum(fingertips_to_palm) / len(fingertips_to_palm)
        
        # 8. 食指和中指特征 - "狼"手势中食指和中指通常是伸出的
        # 计算食指和中指指尖到其MCP的相对长度
        index_extension = self._calculate_distance(
            normalized_landmarks[self.INDEX_TIP], 
            normalized_landmarks[self.INDEX_MCP]
        )
        middle_extension = self._calculate_distance(
            normalized_landmarks[self.MIDDLE_TIP], 
            normalized_landmarks[self.MIDDLE_MCP]
        )
        
        # 计算其他三个手指的平均伸展度
        other_fingers_extension = (
            self._calculate_distance(normalized_landmarks[self.THUMB_TIP], normalized_landmarks[self.THUMB_CMC]) +
            self._calculate_distance(normalized_landmarks[self.RING_TIP], normalized_landmarks[self.RING_MCP]) +
            self._calculate_distance(normalized_landmarks[self.PINKY_TIP], normalized_landmarks[self.PINKY_MCP])
        ) / 3
        
        # 计算食指和中指伸展度与其他手指伸展度的比率
        index_middle_vs_others = (index_extension + middle_extension) / (other_fingers_extension * 2 + 1e-6)
        
        # 9. 食指-中指高度差异 - "狼"手势中食指和中指可能高度相似
        index_middle_height_diff = abs(normalized_landmarks[self.INDEX_TIP]["y"] - normalized_landmarks[self.MIDDLE_TIP]["y"])
        
        # 10. 拇指特征 - 拇指与食指的夹角和距离
        thumb_index_angle = self._calculate_angle(
            normalized_landmarks[self.THUMB_TIP],
            normalized_landmarks[self.WRIST],
            normalized_landmarks[self.INDEX_TIP]
        )
        
        thumb_index_dist = self._calculate_distance(
            normalized_landmarks[self.THUMB_TIP],
            normalized_landmarks[self.INDEX_TIP]
        )
        
        # 存储新增的区分特征
        wolf_fist_features = [
            index_middle_angle,           # 食指-中指V型角度
            fist_closeness,               # 拳头闭合度
            index_extension,              # 食指伸展度
            middle_extension,             # 中指伸展度
            index_middle_vs_others,       # 食指中指与其他手指伸展对比
            index_middle_height_diff,     # 食指中指高度差异
            thumb_index_angle,            # 拇指-食指夹角
            thumb_index_dist              # 拇指-食指距离
        ]
        
        features["wolf_fist_features"] = wolf_fist_features
        
        # 11. 指尖闭合度分布 - 用于进一步区分手势形状
        # 计算各指尖间距离
        fingertip_distances = []
        for i in range(len(self.FINGERTIPS)):
            for j in range(i+1, len(self.FINGERTIPS)):
                dist = self._calculate_distance(
                    normalized_landmarks[self.FINGERTIPS[i]],
                    normalized_landmarks[self.FINGERTIPS[j]]
                )
                fingertip_distances.append(dist)
        
        # 计算指尖距离的标准差 - 低表示拳头(距离相近)，高表示张开(距离各异)
        if len(fingertip_distances) > 0:
            fingertip_distance_std = np.std(fingertip_distances)
        else:
            fingertip_distance_std = 0
        
        features["fingertip_distance_std"] = fingertip_distance_std
        
        # 构建最终特征向量（扁平化所有特征）
        final_feature_vector = []
        final_feature_vector.extend(distance_features)       # 骨架长度特征
        final_feature_vector.extend(angle_features)          # 角度特征
        final_feature_vector.extend(height_pattern)          # 指尖高度排序
        final_feature_vector.extend(wolf_fist_features)      # 狼vs拳头区分特征
        final_feature_vector.append(fingertip_distance_std)  # 指尖闭合度分布
        final_feature_vector.extend(flat_coords)             # 归一化坐标
        
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
        
        # 计算镜像对称性得分 - 越低越对称，鸟手势通常更对称
        symmetry_score = sum(mirror_diff) / len(mirror_diff)
        
        # 计算两手指尖高度差异
        height_diff = []
        for tip in self.FINGERTIPS:
            diff = abs(features1["normalized_landmarks"][tip]["y"] - features2["normalized_landmarks"][tip]["y"])
            height_diff.append(diff)
        
        # 新增特征1：手腕间距离 - 区分"蛙"(大)和"猫头鹰"/"鸟"(小)
        wrist_dist = self._calculate_distance(landmarks1[self.WRIST], landmarks2[self.WRIST])
        
        # 新增特征2：掌心中心点距离 - 区分"鸟"和"猫头鹰"
        # 计算两个手的掌心中心点
        palm_center1 = {
            "x": (landmarks1[self.INDEX_MCP]["x"] + landmarks1[self.PINKY_MCP]["x"]) / 2,
            "y": (landmarks1[self.INDEX_MCP]["y"] + landmarks1[self.PINKY_MCP]["y"]) / 2,
            "z": (landmarks1[self.INDEX_MCP]["z"] + landmarks1[self.PINKY_MCP]["z"]) / 2
        }
        
        palm_center2 = {
            "x": (landmarks2[self.INDEX_MCP]["x"] + landmarks2[self.PINKY_MCP]["x"]) / 2,
            "y": (landmarks2[self.INDEX_MCP]["y"] + landmarks2[self.PINKY_MCP]["y"]) / 2,
            "z": (landmarks2[self.INDEX_MCP]["z"] + landmarks2[self.PINKY_MCP]["z"]) / 2
        }
        
        palm_dist = self._calculate_distance(palm_center1, palm_center2)
        
        # 新增特征3：指尖间最短距离 - 检测手指是否交错(猫头鹰)或分开(其他)
        min_tip_dist = float('inf')
        for tip1 in self.FINGERTIPS:
            for tip2 in self.FINGERTIPS:
                dist = self._calculate_distance(landmarks1[tip1], landmarks2[tip2])
                min_tip_dist = min(min_tip_dist, dist)
        
        # 新增特征4：手指交错模式 - 计算所有指尖对的距离矩阵
        tip_dist_matrix = []
        for tip1 in self.FINGERTIPS:
            for tip2 in self.FINGERTIPS:
                tip_dist_matrix.append(self._calculate_distance(landmarks1[tip1], landmarks2[tip2]))
        
        # 新增特征5：双手左右位置差异 - 特别对"蛙"手势敏感
        x_position_diff = abs(landmarks1[self.WRIST]["x"] - landmarks2[self.WRIST]["x"])
        y_position_diff = abs(landmarks1[self.WRIST]["y"] - landmarks2[self.WRIST]["y"])
        
        # 新增特征6：手掌方向特征 - 两个手掌面朝向的差异
        def get_palm_normal(hand_landmarks):
            # 使用INDEX_MCP, MIDDLE_MCP, RING_MCP三点定义手掌平面
            v1 = [
                hand_landmarks[self.MIDDLE_MCP]["x"] - hand_landmarks[self.INDEX_MCP]["x"],
                hand_landmarks[self.MIDDLE_MCP]["y"] - hand_landmarks[self.INDEX_MCP]["y"],
                hand_landmarks[self.MIDDLE_MCP]["z"] - hand_landmarks[self.INDEX_MCP]["z"]
            ]
            
            v2 = [
                hand_landmarks[self.RING_MCP]["x"] - hand_landmarks[self.INDEX_MCP]["x"],
                hand_landmarks[self.RING_MCP]["y"] - hand_landmarks[self.INDEX_MCP]["y"],
                hand_landmarks[self.RING_MCP]["z"] - hand_landmarks[self.INDEX_MCP]["z"]
            ]
            
            # 计算法向量（叉积）
            normal = [
                v1[1]*v2[2] - v1[2]*v2[1],
                v1[2]*v2[0] - v1[0]*v2[2],
                v1[0]*v2[1] - v1[1]*v2[0]
            ]
            
            # 归一化
            length = math.sqrt(sum([x*x for x in normal]))
            if length > 0:
                return [x/length for x in normal]
            return normal
        
        normal1 = get_palm_normal(landmarks1)
        normal2 = get_palm_normal(landmarks2)
        
        # 计算两个法向量的点积，判断手掌朝向相似度
        dot_product = sum([normal1[i] * normal2[i] for i in range(3)])
        palm_orientation_similarity = abs(dot_product)  # 接近1表示方向相似或相反
        
        # 存储所有双手特征
        combined_features["mirror_diff"] = mirror_diff
        combined_features["height_diff"] = height_diff
        combined_features["symmetry_score"] = symmetry_score
        combined_features["wrist_dist"] = wrist_dist
        combined_features["palm_dist"] = palm_dist
        combined_features["min_tip_dist"] = min_tip_dist
        combined_features["tip_dist_matrix"] = tip_dist_matrix
        combined_features["x_position_diff"] = x_position_diff
        combined_features["y_position_diff"] = y_position_diff
        combined_features["palm_orientation_similarity"] = palm_orientation_similarity
        
        # 合并单手特征
        combined_features["hand1"] = features1
        combined_features["hand2"] = features2
        
        # 构建最终特征向量
        final_feature_vector = []
        # 单手特征
        final_feature_vector.extend(features1["feature_vector"])  # 第一只手特征
        final_feature_vector.extend(features2["feature_vector"])  # 第二只手特征
        
        # 双手空间关系特征
        final_feature_vector.extend(mirror_diff)                  # 镜像差异
        final_feature_vector.extend(height_diff)                  # 高度差异
        final_feature_vector.append(symmetry_score)               # 整体对称得分
        final_feature_vector.append(wrist_dist)                   # 手腕距离
        final_feature_vector.append(palm_dist)                    # 掌心距离
        final_feature_vector.append(min_tip_dist)                 # 最小指尖距离
        final_feature_vector.extend(tip_dist_matrix)              # 指尖距离矩阵
        final_feature_vector.append(x_position_diff)              # 水平位置差异
        final_feature_vector.append(y_position_diff)              # 垂直位置差异
        final_feature_vector.append(palm_orientation_similarity)  # 掌心朝向相似度
        
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