import bpy
import json
import mathutils
import os

# 手指关键点与骨骼的映射关系
# MediaPipe使用21个关键点表示一只手
# 0:手腕, 1-4:拇指, 5-8:食指, 9-12:中指, 13-16:无名指, 17-20:小指
HAND_BONES_MAPPING = {
    "wrist": 0,
    "thumb_cmc": 1,
    "thumb_mcp": 2,
    "thumb_ip": 3,
    "thumb_tip": 4,
    "index_mcp": 5,
    "index_pip": 6,
    "index_dip": 7,
    "index_tip": 8,
    "middle_mcp": 9,
    "middle_pip": 10,
    "middle_dip": 11,
    "middle_tip": 12,
    "ring_mcp": 13,
    "ring_pip": 14,
    "ring_dip": 15,
    "ring_tip": 16,
    "pinky_mcp": 17,
    "pinky_pip": 18,
    "pinky_dip": 19,
    "pinky_tip": 20,
}

def load_tracking_data(json_path):
    try:
        with open(json_path, 'r') as f:
            data = json.load(f)
        return data
    except Exception as e:
        print(f"加载JSON文件时出错: {e}")
        # 尝试使用绝对路径
        try:
            abs_path = r"D:\College\Game\ShadowTheatre\MediapipeHand\hand_tracking_data.json"
            print(f"尝试绝对路径: {abs_path}")
            with open(abs_path, 'r') as f:
                data = json.load(f)
            return data
        except Exception as e2:
            print(f"尝试绝对路径也失败: {e2}")
            raise

def create_hand_armature(name="HandArmature"):
    # 创建一个新的手部骨架
    bpy.ops.object.armature_add()
    armature = bpy.context.active_object
    armature.name = name
    
    # 进入编辑模式添加骨骼
    bpy.ops.object.mode_set(mode='EDIT')
    
    # 先删除默认骨骼
    for bone in armature.data.edit_bones:
        armature.data.edit_bones.remove(bone)
    
    # 创建手腕骨骼作为根骨骼
    wrist = armature.data.edit_bones.new("wrist")
    wrist.head = (0, 0, 0)
    wrist.tail = (0, 0.1, 0)
    
    # 创建拇指骨骼链
    thumb_bones = create_finger_bones(armature, "thumb", wrist, 4)
    
    # 创建其他手指骨骼链
    index_bones = create_finger_bones(armature, "index", wrist, 4)
    middle_bones = create_finger_bones(armature, "middle", wrist, 4)
    ring_bones = create_finger_bones(armature, "ring", wrist, 4)
    pinky_bones = create_finger_bones(armature, "pinky", wrist, 4)
    
    # 返回到对象模式
    bpy.ops.object.mode_set(mode='OBJECT')
    
    return armature

def create_finger_bones(armature, finger_name, parent_bone, num_bones):
    bones = []
    prev_bone = parent_bone
    
    for i in range(num_bones):
        if i == 0:
            bone_name = f"{finger_name}_mcp"
        elif i == 1:
            bone_name = f"{finger_name}_pip" 
        elif i == 2:
            bone_name = f"{finger_name}_dip"
        else:
            bone_name = f"{finger_name}_tip"
            
        bone = armature.data.edit_bones.new(bone_name)
        
        # 设置骨骼初始位置
        if i == 0:
            bone.head = (prev_bone.tail)
            offset = 0.1 if finger_name != "thumb" else 0.07
            bone.tail = (prev_bone.tail[0], prev_bone.tail[1] + offset, prev_bone.tail[2])
        else:
            bone.head = prev_bone.tail
            bone.tail = (prev_bone.tail[0], prev_bone.tail[1] + 0.05, prev_bone.tail[2])
        
        # 设置父骨骼
        bone.parent = prev_bone
        
        bones.append(bone)
        prev_bone = bone
    
    return bones

def add_joint_markers(armature, marker_size=0.03, marker_color=(1, 0.5, 0, 1)):
    """为骨骼的每个关节添加小立方体标记"""
    markers = []
    
    # 确保处于对象模式
    bpy.ops.object.mode_set(mode='OBJECT')
    
    # 为每个骨骼添加一个立方体
    for bone_name in HAND_BONES_MAPPING.keys():
        if bone_name in armature.pose.bones:
            # 创建立方体
            bpy.ops.mesh.primitive_cube_add(size=marker_size)
            marker = bpy.context.active_object
            marker.name = f"marker_{bone_name}_{armature.name}"
            
            # 设置立方体颜色
            mat = bpy.data.materials.new(name=f"MarkerMaterial_{bone_name}_{armature.name}")
            mat.diffuse_color = marker_color
            marker.data.materials.append(mat)
            
            # 设置父级约束以跟随骨骼
            constraint = marker.constraints.new('COPY_LOCATION')
            constraint.target = armature
            constraint.subtarget = bone_name
            
            markers.append(marker)
    
    return markers

# MediaPipe 到 Blender 的坐标转换函数
def convert_mediapipe_to_blender(mp_x, mp_y, mp_z, scale=3.0):
    """
    转换 MediaPipe 坐标到 Blender 坐标
    MediaPipe: x向右, y向下, z向前
    Blender: x向右, y向后, z向上
    """
    blender_x = mp_x * scale
    blender_y = mp_z * scale  # MediaPipe z -> Blender y (翻转)
    blender_z = mp_y * scale  # MediaPipe y -> Blender z (翻转)
    return mathutils.Vector((blender_x, blender_y, blender_z))

