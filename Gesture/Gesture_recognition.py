import cv2
import mediapipe as mp

cap = cv2.VideoCapture(0) # 镜头编号默认为0
mpHands = mp.solutions.hands # 使用mediapipe 的手部模型
hands = mpHands.Hands() # 采用该函数的预设值
mpDraw = mp.solutions.drawing_utils # 保存侦测到的手的坐标

while True:
    ret,img = cap.read()
    if ret:
        # 手部侦测需要rgb格式的图片，但在opencv读取到的默认是bgr的图片，所以需要对图片进行格式转换
        imgRGB = cv2.cvtColor(img,cv2.COLOR_BGR2RGB)
        cv2.imshow('img',img)
        result = hands.process(imgRGB) # 对照片内容进行手部侦测
        print(result.multi_hand_landmarks) # 测试是否检测到了手，并输出手的坐标

        imgHeight = img.shape[0]
        imgWidth = img.shape[1]

        # 检测到了多少只手
        if result.multi_hand_landmarks: # 如果检测到了手
            for handLms in result.multi_hand_landmarks:
                # 无法在窗口中显示点
                mpDraw.draw_landmarks(img,handLms,mpHands.HAND_CONNECTIONS) # draw_landmarks 画出识别的点，第一个参数为在哪里画点，第二个参数为点的坐标，第三个参数把点用线连接
                # 打印出每个点的编号和坐标
                for i ,lm in enumerate(handLms.landmark):
                    xPos = int(lm.x * imgWidth)
                    yPos = int(lm.y * imgHeight)
                    # 在图上显示点的标号
                    cv2.putText(img,str(i),(xPos-25,yPos+25),cv2.FONT_HERSHEY_SIMPLEX,0.4,(0,0,255),2)
                    print(i,xPos,yPos)

    if cv2.waitKey(1)==ord('q'): # 程序等待1毫秒，在显示图像的窗口中按下 q 键，则程序结束
        break
