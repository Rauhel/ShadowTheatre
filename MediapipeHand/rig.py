import bpy
import mathutils
import bmesh
import re

def create_vertex_groups(model, armature):
    """为模型创建与骨骼对应的顶点组"""
    # 确保模型没有顶点组
    for vg in model.vertex_groups:
        model.vertex_groups.remove(vg)
    
    # 为每个骨骼创建顶点组
    for bone in armature.pose.bones:
        model.vertex_groups.new(name=bone.name)
    
    return model.vertex_groups

def detect_finger_bones(armature):
    """检测骨架中的手指骨骼和它们的命名模式"""
    # 骨骼名称的常见模式
    patterns = {
        'thumb': ['thumb', 'pollex', 'toe', '親指'],
        'index': ['index', 'fore', '人指'],
        'middle': ['middle', 'mid', '中指'],
        'ring': ['ring', '薬指'],
        'pinky': ['pinky', 'little', '小指']
    }
    
    # 骨骼位置的后缀模式
    position_patterns = {
        'mcp': ['mcp', 'metacarpal', '01', '1', 'proximal'],
        'pip': ['pip', 'middle', '02', '2', 'intermediate'],
        'dip': ['dip', 'distal', '03', '3'],
        'tip': ['tip', 'end', '04', '4']
    }
    
    # 结果字典
    result = {
        'thumb': [],
        'index': [],
        'middle': [],
        'ring': [],
        'pinky': []
    }
    
    print("检测到的骨骼:")
    for bone in armature.pose.bones:
        bone_name = bone.name.lower()
        print(f"  - {bone.name}")
        
        # 检测是哪个手指
        for finger, finger_patterns in patterns.items():
            if any(pattern in bone_name for pattern in finger_patterns):
                # 检测是哪个关节
                for position, pos_patterns in position_patterns.items():
                    if any(pattern in bone_name for pattern in pos_patterns):
                        if bone.name not in result[finger]:
                            result[finger].append(bone.name)
                        break
                break
    
    # 按深度排序每个手指的骨骼 (基于Y坐标，假设Y轴是手指方向)
    for finger in result:
        sorted_bones = sorted(result[finger], 
                           key=lambda name: armature.pose.bones[name].head.y)
        result[finger] = sorted_bones
    
    # 打印检测结果
    print("\n检测到的手指骨骼结构:")
    for finger, bones in result.items():
        print(f"{finger}: {bones}")
    
    return result