def apply_tracking_data(armature, tracking_data):
    # 首先，清除所有现有的骨架和标记
    # 查找并删除所有之前创建的对象
    objects_to_remove = []
    for obj in bpy.data.objects:
        # 删除所有骨架和标记
        if obj.name.startswith(("LeftHand", "RightHand", "marker_")):
            objects_to_remove.append(obj)
    
    # 删除收集的对象
    for obj in objects_to_remove:
        bpy.data.objects.remove(obj, do_unlink=True)
    
    # 清除所有对象的动画数据
    for object in bpy.data.objects:
        if object.animation_data:
            object.animation_data_clear()
    
    frames = tracking_data["frames"]
    
    # 设置场景帧范围
    bpy.context.scene.frame_start = 0
    bpy.context.scene.frame_end = len(frames) - 1
    
    # 创建两个骨架，分别用于左右手
    left_armature = create_hand_armature("LeftHand")
    right_armature = create_hand_armature("RightHand")
    
    # 为两个骨架添加关节标记
    left_markers = add_joint_markers(left_armature, marker_size=0.03, marker_color=(0, 0.7, 1, 1))  # 蓝色标记
    right_markers = add_joint_markers(right_armature, marker_size=0.03, marker_color=(1, 0.3, 0.3, 1))  # 红色标记
    
    # 对于每一帧
    for frame_idx, frame_data in enumerate(frames):
        bpy.context.scene.frame_set(frame_idx)
        
        # 检查此帧是否有手部数据
        if "hands" not in frame_data or len(frame_data["hands"]) == 0:
            continue
        
        # 记录左右手位置，保持相对位置关系
        left_hand_data = None
        right_hand_data = None
        
        # 首先找出左右手数据
        for hand_data in frame_data["hands"]:
            if hand_data["handedness"] == "Left":
                left_hand_data = hand_data
            else:
                right_hand_data = hand_data
            
        # 处理该帧中的每只手
        for hand_idx, hand_data in enumerate(frame_data["hands"]):
            handedness = hand_data["handedness"]
            landmarks = hand_data["landmarks"]
            
            # 选择对应的骨架
            target_armature = left_armature if handedness == "Left" else right_armature
            
            # 获取手腕位置作为整个手的基准位置
            wrist_landmark = landmarks[0]  # 手腕是索引0
            
            # 这里调整坐标映射，保持实际比例
            scale_factor = 3  # 可调整的缩放因子
            wrist_position = convert_mediapipe_to_blender(
                wrist_landmark["x"], 
                wrist_landmark["y"], 
                wrist_landmark["z"], 
                scale=scale_factor
            )
            
            # 设置整个骨架的位置
            target_armature.location = wrist_position
            target_armature.keyframe_insert(data_path="location", frame=frame_idx)
            
            # 设置骨骼位置
            target_armature.select_set(True)
            bpy.context.view_layer.objects.active = target_armature
            bpy.ops.object.mode_set(mode='POSE')
            
            # 对于每个骨骼，根据映射设置其位置
            for bone_name, landmark_idx in HAND_BONES_MAPPING.items():
                if bone_name in target_armature.pose.bones:
                    landmark = landmarks[landmark_idx]
                    
                    # 计算相对于手腕的位置
                    # 这样可以保持手指的正确姿势，同时整个手的位置由armature.location决定
                    rel_pos = convert_mediapipe_to_blender(
                        landmark["x"] - wrist_landmark["x"],
                        landmark["y"] - wrist_landmark["y"],
                        landmark["z"] - wrist_landmark["z"],
                        scale=scale_factor
                    )
                    
                    # 设置骨骼位置
                    target_armature.pose.bones[bone_name].location = rel_pos
                    
                    # 添加关键帧
                    target_armature.pose.bones[bone_name].keyframe_insert(data_path="location", frame=frame_idx)
            
            bpy.ops.object.mode_set(mode='OBJECT')
        
    print(f"动画已应用于 {len(frames)} 帧，并添加了可视化标记")
    
    # 调整视图
    bpy.ops.view3d.view_all(center=True)

def main():
    # 尝试多个可能的路径
    possible_paths = [
        r"D:\College\Game\ShadowTheatre\MediapipeHand\hand_tracking_data.json",  # 绝对路径
        "hand_tracking_data.json"  # 相对路径
    ]
    
    json_path = None
    for path in possible_paths:
        print(f"检查路径: {path}")
        if os.path.exists(path):
            json_path = path
            print(f"找到文件: {path}")
            break
    
    if json_path is None:
        print("警告: 无法找到JSON文件，使用默认路径")
        json_path = r"D:\College\Game\ShadowTheatre\MediapipeHand\hand_tracking_data.json"
    
    try:
        tracking_data = load_tracking_data(json_path)
        apply_tracking_data(None, tracking_data)
    except Exception as e:
        print(f"发生错误: {e}")
        # 错误提示
        if "FileNotFoundError" in str(e):
            print("\n请确保JSON文件位于正确位置，或者将其复制到Blender文件所在目录")

if __name__ == "__main__":
    main()