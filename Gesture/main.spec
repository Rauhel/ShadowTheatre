# -*- mode: python ; coding: utf-8 -*-

block_cipher = None

a = Analysis(
    ['main.py'],
    pathex=['.'],
    binaries=[
        (r'D:\Users\ryana\anaconda3\python310.dll', '.')  # 使用原始字符串修复路径问题
    ],
    datas=[
        ('gesture_model_single_hand.pkl', '.'),  # 单手模型
        ('gesture_model_two_hands.pkl', '.'),    # 双手模型
        ('gesture_model_hand_types.json', '.'), # 手势类型配置
        ('gesture_data', 'gesture_data'),       # 手势数据文件夹
        (r'D:\Users\ryana\anaconda3\Lib\site-packages\mediapipe\modules\hand_landmark', 'mediapipe/modules/hand_landmark'),  # 手部关键点模型
        (r'D:\Users\ryana\anaconda3\Lib\site-packages\mediapipe\modules\palm_detection', 'mediapipe/modules/palm_detection'),  # 手掌检测模型
    ],
    hiddenimports=[
        'utils.network',                        # 网络管理模块
        'gesture_recognition',                  # 手势识别主模块
        'hand_position',                        # 手部位置跟踪模块
        'feature_extractor',                    # 特征提取模块
        'gesture_trainer',                      # 手势训练模块
        'recognizers.rule_based_recognizer',    # 基于规则的识别器
        'recognizers.single_hand_recognizer',   # 单手识别器
        'recognizers.two_hands_recognizer',     # 双手识别器
        'mediapipe',                            # MediaPipe库
        'cv2',                                  # OpenCV库
        'numpy',                                # NumPy库
        'tensorflow',                           # TensorFlow库
        'keras',                                # Keras库
        'ml_dtypes',                            # ml_dtypes依赖
        'ml_dtypes._ml_dtypes_ext',             # ml_dtypes扩展模块
        'psycopg2',                             # PostgreSQL支持
        'mysqlclient',                          # MySQL支持
        'notebook.services.shutdown',          # Jupyter Notebook服务
        'astropy.visualization.wcsaxes',       # Astropy可视化模块
        'matplotlib',                           # Matplotlib库
    ],
    hookspath=[],
    runtime_hooks=[],
    excludes=[],
    win_no_prefer_redirects=False,
    win_private_assemblies=False,
    cipher=block_cipher,
)

pyz = PYZ(a.pure, a.zipped_data, cipher=block_cipher)

exe = EXE(
    pyz,
    a.scripts,
    [],
    exclude_binaries=False,  # 修改为 False，确保包含所有必要的二进制文件
    name='gesture_app',
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=True,
    console=False  # 设置为 True 可以显示终端窗口，False 为隐藏（GUI模式）
)

coll = COLLECT(
    exe,
    a.binaries,
    a.zipfiles,
    a.datas,
    strip=False,
    upx=True,
    upx_exclude=[],
    name='gesture_app'
)
