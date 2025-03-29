import cv2
import mediapipe as mp
import json
import numpy as np
import os

# 初始化MediaPipe手部解决方案
mp_hands = mp.solutions.hands
mp_drawing = mp.solutions.drawing_utils

def process_video(video_path, output_json_path):
    # 保存所有帧的手部关键点
    frames_data = []
    
    # 打开视频文件
    cap = cv2.VideoCapture(video_path)
    frame_count = 0
    
    # 设置MediaPipe参数 - 增加max_num_hands为2确保检测双手
    with mp_hands.Hands(
        static_image_mode=False,
        max_num_hands=2,  # 确保设置为2以捕获双手
        min_detection_confidence=0.5,
        min_tracking_confidence=0.5) as hands:
        
        while cap.isOpened():
            success, image = cap.read()
            if not success:
                break
                
            # 将BGR图像转换为RGB
            image_rgb = cv2.cvtColor(image, cv2.COLOR_BGR2RGB)
            
            # 处理图像
            results = hands.process(image_rgb)
            
            # 存储当前帧数据
            frame_data = {"frame": frame_count, "hands": []}
            
            # 检查是否检测到手
            if results.multi_hand_landmarks:
                for hand_idx, hand_landmarks in enumerate(results.multi_hand_landmarks):
                    # 获取手的类型（左/右）
                    handedness = results.multi_handedness[hand_idx].classification[0].label
                    confidence = results.multi_handedness[hand_idx].classification[0].score
                    
                    # 存储手部数据，增加置信度信息
                    hand_data = {
                        "handedness": handedness,
                        "confidence": float(confidence),  # 添加置信度
                        "landmarks": []
                    }
                    
                    # 存储所有21个关键点
                    for landmark_idx, landmark in enumerate(hand_landmarks.landmark):
                        hand_data["landmarks"].append({
                            "id": landmark_idx,
                            "x": landmark.x,
                            "y": landmark.y,
                            "z": landmark.z,
                            # 可选：添加可见性或置信度
                            "visibility": 1.0  # MediaPipe手部模型不提供可见性，这里添加占位符
                        })
                    
                    frame_data["hands"].append(hand_data)
                    
                    # 可视化关键点（用于调试）- 为左右手使用不同颜色
                    color = (0, 255, 0) if handedness == "Left" else (0, 0, 255)
                    mp_drawing.draw_landmarks(
                        image, hand_landmarks, mp_hands.HAND_CONNECTIONS,
                        mp_drawing.DrawingSpec(color=color, thickness=2, circle_radius=4),
                        mp_drawing.DrawingSpec(color=color, thickness=2))
            
            frames_data.append(frame_data)
            
            # 显示结果（可选）- 添加帧编号和检测到的手数
            hand_count = len(frame_data["hands"])
            status_text = f"Frame: {frame_count}, Hands: {hand_count}"
            cv2.putText(image, status_text, (10, 30), cv2.FONT_HERSHEY_SIMPLEX, 1, (0, 255, 255), 2)
            
            cv2.imshow('MediaPipe Hands', image)
            if cv2.waitKey(5) & 0xFF == 27:  # ESC键退出
                break
                
            frame_count += 1
    
    cap.release()
    cv2.destroyAllWindows()
    
    # 将数据保存为JSON文件
    with open(output_json_path, 'w') as f:
        json.dump({"frames": frames_data}, f, indent=2)
    
    print(f"处理完成！共 {frame_count} 帧。数据已保存到 {output_json_path}")

if __name__ == "__main__":
    video_path = "VID_20250327_204737.mp4"  # 替换为你的视频路径
    output_json_path = "hand_tracking_data.json"
    process_video(video_path, output_json_path)