def auto_weight_hand_model(model, armature, finger_definitions):
    """为手部模型自动分配权重"""
    # 确保在对象模式
    if bpy.context.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')
    
    # 取消所有选择
    bpy.ops.object.select_all(action='DESELECT')
    
    # 选择模型并设置为活动对象
    model.select_set(True)
    bpy.context.view_layer.objects.active = model
    
    # 进入编辑模式
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='DESELECT')
    
    # 获取网格数据
    mesh = model.data
    bm = bmesh.from_edit_mesh(mesh)
    
    # 使用边界框确定手部方向和大小
    bbox_min = mathutils.Vector((float('inf'), float('inf'), float('inf')))
    bbox_max = mathutils.Vector((float('-inf'), float('-inf'), float('-inf')))
    
    for v in bm.verts:
        for i in range(3):
            bbox_min[i] = min(bbox_min[i], v.co[i])
            bbox_max[i] = max(bbox_max[i], v.co[i])
    
    bbox_center = (bbox_min + bbox_max) / 2
    bbox_size = bbox_max - bbox_min
    
    # 确定手腕位置
    wrist_pos = mathutils.Vector((bbox_center.x, bbox_min.y, bbox_center.z))
    
    # 确定手指方向
    finger_direction = mathutils.Vector((0, 1, 0))  # 假设手指沿Y轴正方向
    
    # 获取所有可用的顶点组
    vertex_groups = model.vertex_groups
    available_groups = [vg.name for vg in vertex_groups]
    
    # 创建一个临时字典来存储权重数据
    # 格式: {vertex_index: {bone_name: weight, ...}, ...}
    weights_data = {}
    
    for finger_name, bones in finger_definitions.items():
        if not bones:  # 跳过没有骨骼的手指
            continue
            
        # 确定此手指的大致位置和区域
        if finger_name == "thumb":
            # 拇指通常在X轴负方向（假设右手）
            finger_center = mathutils.Vector((bbox_min.x + bbox_size.x * 0.2, 
                                             wrist_pos.y + bbox_size.y * 0.3, 
                                             bbox_center.z))
            radius = bbox_size.x * 0.25
        elif finger_name == "index":
            finger_center = mathutils.Vector((bbox_min.x + bbox_size.x * 0.3, 
                                             wrist_pos.y + bbox_size.y * 0.5, 
                                             bbox_center.z))
            radius = bbox_size.x * 0.15
        elif finger_name == "middle":
            finger_center = mathutils.Vector((bbox_center.x, 
                                             wrist_pos.y + bbox_size.y * 0.5, 
                                             bbox_center.z))
            radius = bbox_size.x * 0.15
        elif finger_name == "ring":
            finger_center = mathutils.Vector((bbox_min.x + bbox_size.x * 0.7, 
                                             wrist_pos.y + bbox_size.y * 0.5, 
                                             bbox_center.z))
            radius = bbox_size.x * 0.15
        elif finger_name == "pinky":
            finger_center = mathutils.Vector((bbox_min.x + bbox_size.x * 0.85, 
                                             wrist_pos.y + bbox_size.y * 0.5, 
                                             bbox_center.z))
            radius = bbox_size.x * 0.15
        
        # 计算每段骨骼的位置
        bone_positions = []
        for i, bone_name in enumerate(bones):
            if bone_name in available_groups:  # 确保顶点组存在
                pos = finger_center + finger_direction * (i * bbox_size.y * 0.25)
                bone_positions.append((bone_name, pos))
        
        # 为每个顶点计算权重
        for v in bm.verts:
            for bone_name, bone_pos in bone_positions:
                # 计算顶点到骨骼的距离
                dist = (v.co - bone_pos).length
                
                # 如果顶点在骨骼影响范围内
                if dist < radius:
                    # 计算权重 (距离越近权重越大)
                    weight = 1.0 - (dist / radius)
                    
                    # 存储权重数据
                    if v.index not in weights_data:
                        weights_data[v.index] = {}
                    
                    # 如果已经有权重，使用较大的值
                    current_weight = weights_data[v.index].get(bone_name, 0.0)
                    weights_data[v.index][bone_name] = max(current_weight, weight)
    
    # 更新网格
    bmesh.update_edit_mesh(mesh)
    
    # 返回对象模式
    bpy.ops.object.mode_set(mode='OBJECT')
    
    # 应用权重数据
    for vertex_idx, bones_weights in weights_data.items():
        for bone_name, weight in bones_weights.items():
            vgroup = vertex_groups[bone_name]
            vgroup.add([vertex_idx], weight, 'REPLACE')

def bind_hand_model(model_name, armature_name="RightHand"):
    """将手部模型绑定到骨架"""
    # 获取对象
    model = bpy.data.objects.get(model_name)
    armature = bpy.data.objects.get(armature_name)
    
    if not model or not armature:
        print(f"错误：找不到模型 {model_name} 或骨架 {armature_name}")
        return False
    
    # 确保在对象模式
    if bpy.context.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')
    
    # 取消所有选择
    bpy.ops.object.select_all(action='DESELECT')
    
    # 选择模型并设置为活动对象
    model.select_set(True)
    bpy.context.view_layer.objects.active = model
    
    # 重置模型变换 (避免变形问题)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    
    # 检测骨架中的骨骼结构
    finger_definitions = detect_finger_bones(armature)
    
    # 创建顶点组
    create_vertex_groups(model, armature)
    
    # 分配权重
    auto_weight_hand_model(model, armature, finger_definitions)
    
    # 添加骨架修饰器
    if len(model.modifiers) > 0:
        # 如果已经有修饰器，检查是否有骨架修饰器
        has_armature_modifier = False
        for mod in model.modifiers:
            if mod.type == 'ARMATURE' and mod.object == armature:
                has_armature_modifier = True
                break
        
        if not has_armature_modifier:
            mod = model.modifiers.new(name="Armature", type='ARMATURE')
            mod.object = armature
            mod.use_vertex_groups = True
    else:
        # 添加新修饰器
        mod = model.modifiers.new(name="Armature", type='ARMATURE')
        mod.object = armature
        mod.use_vertex_groups = True
    
    # 绑定完成消息
    print(f"已将模型 {model_name} 绑定到骨架 {armature_name}")
    return True

