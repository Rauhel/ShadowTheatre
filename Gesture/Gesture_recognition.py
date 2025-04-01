import cv2
import mediapipe as mp
import numpy as np

# 初始化MediaPipe手部解决方案
mp_hands = mp.solutions.hands
mp_drawing = mp.solutions.drawing_utils

# 暂定使用识别出的第五点信息作为手部位置信息，并以此位置信息作为player的位置信息。
# 之后根据手势控制结果对该算法进行优化

def hand_position(video_path):
    # 打开视频文件
    cap = cv2.VideoCapture(video_path)
    frame_count = 0
    hand_positions = []  # 用于存储每帧的第五个关键点信息

    # 设置MediaPipe参数 - 增加max_num_hands为2确保检测双手
    with mp_hands.Hands(
            static_image_mode=False,
            max_num_hands=2,
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

            if results.multi_hand_landmarks:
                for hand_idx, hand_landmarks in enumerate(results.multi_hand_landmarks):
                    # 获取第五个关键点的信息
                    landmark_5 = hand_landmarks.landmark[4]  # 索引4代表第五个关键点
                    hand_position_data = {
                        "frame": frame_count,
                        "x": float(landmark_5.x),
                        "y": float(landmark_5.y),
                        "z": float(landmark_5.z)
                    }
                    hand_positions.append(hand_position_data)

                    # 可视化关键点（用于调试）
                    mp_drawing.draw_landmarks(
                        image, hand_landmarks, mp_hands.HAND_CONNECTIONS,
                        mp_drawing.DrawingSpec(color=(0, 255, 0), thickness=2, circle_radius=4),
                        mp_drawing.DrawingSpec(color=(0, 255, 0), thickness=2))

            # 显示结果（可选）
            cv2.imshow('MediaPipe Hands', image)
            if cv2.waitKey(5) & 0xFF == 27:  # ESC键退出
                break

            frame_count += 1

    cap.release()
    cv2.destroyAllWindows()

    return hand_positions

if __name__ == "__main__":
    video_path = "VID_20250327_204737.mp4"  # 替换为你的视频路径
    positions = hand_position(video_path)
    for pos in positions:
        print(pos)