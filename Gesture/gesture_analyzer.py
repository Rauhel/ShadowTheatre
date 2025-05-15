import os
import json
import numpy as np
import matplotlib.pyplot as plt
import pickle
from sklearn.decomposition import PCA
from sklearn.manifold import TSNE
import seaborn as sns
from mpl_toolkits.mplot3d import Axes3D
import pandas as pd
from feature_extractor import FeatureExtractor

class GestureAnalyzer:
    def __init__(self, data_dir="gesture_data", models_dir="."):
        self.data_dir = data_dir
        self.models_dir = models_dir
        self.gesture_data = {}  # Will store all loaded samples for each gesture
        self.feature_stats = {}  # Will store statistical analysis of features
        self.single_hand_model = None
        self.two_hands_model = None
        self.hand_type_dict = {}
        self.feature_extractor = FeatureExtractor()
        
        # Create output directories for organized results
        self.output_dir = os.path.join("analysis_results")
        self._create_output_directories()
        
    def _create_output_directories(self):
        """Create directory structure for analysis results"""
        # Main directories
        dirs = [
            self.output_dir,
            os.path.join(self.output_dir, "model_features"),
            os.path.join(self.output_dir, "gesture_clusters"),
            os.path.join(self.output_dir, "specific_features"),
            os.path.join(self.output_dir, "specific_features", "symmetry"),
            os.path.join(self.output_dir, "specific_features", "finger_features"),
            os.path.join(self.output_dir, "feature_comparison"),
            os.path.join(self.output_dir, "feature_comparison", "single_hand"),
            os.path.join(self.output_dir, "feature_comparison", "two_hands"),
            os.path.join(self.output_dir, "radar_charts"),
            os.path.join(self.output_dir, "radar_charts", "single_hand"),
            os.path.join(self.output_dir, "radar_charts", "two_hands"),
        ]
        
        # Create all directories
        for directory in dirs:
            os.makedirs(directory, exist_ok=True)
        
        print(f"Analysis results will be saved to: {self.output_dir}")
        
    def load_gesture_data(self):
        """Load all gesture data from saved JSON files"""
        print("Loading gesture data...")
        
        # Get all gesture folders
        gesture_folders = [d for d in os.listdir(self.data_dir) 
                         if os.path.isdir(os.path.join(self.data_dir, d))]
        
        if not gesture_folders:
            print(f"Error: No gesture folders found in {self.data_dir}")
            return False
        
        print(f"Found gesture types: {gesture_folders}")
        
        # Process each gesture folder
        for folder_name in gesture_folders:
            gesture_dir = os.path.join(self.data_dir, folder_name)
            json_files = [f for f in os.listdir(gesture_dir) if f.endswith('.json')]
            
            if not json_files:
                print(f"Warning: No data files found in {folder_name} folder")
                continue
            
            # Check if it's a two-handed gesture
            is_two_hands = "_TwoHands" in folder_name
            gesture_name = folder_name.replace("_TwoHands", "") if is_two_hands else folder_name
            
            # Record gesture type
            self.hand_type_dict[gesture_name] = is_two_hands
            
            # Initialize storage for this gesture if not exists
            if gesture_name not in self.gesture_data:
                self.gesture_data[gesture_name] = {
                    "samples": [],
                    "is_two_hands": is_two_hands
                }
            
            print(f"Processing {gesture_name} {'(two hands)' if is_two_hands else '(one hand)'} gesture, {len(json_files)} session files")
            
            # Process each session file
            for json_file in json_files:
                file_path = os.path.join(gesture_dir, json_file)
                try:
                    with open(file_path, 'r') as f:
                        data = json.load(f)
                    
                    # Add all samples to our collection
                    self.gesture_data[gesture_name]["samples"].extend(data['samples'])
                    
                except Exception as e:
                    print(f"Error processing file {file_path}: {e}")
            
            print(f"  - Loaded {len(self.gesture_data[gesture_name]['samples'])} samples for {gesture_name}")
        
        return True
    
    def load_trained_models(self):
        """Load trained models if available"""
        try:
            # Load single hand model
            single_hand_model_file = os.path.join(self.models_dir, 'gesture_model_single_hand.pkl')
            if os.path.exists(single_hand_model_file):
                with open(single_hand_model_file, 'rb') as f:
                    self.single_hand_model = pickle.load(f)
                print(f"Loaded single hand model with classes: {self.single_hand_model.classes_}")
            
            # Load two hands model
            two_hands_model_file = os.path.join(self.models_dir, 'gesture_model_two_hands.pkl')
            if os.path.exists(two_hands_model_file):
                with open(two_hands_model_file, 'rb') as f:
                    self.two_hands_model = pickle.load(f)
                print(f"Loaded two hands model with classes: {self.two_hands_model.classes_}")
            
            # Load hand type dictionary
            hand_types_file = os.path.join(self.models_dir, 'gesture_model_hand_types.json')
            if os.path.exists(hand_types_file):
                with open(hand_types_file, 'r') as f:
                    self.hand_type_dict = json.load(f)
            
            return True
        except Exception as e:
            print(f"Error loading models: {e}")
            return False
    
    def analyze_features(self):
        """Perform statistical analysis on all gesture features"""
        print("\nAnalyzing gesture features...")
        
        # Process each gesture type
        for gesture_name, gesture_info in self.gesture_data.items():
            samples = gesture_info["samples"]
            is_two_hands = gesture_info["is_two_hands"]
            
            print(f"Analyzing {gesture_name} {'(two hands)' if is_two_hands else '(one hand)'} - {len(samples)} samples")
            
            # Extract all feature vectors for this gesture
            feature_vectors = []
            feature_dict = {}  # Used to store original feature names
            
            for sample in samples:
                if is_two_hands and sample["hand2"] is not None:
                    # Extract features for two-handed gestures
                    features = self.feature_extractor.extract_two_hands_features(
                        sample["hand1"], sample["hand2"])
                    feature_vectors.append(features["feature_vector"])
                    
                    # Store feature dictionary for later analysis
                    if not feature_dict:
                        # Extract single hand features
                        if "hand1" in features and "hand2" in features:
                            for key, value in features["hand1"].items():
                                if key != "normalized_landmarks" and key != "feature_vector":
                                    feature_dict[f"hand1_{key}"] = value
                            for key, value in features["hand2"].items():
                                if key != "normalized_landmarks" and key != "feature_vector":
                                    feature_dict[f"hand2_{key}"] = value
                        
                        # Extract combined hand features
                        for key, value in features.items():
                            if key not in ["hand1", "hand2", "feature_vector"]:
                                feature_dict[key] = value
                    
                elif not is_two_hands:
                    # Extract features for single-handed gestures
                    features = self.feature_extractor.extract_single_hand_features(
                        sample["hand1"])
                    feature_vectors.append(features["feature_vector"])
                    
                    # Store feature dictionary for later analysis
                    if not feature_dict:
                        for key, value in features.items():
                            if key != "normalized_landmarks" and key != "feature_vector":
                                feature_dict[key] = value
            
            if not feature_vectors:
                print(f"  - No feature vectors found for {gesture_name}")
                continue
            
            # Convert to numpy array
            feature_array = np.array(feature_vectors)
            
            # Calculate statistics
            feature_mean = np.mean(feature_array, axis=0)
            feature_std = np.std(feature_array, axis=0)
            feature_min = np.min(feature_array, axis=0)
            feature_max = np.max(feature_array, axis=0)
            
            # Store the stats
            self.feature_stats[gesture_name] = {
                "mean": feature_mean,
                "std": feature_std,
                "min": feature_min,
                "max": feature_max,
                "feature_count": feature_array.shape[1],
                "sample_count": len(feature_vectors),
                "feature_dict": feature_dict  # Store original feature names
            }
            
            print(f"  - Feature vector length: {feature_array.shape[1]}")
            print(f"  - Samples with features: {len(feature_vectors)}")
        
        return True
    
    def extract_key_distinguishing_features(self):
        """Extract the most distinguishing features between gestures"""
        print("\nExtracting key distinguishing features...")
        
        # Separate single hand and two hands gestures
        single_hand_gestures = {g: info for g, info in self.gesture_data.items() 
                              if not info["is_two_hands"]}
        two_hands_gestures = {g: info for g, info in self.gesture_data.items() 
                            if info["is_two_hands"]}
        
        # Process based on available models
        if self.single_hand_model and single_hand_gestures:
            self._analyze_model_features(self.single_hand_model, single_hand_gestures, "single hand")
        
        if self.two_hands_model and two_hands_gestures:
            self._analyze_model_features(self.two_hands_model, two_hands_gestures, "two hands")
        
        # If no models are available, analyze based on statistical differences
        if not (self.single_hand_model or self.two_hands_model):
            self._analyze_statistical_differences()
    
    def _analyze_model_features(self, model, gestures, model_type):
        """Analyze feature importance from trained model"""
        print(f"\nAnalyzing {model_type} model feature importance...")
        
        # Get feature importances from Random Forest
        if hasattr(model, 'feature_importances_'):
            importances = model.feature_importances_
            
            # Sort features by importance
            indices = np.argsort(importances)[::-1]
            
            # Get a sample gesture to extract feature names
            sample_gesture = list(gestures.keys())[0]
            if sample_gesture in self.feature_stats:
                feature_dict = self.feature_stats[sample_gesture].get("feature_dict", {})
            
                # Map feature indices to feature names if possible
                feature_names = self._map_feature_indices_to_names(model_type, feature_dict)
            
            # Display the top 20 most important features
            top_n = 20
            print(f"Top {top_n} most important features for {model_type} gestures:")
            for i in range(min(top_n, len(indices))):
                feature_idx = indices[i]
                importance = importances[feature_idx]
                feature_name = feature_names.get(feature_idx, f"Feature #{feature_idx}")
                print(f"  {feature_name}: {importance:.4f}")
            
            # Visualize feature importance
            plt.figure(figsize=(12, 6))
            plt.title(f'Feature Importance for {model_type.capitalize()} Gestures')
            
            # Use feature names in plot if available
            x_labels = [feature_names.get(idx, f"#{idx}") for idx in indices[:30]]
            plt.bar(range(min(30, len(indices))), importances[indices[:30]], align='center')
            plt.xticks(range(min(30, len(indices))), x_labels, rotation=90)
            plt.tight_layout()
            
            # Save to model_features directory
            output_file = os.path.join(self.output_dir, "model_features", 
                                      f"{model_type.replace(' ', '_')}_feature_importance.png")
            plt.savefig(output_file)
            plt.close()
            print(f"  Model feature importance visualization saved to: {output_file}")
    
    def _map_feature_indices_to_names(self, model_type, feature_dict):
        """Map feature indices to their names based on feature_extractor"""
        feature_names = {}
        
        # Set feature order based on model type
        if model_type == "single hand":
            # Single hand feature order matches the order in FeatureExtractor.extract_single_hand_features
            idx = 0
            
            # Distance features
            if "distances" in feature_dict:
                for i in range(len(feature_dict["distances"])):
                    feature_names[idx] = f"Distance_{i}"
                    idx += 1
            
            # Angle features
            if "angles" in feature_dict:
                for i in range(len(feature_dict["angles"])):
                    feature_names[idx] = f"Angle_{i}"
                    idx += 1
            
            # Height pattern
            if "height_pattern" in feature_dict:
                for i in range(len(feature_dict["height_pattern"])):
                    feature_names[idx] = f"Height_Pattern_{i}"
                    idx += 1
            
            # Wolf vs fist features
            if "wolf_fist_features" in feature_dict:
                wolf_fist_feature_names = [
                    "Index-Middle Angle", "Fist Closeness", "Index Extension", 
                    "Middle Extension", "Index-Middle vs Others", "Index-Middle Height Diff",
                    "Thumb-Index Angle", "Thumb-Index Distance"
                ]
                for i, name in enumerate(wolf_fist_feature_names):
                    feature_names[idx] = name
                    idx += 1
            
            # Fingertip closeness
            feature_names[idx] = "Fingertip_Distance_STD"
            idx += 1
            
            # Normalized coordinates
            if "flat_coordinates" in feature_dict:
                for i in range(len(feature_dict["flat_coordinates"])):
                    landmark_idx = i // 3
                    coord = "x" if i % 3 == 0 else ("y" if i % 3 == 1 else "z")
                    feature_names[idx] = f"Landmark_{landmark_idx}_{coord}"
                    idx += 1
                    
        elif model_type == "two hands":
            # Two hands feature order
            idx = 0
            
            # First hand features
            hand1_features = len(self.feature_extractor.extract_single_hand_features(
                [{"x": 0, "y": 0, "z": 0}] * 21)["feature_vector"])
            for i in range(hand1_features):
                feature_names[idx] = f"Hand1_Feature_{i}"
                idx += 1
                
            # Second hand features
            hand2_features = hand1_features
            for i in range(hand2_features):
                feature_names[idx] = f"Hand2_Feature_{i}"
                idx += 1
            
            # Mirror differences
            if "mirror_diff" in feature_dict:
                for i in range(len(feature_dict["mirror_diff"])):
                    feature_names[idx] = f"Mirror_Diff_{i}"
                    idx += 1
            
            # Height differences
            if "height_diff" in feature_dict:
                for i in range(len(feature_dict["height_diff"])):
                    feature_names[idx] = f"Height_Diff_{i}"
                    idx += 1
            
            # Other two-hand features
            other_features = [
                "symmetry_score", "wrist_dist", "palm_dist", "min_tip_dist", 
                "x_position_diff", "y_position_diff", "palm_orientation_similarity"
            ]
            
            for name in other_features:
                if name in feature_dict:
                    feature_names[idx] = name.replace("_", " ").title()
                    idx += 1
            
            # Fingertip distance matrix
            if "tip_dist_matrix" in feature_dict:
                for i in range(len(feature_dict["tip_dist_matrix"])):
                    feature_names[idx] = f"Tip_Distance_{i}"
                    idx += 1
        
        return feature_names
    
    def _analyze_statistical_differences(self):
        """Analyze features based on statistical differences between gestures"""
        print("\nAnalyzing statistical differences between gestures...")
        
        # Group gestures by hand type
        single_hand_gestures = [g for g, info in self.gesture_data.items() 
                              if not info["is_two_hands"]]
        two_hands_gestures = [g for g, info in self.gesture_data.items() 
                            if info["is_two_hands"]]
        
        # Analyze each group separately
        if single_hand_gestures:
            self._find_distinguishing_features(single_hand_gestures, "single hand")
        
        if two_hands_gestures:
            self._find_distinguishing_features(two_hands_gestures, "two hands")
    
    def _find_distinguishing_features(self, gesture_names, hand_type):
        """Find features with the most variation between gestures"""
        print(f"\nFinding distinguishing features for {hand_type} gestures: {gesture_names}")
        
        if len(gesture_names) < 2:
            print(f"  Need at least two gestures to compare")
            return
        
        # Get the means for each gesture
        means = np.array([self.feature_stats[g]["mean"] for g in gesture_names])
        
        # Calculate the standard deviation across gestures for each feature
        feature_variations = np.std(means, axis=0)
        
        # Get the features with highest variation (most distinguishing)
        top_indices = np.argsort(feature_variations)[::-1]
        
        # Get feature names if available
        sample_gesture = gesture_names[0]
        feature_dict = self.feature_stats[sample_gesture].get("feature_dict", {})
        feature_names = self._map_feature_indices_to_names(
            "single hand" if not self.gesture_data[sample_gesture]["is_two_hands"] else "two hands", 
            feature_dict
        )
        
        # Display top distinguishing features
        top_n = 20
        print(f"Top {top_n} most distinguishing features for {hand_type} gestures:")
        for i in range(min(top_n, len(top_indices))):
            feature_idx = top_indices[i]
            variation = feature_variations[feature_idx]
            feature_name = feature_names.get(feature_idx, f"Feature #{feature_idx}")
            print(f"  {feature_name}: variation {variation:.4f}")
            
            # Show the value of this feature for each gesture
            for g in gesture_names:
                mean_val = self.feature_stats[g]["mean"][feature_idx]
                std_val = self.feature_stats[g]["std"][feature_idx]
                print(f"    {g}: {mean_val:.4f} ± {std_val:.4f}")
    
    def visualize_gesture_clusters(self):
        """Visualize gesture samples in reduced feature space to show clustering"""
        print("\nVisualizing gesture clusters...")
        
        # Process separately for single hand and two hands
        single_hand_gestures = {g: info for g, info in self.gesture_data.items() 
                              if not info["is_two_hands"]}
        two_hands_gestures = {g: info for g, info in self.gesture_data.items() 
                            if info["is_two_hands"]}
        
        if single_hand_gestures:
            self._visualize_gesture_type(single_hand_gestures, "single_hand")
        
        if two_hands_gestures:
            self._visualize_gesture_type(two_hands_gestures, "two_hands")
    
    def _visualize_gesture_type(self, gestures, label):
        """Visualize gestures of a specific type (single or two handed)"""
        print(f"Visualizing {label} gestures...")
        
        # Collect all feature vectors and corresponding labels
        X = []
        y = []
        
        for gesture_name, info in gestures.items():
            for sample in info["samples"]:
                # Extract feature vectors directly using FeatureExtractor
                if info["is_two_hands"] and sample["hand2"] is not None:
                    features = self.feature_extractor.extract_two_hands_features(
                        sample["hand1"], sample["hand2"])
                    X.append(features["feature_vector"])
                    y.append(gesture_name)
                elif not info["is_two_hands"]:
                    features = self.feature_extractor.extract_single_hand_features(
                        sample["hand1"])
                    X.append(features["feature_vector"])
                    y.append(gesture_name)
        
        if len(X) == 0:
            print(f"  No feature vectors found for {label} gestures")
            return
            
        X = np.array(X)
        
        # Dimensionality reduction with PCA
        print("  Applying PCA...")
        pca = PCA(n_components=2)
        X_pca = pca.fit_transform(X)
        
        # Plotting PCA
        plt.figure(figsize=(12, 8))
        for gesture in set(y):
            idx = [i for i, label in enumerate(y) if label == gesture]
            plt.scatter(X_pca[idx, 0], X_pca[idx, 1], label=gesture, alpha=0.7)
        
        plt.title(f'PCA Visualization of {label.replace("_", " ").capitalize()} Gestures')
        plt.legend()
        plt.tight_layout()
        
        # Save to clusters directory
        output_file = os.path.join(self.output_dir, "gesture_clusters", f"{label}_pca_visualization.png")
        plt.savefig(output_file)
        plt.close()
        print(f"  PCA visualization saved to: {output_file}")
        
        # Dimensionality reduction with t-SNE (better for clustering visualization)
        print("  Applying t-SNE (this may take a while)...")
        tsne = TSNE(n_components=2, random_state=42, perplexity=min(30, len(X)-1))
        X_tsne = tsne.fit_transform(X)
        
        # Plotting t-SNE
        plt.figure(figsize=(12, 8))
        for gesture in set(y):
            idx = [i for i, label in enumerate(y) if label == gesture]
            plt.scatter(X_tsne[idx, 0], X_tsne[idx, 1], label=gesture, alpha=0.7)
        
        plt.title(f't-SNE Visualization of {label.replace("_", " ").capitalize()} Gestures')
        plt.legend()
        plt.tight_layout()
        
        # Save to clusters directory
        output_file = os.path.join(self.output_dir, "gesture_clusters", f"{label}_tsne_visualization.png")
        plt.savefig(output_file)
        plt.close()
        print(f"  t-SNE visualization saved to: {output_file}")
        
        # Additional visualization: 3D PCA
        print("  Creating 3D PCA visualization...")
        pca_3d = PCA(n_components=3)
        X_pca_3d = pca_3d.fit_transform(X)
        
        fig = plt.figure(figsize=(12, 10))
        ax = fig.add_subplot(111, projection='3d')
        
        for gesture in set(y):
            idx = [i for i, label in enumerate(y) if label == gesture]
            ax.scatter(X_pca_3d[idx, 0], X_pca_3d[idx, 1], X_pca_3d[idx, 2], label=gesture, alpha=0.7)
        
        ax.set_title(f'3D PCA Visualization of {label.replace("_", " ").capitalize()} Gestures')
        ax.legend()
        plt.tight_layout()
        
        # Save to clusters directory
        output_file = os.path.join(self.output_dir, "gesture_clusters", f"{label}_pca_3d_visualization.png")
        plt.savefig(output_file)
        plt.close()
        print(f"  3D PCA visualization saved to: {output_file}")
    
    def visualize_specific_features(self):
        """Visualize specific features that are interesting for each gesture type"""
        # Example for two hands gestures: visualize mirror difference (symmetry)
        two_hands_gestures = {g: info for g, info in self.gesture_data.items() 
                            if info["is_two_hands"]}
        
        if two_hands_gestures:
            self._visualize_mirror_differences(two_hands_gestures)
            
        # For single hand gestures: visualize finger angles
        single_hand_gestures = {g: info for g, info in self.gesture_data.items() 
                                if not info["is_two_hands"]}
        
        if single_hand_gestures:
            self._visualize_finger_features(single_hand_gestures)
    
    def _visualize_mirror_differences(self, gestures):
        """Visualize mirror differences for two handed gestures"""
        print("\nVisualizing symmetry (mirror differences) for two-handed gestures...")
        
        # Collect mirror difference data
        gesture_symmetry = {}
        
        for gesture_name, info in gestures.items():
            symmetry_scores = []
            
            for sample in info["samples"]:
                if sample["hand2"] is not None:
                    # Calculate features directly from extractor
                    features = self.feature_extractor.extract_two_hands_features(
                        sample["hand1"], sample["hand2"])
                    
                    # Use symmetry score
                    if "symmetry_score" in features:
                        symmetry_scores.append(features["symmetry_score"])
                    elif "mirror_diff" in features:
                        # If no direct symmetry score, use mean of mirror differences
                        symmetry_scores.append(np.mean(features["mirror_diff"]))
            
            if symmetry_scores:
                gesture_symmetry[gesture_name] = symmetry_scores
        
        if not gesture_symmetry:
            print("  No symmetry data available")
            return
        
        # Plotting boxplot of symmetry scores - fix deprecated warning
        plt.figure(figsize=(12, 6))
        boxplot_data = [scores for gesture, scores in gesture_symmetry.items()]
        plt.boxplot(boxplot_data, tick_labels=list(gesture_symmetry.keys()))  # Use tick_labels instead of labels
        plt.title('Symmetry Comparison Between Two-Handed Gestures')
        plt.ylabel('Symmetry Score (lower = more symmetric)')
        plt.xticks(rotation=45)
        plt.tight_layout()
        
        # Save to symmetry directory
        output_file = os.path.join(self.output_dir, "specific_features", "symmetry", "two_hands_symmetry_comparison.png")
        plt.savefig(output_file)
        plt.close()
        print(f"  Symmetry comparison boxplot saved to: {output_file}")
        
        # Add heatmap visualization - fix colorbar error
        symmetry_means = {g: np.mean(scores) for g, scores in gesture_symmetry.items()}
        symmetry_stds = {g: np.std(scores) for g, scores in gesture_symmetry.items()}
        
        # Use object-oriented approach to create plot
        fig, ax = plt.subplots(figsize=(10, 6))
        gestures_list = list(symmetry_means.keys())
        means_list = [symmetry_means[g] for g in gestures_list]
        
        # Create bar chart
        bars = ax.bar(gestures_list, means_list, yerr=[symmetry_stds[g] for g in gestures_list])
        
        # Color mapping - lower (more symmetric) is greener, higher is redder
        cmap = plt.cm.RdYlGn_r
        max_val = max(means_list)
        min_val = min(means_list)
        norm = plt.Normalize(min_val, max_val)
        
        for bar, val in zip(bars, means_list):
            bar.set_color(cmap(norm(val)))
            
        ax.set_title('Symmetry Scores Across Two-Handed Gestures')
        ax.set_ylabel('Symmetry Score (lower = more symmetric)')
        plt.xticks(rotation=45)
        
        # Correctly add colorbar by specifying ax parameter
        fig.colorbar(plt.cm.ScalarMappable(norm=norm, cmap=cmap), ax=ax, label='Symmetry Score')
        
        plt.tight_layout()
        
        # Save to symmetry directory
        output_file = os.path.join(self.output_dir, "specific_features", "symmetry", "two_hands_symmetry_bars.png")
        plt.savefig(output_file)
        plt.close()
        print(f"  Symmetry bars chart saved to: {output_file}")
    
    def _visualize_finger_features(self, gestures):
        """Visualize finger-specific features for single-handed gestures"""
        print("\nVisualizing finger features for single-handed gestures...")
        
        # Collect finger angle/extension data
        all_gesture_data = {}
        
        for gesture_name, info in gestures.items():
            # Extract features directly from feature extractor
            angle_features = []
            wolf_fist_features = []
            
            for sample in info["samples"]:
                features = self.feature_extractor.extract_single_hand_features(sample["hand1"])
                
                if "angles" in features:
                    angle_features.append(features["angles"])
                
                if "wolf_fist_features" in features:
                    wolf_fist_features.append(features["wolf_fist_features"])
            
            all_gesture_data[gesture_name] = {
                "angles": angle_features,
                "wolf_fist_features": wolf_fist_features
            }
        
        # Check if we have enough data
        has_angle_data = all(len(data["angles"]) > 0 for _, data in all_gesture_data.items())
        has_wolf_fist_data = all(len(data["wolf_fist_features"]) > 0 for _, data in all_gesture_data.items())
        
        if not (has_angle_data or has_wolf_fist_data):
            print("  No finger feature data available for visualization")
            return
        
        # Plot finger joint angles
        if has_angle_data:
            angle_means = {gesture: np.mean(data["angles"], axis=0) for gesture, data in all_gesture_data.items()}
            
            # Prepare data for plotting
            angle_df = pd.DataFrame(angle_means).T
            
            # Set angle feature names
            angle_names = []
            # Fingertip-wrist-fingertip angles
            for i in range(5):  # 5 fingertips
                for j in range(i+1, 5):
                    angle_names.append(f"Tip{i}-Wrist-Tip{j}")
            
            # MCP-PIP-TIP joint angles
            finger_names = ["Index", "Middle", "Ring", "Pinky"]
            for finger in finger_names:
                angle_names.append(f"{finger}_MCP-PIP-TIP")
            
            # Ensure column names match
            if len(angle_names) == angle_df.shape[1]:
                angle_df.columns = angle_names
            else:
                angle_df.columns = [f"Angle_{i}" for i in range(angle_df.shape[1])]
            
            # Draw heatmap
            plt.figure(figsize=(14, 8))
            sns.heatmap(angle_df, annot=True, cmap="viridis", fmt=".2f")
            plt.title("Finger Joint Angles Comparison")
            plt.tight_layout()
            
            # Save to finger_features directory
            output_file = os.path.join(self.output_dir, "specific_features", "finger_features", "single_hand_angle_comparison.png")
            plt.savefig(output_file)
            plt.close()
            print(f"  Finger joint angles comparison saved to: {output_file}")
        
        # Plot wolf vs fist specific features
        if has_wolf_fist_data:
            wolf_fist_means = {gesture: np.mean(data["wolf_fist_features"], axis=0) 
                              for gesture, data in all_gesture_data.items()}
            
            # Feature names from FeatureExtractor
            feature_names = [
                "Index-Middle Angle", "Fist Closeness", "Index Extension", 
                "Middle Extension", "Index-Middle vs Others", "Index-Middle Height Diff",
                "Thumb-Index Angle", "Thumb-Index Distance"
            ]
            
            wolf_fist_df = pd.DataFrame(wolf_fist_means).T
            
            # Ensure column names match
            if len(feature_names) == wolf_fist_df.shape[1]:
                wolf_fist_df.columns = feature_names
            else:
                wolf_fist_df.columns = [f"Feature_{i}" for i in range(wolf_fist_df.shape[1])]
            
            # Draw bar chart
            plt.figure(figsize=(14, 10))
            wolf_fist_df.plot(kind='bar', figsize=(14, 8))
            plt.title("Wolf vs Fist Feature Comparison")
            plt.ylabel("Feature Value")
            plt.xticks(rotation=45)
            plt.legend(loc='upper center', bbox_to_anchor=(0.5, -0.15), ncol=4)
            plt.tight_layout()
            
            # Save to finger_features directory
            output_file = os.path.join(self.output_dir, "specific_features", "finger_features", "wolf_fist_feature_comparison.png")
            plt.savefig(output_file)
            plt.close()
            print(f"  Wolf vs fist feature comparison saved to: {output_file}")
            
            # Add radar chart visualization
            plt.figure(figsize=(10, 10))
            
            # Set radar chart angles
            categories = feature_names
            N = len(categories)
            angles = [n / float(N) * 2 * np.pi for n in range(N)]
            angles += angles[:1]  # Close the radar chart
            
            # Initialize radar chart
            ax = plt.subplot(111, polar=True)
            
            # For each gesture, draw a radar line
            for gesture in wolf_fist_df.index:
                values = wolf_fist_df.loc[gesture].values.flatten().tolist()
                values += values[:1]  # Close the radar chart
                
                # Draw line and fill area
                ax.plot(angles, values, linewidth=1, linestyle='solid', label=gesture)
                ax.fill(angles, values, alpha=0.1)
            
            # Set tick labels
            plt.xticks(angles[:-1], categories, size=8)
            
            # Add legend
            plt.legend(loc='upper right', bbox_to_anchor=(0.1, 0.1))
            plt.title("Gesture Feature Comparison (Radar Chart)")
            plt.tight_layout()
            
            # Save to finger_features directory
            output_file = os.path.join(self.output_dir, "specific_features", "finger_features", "gesture_feature_radar.png")
            plt.savefig(output_file)
            plt.close()
            print(f"  Gesture feature radar chart saved to: {output_file}")
    
    def compare_gesture_features(self):
        """Compare feature values across different gestures and generate comparison tables"""
        print("\nComparing feature values across gestures...")
        
        # Process separately for single hand and two hands gestures
        single_hand_gestures = {g: info for g, info in self.gesture_data.items() 
                              if not info["is_two_hands"]}
        two_hands_gestures = {g: info for g, info in self.gesture_data.items() 
                            if info["is_two_hands"]}
        
        # Process single hand gesture comparisons
        if single_hand_gestures:
            self._create_feature_comparison_table(single_hand_gestures, "single_hand")
        
        # Process two hands gesture comparisons
        if two_hands_gestures:
            self._create_feature_comparison_table(two_hands_gestures, "two_hands")
            
    def _create_feature_comparison_table(self, gestures, hand_type):
        """Create feature comparison table for specified hand type"""
        print(f"\nCreating {hand_type} gesture feature comparison table...")
        
        # Collect all gesture feature statistics
        gesture_names = list(gestures.keys())
        
        if not gesture_names:
            print(f"  No {hand_type} gesture data available")
            return
            
        # Get a sample gesture to determine feature names
        sample_gesture = gesture_names[0]
        if sample_gesture not in self.feature_stats:
            print(f"  No feature statistics for {sample_gesture}")
            return
        
        # Get feature name mappings
        feature_dict = self.feature_stats[sample_gesture].get("feature_dict", {})
        feature_names = self._map_feature_indices_to_names(
            "single hand" if hand_type == "single_hand" else "two hands", 
            feature_dict
        )
        
        # Extract mean feature values for each gesture
        means_data = {}
        for gesture in gesture_names:
            if gesture in self.feature_stats:
                means_data[gesture] = self.feature_stats[gesture]["mean"]
        
        # Determine number of features to display (max 20 key features)
        feature_count = min(20, len(means_data[gesture_names[0]]))
        
        # Create DataFrame for visualization
        mean_values = np.array([means_data[g][:feature_count] for g in gesture_names])
        df = pd.DataFrame(mean_values, index=gesture_names)
        
        # Set column names to feature names
        column_names = [feature_names.get(i, f"Feature #{i}") for i in range(feature_count)]
        df.columns = column_names
        
        # Save to CSV file
        output_dir = os.path.join(self.output_dir, "feature_comparison", hand_type)
        csv_filename = os.path.join(output_dir, "features_comparison.csv")
        df.to_csv(csv_filename)
        print(f"  Feature comparison saved to: {csv_filename}")
        
        # Create heatmap visualization
        plt.figure(figsize=(16, 8))
        
        # Use "coolwarm" colormap commonly used in deep learning
        sns.heatmap(df, annot=True, fmt=".2f", cmap="coolwarm", linewidths=.5)
        
        plt.title(f"{hand_type.replace('_', ' ').capitalize()} Gesture Feature Comparison")
        plt.tight_layout()
        
        # Save to feature_comparison directory
        heatmap_file = os.path.join(output_dir, "features_heatmap.png")
        plt.savefig(heatmap_file, dpi=300)
        plt.close()
        print(f"  Heatmap saved to: {heatmap_file}")
        
        # Create radar charts for each gesture
        self._create_feature_radar_charts(df, hand_type)
        
    def _create_feature_radar_charts(self, feature_df, hand_type):
        """Create feature radar charts for each gesture"""
        # Prepare plotting data
        categories = feature_df.columns.tolist()
        N = len(categories)
        
        # Angle calculations
        angles = [n / float(N) * 2 * np.pi for n in range(N)]
        angles += angles[:1]  # Close radar chart
        
        # Create radar directory
        output_dir = os.path.join(self.output_dir, "radar_charts", hand_type)
        
        # Create separate radar chart for each gesture
        for gesture in feature_df.index:
            fig, ax = plt.subplots(figsize=(10, 10), subplot_kw=dict(polar=True))
            
            # Extract and normalize feature values (scale to 0-1 range)
            values = feature_df.loc[gesture].values
            min_vals = feature_df.min()
            max_vals = feature_df.max()
            norm_values = (values - min_vals) / (max_vals - min_vals + 1e-10)  # Avoid division by zero
            
            # Close radar chart
            values_plot = np.append(norm_values, norm_values[0])
            
            # Draw radar chart
            ax.plot(angles, values_plot, linewidth=2, linestyle='solid')
            ax.fill(angles, values_plot, alpha=0.25)
            
            # Set labels
            plt.xticks(angles[:-1], categories, size=8)
            
            # Set y-axis range
            ax.set_ylim(0, 1)
            
            # Add title
            plt.title(f"{gesture} Gesture Feature Distribution")
            
            # Save image
            plt.tight_layout()
            radar_file = os.path.join(output_dir, f"{gesture}_radar.png")
            plt.savefig(radar_file, dpi=300)
            plt.close()
        
        print(f"  Radar charts generated for each gesture")
    
    def analyze_all(self):
        """Run all analysis functions"""
        # Load data
        if not self.load_gesture_data():
            print("Failed to load gesture data.")
            return False
        
        # Try to load models (optional)
        self.load_trained_models()
        
        # Run analysis
        self.analyze_features()
        self.extract_key_distinguishing_features()
        self.compare_gesture_features()  # Add new feature comparison analysis
        self.visualize_gesture_clusters()
        self.visualize_specific_features()
        
        print("\nAnalysis complete! All visualizations have been saved to the following directory:")
        print(f"  {os.path.abspath(self.output_dir)}")
        return True

# Run the analyzer if this script is executed directly
if __name__ == "__main__":
    analyzer = GestureAnalyzer()
    analyzer.analyze_all()