def create_hand_rotation_animation(armature_name="RightHand"):
    """修改骨骼动画为旋转控制而非位置控制"""
    # 获取骨架对象
    armature = bpy.data.objects.get(armature_name)
    if not armature:
        print(f"错误：找不到骨架 {armature_name}")
        return False
    
    # 确保在对象模式
    if bpy.context.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')
    
    # 取消所有选择
    bpy.ops.object.select_all(action='DESELECT')
    
    # 选择骨架并设置为活动对象
    armature.select_set(True)
    bpy.context.view_layer.objects.active = armature
    
    # 获取所有关键帧
    keyframes = set()
    if armature.animation_data and armature.animation_data.action:
        for fcu in armature.animation_data.action.fcurves:
            for kp in fcu.keyframe_points:
                keyframes.add(int(kp.co[0]))
    
    keyframes = sorted(list(keyframes))
    if not keyframes:
        print("没有找到关键帧")
        return False
    
    # 检测骨架中的骨骼结构
    finger_definitions = detect_finger_bones(armature)
    
    # 设置骨骼旋转模式为四元数
    for bone in armature.pose.bones:
        bone.rotation_mode = 'QUATERNION'
    
    # 遍历所有关键帧
    for frame in keyframes:
        bpy.context.scene.frame_set(frame)
        
        # 确保骨架是活动对象
        bpy.context.view_layer.objects.active = armature
        
        # 进入姿势模式
        bpy.ops.object.mode_set(mode='POSE')
        
        # 对每个手指应用旋转
        for finger, bones in finger_definitions.items():
            if len(bones) < 2:  # 需要至少两个骨骼才能计算旋转
                continue
                
            # 计算骨骼之间的旋转
            for i in range(len(bones)-1):
                parent_bone = armature.pose.bones.get(bones[i])
                child_bone = armature.pose.bones.get(bones[i+1])
                
                if parent_bone and child_bone:
                    # 获取骨骼的世界位置
                    parent_head = parent_bone.head
                    parent_tail = parent_bone.tail
                    child_tail = child_bone.tail
                    
                    # 计算骨骼方向向量
                    parent_dir = parent_tail - parent_head
                    child_dir = child_tail - parent_tail
                    
                    # 计算从父骨骼到子骨骼的旋转
                    if parent_dir.length > 0 and child_dir.length > 0:
                        rot_diff = parent_dir.rotation_difference(child_dir)
                        
                        # 应用旋转
                        parent_bone.rotation_quaternion = rot_diff
                        
                        # 添加关键帧
                        parent_bone.keyframe_insert(data_path="rotation_quaternion", frame=frame)
        
        # 返回对象模式
        bpy.ops.object.mode_set(mode='OBJECT')
    
    print(f"已将 {armature_name} 的位置动画转换为旋转动画")
    return True

# 用法示例
if __name__ == "__main__":
    # 确保在对象模式
    if bpy.context.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')
    
    # 查找手部模型和骨架
    hand_model = bpy.data.objects.get("Hand")
    right_hand_armature = bpy.data.objects.get("RightHand")
    
    if hand_model and right_hand_armature:
        print(f"找到模型: {hand_model.name} 和骨架: {right_hand_armature.name}")
        
        # 执行绑定
        bind_hand_model(hand_model.name, right_hand_armature.name)
        
        # 可选：转换为旋转动画
        # 如果要运行此功能，请取消下面一行的注释
        # create_hand_rotation_animation(right_hand_armature.name)
        
        print("操作完成")
    else:
        if not hand_model:
            print(f"错误: 未找到模型 'Hand'")
        if not right_hand_armature:
            print(f"错误: 未找到骨架 'RightHand'")
        
        # 回退到查找活动对象
        active_obj = bpy.context.active_object
        if active_obj and active_obj.type == 'MESH':
            print(f"使用活动模型: {active_obj.name}")
            
            # 查找任何骨架对象
            armature_obj = None
            for obj in bpy.data.objects:
                if obj.type == 'ARMATURE':
                    armature_obj = obj
                    break
            
            if armature_obj:
                print(f"找到骨架: {armature_obj.name}")
                bind_hand_model(active_obj.name, armature_obj.name)
            else:
                print("错误: 场景中没有骨架对象")
        else:
            print("错误: 请先选择一个网格模型")