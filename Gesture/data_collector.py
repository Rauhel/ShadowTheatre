import cv2
import mediapipe as mp
import numpy as np
import os
import time
import json
import datetime
import sys

# 导入特征提取器
from feature_extractor import FeatureExtractor

class GestureDataCollector:
    def __init__(self, base_dir="gesture_data"):
        self.mp_hands = mp.solutions.hands
        self.mp_drawing = mp.solutions.drawing_utils
        self.base_dir = base_dir
        self.feature_extractor = FeatureExtractor()
        
        # Create base data directory
        os.makedirs(base_dir, exist_ok=True)
        
    def collect_gesture_data(self, gesture_name, is_two_hands=False, samples_count=100):
        """
        Collect feature data for specified gesture
        
        Parameters:
            gesture_name: name of the gesture
            is_two_hands: whether it's a two-handed gesture
            samples_count: number of samples to collect
        """
        # Add two-hands mark to gesture name
        folder_name = gesture_name
        if is_two_hands:
            folder_name = f"{gesture_name}_TwoHands"
        
        # Create specific folder for each gesture
        gesture_dir = os.path.join(self.base_dir, folder_name)
        os.makedirs(gesture_dir, exist_ok=True)
        
        # Generate unique timestamp for this collection
        timestamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
        session_file = os.path.join(gesture_dir, f"session_{timestamp}.json")
        
        print(f"Ready to collect {gesture_name} {'(two hands)' if is_two_hands else '(one hand)'} gesture data, {samples_count} samples needed")
        print(f"Data will be saved to: {session_file}")
        print("Please place your hands in front of the camera, press SPACE to start or ESC to exit")
        
        cap = cv2.VideoCapture(0)
        
        with self.mp_hands.Hands(
            static_image_mode=False,
            max_num_hands=2,
            min_detection_confidence=0.5) as hands:
            
            # Wait for user to get ready
            while True:
                success, image = cap.read()
                cv2.putText(image, f"Ready to collect: {gesture_name} {'(two hands)' if is_two_hands else '(one hand)'}", (10, 30), 
                           cv2.FONT_HERSHEY_SIMPLEX, 1, (0, 255, 0), 2)
                cv2.putText(image, "Press SPACE to start, ESC to exit", (10, 70), 
                           cv2.FONT_HERSHEY_SIMPLEX, 1, (0, 255, 0), 2)
                cv2.imshow("Gesture Data Collection", image)
                
                key = cv2.waitKey(1)
                if key == 32:  # SPACE key
                    break
                elif key == 27:  # ESC key
                    cap.release()
                    cv2.destroyAllWindows()
                    return False  # Return False to indicate user wants to exit
            
            # Start data collection
            collected_samples = 0
            samples = []
            
            while collected_samples < samples_count:
                success, image = cap.read()
                if not success:
                    continue
                
                # Process image
                image_rgb = cv2.cvtColor(image, cv2.COLOR_BGR2RGB)
                results = hands.process(image_rgb)
                
                # Display real-time progress
                cv2.putText(image, f"Collecting: {gesture_name} {'(two hands)' if is_two_hands else '(one hand)'}", (10, 30), 
                           cv2.FONT_HERSHEY_SIMPLEX, 1, (0, 255, 0), 2)
                cv2.putText(image, f"Samples: {collected_samples}/{samples_count}", (10, 70), 
                           cv2.FONT_HERSHEY_SIMPLEX, 1, (0, 255, 0), 2)
                
                # Number of detected hands
                hand_count = 0 if results.multi_hand_landmarks is None else len(results.multi_hand_landmarks)
                
                # For two-handed gestures, need to detect 2 hands
                # For one-handed gestures, need to detect at least 1 hand
                valid_sample = False
                
                if is_two_hands and hand_count == 2:
                    valid_sample = True
                    # Collect keypoints for both hands
                    hand_data = []
                    
                    for hand_landmarks in results.multi_hand_landmarks:
                        # Draw hand landmarks
                        self.mp_drawing.draw_landmarks(
                            image, hand_landmarks, self.mp_hands.HAND_CONNECTIONS)
                        
                        # Extract features for this hand
                        landmarks_list = []
                        for landmark in hand_landmarks.landmark:
                            landmarks_list.append({
                                "x": landmark.x,
                                "y": landmark.y,
                                "z": landmark.z
                            })
                        hand_data.append(landmarks_list)
                    
                    # Extract optimized features for two hands
                    enhanced_features = self.feature_extractor.extract_two_hands_features(hand_data[0], hand_data[1])
                    
                    # Add to sample set with both original landmarks and enhanced features
                    samples.append({
                        "hand1": hand_data[0], 
                        "hand2": hand_data[1],
                        "is_two_hands": True,
                        "enhanced_features": {
                            "distances": enhanced_features["hand1"]["distances"] + enhanced_features["hand2"]["distances"],
                            "angles": enhanced_features["hand1"]["angles"] + enhanced_features["hand2"]["angles"],
                            "height_pattern": enhanced_features["hand1"]["height_pattern"] + enhanced_features["hand2"]["height_pattern"],
                            "mirror_diff": enhanced_features["mirror_diff"],
                            "height_diff": enhanced_features["height_diff"],
                            "feature_vector": enhanced_features["feature_vector"]
                        }
                    })
                    collected_samples += 1
                    
                elif not is_two_hands and hand_count >= 1:
                    valid_sample = True
                    # Only collect keypoints for the first hand
                    hand_landmarks = results.multi_hand_landmarks[0]
                    
                    # Draw hand landmarks
                    self.mp_drawing.draw_landmarks(
                        image, hand_landmarks, self.mp_hands.HAND_CONNECTIONS)
                    
                    # Extract features
                    landmarks_list = []
                    for landmark in hand_landmarks.landmark:
                        landmarks_list.append({
                            "x": landmark.x,
                            "y": landmark.y,
                            "z": landmark.z
                        })
                    
                    # Extract optimized features
                    enhanced_features = self.feature_extractor.extract_single_hand_features(landmarks_list)
                    
                    # Add to sample set with both original landmarks and enhanced features
                    samples.append({
                        "hand1": landmarks_list,
                        "hand2": None,
                        "is_two_hands": False,
                        "enhanced_features": {
                            "distances": enhanced_features["distances"],
                            "angles": enhanced_features["angles"],
                            "height_pattern": enhanced_features["height_pattern"],
                            "feature_vector": enhanced_features["feature_vector"]
                        }
                    })
                    collected_samples += 1
                
                if valid_sample:
                    # Pause slightly after collecting a sample to prevent consecutive frames being too similar
                    time.sleep(0.1)
                
                cv2.imshow("Gesture Data Collection", image)
                if cv2.waitKey(5) & 0xFF == 27:  # ESC key to exit
                    cap.release()
                    cv2.destroyAllWindows()
                    return False  # Return False to indicate user wants to exit
            
            # Save data
            if samples:
                with open(session_file, 'w') as f:
                    json.dump({
                        "gesture": gesture_name,
                        "is_two_hands": is_two_hands,
                        "samples": samples,
                        "version": "2.0"  # 添加版本标记，表示使用了增强特征
                    }, f)
                
                print(f"Successfully collected and saved {len(samples)} samples for {gesture_name} {'(two hands)' if is_two_hands else '(one hand)'} gesture")
                
                # Update total sample count for this gesture
                total_samples = self.count_gesture_samples(gesture_dir)
                print(f"{folder_name} gesture now has {total_samples} samples in total")
            
        cap.release()
        cv2.destroyAllWindows()
        return True  # Return True to indicate successful completion
    
    def count_gesture_samples(self, gesture_dir):
        """Count total samples in a gesture directory"""
        total_samples = 0
        for filename in os.listdir(gesture_dir):
            if filename.endswith('.json'):
                try:
                    with open(os.path.join(gesture_dir, filename), 'r') as f:
                        data = json.load(f)
                        total_samples += len(data['samples'])
                except Exception as e:
                    print(f"Error reading file {filename}: {e}")
        return total_samples

if __name__ == "__main__":
    collector = GestureDataCollector()
    
    # Define gestures to collect - (gesture name, is two hands)
    gestures = [
        ("Bird", True),  # Two hands gesture
        ("Owl", True),  
        ("Wolf", False),   # One hand gesture
        ("Frog", True), 
        ("Goose", True),
        ("Fist", False),
    ]
    
    for gesture_name, is_two_hands in gestures:
        print(f"\n-------- Next gesture: {gesture_name} {'(two hands)' if is_two_hands else '(one hand)'} --------")
        continue_program = collector.collect_gesture_data(gesture_name, is_two_hands, samples_count=100)
        if not continue_program:
            print("Program terminated by user")
            break
            
        print(f"{gesture_name} {'(two hands)' if is_two_hands else '(one hand)'} gesture data collection completed!")
        print("Press any key to continue to next gesture, or press ESC to exit...")
        
        if cv2.waitKey(0) == 27:  # Check if ESC was pressed
            print("Program terminated by user")
            break