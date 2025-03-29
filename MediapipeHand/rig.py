import bpy
import mathutils

def create_vertex_groups(model, armature):
    """为模型创建与骨骼对应的顶点组"""
    # 确保模型没有顶点组
    for vg in model.vertex_groups:
        model.vertex_groups.remove(vg)
    
    # 为每个骨骼创建顶点组
    for bone in armature.pose.bones:
        model.vertex_groups.new(name=bone.name)
    
    return model.vertex_groups

def auto_weight_hand_model(model, armature, finger_definitions):
    """为手部模型自动分配权重"""
    # 进入编辑模式
    bpy.context.view_layer.objects.active = model
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
    
    # 为每个手指分配权重
    vertex_groups = model.vertex_groups
    
    for finger_name, bones in finger_definitions.items():
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
            pos = finger_center + finger_direction * (i * bbox_size.y * 0.25)
            bone_positions.append((bone_name, pos))
        
        # 为每个顶点分配权重
        for v in bm.verts:
            for bone_name, bone_pos in bone_positions:
                # 计算顶点到骨骼的距离
                dist = (v.co - bone_pos).length
                
                # 如果顶点在骨骼影响范围内
                if dist < radius:
                    # 计算权重 (距离越近权重越大)
                    weight = 1.0 - (dist / radius)
                    
                    # 分配权重
                    vgroup = vertex_groups[bone_name]
                    vgroup.add([v.index], weight, 'REPLACE')
    
    # 更新网格
    bmesh.update_edit_mesh(mesh)
    
    # 返回对象模式
    bpy.ops.object.mode_set(mode='OBJECT')

def bind_hand_model(model_name, armature_name="RightHand"):
    """将手部模型绑定到骨架"""
    # 获取对象
    model = bpy.data.objects.get(model_name)
    armature = bpy.data.objects.get(armature_name)
    
    if not model or not armature:
        print(f"错误：找不到模型 {model_name} 或骨架 {armature_name}")
        return False
    
    # 定义手指骨骼结构
    finger_definitions = {
        "thumb": ["thumb_cmc", "thumb_mcp", "thumb_ip", "thumb_tip"],
        "index": ["index_mcp", "index_pip", "index_dip", "index_tip"],
        "middle": ["middle_mcp", "middle_pip", "middle_dip", "middle_tip"],
        "ring": ["ring_mcp", "ring_pip", "ring_dip", "ring_tip"],
        "pinky": ["pinky_mcp", "pinky_pip", "pinky_dip", "pinky_tip"]
    }
    
    # 创建顶点组
    create_vertex_groups(model, armature)
    
    # 导入bmesh模块(仅在函数内使用，避免全局导入)
    import bmesh
    
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
    armature = bpy.data.objects.get(armature_name)
    if not armature:
        print(f"错误：找不到骨架 {armature_name}")
        return False
    
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
    
    # 设置骨骼旋转模式为四元数
    for bone in armature.pose.bones:
        bone.rotation_mode = 'QUATERNION'
    
    # 遍历所有关键帧
    for frame in keyframes:
        bpy.context.scene.frame_set(frame)
        
        # 进入姿势模式
        bpy.context.view_layer.objects.active = armature
        bpy.ops.object.mode_set(mode='POSE')
        
        # 修改每个手指的骨骼连接
        for finger in ["thumb", "index", "middle", "ring", "pinky"]:
            bones = []
            
            # 收集此手指的所有骨骼
            if finger == "thumb":
                bones = ["thumb_cmc", "thumb_mcp", "thumb_ip"]
            else:
                bones = [f"{finger}_mcp", f"{finger}_pip", f"{finger}_dip"]
            
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
# bind_hand_model("YourHandModel", "RightHand") # 替换为您的模型名称
# create_hand_rotation_animation("RightHand")   # 如果需要转换为旋转动画